import { RESPUESTAS, destinoDe, entrarComo, entrarComoCliente } from './apiSimulada.js';

// Las pantallas de la aplicación con el título de su <h1> (fase 15B: antes vivían dentro de pantallas.spec.js). Las usan el recorrido
// de accesibilidad (axe a 360, 768 y 1440 px), la prueba de llamadas duplicadas y la de red lenta.
export const PANTALLAS = [
  ['/', 'Letreros que se ven de lejos.'],
  ['/ingresar', '¿Quién está iniciando sesión?'],
  ['/ingresar/cliente', 'Ingresar a tu cuenta'],
  ['/consultas', 'Escríbenos'],
  ['/cuenta/registro', 'Crear cuenta'],
  ['/cuenta/recuperar', 'Recuperar tu contraseña'],
  ['/cuenta', 'Mi cuenta'],
  ['/personal/ingresar', 'Acceso del personal'],
  ['/privacidad', 'Política de privacidad'],
  ['/terminos', 'Términos y condiciones'],
  ['/panel', 'Hola, Marta'],
  ['/panel/pedidos', 'Pedidos'],
  ['/panel/local', 'Estado del local'],
  ['/panel/indicadores', 'Indicadores'],
  ['/panel/reportes', 'Reportes'],
  // Fase 13B: el gerencial (con sus gráficos) y la cobranza que llega desde el dashboard.
  ['/panel/reportes?reporte=gerencial', 'Reportes'],
  ['/panel/reportes?reporte=parametrizable&estadoPago=por_cobrar&alcanceSaldo=vigente', 'Reportes'],
  ['/cuenta/solicitud', 'Solicitar una cotización'],
  ['/cuenta/solicitudes', 'Mis solicitudes'],
  ['/cuenta/solicitudes/123', 'Solicitud #123'],
  ['/panel/solicitudes', 'Solicitudes'],
  ['/panel/solicitudes/123', 'Solicitud #123'],
  ['/panel/solicitudes/nueva', 'Registrar solicitud presencial'],
  ['/panel/clientes', 'Clientes'],
  ['/panel/clientes/9', 'Rosa Pino'],
  ['/panel/cotizaciones', 'Cotizaciones'],
  ['/panel/cotizaciones/45', 'Cotización #45'],
  ['/panel/cotizaciones/45/editar', 'Editar la cotización #45'],
  ['/panel/solicitudes/123/cotizar', 'Cotizar la solicitud #123'],
  ['/panel/cotizaciones/46/pedido', 'Crear pedido de la cotización #46'],
  ['/panel/pedidos?cobranza=true', 'Pedidos por cobrar'],
  ['/panel/pedidos/1', 'Pedido #1'],
  ['/panel/pedidos/directo', 'Pedido directo en el taller'],
  ['/panel/calendario', 'Calendario de entregas'],
  ['/panel/perfil', 'Mi perfil'],
  ['/panel/rostros', 'Rostros registrados'],
  ['/panel/rostros/calibrar', 'Calibrar el rostro'],
  ['/panel/mantenedores/categorias', 'Catálogo y mantenedores'],
  ['/panel/mantenedores/vitrina', 'Catálogo y mantenedores'],
  ['/panel/empleados', 'Empleados'],
  ['/panel/mantenedores/categorias/1', 'Luminosos'],
  ['/panel/mantenedores/vitrina/2', 'Vinilo para vitrina de ferretería'],
  ['/panel/empleados/2', 'Hugo Vera'],
  ['/panel/mantenedores/tipos-de-letrero/1', 'Roll Up / Pendones Roller'],
  ['/panel/empleados/nuevo', 'Agregar persona'],
  ['/avisos/confirmar?token=abc123', 'Correo confirmado'],
  // Fase 11
  ['/panel/correos-no-enviados', 'Correos no enviados'],
  ['/panel/auditoria', 'Auditoría'],
  ['/panel/auditoria/consentimientos', 'Auditoría'],
  ['/baja?t=abc123', 'Darte de baja de los correos'],
  // Fase 15B: las rutas que faltaban para recorrer TODAS las de App.jsx (la prueba src/test/fase15bRutas.test.js lo exige).
  ['/cuenta/verificar?token=abc123', 'Tu correo está confirmado'],
  ['/panel/mantenedores', 'Catálogo y mantenedores'],
  ['/panel/mantenedores/categorias/nuevo', 'Nueva categoría'],
  ['/ruta-que-no-existe', 'No encontramos esta página'],
  ['/panel/ruta-que-no-existe', 'No encontramos esta página'],
];

/** Las direcciones antiguas que solo redirigen: [desde, hasta, título de la pantalla de destino]. Las abre e2e/rutasNuevas.spec.js. */
export const REDIRECCIONES = [
  ['/login', '/personal/ingresar', 'Acceso del personal'],
  ['/dashboard', '/panel', 'Hola, Marta'],
];

/**
 * Pantallas que no se pueden abrir con una dirección a secas y por eso se recorren en otra prueba que también pasa axe:
 * «Completa tu registro» necesita el pase que entrega Google en esa misma sesión (vive solo en memoria).
 */
export const VIVEN_EN_OTRA_PRUEBA = {
  '/cuenta/google/completar': 'e2e/google.spec.js',
};

/**
 * Las rutas del conjunto esencial (etiqueta @esencial): lo que corre además en Edge, Brave y el teléfono (RNFP05). El recorrido completo corre
 * en Chrome. Portada, ingreso del cliente y del personal, registro, Mi cuenta, solicitud en línea y panel.
 */
export const ESENCIALES = new Set(['/', '/ingresar/cliente', '/personal/ingresar', '/cuenta/registro', '/cuenta', '/cuenta/solicitud', '/panel']);

/** Quién entra a cada ruta: el personal al panel, el cliente a su cuenta y las demás son públicas. */
export function contextoDe(ruta) {
  if (ruta.startsWith('/panel')) return 'personal';
  if (ruta === '/cuenta' || ruta.startsWith('/cuenta/solicitud')) return 'cliente';
  return 'publico';
}

/** Deja la API simulada y la sesión que pide la ruta (el personal entra como dueño; el cliente, con el correo verificado salvo en /cuenta). */
export async function prepararApi(page, ruta) {
  const contexto = contextoDe(ruta);
  if (contexto === 'personal') return entrarComo(page, 'dueño');
  if (contexto === 'cliente') return entrarComoCliente(page, { verificado: ruta !== '/cuenta' });
  return page.route('**/api/v1/**', (r) => r.fulfill({ json: RESPUESTAS[destinoDe(r)] ?? [] }));
}
