// Los usuarios virtuales de la prueba de carga (fase 15A, RNFP01): visitantes, clientes y personal del taller.
// Solo Node.js nativo (node:http, zlib, crypto): ningun paquete.
//
// Cada usuario tiene su PROPIA direccion de origen (127.0.1.N): los limites de tasa de la API cuentan por IP, y veinte usuarios
// reales no comparten una. Las personas del taller si comparten la suya (127.0.0.2), como en la oficina.
import http from 'node:http';
import crypto from 'node:crypto';
import zlib from 'node:zlib';
import { performance } from 'node:perf_hooks';

// ── Generador pseudoaleatorio con semilla: dos corridas con la misma semilla hacen lo mismo ──
export function generador(semilla) {
  let a = semilla >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// Una respuesta de error (por ejemplo un 429) trae un objeto, no una lista: nunca se asume la forma.
const lista = (r) => (Array.isArray(r) ? r : Array.isArray(r?.elementos) ? r.elementos : []);
const dormir = (ms) => new Promise((r) => setTimeout(r, ms));
const entre = (rnd, min, max) => min + rnd() * (max - min);
const elegir = (rnd, lista) => lista[Math.floor(rnd() * lista.length)];

// ── Una peticion HTTP medida. El tiempo incluye abrir la conexion si hace falta y descargar y descomprimir todo el cuerpo ──
function enviar(contexto, vu, { metodo, ruta, cabeceras, cuerpo, limiteMs }) {
  return new Promise((resolver) => {
    const inicio = performance.now();
    let terminado = false;
    const fin = (resultado) => {
      if (terminado) return;
      terminado = true;
      resolver({ ...resultado, ms: performance.now() - inicio });
    };
    const peticion = http.request({
      host: contexto.host, port: contexto.puerto, path: ruta, method: metodo, agent: vu.agente, localAddress: vu.ip, timeout: limiteMs,
      headers: { 'Accept-Encoding': 'gzip, br', Accept: 'application/json', ...cabeceras },
    }, (respuesta) => {
      const trozos = [];
      respuesta.on('data', (t) => trozos.push(t));
      respuesta.on('error', (e) => fin({ estado: 0, texto: null, error: e.code ?? e.name }));
      respuesta.on('end', () => {
        const crudo = Buffer.concat(trozos);
        const codificacion = respuesta.headers['content-encoding'];
        try {
          const plano = codificacion === 'gzip' ? zlib.gunzipSync(crudo) : codificacion === 'br' ? zlib.brotliDecompressSync(crudo) : crudo;
          fin({ estado: respuesta.statusCode, texto: plano.toString('utf8'), bytes: crudo.length });
        } catch (e) {
          fin({ estado: 0, texto: null, error: 'descompresion' });
        }
      });
    });
    peticion.on('timeout', () => { peticion.destroy(); fin({ estado: 0, texto: null, error: 'tiempo_de_espera' }); });
    peticion.on('error', (e) => fin({ estado: 0, texto: null, error: e.code ?? e.name }));
    if (cuerpo !== undefined) peticion.write(cuerpo);
    peticion.end();
  });
}

// Pide, mide y guarda la muestra (solo si ya terminó el calentamiento). `etiqueta` agrupa rutas con ids o filtros distintos.
export async function pedir(contexto, vu, { metodo = 'GET', ruta, etiqueta, token, json, esperado = [200], cabeceras = {}, limiteMs = 30000 }) {
  const encabezados = { ...cabeceras };
  if (token) encabezados.Authorization = `Bearer ${token}`;
  let cuerpo;
  if (json !== undefined) {
    cuerpo = JSON.stringify(json);
    encabezados['Content-Type'] = 'application/json';
    encabezados['Content-Length'] = Buffer.byteLength(cuerpo);
  }
  const r = await enviar(contexto, vu, { metodo, ruta, cabeceras: encabezados, cuerpo, limiteMs });
  const muestra = { ruta: etiqueta ?? `${metodo} ${ruta.split('?')[0]}`, estado: r.estado, ms: r.ms, esperado: esperado.includes(r.estado), perfil: vu.perfil };
  if (r.error) muestra.error = r.error;
  if (contexto.midiendo) contexto.muestras.push(muestra);
  let datos = null;
  try { datos = r.texto ? JSON.parse(r.texto) : null; } catch { /* no era JSON */ }
  // Una respuesta inesperada guarda el codigo de error de la API (por ejemplo «contacto_directo»): dice la causa sin guardar el cuerpo.
  if (!muestra.esperado && typeof datos?.codigo === 'string') muestra.codigo = datos.codigo;
  return { estado: r.estado, datos, muestra };
}

// ── Preparacion (antes de medir): cuentas del personal y de los clientes de la prueba ──
export function digitoVerificador(cuerpo) {
  let suma = 0;
  let factor = 2;
  for (const d of String(cuerpo).split('').reverse()) {
    suma += Number(d) * factor;
    factor = factor === 7 ? 2 : factor + 1;
  }
  const resto = 11 - (suma % 11);
  return resto === 11 ? '0' : resto === 10 ? 'K' : String(resto);
}

// La API acepta solo letras en nombres y apellidos: un numero se escribe en letras (1 → B, 27 → BB...).
const letras = (n) => {
  let texto = '';
  let resto = n;
  do {
    texto = String.fromCharCode(65 + (resto % 26)) + texto;
    resto = Math.floor(resto / 26);
  } while (resto > 0);
  return texto;
};

const ROLES_PERSONAL = ['administrador', 'operario', 'dueño', 'diseñador', 'operario', 'administrador'];

export function planificarUsuarios({ visitantes, clientes, personal, semilla = 15 }) {
  const usuarios = [];
  let n = 0;
  for (let i = 0; i < visitantes; i += 1) {
    n += 1;
    usuarios.push({ id: n, perfil: 'visitante', ip: `127.0.1.${n}`, rnd: generador(semilla * 1000 + n) });
  }
  for (let i = 0; i < clientes; i += 1) {
    n += 1;
    usuarios.push({ id: n, perfil: 'cliente', ip: `127.0.1.${n}`, correo: `vu${n}@carga.test`, telefono: `6${String(10000000 + n)}`, rnd: generador(semilla * 1000 + n), solicitudesEnviadas: 0 });
  }
  for (let i = 0; i < personal; i += 1) {
    n += 1;
    const rol = ROLES_PERSONAL[i % ROLES_PERSONAL.length];
    usuarios.push({
      id: n, perfil: 'personal', ip: '127.0.0.2', rol, gestion: rol === 'administrador' || rol === 'dueño',
      correo: `empleado${n}@carga.test`, rnd: generador(semilla * 1000 + n),
    });
  }
  for (const u of usuarios) {
    u.agente = new http.Agent({ keepAlive: true, maxSockets: 8, localAddress: u.ip });
    u.datos = {};
  }
  return usuarios;
}

// Crea las cuentas por la API (sin medir). El administrador de arranque ya existe (lo crea la API con Bootstrap__Admin__*).
export async function prepararCuentas(contexto, usuarios, { correoAdmin, claveAdmin, clave }) {
  const admin = { ip: '127.0.0.2', agente: new http.Agent({ keepAlive: true, localAddress: '127.0.0.2' }), perfil: 'preparacion' };
  const entrada = await pedir(contexto, admin, { metodo: 'POST', ruta: '/api/v1/auth/login', json: { email: correoAdmin, password: claveAdmin } });
  if (entrada.estado !== 200) throw new Error(`No se pudo ingresar como administrador de arranque (${entrada.estado}).`);
  const token = entrada.datos.token;

  const documentos = (await pedir(contexto, admin, { ruta: '/api/v1/documentos-legales' })).datos ?? [];
  const version = (parte, defecto) => documentos.find((d) => String(d.tipo).toLowerCase().includes(parte))?.version ?? defecto;

  for (const u of usuarios.filter((x) => x.perfil === 'personal')) {
    const cuerpo = String(30_000_000 + u.id);
    const r = await pedir(contexto, admin, {
      metodo: 'POST', ruta: '/api/v1/empleados', token, esperado: [201],
      json: { rut: `${cuerpo}-${digitoVerificador(cuerpo)}`, nombre: 'Empleado', apellido: `Carga${letras(u.id)}`, telefono: `5${String(10000000 + u.id)}`, email: u.correo, password: clave, rol: u.rol, passwordActual: claveAdmin },
    });
    if (r.estado !== 201) throw new Error(`No se pudo crear al empleado de prueba ${u.id} (${r.estado}): ${JSON.stringify(r.datos)}`);
  }
  for (const u of usuarios.filter((x) => x.perfil === 'cliente')) {
    const r = await pedir(contexto, u, {
      metodo: 'POST', ruta: '/api/v1/auth/registro-cliente', esperado: [201],
      json: {
        nombre: 'Cliente', apellido: `Carga${letras(u.id)}`, telefono: u.telefono, correo: u.correo, password: clave,
        aceptaTerminosYPrivacidad: true, aceptaMarketing: false, versionTerminos: version('termin', '1.0'), versionPrivacidad: version('privac', '1.1'),
      },
    });
    if (r.estado !== 201) throw new Error(`No se pudo registrar al cliente de prueba ${u.id} (${r.estado}): ${JSON.stringify(r.datos)}`);
  }
  admin.agente.destroy();
}

// Ingreso de cada persona al empezar (se mide: el ingreso con BCrypt es una de las rutas a vigilar).
export async function ingresar(contexto, vu, clave) {
  const r = vu.perfil === 'personal'
    ? await pedir(contexto, vu, { metodo: 'POST', ruta: '/api/v1/auth/login', json: { email: vu.correo, password: clave } })
    : await pedir(contexto, vu, { metodo: 'POST', ruta: '/api/v1/auth/login-cliente', json: { correo: vu.correo, password: clave } });
  if (r.estado === 200) vu.token = r.datos.token;
  return r.estado === 200;
}

// ── Lo que hace cada tipo de persona (una «pagina» = las peticiones que dispara abrir esa pantalla) ──
const hoy = () => new Date();
const iso = (f) => f.toISOString().slice(0, 10);
const APELLIDOS = ['gonz', 'muñ', 'roj', 'diaz', 'per', 'sot', 'contr', 'silv', 'mart', 'sepul', 'mora', 'rodr', 'lop', 'fuen', 'hern', 'torr'];

async function paginaPortada(c, vu) {
  await Promise.all([
    pedir(c, vu, { ruta: '/api/v1/local/estado' }),
    pedir(c, vu, { ruta: '/api/v1/catalogo?pagina=1&tamano=12', etiqueta: 'GET /api/v1/catalogo (portada)' }),
    pedir(c, vu, { ruta: '/api/v1/documentos-legales' }),
  ]);
}

async function paginaVitrina(c, vu) {
  const [categorias, primera] = await Promise.all([
    pedir(c, vu, { ruta: '/api/v1/categorias?activo=true&tamano=100' }),
    pedir(c, vu, { ruta: '/api/v1/catalogo?pagina=1&tamano=100', etiqueta: 'GET /api/v1/catalogo (vitrina completa)' }),
  ]);
  vu.datos.categorias = lista(categorias.datos).map((x) => x.idCategoria).filter(Boolean);
  vu.datos.trabajos = lista(primera.datos).map((x) => x.idCatalogo).filter(Boolean);
}

async function paginaFiltroVitrina(c, vu) {
  if (!vu.datos.categorias?.length) return paginaVitrina(c, vu);
  const categoria = elegir(vu.rnd, vu.datos.categorias);
  await pedir(c, vu, { ruta: `/api/v1/catalogo?categoria=${categoria}&pagina=1&tamano=12`, etiqueta: 'GET /api/v1/catalogo?categoria=' });
}

async function paginaDetalleTrabajo(c, vu) {
  if (!vu.datos.trabajos?.length) return paginaVitrina(c, vu);
  const id = elegir(vu.rnd, vu.datos.trabajos);
  await pedir(c, vu, { ruta: `/api/v1/catalogo/${id}`, etiqueta: 'GET /api/v1/catalogo/{id}' });
}

async function paginaFormularioSolicitud(c, vu) {
  await Promise.all([
    pedir(c, vu, { ruta: '/api/v1/productos?activo=true&tamano=100' }),
    pedir(c, vu, { ruta: '/api/v1/categorias?activo=true&tamano=100' }),
  ]);
}

async function paginaMiCuenta(c, vu) {
  const [, solicitudes] = await Promise.all([
    pedir(c, vu, { ruta: '/api/v1/mi-cuenta/', token: vu.token }),
    pedir(c, vu, { ruta: '/api/v1/mis/solicitudes?pagina=1&tamano=20', token: vu.token }),
    pedir(c, vu, { ruta: '/api/v1/mis/pedidos', token: vu.token }),
  ]);
  vu.datos.mias = lista(solicitudes.datos).map((x) => x.idSolicitud ?? x.id).filter(Boolean);
}

async function paginaMiSolicitud(c, vu) {
  if (!vu.datos.mias?.length) return paginaMiCuenta(c, vu);
  await pedir(c, vu, { ruta: `/api/v1/mis/solicitudes/${elegir(vu.rnd, vu.datos.mias)}`, token: vu.token, etiqueta: 'GET /api/v1/mis/solicitudes/{id}' });
}

// Enviar una solicitud en linea: cada cliente manda UNA (la regla contra solicitudes falsas no admite una segunda mientras la primera esta en revision).
async function paginaEnviarSolicitud(c, vu) {
  if (vu.solicitudesEnviadas >= 1) return paginaMiCuenta(c, vu);
  await paginaFormularioSolicitud(c, vu);
  vu.solicitudesEnviadas += 1;
  const r = await pedir(c, vu, {
    metodo: 'POST', ruta: '/api/v1/mis/solicitudes', token: vu.token, esperado: [201],
    cabeceras: { 'Idempotency-Key': crypto.randomUUID() },
    json: {
      descripcionGeneral: 'Letrero de prueba de carga para la vitrina del local',
      detalles: [{ idProducto: 1, descripcion: 'Letrero luminoso de ejemplo', anchoMm: 1000, altoMm: 2000, cantidad: 2 }],
      adjuntos: [],
    },
  });
  const id = r.datos?.idSolicitud;
  if (id) await pedir(c, vu, { ruta: `/api/v1/mis/solicitudes/${id}`, token: vu.token, etiqueta: 'GET /api/v1/mis/solicitudes/{id}' });
}

async function paginaBandeja(c, vu) {
  const variantes = [
    ['/api/v1/solicitudes?pagina=1&tamano=20', 'GET /api/v1/solicitudes'],
    ['/api/v1/solicitudes?estado=solicitado&pagina=1&tamano=20', 'GET /api/v1/solicitudes?estado='],
    ['/api/v1/solicitudes?origen=online&pagina=2&tamano=20', 'GET /api/v1/solicitudes?origen='],
    [`/api/v1/solicitudes?q=${encodeURIComponent(elegir(vu.rnd, APELLIDOS))}&pagina=1&tamano=20`, 'GET /api/v1/solicitudes?q='],
  ];
  const [ruta, etiqueta] = elegir(vu.rnd, variantes);
  const r = await pedir(c, vu, { ruta, etiqueta, token: vu.token });
  vu.datos.solicitudes = lista(r.datos).map((x) => x.idSolicitud).filter(Boolean);
}

async function paginaFichaSolicitud(c, vu) {
  if (!vu.datos.solicitudes?.length) return paginaBandeja(c, vu);
  await pedir(c, vu, { ruta: `/api/v1/solicitudes/${elegir(vu.rnd, vu.datos.solicitudes)}`, token: vu.token, etiqueta: 'GET /api/v1/solicitudes/{id}' });
}

async function paginaPedidos(c, vu) {
  const variantes = [
    ['/api/v1/pedidos?pagina=1&tamano=20', 'GET /api/v1/pedidos'],
    ['/api/v1/pedidos?estadoPedido=en_proceso&pagina=1&tamano=20', 'GET /api/v1/pedidos?estadoPedido='],
    ['/api/v1/pedidos?cobranza=true&pagina=1&tamano=20', 'GET /api/v1/pedidos?cobranza='],
    [`/api/v1/pedidos?anio=${hoy().getFullYear() - 1}&mes=${1 + Math.floor(vu.rnd() * 12)}&pagina=1&tamano=20`, 'GET /api/v1/pedidos?anio=&mes='],
    [`/api/v1/pedidos?q=${encodeURIComponent(elegir(vu.rnd, APELLIDOS))}&pagina=1&tamano=20`, 'GET /api/v1/pedidos?q='],
  ];
  const [ruta, etiqueta] = elegir(vu.rnd, variantes);
  const r = await pedir(c, vu, { ruta, etiqueta, token: vu.token });
  vu.datos.pedidos = lista(r.datos).map((x) => x.idPedido).filter(Boolean);
}

async function paginaFichaPedido(c, vu) {
  if (!vu.datos.pedidos?.length) return paginaPedidos(c, vu);
  await pedir(c, vu, { ruta: `/api/v1/pedidos/${elegir(vu.rnd, vu.datos.pedidos)}`, token: vu.token, etiqueta: 'GET /api/v1/pedidos/{id}' });
}

async function paginaCalendario(c, vu) {
  const desde = new Date(hoy().getFullYear(), hoy().getMonth(), 1);
  const hasta = new Date(hoy().getFullYear(), hoy().getMonth() + 1, 0);
  await pedir(c, vu, { ruta: `/api/v1/pedidos/calendario?desde=${iso(desde)}&hasta=${iso(hasta)}`, token: vu.token });
}

// Buscar un cliente escribiendo (el filtro «Cliente» con debounce): tres busquedas seguidas, cada vez con mas letras.
async function paginaBuscarCliente(c, vu) {
  const palabra = elegir(vu.rnd, APELLIDOS);
  for (const largo of [3, 4, 5]) {
    const q = palabra.slice(0, Math.min(largo, palabra.length));
    await pedir(c, vu, { ruta: `/api/v1/clientes?q=${encodeURIComponent(q)}&pagina=1&tamano=10`, token: vu.token, etiqueta: 'GET /api/v1/clientes?q=&tamano=10' });
    await dormir(entre(vu.rnd, 250, 450)); // el debounce de la pantalla
  }
}

async function paginaListaClientes(c, vu) {
  const r = await pedir(c, vu, { ruta: `/api/v1/clientes?pagina=${1 + Math.floor(vu.rnd() * 20)}&tamano=20`, token: vu.token, etiqueta: 'GET /api/v1/clientes' });
  vu.datos.clientes = lista(r.datos).map((x) => x.idCliente).filter(Boolean);
}

async function paginaFichaCliente(c, vu) {
  if (!vu.datos.clientes?.length) return paginaListaClientes(c, vu);
  await pedir(c, vu, { ruta: `/api/v1/clientes/${elegir(vu.rnd, vu.datos.clientes)}`, token: vu.token, etiqueta: 'GET /api/v1/clientes/{id}' });
}

// El panel de indicadores pide sus seis bloques a la vez (useIndicadores.js).
async function paginaIndicadores(c, vu) {
  const periodo = elegir(vu.rnd, ['semana', 'mes', 'anio']);
  await Promise.all(['resumen', 'evolucion', 'cobranza', 'embudo', 'demanda', 'por-cobrar'].map((bloque) =>
    pedir(c, vu, { ruta: `/api/v1/indicadores/${bloque}?periodo=${periodo}`, token: vu.token, etiqueta: `GET /api/v1/indicadores/${bloque}` })));
}

async function paginaReporte(c, vu) {
  const tipo = vu.gestion ? elegir(vu.rnd, ['operativo', 'gerencial', 'parametrizable']) : 'operativo';
  await pedir(c, vu, { ruta: `/api/v1/reportes/${tipo}?periodo=${elegir(vu.rnd, ['mes', 'anio'])}`, token: vu.token, etiqueta: `GET /api/v1/reportes/${tipo}` });
}

async function paginaExportar(c, vu) {
  const r = await pedir(c, vu, {
    metodo: 'POST', ruta: '/api/v1/reportes/operativo/exportaciones', token: vu.token, esperado: [202],
    json: { formato: 'csv', filtros: { periodo: 'mes' } }, etiqueta: 'POST /api/v1/reportes/operativo/exportaciones',
  });
  const id = r.datos?.id;
  if (id) {
    await dormir(1500);
    await pedir(c, vu, { ruta: `/api/v1/reportes/exportaciones/${id}`, token: vu.token, etiqueta: 'GET /api/v1/reportes/exportaciones/{id}' });
  }
}

async function paginaAuditoria(c, vu) {
  const variantes = [
    ['/api/v1/auditoria?pagina=1&tamano=20', 'GET /api/v1/auditoria'],
    ['/api/v1/auditoria?entidad=pedido&pagina=1&tamano=20', 'GET /api/v1/auditoria?entidad='],
    [`/api/v1/auditoria?desde=${iso(new Date(Date.now() - 30 * 864e5))}&hasta=${iso(hoy())}&pagina=1&tamano=20`, 'GET /api/v1/auditoria?desde=&hasta='],
    [`/api/v1/auditoria?q=${1 + Math.floor(vu.rnd() * 500)}&pagina=1&tamano=20`, 'GET /api/v1/auditoria?q='],
  ];
  const [ruta, etiqueta] = elegir(vu.rnd, variantes);
  await pedir(c, vu, { ruta, etiqueta, token: vu.token });
}

// Cada perfil: lista de [peso, pagina]. Una persona abre una pantalla, la mira unos segundos y abre otra.
const MEZCLA = {
  visitante: [[30, paginaPortada], [25, paginaVitrina], [15, paginaFiltroVitrina], [15, paginaDetalleTrabajo], [15, paginaFormularioSolicitud]],
  cliente: [[30, paginaMiCuenta], [15, paginaMiSolicitud], [15, paginaEnviarSolicitud], [25, paginaPortada], [15, paginaVitrina]],
  personal: [
    [18, paginaBandeja], [8, paginaFichaSolicitud], [14, paginaPedidos], [8, paginaFichaPedido], [4, paginaCalendario],
    [12, paginaBuscarCliente], [6, paginaListaClientes], [4, paginaFichaCliente],
    [10, paginaReporte], [2, paginaExportar],
  ],
  personalGestion: [[14, paginaIndicadores], [8, paginaAuditoria]],
};

function elegirPagina(vu) {
  const lista = vu.perfil === 'personal' && vu.gestion ? [...MEZCLA.personal, ...MEZCLA.personalGestion] : MEZCLA[vu.perfil];
  let r = vu.rnd() * lista.reduce((s, [p]) => s + p, 0);
  for (const [peso, pagina] of lista) {
    r -= peso;
    if (r < 0) return pagina;
  }
  return lista[0][1];
}

// Un usuario: ingresa (si tiene cuenta) y recorre pantallas hasta que se acaba el tiempo. La pausa entre pantallas es el «leer»; entre
// peticiones de una misma pantalla no hay pausa (las dispara el navegador).
export async function correrUsuario(contexto, vu, { clave, pensarMs: [min, max] }) {
  if (vu.perfil !== 'visitante') {
    const dentro = await ingresar(contexto, vu, clave);
    if (!dentro) return;
    await dormir(entre(vu.rnd, min, max));
  }
  while (!contexto.detener) {
    await elegirPagina(vu)(contexto, vu);
    await dormir(entre(vu.rnd, min, max));
    // Una persona de la oficina vuelve a ingresar de vez en cuando (se le vence la sesion): mide el costo de BCrypt.
    if (vu.perfil !== 'visitante' && vu.rnd() < (vu.perfil === 'personal' ? 0.005 : 0.03)) await ingresar(contexto, vu, clave);
  }
}
