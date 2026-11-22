// Calculos del guion de carga (fase 15A, RNFP01). Solo numeros: sin red, sin reloj, sin base de datos.
// Una MUESTRA es { ruta: 'GET /api/v1/x', estado: 200, ms: 123, esperado: true, error?: 'timeout' }.
//   - estado 0 = no hubo respuesta (red, tiempo de espera).
//   - esperado = la respuesta es la que el guion queria (por ejemplo 200, o 201 al crear). Un 429 o un 500 NO lo son.

// Rango mas cercano: el menor valor que deja al p % de la lista a su izquierda. No modifica la lista.
export function percentil(valores, p) {
  if (valores.length === 0) return null;
  const orden = [...valores].sort((a, b) => a - b);
  const lugar = Math.max(1, Math.ceil((p / 100) * orden.length));
  return orden[lugar - 1];
}

const esBuena = (m, umbralMs) => m.esperado === true && m.estado !== 0 && m.ms <= umbralMs;

// La meta del requisito: «el 95 % de las respuestas en 2 segundos o menos». Una respuesta equivocada (500, 429, sin respuesta)
// cuenta como NO buena aunque sea rapida: responder rapido un error no es atender a la persona.
export function evaluarMeta(muestras, { umbralMs = 2000, fraccion = 0.95 } = {}) {
  const total = muestras.length;
  const buenas = muestras.filter((m) => esBuena(m, umbralMs)).length;
  const fraccionBuena = total === 0 ? 0 : buenas / total;
  return { total, buenas, fraccionBuena, umbralMs, fraccionMeta: fraccion, cumple: total > 0 && fraccionBuena >= fraccion };
}

// Una fila por ruta, de la mas lenta (p95) a la mas rapida: lo primero que se lee es lo que hay que arreglar.
export function resumirPorRuta(muestras, { umbralMs = 2000 } = {}) {
  const porRuta = new Map();
  for (const m of muestras) {
    if (!porRuta.has(m.ruta)) porRuta.set(m.ruta, []);
    porRuta.get(m.ruta).push(m);
  }
  const filas = [];
  for (const [ruta, lista] of porRuta) {
    const tiempos = lista.map((m) => m.ms);
    filas.push({
      ruta,
      n: lista.length,
      mediana: percentil(tiempos, 50),
      p95: percentil(tiempos, 95),
      max: Math.max(...tiempos),
      dentroDeLaMeta: lista.filter((m) => esBuena(m, umbralMs)).length,
      inesperadas: lista.filter((m) => m.esperado !== true).length,
    });
  }
  return filas.sort((a, b) => b.p95 - a.p95 || b.n - a.n || a.ruta.localeCompare(b.ruta));
}

export function resumirEstados(muestras) {
  const porCodigo = {};
  for (const m of muestras) porCodigo[m.estado] = (porCodigo[m.estado] ?? 0) + 1;
  return {
    porCodigo,
    limitadas: muestras.filter((m) => m.estado === 429).length,
    errores5xx: muestras.filter((m) => m.estado >= 500).length,
    sinRespuesta: muestras.filter((m) => m.estado === 0).length,
    inesperadas: muestras.filter((m) => m.esperado !== true).length,
  };
}
