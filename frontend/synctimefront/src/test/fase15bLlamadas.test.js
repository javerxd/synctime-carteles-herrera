import { llamadasDuplicadas, resumirLlamadas } from '../../e2e/llamadas.js';

// Fase 15B (RNFP23): el detector de llamadas duplicadas de Playwright (e2e/llamadas.js). Se prueba aparte, con listas armadas a mano,
// para que «no encontró duplicadas» signifique que no las hay y no que el detector está ciego.
const get = (url, extra = {}) => ({ metodo: 'GET', url, cuerpo: null, abortada: false, ...extra });
const envio = (url, cuerpo, extra = {}) => ({ metodo: 'POST', url, cuerpo, abortada: false, ...extra });

describe('llamadasDuplicadas', () => {
  it('no marca nada si cada lectura sale una vez', () => {
    expect(llamadasDuplicadas([get('/api/v1/pedidos?pagina=1'), get('/api/v1/cartel-digital'), get('/api/v1/pedidos?pagina=2')])).toEqual([]);
  });

  it('marca la misma lectura (con la misma consulta) dos veces', () => {
    const lista = [get('/api/v1/pedidos?pagina=1'), get('/api/v1/cartel-digital'), get('/api/v1/pedidos?pagina=1')];
    expect(llamadasDuplicadas(lista)).toEqual([{ clave: 'GET /api/v1/pedidos?pagina=1', veces: 2 }]);
  });

  it('descuenta las canceladas: el doble montaje de StrictMode en desarrollo cancela la primera', () => {
    const lista = [get('/api/v1/mi-cuenta', { abortada: true }), get('/api/v1/mi-cuenta')];
    expect(llamadasDuplicadas(lista)).toEqual([]);
  });

  it('una escritura entre dos lecturas iguales las separa: recargar la ficha tras guardar es legítimo', () => {
    const lista = [get('/api/v1/pedidos/1'), envio('/api/v1/pedidos/1/pagos', '{"monto":1}'), get('/api/v1/pedidos/1')];
    expect(llamadasDuplicadas(lista)).toEqual([]);
  });

  it('marca el mismo envío con el mismo cuerpo dos veces, aunque no haya nada entre medio', () => {
    const lista = [envio('/api/v1/mis/solicitudes', '{"a":1}'), envio('/api/v1/mis/solicitudes', '{"a":1}')];
    expect(llamadasDuplicadas(lista)).toEqual([{ clave: 'POST /api/v1/mis/solicitudes {"a":1}', veces: 2 }]);
  });

  it('dos envíos distintos a la misma dirección no son duplicados', () => {
    const lista = [envio('/api/v1/pedidos/1/pagos', '{"monto":1}'), envio('/api/v1/pedidos/1/pagos', '{"monto":2}')];
    expect(llamadasDuplicadas(lista)).toEqual([]);
  });

  it('las direcciones permitidas (un reintento a propósito) no se marcan', () => {
    const lista = [get('/api/v1/avisos-taller/prueba/estado?id=3'), get('/api/v1/avisos-taller/prueba/estado?id=3')];
    expect(llamadasDuplicadas(lista, { permitidas: [/prueba\/estado/] })).toEqual([]);
    expect(llamadasDuplicadas(lista)).toHaveLength(1);
  });
});

describe('resumirLlamadas', () => {
  it('cuenta las llamadas que no se cancelaron, por método', () => {
    const lista = [get('/a'), get('/b', { abortada: true }), envio('/c', '{}')];
    expect(resumirLlamadas(lista)).toEqual({ total: 2, lecturas: 1, escrituras: 1 });
  });
});
