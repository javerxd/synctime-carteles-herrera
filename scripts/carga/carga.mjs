#!/usr/bin/env node
// Prueba de carga de la API de SyncTime (fase 15A, RNFP01): 20 usuarios simultaneos y la meta del 95 % de las respuestas en 2 s o menos.
//
//   node scripts/carga/carga.mjs [opciones]      (ver scripts/carga/README.md)
//
// Que hace, de principio a fin y sin tocar la base real ni la demo:
//   1. Construye la API en Release en una carpeta temporal (no pisa los archivos de una API que tengas encendida).
//   2. La levanta contra una base NUEVA y TEMPORAL (SyncTime_Pruebas_carga_xxxxxxxx); las migraciones crean el esquema.
//   3. Crea cuentas de prueba por la API y siembra datos sinteticos con sqlcmd.
//   4. Lanza los usuarios virtuales, mide, y escribe el informe en docs/evidencias/.
//   5. Apaga la API y borra la base temporal y la carpeta temporal, pase lo que pase.
import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { crearOpciones } from './opciones.mjs';
import { redactarInforme } from './informe.mjs';
import { evaluarMeta } from './metricas.mjs';
import {
  RAIZ, apagarApi, borrarBase, confirmarCorreosDeLaPrueba, consultasLentas, construirApi, equipo, esperarApi, existeBase, iniciarApi, leerProcesos,
  nombreDeBaseNueva, pidsDeSqlServer, puertoLibre, sembrar, sql, variablesDeLaApi,
} from './entorno.mjs';
import { correrUsuario, planificarUsuarios, prepararCuentas } from './escenario.mjs';

const o = crearOpciones(process.argv.slice(2));
if (o.ayuda) {
  console.log(o.textoDeAyuda);
  process.exit(0);
}

const dormir = (ms) => new Promise((r) => setTimeout(r, ms));
const registrar = (...partes) => console.log(`[${new Date().toLocaleTimeString('es-CL')}]`, ...partes);

const base = nombreDeBaseNueva();
const carpeta = fs.mkdtempSync(path.join(os.tmpdir(), 'synctime-carga-'));
const destino = path.join(carpeta, 'api');
const archivoLog = path.join(carpeta, 'api.log');
let api = null;
let limpiando = false;

async function limpiar() {
  if (limpiando) return;
  limpiando = true;
  apagarApi(api);
  await dormir(1500);
  if (!o.conservar) {
    try {
      borrarBase(base);
      registrar(`Base temporal ${base} borrada.`);
    } catch (e) {
      console.error(`NO SE PUDO BORRAR LA BASE TEMPORAL ${base}: ${e.message}\nBórrala a mano (solo esa, que empieza con SyncTime_Pruebas_carga_).`);
    }
    fs.rmSync(carpeta, { recursive: true, force: true });
  } else {
    registrar(`Se conservan la base ${base} y la carpeta ${carpeta} (--conservar). Bórralas cuando termines.`);
  }
}
for (const senal of ['SIGINT', 'SIGTERM']) process.on(senal, () => limpiar().finally(() => process.exit(130)));

// Las credenciales de la prueba nacen aqui, valen solo para esta base temporal y no se escriben en ningun archivo ni en el informe.
const clave = process.env.SYNCTIME_CARGA_CLAVE?.trim() || `Pr-${crypto.randomBytes(10).toString('hex')}`;
const claves = {
  jwt: crypto.randomBytes(48).toString('base64'),
  correoAdmin: 'admin.carga@carga.test',
  claveAdmin: `Pr-${crypto.randomBytes(10).toString('hex')}`,
};

async function principal() {
  const hardware = equipo();
  if (hardware.ramLibreMb < 1500) {
    registrar(`AVISO: solo hay ${hardware.ramLibreMb} MB de RAM libres. SQL Server Express se queda sin memoria y las cifras salen peores que las reales: cierra programas (navegadores, el IDE, Discord) y vuelve a correr.`);
  }
  if (sql(null, ['-h', '-1', '-W', '-Q', 'SET NOCOUNT ON; SELECT 1']).trim() !== '1') throw new Error('SQL Server no responde.');
  if (existeBase(base)) throw new Error(`La base ${base} ya existe: algo salio mal con el nombre.`);

  registrar('Construyendo la API (Release)…');
  construirApi(destino);

  const puerto = await puertoLibre();
  registrar(`Levantando la API en http://127.0.0.1:${puerto} sobre la base temporal ${base}…`);
  const env = variablesDeLaApi({ puerto, base, carpeta, claves, limites: o.limites, diagnostico: o.diagnostico });
  api = iniciarApi({ destino, env, archivoLog });
  await esperarApi(puerto, api);
  registrar('API lista (migraciones aplicadas).');

  const contexto = { host: '127.0.0.1', puerto, muestras: [], midiendo: false, detener: false };
  const usuarios = planificarUsuarios({ visitantes: o.visitantes, clientes: o.clientes, personal: o.personal, semilla: o.semilla });

  registrar('Creando las cuentas de prueba…');
  // Las cuentas se crean con limites aflojados SOLO si la API ya los trae aflojados; con limites reales los de ingreso/registro (10 por minuto
  // por IP) alcanzan: cada cliente tiene su IP y el personal se crea desde una sola IP en pocas peticiones.
  await prepararCuentas(contexto, usuarios, { correoAdmin: claves.correoAdmin, claveAdmin: claves.claveAdmin, clave });

  registrar(`Sembrando datos sintéticos (${o.volumen.clientes} clientes, ${o.volumen.solicitudes} solicitudes, ${o.volumen.auditoria} filas de bitácora)…`);
  const inicioSiembra = Date.now();
  sembrar(base, o.volumen);
  confirmarCorreosDeLaPrueba(base);
  registrar(`Datos sembrados en ${((Date.now() - inicioSiembra) / 1000).toFixed(1)} s.`);

  // Con muchos datos nuevos, la primera consulta de cada tipo es mas lenta (planes, estadisticas): se actualizan como lo haria un mantenimiento.
  sql(base, ['-Q', 'EXEC sp_updatestats']);

  const sqlPids = pidsDeSqlServer();
  const seguidos = [api.pid, ...sqlPids];
  const muestrasDeProceso = [];
  const muestreador = setInterval(() => {
    const lectura = leerProcesos(seguidos);
    if (lectura && contexto.midiendo) muestrasDeProceso.push({ t: Date.now(), lectura });
  }, 5000);

  registrar(`Lanzando ${usuarios.length} usuarios (${o.visitantes} visitantes, ${o.clientes} clientes, ${o.personal} personal)…`);
  const trabajos = usuarios.map((vu) => correrUsuario(contexto, vu, { clave, pensarMs: o.pensarMs }).catch((e) => { registrar(`Usuario ${vu.id} (${vu.perfil}) se detuvo: ${e.message}`); }));
  await dormir(o.calentamientoS * 1000);
  registrar(`Calentamiento listo. Midiendo ${o.duracionS} s…`);
  contexto.midiendo = true;
  const inicioMedicion = Date.now();
  const cpuInicio = leerProcesos(seguidos);
  await dormir(o.duracionS * 1000);
  contexto.midiendo = false;
  const cpuFin = leerProcesos(seguidos);
  const segundos = (Date.now() - inicioMedicion) / 1000;
  clearInterval(muestreador);
  contexto.detener = true;
  await Promise.race([Promise.all(trabajos), dormir(35_000)]);
  for (const vu of usuarios) vu.agente.destroy();

  const recursos = calcularRecursos(cpuInicio, cpuFin, segundos, muestrasDeProceso, api.pid, sqlPids);
  const meta = evaluarMeta(contexto.muestras, { umbralMs: 2000, fraccion: 0.95 });
  registrar(`Medidas ${meta.total} respuestas; buenas ${(meta.fraccionBuena * 100).toFixed(1)} % → ${meta.cumple ? 'CUMPLE' : 'NO CUMPLE'}.`);

  const lentas = o.diagnostico ? consultasLentas(archivoLog) : [];
  const notas = [];
  if (hardware.ramLibreMb < 1500) notas.push(`Había solo ${hardware.ramLibreMb} MB de RAM libres al empezar: SQL Server Express trabajó con muy poca memoria, así que estas cifras son más pesimistas que las de un equipo desocupado.`);
  if (o.limites === 'holgados') notas.push('Los límites de tasa estaban aflojados: esta corrida aísla el rendimiento, no es el comportamiento real de la API.');
  if (o.diagnostico) notas.push('Modo diagnóstico: la API registró cada consulta de EF Core, lo que la hace algo más lenta.');

  const texto = redactarInforme({
    fecha: new Date().toLocaleDateString('es-CL', { day: '2-digit', month: '2-digit', year: 'numeric' }).replaceAll('/', '-'),
    configuracion: { usuarios: usuarios.length, visitantes: o.visitantes, clientes: o.clientes, personal: o.personal, duracionS: o.duracionS, calentamientoS: o.calentamientoS, pensarMs: o.pensarMs, limites: o.limites },
    volumen: o.volumen, equipo: hardware, muestras: contexto.muestras, recursos: { ...recursos, ramLibreFin: Math.round(os.freemem() / 2 ** 20) }, consultasLentas: lentas, notas,
  });
  const salida = path.resolve(RAIZ, o.salida);
  fs.mkdirSync(path.dirname(salida), { recursive: true });
  fs.writeFileSync(salida, texto, 'utf8');
  registrar(`Informe escrito en ${path.relative(RAIZ, salida)}.`);
  if (o.json) fs.writeFileSync(path.resolve(RAIZ, o.json), JSON.stringify(contexto.muestras), 'utf8');
  process.exitCode = meta.cumple ? 0 : 2;
}

function calcularRecursos(inicio, fin, segundos, muestras, pidApi, pidsSql) {
  const delta = (pids) => {
    if (!inicio || !fin) return null;
    if ([...inicio, ...fin].some((p) => pids.includes(p.id) && p.cpu === null)) return null;
    const suma = (lectura) => lectura.filter((p) => pids.includes(p.id)).reduce((s, p) => s + p.cpu, 0);
    return Math.max(0, suma(fin) - suma(inicio)) / segundos;
  };
  const memoriaMax = (pids) => Math.max(0, ...muestras.flatMap((m) => m.lectura.filter((p) => pids.includes(p.id)).map((p) => p.memoria)));
  const dotnet = [pidApi];
  return {
    api: { nucleos: delta(dotnet), memoriaMax: memoriaMax(dotnet) },
    sql: pidsSql.length ? { nucleos: delta(pidsSql), memoriaMax: memoriaMax(pidsSql) } : null,
  };
}

principal()
  .catch((e) => {
    console.error(`\nLa prueba de carga fallo: ${e.message}`);
    if (fs.existsSync(archivoLog)) console.error(`Registro de la API (últimas líneas):\n${fs.readFileSync(archivoLog, 'utf8').split(/\r?\n/).slice(-25).join('\n')}`);
    process.exitCode = 1;
  })
  .finally(() => limpiar());
