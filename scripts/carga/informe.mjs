// El informe en Markdown de la prueba de carga (fase 15A). Funcion pura: recibe los datos medidos y devuelve el texto.
import { evaluarMeta, resumirPorRuta, resumirEstados, percentil } from './metricas.mjs';

const UMBRAL_MS = 2000;
const FRACCION = 0.95;
const pct = (x) => `${(x * 100).toFixed(1).replace('.', ',')} %`;
const ms = (x) => (x === null || x === undefined ? '—' : `${Math.round(x).toLocaleString('es-CL')} ms`);
const mb = (bytes) => `${Math.round(bytes / 2 ** 20).toLocaleString('es-CL')} MB`;
const celda = (t) => String(t).replace(/\|/g, '\\|');

export function redactarInforme({ fecha, configuracion: c, volumen: v, equipo: e, muestras, recursos, consultasLentas, notas = [] }) {
  const meta = evaluarMeta(muestras, { umbralMs: UMBRAL_MS, fraccion: FRACCION });
  const estados = resumirEstados(muestras);
  const filas = resumirPorRuta(muestras, { umbralMs: UMBRAL_MS });
  const todos = muestras.map((m) => m.ms);
  const veredicto = meta.cumple ? '**CUMPLE**' : '**NO CUMPLE**';
  const l = [];

  l.push('# Prueba de carga — fase 15A (RNFP01, RNFO13)', '');
  l.push(`Fecha: ${fecha} · Guion: \`scripts/carga/carga.mjs\` · Resultado frente a la meta (el ${pct(FRACCION)} de las respuestas en ${UMBRAL_MS / 1000} s o menos): ${veredicto}`, '');
  l.push('## Qué se midió', '');
  l.push(`- **${c.usuarios} usuarios simultáneos** (${c.visitantes} visitantes, ${c.clientes} clientes con cuenta y ${c.personal} personas del taller), cada uno con su propia dirección de origen; el personal comparte una, como en la oficina.`);
  l.push(`- Duración medida: ${c.duracionS} s (más ${c.calentamientoS} s de calentamiento que no se cuentan). Pausa entre pantallas: ${c.pensarMs[0] / 1000} a ${c.pensarMs[1] / 1000} s por persona.`);
  l.push(`- Límites de tasa de la API: **${c.limites === 'reales' ? 'los reales de producción (sin aflojar nada)' : 'aflojados (solo para aislar el rendimiento; no es el comportamiento real)'}**.`);
  l.push(`- Datos sintéticos (nada real ni personal): ${v.clientes.toLocaleString('es-CL')} clientes, ${v.solicitudes.toLocaleString('es-CL')} solicitudes, unos ${Math.round(v.solicitudes * 0.4).toLocaleString('es-CL')} pedidos con sus abonos, ${v.auditoria.toLocaleString('es-CL')} filas de bitácora y ${v.publicaciones} publicaciones, en una **base temporal** que se borró al terminar. Nunca la base real ni la demo.`);
  l.push(`- Equipo: ${e.sistema}, ${e.procesador} (${e.nucleos} núcleos lógicos), ${mb(e.ramTotalMb * 2 ** 20)} de RAM con ${mb(e.ramLibreMb * 2 ** 20)} libres al empezar, SQL Server Express en el mismo equipo. **Las cifras dependen de este hardware**: la API, la base de datos y los veinte usuarios virtuales compiten por la misma CPU y memoria.`, '');

  l.push('## Resultado', '');
  l.push(`- Respuestas medidas: **${meta.total.toLocaleString('es-CL')}**. Buenas (el código esperado y en ${UMBRAL_MS / 1000} s o menos): **${meta.buenas.toLocaleString('es-CL')} (${pct(meta.fraccionBuena)})** → ${veredicto}.`);
  l.push(`- Mediana ${ms(percentil(todos, 50))} · p95 ${ms(percentil(todos, 95))} · p99 ${ms(percentil(todos, 99))} · máximo ${ms(Math.max(...todos, 0))}.`);
  l.push(`- Límite de tasa (429): ${estados.limitadas} · Errores del servidor (5xx): ${estados.errores5xx} · Sin respuesta: ${estados.sinRespuesta} · Respuestas distintas de las esperadas: ${estados.inesperadas}.`);
  l.push(`- Códigos recibidos: ${Object.entries(estados.porCodigo).map(([k, n]) => `${k} × ${n}`).join(', ') || '—'}.`, '');

  const porPerfil = ['visitante', 'cliente', 'personal'].map((p) => {
    const s = muestras.filter((m) => m.perfil === p);
    if (s.length === 0) return null;
    const mp = evaluarMeta(s, { umbralMs: UMBRAL_MS, fraccion: FRACCION });
    return `| ${p} | ${s.length.toLocaleString('es-CL')} | ${ms(percentil(s.map((m) => m.ms), 50))} | ${ms(percentil(s.map((m) => m.ms), 95))} | ${pct(mp.fraccionBuena)} |`;
  }).filter(Boolean);
  l.push('### Por tipo de usuario', '', '| Tipo | Respuestas | Mediana | p95 | Buenas |', '|---|---:|---:|---:|---:|', ...porPerfil, '');

  l.push('### Por ruta (de la más lenta a la más rápida, por p95)', '');
  l.push('| Ruta | Respuestas | Mediana | p95 | Máximo | Buenas | Inesperadas |', '|---|---:|---:|---:|---:|---:|---:|');
  for (const f of filas) l.push(`| \`${celda(f.ruta)}\` | ${f.n} | ${ms(f.mediana)} | ${ms(f.p95)} | ${ms(f.max)} | ${f.dentroDeLaMeta}/${f.n} | ${f.inesperadas} |`);
  l.push('');

  const lentas = filas.filter((f) => f.p95 > UMBRAL_MS);
  const malas = filas.filter((f) => f.inesperadas > 0);
  if (lentas.length > 0) l.push(`**Rutas con p95 sobre ${UMBRAL_MS / 1000} s:** ${lentas.map((f) => `\`${f.ruta}\``).join(', ')}.`, '');
  if (malas.length > 0) {
    const detalle = new Map();
    for (const m of muestras.filter((x) => x.esperado !== true)) {
      const clave = `${m.ruta} → ${m.estado === 0 ? `sin respuesta (${m.error ?? 'error'})` : m.estado}${m.codigo ? ` ${m.codigo}` : ''}`;
      detalle.set(clave, (detalle.get(clave) ?? 0) + 1);
    }
    l.push('**Respuestas distintas de las esperadas:**', '', ...[...detalle].sort((a, b) => b[1] - a[1]).map(([k, n]) => `- ${n} × \`${celda(k)}\``), '');
  }

  if (recursos) {
    l.push('## Recursos del equipo durante la medición', '');
    const cpu = (n) => (n === null ? 'CPU no disponible (Windows exige permisos de administrador para leerla)' : `${n.toFixed(2)} núcleos de CPU en promedio`);
    l.push(`- API (.NET): ${cpu(recursos.api.nucleos)}, ${mb(recursos.api.memoriaMax)} de memoria como máximo.`);
    if (recursos.sql) l.push(`- SQL Server: ${cpu(recursos.sql.nucleos)}, ${mb(recursos.sql.memoriaMax)} de memoria como máximo.`);
    l.push(`- Memoria libre del equipo al terminar: ${mb(recursos.ramLibreFin * 2 ** 20)}.`, '');
  }

  if (consultasLentas?.length > 0) {
    l.push('## Consultas SQL más lentas (modo diagnóstico)', '', '| Tiempo | Consulta |', '|---:|---|');
    for (const q of consultasLentas) l.push(`| ${ms(q.ms)} | \`${celda(q.sql)}\` |`);
    l.push('');
  }
  if (notas.length > 0) l.push('## Notas', '', ...notas.map((n) => `- ${n}`), '');
  return l.join('\n');
}
