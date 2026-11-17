import {
  DEFINICION_FILTROS, parametrosIndicadores, periodoComparacion, resolverPeriodo,
} from '../paginas/indicadores/reglasIndicadores';
import { filtrosDesde } from '../utilidades/filtrosUrl';

// Fase 12A y 12B: los períodos, la comparación y los filtros de la dirección (hoy: lunes 16-11-2026). Las cifras de los
// indicadores se prueban en el backend contra SQL Server, con cuentas hechas a mano (IndicadoresFase12bTests).
const HOY = '2026-11-16';

const MES = { periodo: 'mes', comparar: 'anterior' };

describe('períodos y comparación', () => {
  it('el mes en curso se compara con el mes anterior completo', () => {
    const p = resolverPeriodo(MES, HOY);
    expect(p).toMatchObject({ desde: '2026-11-01', hasta: '2026-11-30', etiqueta: 'noviembre de 2026' });
    expect(periodoComparacion(p, 'anterior')).toMatchObject({ desde: '2026-10-01', hasta: '2026-10-31', etiqueta: 'octubre de 2026' });
  });

  it('la semana va de lunes a domingo y el año completo se compara con el anterior', () => {
    expect(resolverPeriodo({ periodo: 'semana' }, HOY)).toMatchObject({ desde: '2026-11-16', hasta: '2026-11-22' });
    expect(periodoComparacion(resolverPeriodo({ periodo: 'semana' }, HOY), 'anterior')).toMatchObject({ desde: '2026-11-09', hasta: '2026-11-15' });
    expect(periodoComparacion(resolverPeriodo({ periodo: 'anio' }, HOY), 'anterior')).toMatchObject({ desde: '2025-01-01', hasta: '2025-12-31' });
  });

  it('el acceso por año y mes gana al período, y el rango propio gana a los dos', () => {
    expect(resolverPeriodo({ periodo: 'semana', anio: '2026', mes: '1' }, HOY)).toMatchObject({ desde: '2026-01-01', hasta: '2026-01-31' });
    const rango = resolverPeriodo({ anio: '2026', desde: '2026-11-01', hasta: '2026-11-10' }, HOY);
    expect(rango).toMatchObject({ desde: '2026-11-01', hasta: '2026-11-10', etiqueta: 'del 01-11-2026 al 10-11-2026' });
    expect(periodoComparacion(rango, 'anterior')).toMatchObject({ desde: '2026-10-22', hasta: '2026-10-31' });
  });

  it('«mismo período del año anterior» y «sin comparación»', () => {
    expect(periodoComparacion(resolverPeriodo(MES, HOY), 'anio_anterior')).toMatchObject({ desde: '2025-11-01', hasta: '2025-11-30' });
    expect(periodoComparacion(resolverPeriodo(MES, HOY), 'ninguno')).toBeNull();
  });
});

describe('filtros en la dirección', () => {
  const desde = (texto) => filtrosDesde(new URLSearchParams(texto), DEFINICION_FILTROS);

  it('acepta varios estados separados por coma y descarta lo que no existe', () => {
    expect(desde('estadoPedido=terminado,entregado').estadoPedido).toBe('terminado,entregado');
    expect(desde('estadoPedido=terminado,anulado').estadoPedido).toBe('');
    expect(desde('estadoPago=por_cobrar').estadoPago).toBe('por_cobrar');
    expect(desde('estadoPago=por_cobrar,abonado').estadoPago).toBe('');
  });

  it('el mes exige año, el rango exige las dos fechas en orden y los predeterminados son mes y período anterior', () => {
    expect(desde('mes=3').mes).toBe('');
    expect(desde('anio=2026&mes=3').mes).toBe('3');
    expect(desde('desde=2026-11-10&hasta=2026-11-01').desde).toBe('');
    const base = desde('');
    expect(base).toMatchObject({ periodo: 'mes', comparar: 'anterior', alcanceSaldo: 'periodo' });
  });

  it('la agrupación de la demanda viaja solo a /demanda', () => {
    const filtros = desde('agrupar=producto');

    expect(parametrosIndicadores(filtros, { ruta: 'demanda' })).toMatchObject({ agrupar: 'producto' });
    expect(parametrosIndicadores(filtros, { ruta: 'resumen' })).not.toHaveProperty('agrupar');
    expect(desde('agrupar=marca').agrupar).toBe('categoria');
  });

  it('los parámetros que viajan a la API: sin vacíos y con la página solo en el listado', () => {
    const filtros = desde('periodo=anio&cliente=3&cobro=abonado&pagina=2');
    expect(parametrosIndicadores(filtros)).toEqual({ periodo: 'anio', comparar: 'anterior', cliente: '3', alcanceSaldo: 'periodo' });
    expect(parametrosIndicadores(filtros, { listado: true })).toMatchObject({ estadoPago: 'abonado', pagina: 2, tamano: 10 });
  });
});
