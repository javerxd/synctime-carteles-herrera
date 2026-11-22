import { describe, expect, it } from 'vitest';
import { filtrosDesde, paraDireccion, parametrosInvalidos } from '../utilidades/filtrosUrl';
import {
  anuncioExportacion, DEFINICION_REPORTES, EXPORTACION_INICIAL, enlaceCobranza, estaOcupada, FORMATOS, formatearCelda, nombreDelReporte,
  parametrosReporte, pestanasPara, reducirExportacion, reporteDeRol, textoDelCsv,
} from '../paginas/reportes/reglasReportes';

// Fase 13B (RFS10, RFU24 a RFU26, RFU36, RNFP18, E16, E20, S18, S24): reglas puras de la pantalla de reportes. Sin red ni DOM.
const desde = (texto) => filtrosDesde(new URLSearchParams(texto), DEFINICION_REPORTES);

describe('quién ve qué pestaña (RFU24, RFU25)', () => {
  it('el diseñador y el operario ven solo el operativo; el dueño y el administrador, los tres', () => {
    expect(pestanasPara('diseñador')).toEqual(['operativo']);
    expect(pestanasPara('operario')).toEqual(['operativo']);
    expect(pestanasPara('dueño')).toEqual(['operativo', 'gerencial', 'parametrizable']);
    expect(pestanasPara('administrador')).toEqual(['operativo', 'gerencial', 'parametrizable']);
  });

  it('un rol desconocido (o un cliente) no ve ninguna', () => {
    expect(pestanasPara('cliente')).toEqual([]);
    expect(pestanasPara(undefined)).toEqual([]);
  });

  it('pedir en la dirección un reporte que el rol no ve cae en el operativo, sin error', () => {
    expect(reporteDeRol('gerencial', 'operario')).toBe('operativo');
    expect(reporteDeRol('parametrizable', 'diseñador')).toBe('operativo');
    expect(reporteDeRol('gerencial', 'dueño')).toBe('gerencial');
    expect(reporteDeRol('inventado', 'dueño')).toBe('operativo');
  });

  it('los reportes tienen su nombre', () => {
    expect(nombreDelReporte('operativo')).toBe('Reporte operativo');
    expect(nombreDelReporte('gerencial')).toBe('Reporte gerencial');
    expect(nombreDelReporte('parametrizable')).toBe('Reporte parametrizable');
  });
});

describe('filtros en la dirección', () => {
  it('sin nada en la dirección: el operativo del mes, con los predeterminados', () => {
    const f = desde('');
    expect(f.reporte).toBe('operativo');
    expect(f.periodo).toBe('mes');
    expect(f.comparar).toBe('anterior');
    expect(f.alcanceSaldo).toBe('periodo');
  });

  it('lo inválido se ignora en silencio y se quita de la dirección', () => {
    const parametros = new URLSearchParams('reporte=secreto&mes=5&estadoPedido=enviado&cliente=abc&categoria=3&origen=online');
    const f = filtrosDesde(parametros, DEFINICION_REPORTES);
    expect(f.reporte).toBe('operativo');
    expect(f.mes).toBe('');                 // el mes necesita el año
    expect(f.estadoPedido).toBe('');
    expect(f.cliente).toBe('');
    // «categoria» y «origen» existen en el dashboard, no en los reportes: sobran.
    expect(parametrosInvalidos(parametros, DEFINICION_REPORTES).sort()).toEqual(['categoria', 'cliente', 'estadoPedido', 'mes', 'origen', 'reporte']);
  });

  it('lo predeterminado no ensucia la dirección', () => {
    expect(paraDireccion(desde('reporte=operativo&periodo=mes&comparar=anterior'), DEFINICION_REPORTES)).toEqual({});
    expect(paraDireccion(desde('reporte=gerencial&anio=2026&mes=11'), DEFINICION_REPORTES)).toEqual({ reporte: 'gerencial', anio: '2026', mes: '11' });
  });
});

describe('lo que viaja a la API: cada reporte pide solo sus filtros (nada se ignora en silencio)', () => {
  const todos = {
    reporte: 'operativo', periodo: 'mes', desde: '', hasta: '', anio: '2026', mes: '11', estadoPedido: 'entregado,en_proceso', estadoPago: 'por_cobrar',
    cliente: '7', comparar: 'anterior', alcanceSaldo: 'vigente', pagina: 1,
  };

  it('el operativo no lleva ni «comparar» ni «alcanceSaldo»', () => {
    expect(parametrosReporte('operativo', todos)).toEqual({
      periodo: 'mes', anio: '2026', mes: '11', estadoPedido: 'entregado,en_proceso', estadoPago: 'por_cobrar', cliente: '7',
    });
  });

  it('el gerencial suma «comparar»', () => {
    expect(parametrosReporte('gerencial', todos)).toEqual({
      periodo: 'mes', anio: '2026', mes: '11', estadoPedido: 'entregado,en_proceso', estadoPago: 'por_cobrar', cliente: '7', comparar: 'anterior',
    });
  });

  it('el parametrizable suma «alcanceSaldo»', () => {
    expect(parametrosReporte('parametrizable', todos)).toEqual({
      periodo: 'mes', anio: '2026', mes: '11', estadoPedido: 'entregado,en_proceso', estadoPago: 'por_cobrar', cliente: '7', alcanceSaldo: 'vigente',
    });
  });

  it('los vacíos no viajan, y la página nunca', () => {
    const p = parametrosReporte('operativo', { ...todos, anio: '', mes: '', estadoPedido: '', estadoPago: '', cliente: '' });
    expect(p).toEqual({ periodo: 'mes' });
    expect(p).not.toHaveProperty('pagina');
    expect(p).not.toHaveProperty('reporte');
  });
});

describe('el atajo «Exportar cobranza» del dashboard (RFU36)', () => {
  const dashboard = (texto) => filtrosDesde(new URLSearchParams(texto), {
    campos: {
      periodo: () => true, desde: () => true, hasta: () => true, anio: () => true, mes: () => true, comparar: () => true, estadoPedido: () => true,
      estadoPago: () => true, categoria: () => true, origen: () => true, cliente: () => true, alcanceSaldo: () => true, cobro: () => true,
    },
    predeterminados: { periodo: 'mes', comparar: 'anterior', alcanceSaldo: 'periodo' },
  });

  it('abre el parametrizable ya filtrado por cobrar, con el período y el alcance del dashboard', () => {
    const enlace = enlaceCobranza(dashboard('anio=2026&mes=10&alcanceSaldo=vigente&cliente=7'));
    const url = new URL(enlace, 'http://x');
    expect(url.pathname).toBe('/panel/reportes');
    expect(Object.fromEntries(url.searchParams)).toEqual({ reporte: 'parametrizable', estadoPago: 'por_cobrar', anio: '2026', mes: '10', alcanceSaldo: 'vigente', cliente: '7' });
  });

  it('lo que los reportes no tienen (categoría, origen, comparación) no viaja', () => {
    const url = new URL(enlaceCobranza(dashboard('categoria=3&origen=online&comparar=ninguno')), 'http://x');
    expect(Array.from(url.searchParams.keys()).sort()).toEqual(['estadoPago', 'reporte']);
  });

  it('un segmento elegido en el anillo de cobranza afina el estado de pago', () => {
    const url = new URL(enlaceCobranza(dashboard('cobro=abonado')), 'http://x');
    expect(url.searchParams.get('estadoPago')).toBe('abonado');
  });

  it('el enlace es un enlace que la propia pantalla acepta, sin parámetros que sobren', () => {
    const url = new URL(enlaceCobranza(dashboard('anio=2026&mes=10&cliente=7')), 'http://x');
    expect(parametrosInvalidos(url.searchParams, DEFINICION_REPORTES)).toEqual([]);
  });
});

describe('cómo se ve cada celda (la misma tabla que luego se exporta)', () => {
  it('pesos con punto de miles, fechas día-mes-año, porcentajes con coma y enteros tal cual', () => {
    expect(formatearCelda(1234567, { tipo: 'pesos' })).toMatch(/^\$\s?1\.234\.567$/);
    expect(formatearCelda('2026-11-05', { tipo: 'fecha' })).toBe('05-11-2026');
    expect(formatearCelda(25.5, { tipo: 'porcentaje' })).toBe('25,5 %');
    expect(formatearCelda(12, { tipo: 'entero' })).toBe('12');
    expect(formatearCelda('Ñandú Peña', { tipo: 'texto' })).toBe('Ñandú Peña');
  });

  it('una celda sin valor muestra el texto de vacío de su columna, o una raya', () => {
    expect(formatearCelda(null, { tipo: 'pesos', textoNulo: 'Sin costo' })).toBe('Sin costo');
    expect(formatearCelda(null, { tipo: 'porcentaje', textoNulo: '—' })).toBe('—');
    expect(formatearCelda(null, { tipo: 'texto', textoNulo: '' })).toBe('—');
  });

  it('un texto que empieza como fórmula se muestra como texto (la pantalla nunca lo evalúa)', () => {
    expect(formatearCelda('=1+1', { tipo: 'texto' })).toBe('=1+1');
  });
});

describe('qué lleva cada archivo (la leyenda de los botones)', () => {
  it('el CSV avisa su separador y su codificación', () => {
    expect(textoDelCsv('parametrizable')).toMatch(/«;»/);
    expect(textoDelCsv('parametrizable')).toMatch(/UTF-8/);
  });

  it('el CSV del operativo exporta solo la tabla de pedidos, y la leyenda lo dice', () => {
    expect(textoDelCsv('operativo')).toMatch(/solo la tabla de pedidos/i);
    expect(textoDelCsv('gerencial')).toMatch(/pedidos entregados/i);
    expect(textoDelCsv('parametrizable')).not.toMatch(/solo la tabla/i);
  });

  it('los tres formatos, con el nombre que ve la persona', () => {
    expect(FORMATOS.map((f) => [f.id, f.etiqueta])).toEqual([['xlsx', 'Excel'], ['csv', 'CSV'], ['pdf', 'PDF']]);
  });
});

describe('máquina de estados de una descarga (RNFP18)', () => {
  const pedir = (formato = 'xlsx') => reducirExportacion(EXPORTACION_INICIAL, { tipo: 'pedir', formato });

  it('parte inactiva, sin nada que anunciar', () => {
    expect(EXPORTACION_INICIAL.fase).toBe('inactiva');
    expect(estaOcupada(EXPORTACION_INICIAL)).toBe(false);
    expect(anuncioExportacion(EXPORTACION_INICIAL)).toBe('');
  });

  it('pedir → preparando: ocupada, y lo anuncia', () => {
    const pidiendo = pedir('pdf');
    expect(pidiendo).toMatchObject({ fase: 'pidiendo', formato: 'pdf' });
    expect(estaOcupada(pidiendo)).toBe(true);
    const preparando = reducirExportacion(pidiendo, { tipo: 'aceptada', id: 'abc' });
    expect(preparando).toMatchObject({ fase: 'preparando', formato: 'pdf', id: 'abc' });
    expect(estaOcupada(preparando)).toBe(true);
    expect(anuncioExportacion(preparando)).toBe('Preparando tu archivo');
  });

  it('preparando → lista: ya no está ocupada, y lo anuncia con el nombre del archivo', () => {
    const lista = reducirExportacion(reducirExportacion(pedir(), { tipo: 'aceptada', id: 'abc' }), { tipo: 'lista', nombreArchivo: 'reporte-operativo-2026-11-20.xlsx' });
    expect(lista).toMatchObject({ fase: 'lista', id: 'abc', nombreArchivo: 'reporte-operativo-2026-11-20.xlsx' });
    expect(estaOcupada(lista)).toBe(false);
    expect(anuncioExportacion(lista)).toBe('Tu archivo está listo');
  });

  it('un fallo guarda su motivo y si se puede reintentar; ya no está ocupada', () => {
    const fallida = reducirExportacion(pedir(), { tipo: 'fallida', mensaje: 'El archivo no se pudo preparar.', reintentable: true });
    expect(fallida).toMatchObject({ fase: 'fallida', formato: 'xlsx', mensaje: 'El archivo no se pudo preparar.', reintentable: true });
    expect(estaOcupada(fallida)).toBe(false);
    expect(anuncioExportacion(fallida)).toBe('');
  });

  it('se puede pedir otro formato después de un fallo o de uno listo (reintento)', () => {
    const fallida = reducirExportacion(pedir('csv'), { tipo: 'fallida', mensaje: 'x', reintentable: true });
    expect(reducirExportacion(fallida, { tipo: 'pedir', formato: 'csv' })).toMatchObject({ fase: 'pidiendo', formato: 'csv' });
  });

  it('mientras hay una ocupada, pedir otra no cambia nada (un trabajo por persona)', () => {
    const ocupada = pedir('xlsx');
    expect(reducirExportacion(ocupada, { tipo: 'pedir', formato: 'pdf' })).toBe(ocupada);
  });

  it('«cerrar» descarta el aviso, salvo que siga ocupada', () => {
    const lista = reducirExportacion(reducirExportacion(pedir(), { tipo: 'aceptada', id: 'a' }), { tipo: 'lista', nombreArchivo: 'x.xlsx' });
    expect(reducirExportacion(lista, { tipo: 'cerrar' })).toEqual(EXPORTACION_INICIAL);
    const ocupada = pedir();
    expect(reducirExportacion(ocupada, { tipo: 'cerrar' })).toBe(ocupada);
  });
});
