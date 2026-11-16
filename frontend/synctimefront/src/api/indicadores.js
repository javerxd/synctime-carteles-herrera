import { api } from './cliente';
import { crearDatosDemo } from './datosDemoIndicadores';
import {
  calcularCobranza, calcularDemanda, calcularEmbudo, calcularEvolucion, calcularPorCobrar, calcularResumen,
} from './indicadoresDemo';
import { parametrosIndicadores } from '../paginas/indicadores/reglasIndicadores';
import { hoyChile } from '../utilidades/fechas';

// Dashboard de gestión (fase 12): GET /api/v1/indicadores/* (solo dueño y administrador). Contrato en
// docs/plan/fase-12-especificacion.md. Mientras el backend no exista (12A), las respuestas las calcula el
// adaptador de demostración con datos de ejemplo; la pantalla lo avisa con «Datos de demostración».
// En la 12B se apaga con VITE_INDICADORES_DEMO=false, sin tocar las pantallas. Sin la variable, la demo solo se
// enciende en desarrollo y en las pruebas: un build de producción nunca muestra cifras de ejemplo por olvido.
const VARIABLE_DEMO = import.meta.env.VITE_INDICADORES_DEMO;
export const ES_DEMO = VARIABLE_DEMO === 'true' || (VARIABLE_DEMO === undefined && (import.meta.env.DEV || import.meta.env.MODE === 'test'));

// Una pequeña espera en la demo deja ver los esqueletos de carga, como con la API real. En las pruebas, ninguna.
const ESPERA_DEMO = import.meta.env.MODE === 'test' ? 0 : 280;

let cache = null;
function datosDemo() {
  const hoy = hoyChile();
  if (cache?.hoy !== hoy) cache = crearDatosDemo(hoy);
  return cache;
}

function demo(calcular, filtros, senal, extra) {
  return new Promise((resolver, rechazar) => {
    const abortar = () => rechazar(new DOMException('Consulta cancelada', 'AbortError'));
    if (senal?.aborted) return abortar();
    const reloj = setTimeout(() => {
      senal?.removeEventListener('abort', alAbortar);
      try {
        resolver(calcular(datosDemo(), { ...filtros, ...extra }));
      } catch (error) {
        rechazar(error);
      }
    }, ESPERA_DEMO);
    function alAbortar() {
      clearTimeout(reloj);
      abortar();
    }
    senal?.addEventListener('abort', alAbortar, { once: true });
    return undefined;
  });
}

const consulta = (ruta, calcular, { listado = false } = {}) => (filtros, { senal } = {}) => {
  const parametros = parametrosIndicadores(filtros, { listado });
  return ES_DEMO
    ? demo(calcular, parametros, senal)
    : api.get(`/indicadores/${ruta}`, { parametros, senal });
};

/**
 * Las seis consultas del dashboard. Cada una recibe los filtros de la pantalla ({ periodo, desde, hasta, anio, mes,
 * comparar, estadoPedido, estadoPago, categoria, origen, cliente, alcanceSaldo, cobro, pagina }) y { senal }.
 */
export const indicadores = {
  resumen: consulta('resumen', calcularResumen),
  evolucion: consulta('evolucion', calcularEvolucion),
  cobranza: consulta('cobranza', calcularCobranza),
  embudo: consulta('embudo', calcularEmbudo),
  demanda: consulta('demanda', calcularDemanda),
  porCobrar: consulta('por-cobrar', calcularPorCobrar, { listado: true }),

  /** Las opciones de los filtros: categorías y clientes ({ idCliente, nombre }). */
  async catalogos({ senal } = {}) {
    if (ES_DEMO) {
      const { categorias, clientes } = datosDemo();
      return { categorias, clientes: clientes.map((c) => ({ idCliente: c.idCliente, nombre: `${c.nombre} ${c.apellido}`.trim() })) };
    }
    const [categorias, clientes] = await Promise.all([
      api.get('/categorias', { senal }),
      api.get('/clientes', { parametros: { tamano: 100 }, senal }),
    ]);
    const filas = Array.isArray(clientes) ? clientes : clientes?.elementos ?? [];
    return {
      categorias: (Array.isArray(categorias) ? categorias : categorias?.elementos ?? []).map(({ idCategoria, nombre }) => ({ idCategoria, nombre })),
      clientes: filas.map((c) => ({ idCliente: c.idCliente, nombre: [c.nombre, c.apellido].filter(Boolean).join(' ') })),
    };
  },
};
