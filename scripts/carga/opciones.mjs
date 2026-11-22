// Opciones de linea de comandos de la prueba de carga (fase 15A). Sin paquetes: un lector pequeno y estricto.
// Una opcion desconocida o un numero absurdo se rechaza en vez de ignorarse: una prueba de carga que corre con otra cosa que la pedida miente.

const AYUDA = `Prueba de carga de SyncTime (fase 15A). Uso: node scripts/carga/carga.mjs [opciones]

  --usuarios N            usuarios simultaneos (20). Se reparten en ~40 % visitantes, ~30 % clientes y ~30 % personal
  --duracion S            segundos de medicion (120)
  --calentamiento S       segundos previos que no se cuentan (15)
  --pensar MIN,MAX        pausa entre pantallas, en milisegundos (1000,3000). Para un estres: 100,400
  --limites reales|holgados   «reales» deja los limites de tasa de produccion; «holgados» los sube para aislar el rendimiento (reales)
  --datos-clientes N      clientes sinteticos (2000)
  --datos-solicitudes N   solicitudes sinteticas (6000); ~40 % pasan a pedido
  --datos-auditoria N     filas de bitacora sinteticas (40000)
  --datos-publicaciones N publicaciones de la vitrina (60)
  --salida RUTA           informe en Markdown (docs/evidencias/carga-15a.md)
  --json RUTA             ademas, todas las muestras en JSON
  --diagnostico           registra las consultas de EF Core y lista las mas lentas (algo mas lento)
  --conservar             no borra la base temporal ni la carpeta temporal al terminar
  --semilla N             semilla del azar (15)
  --ayuda

Variables de entorno: TEST_SQL_SERVER (servidor, por omision localhost\\SQLEXPRESS), SYNCTIME_CARGA_CLAVE (clave de las cuentas de
prueba; si falta, se genera una al azar para esta corrida). Nunca se usa la base real ni la demo: la base es temporal y se borra al terminar.`;

function entero(valor, nombre, min, max) {
  const n = Number(valor);
  if (!Number.isInteger(n) || n < min || n > max) throw new Error(`--${nombre} debe ser un entero entre ${min} y ${max}.`);
  return n;
}

// Reparto de los usuarios: personal y clientes cerca del 30 % cada uno (al menos 1 de cada uno desde 3 usuarios); el resto, visitantes.
export function repartir(usuarios) {
  if (usuarios < 3) return { visitantes: usuarios, clientes: 0, personal: 0 };
  const personal = Math.max(1, Math.round(usuarios * 0.3));
  const clientes = Math.max(1, Math.round(usuarios * 0.3));
  return { visitantes: usuarios - personal - clientes, clientes, personal };
}

export function crearOpciones(argv) {
  const o = {
    usuarios: 20, duracionS: 120, calentamientoS: 15, pensarMs: [1000, 3000], limites: 'reales',
    volumen: { clientes: 2000, solicitudes: 6000, auditoria: 40000, publicaciones: 60 },
    salida: 'docs/evidencias/carga-15a.md', json: null, diagnostico: false, conservar: false, semilla: 15, ayuda: false, textoDeAyuda: AYUDA,
  };
  for (let i = 0; i < argv.length; i += 1) {
    const bandera = argv[i];
    const siguiente = () => {
      if (i + 1 >= argv.length) throw new Error(`Falta el valor de ${bandera}.`);
      i += 1;
      return argv[i];
    };
    switch (bandera) {
      case '--usuarios': o.usuarios = entero(siguiente(), 'usuarios', 1, 100); break;
      case '--duracion': o.duracionS = entero(siguiente(), 'duracion', 5, 3600); break;
      case '--calentamiento': o.calentamientoS = entero(siguiente(), 'calentamiento', 0, 600); break;
      case '--pensar': {
        const partes = siguiente().split(',').map(Number);
        if (partes.length !== 2 || partes.some((n) => !Number.isFinite(n) || n < 0) || partes[0] > partes[1]) throw new Error('--pensar debe ser «min,max» en milisegundos, con min menor o igual que max.');
        o.pensarMs = partes;
        break;
      }
      case '--limites': {
        const v = siguiente();
        if (v !== 'reales' && v !== 'holgados') throw new Error('--limites admite «reales» o «holgados».');
        o.limites = v;
        break;
      }
      case '--datos-clientes': o.volumen.clientes = entero(siguiente(), 'datos-clientes', 10, 50_000); break;
      case '--datos-solicitudes': o.volumen.solicitudes = entero(siguiente(), 'datos-solicitudes', 10, 100_000); break;
      case '--datos-auditoria': o.volumen.auditoria = entero(siguiente(), 'datos-auditoria', 0, 500_000); break;
      case '--datos-publicaciones': o.volumen.publicaciones = entero(siguiente(), 'datos-publicaciones', 0, 2000); break;
      case '--salida': o.salida = siguiente(); break;
      case '--json': o.json = siguiente(); break;
      case '--diagnostico': o.diagnostico = true; break;
      case '--conservar': o.conservar = true; break;
      case '--semilla': o.semilla = entero(siguiente(), 'semilla', 0, 1_000_000); break;
      case '--ayuda': case '-h': case '--help': o.ayuda = true; break;
      default: throw new Error(`Opcion desconocida: ${bandera}. Usa --ayuda.`);
    }
  }
  return { ...o, ...repartir(o.usuarios) };
}
