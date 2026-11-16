import { avisosTaller } from '../api/avisosTaller';
import { configurarClienteApi } from '../api/cliente';
import { cotizaciones } from '../api/cotizaciones';
import { bajaCorreos, correos } from '../api/correos';
import { cuenta } from '../api/cuenta';
import { panel } from '../api/panel';
import { pedidos } from '../api/pedidos';
import { publico } from '../api/publico';
import { clientes, misSolicitudes, solicitudes } from '../api/solicitudes';

function respuesta(estado, cuerpo, cabeceras = {}) {
  return Promise.resolve(new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status: estado,
    headers: { 'Content-Type': 'application/json', ...cabeceras },
  }));
}

let fetchSimulado;
beforeEach(() => {
  fetchSimulado = vi.fn(() => respuesta(201, { ok: true }));
  vi.stubGlobal('fetch', fetchSimulado);
  configurarClienteApi({ obtenerToken: () => null, alSesionVencida: vi.fn() });
});
afterEach(() => vi.unstubAllGlobals());

const ultima = () => {
  const [url, opciones] = fetchSimulado.mock.calls.at(-1);
  return { url, metodo: opciones.method, cabeceras: opciones.headers, cuerpo: opciones.body ? JSON.parse(opciones.body) : undefined };
};

describe('Idempotency-Key en las seis creaciones', () => {
  const CASOS = [
    ['POST /solicitudes', () => solicitudes.crear({ idCliente: 1 }, { clave: 'k-1' }), '/api/v1/solicitudes'],
    ['POST /mis/solicitudes', () => misSolicitudes.enviar({ detalles: [] }, { clave: 'k-1' }), '/api/v1/mis/solicitudes'],
    ['POST /solicitudes/{id}/cotizaciones', () => cotizaciones.emitir(5, { materiales: [] }, { clave: 'k-1' }), '/api/v1/solicitudes/5/cotizaciones'],
    ['POST /pedidos/directo', () => pedidos.crearDirecto({ idCliente: 1 }, { clave: 'k-1' }), '/api/v1/pedidos/directo'],
    ['POST /cotizaciones/{id}/pedido', () => pedidos.crearDesdeCotizacion(46, {}, { clave: 'k-1' }), '/api/v1/cotizaciones/46/pedido'],
    ['POST /pedidos/{id}/pagos', () => pedidos.registrarAbono(1, { monto: 1000 }, { clave: 'k-1' }), '/api/v1/pedidos/1/pagos'],
  ];
  for (const [nombre, llamar, url] of CASOS) {
    it(`${nombre} envía la clave en la cabecera`, async () => {
      await llamar();
      expect(ultima().url).toBe(url);
      expect(ultima().metodo).toBe('POST');
      expect(ultima().cabeceras.get('Idempotency-Key')).toBe('k-1');
    });
  }

  it('sin clave no envía la cabecera (retrocompatible)', async () => {
    await solicitudes.crear({ idCliente: 1 });
    expect(ultima().cabeceras.has('Idempotency-Key')).toBe(false);
  });

  it('una respuesta repetida (Idempotent-Replayed) es un éxito con el cuerpo original', async () => {
    fetchSimulado.mockReturnValueOnce(respuesta(201, { idPedido: 7 }, { 'Idempotent-Replayed': 'true' }));
    await expect(pedidos.registrarAbono(1, { monto: 1000 }, { clave: 'k-2' })).resolves.toEqual({ idPedido: 7 });
  });
});

describe('versión del horario y del cartel', () => {
  it('el horario se lee con su versión (cuerpo o ETag) y se guarda con If-Match', async () => {
    fetchSimulado.mockReturnValueOnce(respuesta(200, [{ diaSemana: 1, abierto: true }], { ETag: '"AAAAAAAAHOR="' }));
    await expect(panel.horario()).resolves.toEqual({ dias: [{ diaSemana: 1, abierto: true }], rowVersion: 'AAAAAAAAHOR=' });
    fetchSimulado.mockReturnValueOnce(respuesta(200, { dias: [{ diaSemana: 2 }], rowVersion: 'AAAAAAAAHO2=' }));
    await expect(panel.horario()).resolves.toEqual({ dias: [{ diaSemana: 2 }], rowVersion: 'AAAAAAAAHO2=' });
    await panel.guardarHorario([{ diaSemana: 1 }], 'AAAAAAAAHOR=');
    expect(ultima()).toMatchObject({ url: '/api/v1/horarios', metodo: 'PUT', cuerpo: { dias: [{ diaSemana: 1 }] } });
    expect(ultima().cabeceras.get('If-Match')).toBe('"AAAAAAAAHOR="');
  });

  it('la portada sigue recibiendo los siete días aunque la API los envuelva', async () => {
    fetchSimulado.mockReturnValueOnce(respuesta(200, { dias: [{ diaSemana: 3 }], rowVersion: 'x' }));
    await expect(publico.horarios()).resolves.toEqual([{ diaSemana: 3 }]);
  });

  it('el cartel se lee con su versión y se guarda con If-Match', async () => {
    fetchSimulado.mockReturnValueOnce(respuesta(200, { abiertoEnLocal: true }, { ETag: '"AAAAAAAACAR="' }));
    await expect(panel.cartel()).resolves.toEqual({ abiertoEnLocal: true, rowVersion: 'AAAAAAAACAR=' });
    await panel.actualizarCartel({ abiertoEnLocal: false }, 'AAAAAAAACAR=');
    expect(ultima()).toMatchObject({ url: '/api/v1/cartel-digital', metodo: 'PUT', cuerpo: { abiertoEnLocal: false } });
    expect(ultima().cabeceras.get('If-Match')).toBe('"AAAAAAAACAR="');
  });

  it('la ficha de clientes ya envía su versión', async () => {
    await clientes.actualizar(9, { nombre: 'Rosa' }, 'AAAAAAAACLI=');
    expect(ultima().cabeceras.get('If-Match')).toBe('"AAAAAAAACLI="');
  });
});

describe('rutas nuevas de la fase 11', () => {
  it('preferencia de avisos con If-Match', async () => {
    await avisosTaller.preferencia(3, { recibeEnLinea: true, recibePresenciales: false }, 'AAAAAAAAPRE=');
    expect(ultima()).toMatchObject({ url: '/api/v1/avisos-taller/destinos/3/preferencia', metodo: 'PUT', cuerpo: { recibeEnLinea: true, recibePresenciales: false } });
    expect(ultima().cabeceras.get('If-Match')).toBe('"AAAAAAAAPRE="');
  });

  it('correos no enviados: listado, resumen, reenviar y revisado', async () => {
    await correos.noEnviados({ pagina: 2, tamano: 20, revisado: 'false', tipo: '' });
    expect(ultima().url).toBe('/api/v1/correos/no-enviados?pagina=2&tamano=20&revisado=false');
    await correos.resumen();
    expect(ultima().url).toBe('/api/v1/correos/no-enviados/resumen');
    await correos.reenviar(51);
    expect(ultima()).toMatchObject({ url: '/api/v1/correos/no-enviados/51/reenviar', metodo: 'POST' });
    fetchSimulado.mockReturnValueOnce(Promise.resolve(new Response(null, { status: 204 })));
    await correos.marcarRevisado(51);
    expect(ultima()).toMatchObject({ url: '/api/v1/correos/no-enviados/51/revisado', metodo: 'POST' });
  });

  it('baja: consultar con GET y confirmar con POST, sin sesión', async () => {
    await bajaCorreos.consultar('tok/+=');
    expect(ultima()).toMatchObject({ url: '/api/v1/correos/baja?t=tok%2F%2B%3D', metodo: 'GET' });
    await bajaCorreos.confirmar({ t: 'tok', avisosEstado: false });
    expect(ultima()).toMatchObject({ url: '/api/v1/correos/baja', metodo: 'POST', cuerpo: { t: 'tok', avisosEstado: false } });
  });

  it('mi cuenta: preferencias, descarga como archivo y baja', async () => {
    await cuenta.preferencias({ avisosEstado: false, marketing: false });
    expect(ultima()).toMatchObject({ url: '/api/v1/mi-cuenta/preferencias', metodo: 'PUT', cuerpo: { avisosEstado: false, marketing: false } });
    fetchSimulado.mockReturnValueOnce(Promise.resolve(new Response('{"cuenta":{}}', { status: 200, headers: { 'Content-Type': 'application/json' } })));
    const archivo = await cuenta.descargarDatos('clave-1');
    expect(archivo).toBeInstanceOf(Blob);
    expect(ultima()).toMatchObject({ url: '/api/v1/mi-cuenta/datos', metodo: 'POST', cuerpo: { password: 'clave-1' } });
    expect(fetchSimulado.mock.calls.at(-1)[1].cache).toBe('no-store');
    await cuenta.darDeBaja({ password: 'clave-1', confirmacion: 'DAR DE BAJA' });
    expect(ultima()).toMatchObject({ url: '/api/v1/mi-cuenta/baja', metodo: 'POST', cuerpo: { password: 'clave-1', confirmacion: 'DAR DE BAJA' } });
  });

  it('la descarga con contraseña incorrecta llega como error con su código', async () => {
    fetchSimulado.mockReturnValueOnce(respuesta(400, { error: 'La contraseña no es correcta.', codigo: 'datos_invalidos', errores: { password: ['La contraseña no es correcta.'] } }));
    await expect(cuenta.descargarDatos('mala')).rejects.toMatchObject({ estado: 400, codigo: 'datos_invalidos' });
  });
});
