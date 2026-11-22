// El entorno de la prueba de carga (fase 15A): construir y levantar una API propia sobre una base TEMPORAL, sembrar datos sinteticos,
// medir los procesos y, al terminar, apagar y borrar todo. Solo Node.js nativo.
//
// Seguridad: la base se llama SyncTime_Pruebas_carga_<8 hex>, el prefijo que el codigo de la API reconoce como «de pruebas» (se migra sola y
// es la unica que se puede borrar). Antes de borrar se vuelve a comprobar el nombre. Nunca se toca la base real ni la demo.
import { spawn, spawnSync } from 'node:child_process';
import crypto from 'node:crypto';
import fs from 'node:fs';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';

export const PATRON_BASE = /^SyncTime_Pruebas_carga_[0-9a-f]{8}$/;
export const RAIZ = path.resolve(import.meta.dirname, '..', '..');
const PROYECTO = path.join(RAIZ, 'backend', 'Proyecto-Carteles-herrera-synctime-main', 'LetrerosHerreraSYNCTIMEapi', 'LetrerosHerreraSYNCTIMEapi.csproj');

export const servidorSql = () => process.env.TEST_SQL_SERVER ?? 'localhost\\SQLEXPRESS';
export const nombreDeBaseNueva = () => `SyncTime_Pruebas_carga_${crypto.randomBytes(4).toString('hex')}`;

export function puertoLibre() {
  return new Promise((resolver, rechazar) => {
    const servidor = net.createServer();
    servidor.on('error', rechazar);
    servidor.listen(0, '127.0.0.1', () => {
      const { port } = servidor.address();
      servidor.close(() => resolver(port));
    });
  });
}

// Con poca memoria libre SQL Server Express puede quedarse esperando paginas durante minutos (SLEEP_BPOOL_STEAL): un limite de tiempo evita que la
// prueba se cuelgue para siempre; al vencer, sqlcmd se corta y SQL Server deshace lo que habia hecho.
export function sql(base, args, { entrada, limiteMs = 300_000 } = {}) {
  const r = spawnSync('sqlcmd', ['-S', servidorSql(), '-E', '-C', '-l', '60', '-b', '-I', ...(base ? ['-d', base] : []), ...args],
    { encoding: 'utf8', input: entrada, maxBuffer: 64 * 1024 * 1024, timeout: limiteMs, killSignal: 'SIGKILL' });
  if (r.error?.code === 'ETIMEDOUT') throw new Error(`SQL Server tardo mas de ${limiteMs / 1000} s en responder (suele ser falta de memoria libre en el equipo: cierra programas y repite).`);
  if (r.error) throw new Error(`No se pudo ejecutar sqlcmd: ${r.error.message}`);
  if (r.status !== 0) throw new Error(`sqlcmd fallo (${r.status}): ${(r.stdout + r.stderr).trim().slice(0, 2000)}`);
  return r.stdout;
}

export function sembrar(base, { clientes, solicitudes, auditoria, publicaciones }) {
  if (!PATRON_BASE.test(base)) throw new Error(`Se niega a sembrar en «${base}»: no es una base temporal de la prueba de carga.`);
  return sql(base, ['-i', path.join(import.meta.dirname, 'datos-sinteticos.sql'), '-v',
    `Clientes=${clientes}`, `Solicitudes=${solicitudes}`, `Auditoria=${auditoria}`, `Publicaciones=${publicaciones}`, '-W']);
}

// Los clientes de la prueba ya tienen cuenta; las solicitudes en linea exigen un correo confirmado.
export function confirmarCorreosDeLaPrueba(base) {
  if (!PATRON_BASE.test(base)) throw new Error(`Se niega a modificar «${base}».`);
  sql(base, ['-Q', "UPDATE clientes SET fecha_verificacion_correo = SYSDATETIMEOFFSET() WHERE email LIKE N'vu%@carga.test'"]);
}

export function borrarBase(base) {
  if (!PATRON_BASE.test(base)) throw new Error(`Se niega a borrar «${base}»: no es una base temporal de la prueba de carga.`);
  sql(null, ['-Q', `IF DB_ID(N'${base}') IS NOT NULL BEGIN ALTER DATABASE [${base}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${base}]; END`]);
}

export function existeBase(base) {
  return sql(null, ['-h', '-1', '-W', '-Q', `SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = N'${base}'`]).trim() === '1';
}

export function construirApi(destino) {
  const r = spawnSync('dotnet', ['build', PROYECTO, '-c', 'Release', '-o', destino, '--nologo', '-v', 'q'], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  if (r.status !== 0) throw new Error(`dotnet build fallo:\n${(r.stdout + r.stderr).slice(-3000)}`);
}

// Variables de la API de la prueba. Pisan los User Secrets del equipo (correo, Google...): ningun secreto real llega aqui.
export function variablesDeLaApi({ puerto, base, carpeta, claves, limites, diagnostico }) {
  const holgado = '1000000';
  const env = {
    ASPNETCORE_ENVIRONMENT: 'Development',
    ASPNETCORE_URLS: `http://127.0.0.1:${puerto}`,
    ConnectionStrings__DefaultConnection: `Server=${servidorSql()};Database=${base};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60;Command Timeout=120;`,
    Jwt__Key: claves.jwt, Jwt__Issuer: 'carga', Jwt__Audience: 'carga',
    Bootstrap__Admin__Rut: '1-9', Bootstrap__Admin__Nombre: 'Admin', Bootstrap__Admin__Apellido: 'Carga', Bootstrap__Admin__Telefono: '900000000',
    Bootstrap__Admin__Email: claves.correoAdmin, Bootstrap__Admin__Password: claves.claveAdmin,
    RostroFacial__ClaveCifrado: crypto.randomBytes(32).toString('base64'),
    Correo__Modo: 'Bandeja', Correo__Bandeja__Carpeta: path.join(carpeta, 'bandeja'), Correo__AvisosTaller: 'avisos.taller@carga.test',
    Correo__Remitente__Direccion: 'synctime@carga.test', Correo__Smtp__Servidor: '127.0.0.1', Correo__Smtp__Puerto: '9',
    Correo__Smtp__Seguridad: 'Ninguna', Correo__Smtp__Usuario: 'sin-uso@carga.test', Correo__Smtp__Password: 'sin-uso',
    Sitio__UrlPublica: 'https://synctime.carga.test',
    Imagenes__Carpeta: path.join(carpeta, 'imagenes'), Adjuntos__Carpeta: path.join(carpeta, 'adjuntos'), Reportes__Carpeta: path.join(carpeta, 'exportaciones'),
    Google__ClientId: '',
    Privacidad__PurgaActiva: 'false',
    'Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command': diagnostico ? 'Information' : 'Warning',
  };
  if (limites === 'holgados') {
    for (const clave of ['ApiGeneral', 'Login', 'LoginFacial', 'DesafioFacial', 'Reautenticacion', 'Publico', 'ContactoPorHora', 'CookiesPorHora', 'PorCuenta',
      'Salud', 'Adjuntos', 'Exportaciones', 'Reportes', 'IngresoGoogle', 'Imagenes'])
      env[`RateLimiting__${clave}`] = holgado;
    env.Solicitudes__MaxEnLineaPorIp = holgado;
  }
  return env;
}

export function iniciarApi({ destino, env, archivoLog }) {
  const log = fs.openSync(archivoLog, 'w');
  const proceso = spawn('dotnet', [path.join(destino, 'LetrerosHerreraSYNCTIMEapi.dll')], {
    cwd: destino, env: { ...process.env, ...env }, stdio: ['ignore', log, log], windowsHide: true,
  });
  proceso.on('exit', () => fs.closeSync(log));
  return proceso;
}

export function esperarApi(puerto, proceso, { limiteMs = 240_000 } = {}) {
  const inicio = Date.now();
  return new Promise((resolver, rechazar) => {
    let murio = false;
    proceso.on('exit', (codigo) => { murio = true; rechazar(new Error(`La API se cerro al arrancar (codigo ${codigo}). Revisa su registro.`)); });
    const intentar = () => {
      if (murio) return;
      const peticion = http.get({ host: '127.0.0.1', port: puerto, path: '/salud/lista', timeout: 5000 }, (r) => {
        r.resume();
        if (r.statusCode === 200) resolver();
        else if (Date.now() - inicio > limiteMs) rechazar(new Error(`La API no quedo lista (salud ${r.statusCode}).`));
        else setTimeout(intentar, 1000);
      });
      peticion.on('error', () => (Date.now() - inicio > limiteMs ? rechazar(new Error('La API no respondio a tiempo.')) : setTimeout(intentar, 1000)));
      peticion.on('timeout', () => peticion.destroy());
    };
    intentar();
  });
}

export function apagarApi(proceso) {
  if (!proceso || proceso.exitCode !== null) return;
  // taskkill con /T mata tambien a los procesos hijos; en otros sistemas basta con la senal.
  if (process.platform === 'win32') spawnSync('taskkill', ['/PID', String(proceso.pid), '/T', '/F'], { stdio: 'ignore' });
  else proceso.kill('SIGKILL');
}

// ── Recursos del equipo durante la medicion: segundos de CPU y memoria de la API y de SQL Server (PowerShell, solo Windows) ──
export function leerProcesos(pids) {
  if (process.platform !== 'win32') return null;
  const r = spawnSync('powershell', ['-NoProfile', '-NonInteractive', '-Command',
    `Get-Process -Id ${pids.join(',')} -ErrorAction SilentlyContinue | ForEach-Object { "$($_.Id);$($_.ProcessName);$(if ($null -eq $_.CPU) { '' } else { [math]::Round($_.CPU,2) });$($_.WorkingSet64)" }`],
  { encoding: 'utf8' });
  if (r.status !== 0) return null;
  return r.stdout.split(/\r?\n/).filter(Boolean).map((l) => {
    const [id, nombre, cpu, memoria] = l.split(';');
    // Windows no deja leer el tiempo de CPU de un servicio (SQL Server) sin permisos de administrador: llega vacio y se informa como no disponible.
    return { id: Number(id), nombre, cpu: cpu === '' ? null : Number(cpu.replace(',', '.')), memoria: Number(memoria) };
  });
}

export function pidsDeSqlServer() {
  if (process.platform !== 'win32') return [];
  const r = spawnSync('powershell', ['-NoProfile', '-NonInteractive', '-Command', '(Get-Process sqlservr -ErrorAction SilentlyContinue).Id'], { encoding: 'utf8' });
  return r.stdout.split(/\r?\n/).filter(Boolean).map(Number);
}

export function equipo() {
  const cpus = os.cpus();
  return { sistema: `${os.type()} ${os.release()}`, procesador: cpus[0]?.model?.trim(), nucleos: cpus.length, ramTotalMb: Math.round(os.totalmem() / 2 ** 20), ramLibreMb: Math.round(os.freemem() / 2 ** 20) };
}

// Consultas lentas del registro de la API (solo con --diagnostico, que enciende el registro de las consultas de EF Core).
export function consultasLentas(archivoLog, { desdeMs = 100, cuantas = 12 } = {}) {
  const lineas = fs.readFileSync(archivoLog, 'utf8').split(/\r?\n/);
  const consultas = [];
  for (let i = 0; i < lineas.length; i += 1) {
    const m = /Executed DbCommand \((\d+)ms\)/.exec(lineas[i]);
    if (!m) continue;
    const ms = Number(m[1]);
    if (ms < desdeMs) continue;
    const cuerpo = [];
    for (let j = i + 1; j < lineas.length && !/^(info|warn|fail|dbug|crit):/.test(lineas[j]); j += 1) cuerpo.push(lineas[j].trim());
    consultas.push({ ms, sql: cuerpo.join(' ').replace(/\s+/g, ' ').slice(0, 360) });
  }
  return consultas.sort((a, b) => b.ms - a.ms).slice(0, cuantas);
}
