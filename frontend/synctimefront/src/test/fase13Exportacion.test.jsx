import { act, fireEvent, render, renderHook, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ErrorApi } from '../api/cliente';
import { BotonesExportar } from '../paginas/reportes/BotonesExportar';
import { EXPORTACION_INICIAL } from '../paginas/reportes/reglasReportes';
import { useExportacion } from '../paginas/reportes/useExportacion';

// Fase 13B (RNFP18, RFU26, E20): pedir un archivo, esperar a que el servidor lo prepare y bajarlo, sin bloquear la pantalla.
// Los tres botones, su estado de carga y los avisos se prueban aquí; la red es una simulación del contrato (docs/api/openapi.json).

const FILTROS = { reporte: 'operativo', periodo: 'mes', anio: '2026', mes: '11', estadoPedido: 'entregado', estadoPago: '', cliente: '7', comparar: 'anterior', alcanceSaldo: 'periodo', pagina: 1 };
const sinEspera = () => Promise.resolve();

function servidor({ estados = [], pedir, descargar } = {}) {
  const cola = [...estados];
  return {
    pedirExportacion: pedir ?? vi.fn(async () => ({ id: 'trabajo-1', estado: 'pendiente' })),
    estado: vi.fn(async () => cola.shift() ?? { id: 'trabajo-1', estado: 'listo', nombreArchivo: 'reporte-operativo-2026-11-20.xlsx' }),
    descargar: descargar ?? vi.fn(async () => new Blob(['datos'])),
  };
}

function dibujarHook(api, extra = {}) {
  const guardar = vi.fn();
  const esperar = extra.esperar ?? vi.fn(sinEspera);
  const vista = renderHook(() => useExportacion({ reporte: 'operativo', filtros: FILTROS }, { api, guardar, esperar }));
  return { ...vista, guardar, esperar };
}

describe('pedir un archivo', () => {
  it('manda el formato y los filtros de pantalla tal cual, solo los que ese reporte admite', async () => {
    const api = servidor();
    const { result } = dibujarHook(api);

    await act(async () => { await result.current.pedir('xlsx'); });

    expect(api.pedirExportacion).toHaveBeenCalledTimes(1);
    expect(api.pedirExportacion).toHaveBeenCalledWith('operativo', 'xlsx', {
      periodo: 'mes', anio: '2026', mes: '11', estadoPedido: 'entregado', cliente: '7',
    });
  });

  it('pasa de «preparando» a «lista» y baja el archivo con el nombre del servidor', async () => {
    const api = servidor({ estados: [{ id: 'trabajo-1', estado: 'pendiente' }, { id: 'trabajo-1', estado: 'procesando' }] });
    const { result, guardar } = dibujarHook(api);

    await act(async () => { await result.current.pedir('xlsx'); });

    expect(api.estado).toHaveBeenCalledTimes(3);
    expect(api.descargar).toHaveBeenCalledWith('trabajo-1');
    expect(guardar).toHaveBeenCalledTimes(1);
    expect(guardar.mock.calls[0][1]).toBe('reporte-operativo-2026-11-20.xlsx');
    expect(result.current.estado).toMatchObject({ fase: 'lista', formato: 'xlsx', nombreArchivo: 'reporte-operativo-2026-11-20.xlsx' });
  });

  it('mientras se prepara no deja pedir otro (un trabajo por persona): no hay una segunda petición', async () => {
    let terminar;
    const api = servidor();
    api.estado.mockImplementation(() => new Promise((resolver) => { terminar = () => resolver({ id: 'trabajo-1', estado: 'listo', nombreArchivo: 'a.xlsx' }); }));
    const { result } = dibujarHook(api);

    let primera;
    act(() => { primera = result.current.pedir('xlsx'); });
    await waitFor(() => expect(result.current.estado.fase).toBe('preparando'));
    await waitFor(() => expect(typeof terminar).toBe('function'));
    await act(async () => { await result.current.pedir('pdf'); });

    expect(api.pedirExportacion).toHaveBeenCalledTimes(1);
    await act(async () => { terminar(); await primera; });
    expect(result.current.estado.fase).toBe('lista');
  });

  it('espera cada vez más entre consultas, con tope', async () => {
    const api = servidor({ estados: Array.from({ length: 8 }, () => ({ id: 'trabajo-1', estado: 'procesando' })) });
    const { result, esperar } = dibujarHook(api);

    await act(async () => { await result.current.pedir('pdf'); });

    const esperas = esperar.mock.calls.map(([ms]) => ms);
    expect(esperas.length).toBeGreaterThanOrEqual(8);
    expect(esperas[0]).toBeLessThanOrEqual(esperas[3]);
    expect(Math.max(...esperas)).toBeLessThanOrEqual(5000);
  });
});

describe('cuando algo falla', () => {
  it('un trabajo fallido muestra el motivo del servidor y se puede reintentar', async () => {
    const api = servidor({ estados: [{ id: 'trabajo-1', estado: 'fallido', error: 'No se pudo preparar el archivo. Inténtalo de nuevo.' }] });
    const { result, guardar } = dibujarHook(api);

    await act(async () => { await result.current.pedir('pdf'); });

    expect(result.current.estado).toMatchObject({ fase: 'fallida', formato: 'pdf', mensaje: 'No se pudo preparar el archivo. Inténtalo de nuevo.', reintentable: true });
    expect(guardar).not.toHaveBeenCalled();

    await act(async () => { await result.current.reintentar(); });
    expect(api.pedirExportacion).toHaveBeenCalledTimes(2);
    expect(api.pedirExportacion.mock.calls[1][1]).toBe('pdf');
  });

  it('«ya tienes un archivo en preparación» (409) se explica y no pide más', async () => {
    const pedir = vi.fn().mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'conflicto', mensaje: 'Ya tienes un archivo en preparación. Espera a que termine para pedir otro.' }));
    const { result } = dibujarHook(servidor({ pedir }));

    await act(async () => { await result.current.pedir('csv'); });

    expect(result.current.estado).toMatchObject({ fase: 'fallida', mensaje: expect.stringContaining('en preparación') });
  });

  it('un reporte demasiado grande (400) muestra el mensaje de la API y no ofrece reintentar', async () => {
    const pedir = vi.fn().mockRejectedValue(new ErrorApi({ estado: 400, codigo: 'reporte_muy_grande', mensaje: 'Un PDF admite hasta 10.000 filas. Acota el rango, o descarga el reporte en Excel o CSV.' }));
    const { result } = dibujarHook(servidor({ pedir }));

    await act(async () => { await result.current.pedir('pdf'); });

    expect(result.current.estado).toMatchObject({ fase: 'fallida', reintentable: false, mensaje: expect.stringContaining('10.000 filas') });
  });

  it('sin permiso (403) tampoco se reintenta', async () => {
    const pedir = vi.fn().mockRejectedValue(new ErrorApi({ estado: 403, codigo: 'sin_permiso', mensaje: 'No tienes permiso para hacer esto.' }));
    const { result } = dibujarHook(servidor({ pedir }));

    await act(async () => { await result.current.pedir('xlsx'); });

    expect(result.current.estado.reintentable).toBe(false);
  });

  it('sin conexión se puede reintentar', async () => {
    const pedir = vi.fn().mockRejectedValue(new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: 'No pudimos conectar con el servidor.' }));
    const { result } = dibujarHook(servidor({ pedir }));

    await act(async () => { await result.current.pedir('xlsx'); });

    expect(result.current.estado).toMatchObject({ fase: 'fallida', reintentable: true });
  });

  it('un archivo que venció (410) al bajarlo dice que hay que pedirlo de nuevo', async () => {
    const descargar = vi.fn().mockRejectedValue(new ErrorApi({ estado: 410, codigo: 'exportacion_vencida', mensaje: 'El archivo venció y ya no está disponible. Vuelve a pedirlo.' }));
    const { result } = dibujarHook(servidor({ descargar }));

    await act(async () => { await result.current.pedir('xlsx'); });

    expect(result.current.estado).toMatchObject({ fase: 'fallida', mensaje: expect.stringContaining('venció') });
  });

  it('si el trabajo nunca termina, se rinde con un mensaje claro en vez de esperar para siempre', async () => {
    const api = servidor();
    api.estado.mockImplementation(async () => ({ id: 'trabajo-1', estado: 'procesando' }));
    const { result } = dibujarHook(api);

    await act(async () => { await result.current.pedir('pdf'); });

    expect(result.current.estado).toMatchObject({ fase: 'fallida', mensaje: expect.stringMatching(/tarda más de lo normal/i), reintentable: true });
  });
});

describe('errores pasajeros y «Reintentar» (revisión de ECC)', () => {
  const sinRed = () => new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: 'No pudimos conectar con el servidor.' });

  it('un par de fallos de red al consultar no pierde el trabajo: sigue consultando y lo baja', async () => {
    const api = servidor();
    const respuestas = [sinRed(), sinRed(), { id: 'trabajo-1', estado: 'listo', nombreArchivo: 'a.xlsx' }];
    api.estado.mockImplementation(async () => { const r = respuestas.shift(); if (r instanceof Error) throw r; return r; });
    const { result, guardar } = dibujarHook(api);

    await act(async () => { await result.current.pedir('xlsx'); });

    expect(result.current.estado.fase).toBe('lista');
    expect(guardar).toHaveBeenCalledTimes(1);
    expect(api.pedirExportacion).toHaveBeenCalledTimes(1);
  });

  it('tres fallos seguidos al consultar abandonan; «Reintentar» retoma ESE trabajo en vez de pedir otro (que daría 409)', async () => {
    const api = servidor();
    const respuestas = [sinRed(), sinRed(), sinRed()];
    api.estado.mockImplementation(async () => {
      const r = respuestas.shift();
      if (r) throw r;
      return { id: 'trabajo-1', estado: 'listo', nombreArchivo: 'a.xlsx' };
    });
    const { result } = dibujarHook(api);

    await act(async () => { await result.current.pedir('xlsx'); });
    expect(result.current.estado).toMatchObject({ fase: 'fallida', reintentable: true });

    await act(async () => { await result.current.reintentar(); });

    expect(api.pedirExportacion).toHaveBeenCalledTimes(1);
    expect(result.current.estado.fase).toBe('lista');
  });

  it('«Reintentar» repite el reporte, el formato y los filtros del pedido original, no los de la pantalla de ahora', async () => {
    const pedir = vi.fn().mockRejectedValueOnce(sinRed()).mockResolvedValue({ id: 'trabajo-1', estado: 'pendiente' });
    const api = servidor({ pedir });
    let reporte = 'operativo';
    let filtros = FILTROS;
    const guardar = vi.fn();
    const { result, rerender } = renderHook(() => useExportacion({ reporte, filtros }, { api, guardar, esperar: sinEspera }));

    await act(async () => { await result.current.pedir('pdf'); });
    expect(result.current.estado.fase).toBe('fallida');

    reporte = 'parametrizable';
    filtros = { ...FILTROS, estadoPago: 'por_cobrar' };
    rerender();
    await act(async () => { await result.current.reintentar(); });

    expect(pedir.mock.calls[1]).toEqual(pedir.mock.calls[0]);
    expect(pedir.mock.calls[1][0]).toBe('operativo');
    expect(pedir.mock.calls[1][1]).toBe('pdf');
  });

  it('una llamada colgada vence por tiempo y es un fallo reintentable, no botones inactivos para siempre', async () => {
    vi.useFakeTimers();
    try {
      const api = servidor({ pedir: vi.fn(() => new Promise(() => {})) });
      const { result } = dibujarHook(api);

      act(() => { result.current.pedir('xlsx'); });
      await act(async () => { await vi.advanceTimersByTimeAsync(31_000); });

      expect(result.current.estado).toMatchObject({ fase: 'fallida', reintentable: true });
      expect(result.current.ocupada).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });
});

describe('«Descargar de nuevo» y salir de la pantalla', () => {
  it('«descargar de nuevo» vuelve a bajar el mismo archivo, sin pedir otro', async () => {
    const api = servidor();
    const { result, guardar } = dibujarHook(api);
    await act(async () => { await result.current.pedir('xlsx'); });

    await act(async () => { await result.current.descargarDeNuevo(); });

    expect(api.pedirExportacion).toHaveBeenCalledTimes(1);
    expect(api.descargar).toHaveBeenCalledTimes(2);
    expect(guardar).toHaveBeenCalledTimes(2);
  });

  it('si la persona cambia de página mientras se prepara, deja de consultar y no escribe en una pantalla que ya no existe', async () => {
    const api = servidor();
    let consultas = 0;
    api.estado.mockImplementation(async () => { consultas += 1; return { id: 'trabajo-1', estado: 'procesando' }; });
    const espera = vi.fn(() => new Promise(() => {}));   // la espera nunca vence: el desmontaje la corta
    const { result, unmount, guardar } = dibujarHook(api, { esperar: espera });

    act(() => { result.current.pedir('xlsx'); });
    await waitFor(() => expect(consultas).toBe(1));
    unmount();
    await new Promise((r) => setTimeout(r, 20));

    expect(consultas).toBe(1);
    expect(guardar).not.toHaveBeenCalled();
  });

  it('«cerrar» descarta el aviso de un archivo ya listo', async () => {
    const { result } = dibujarHook(servidor());
    await act(async () => { await result.current.pedir('csv'); });
    expect(result.current.estado.fase).toBe('lista');

    act(() => result.current.cerrar());

    expect(result.current.estado).toEqual(EXPORTACION_INICIAL);
  });
});

describe('los tres botones (RFU26)', () => {
  const dibujar = (estado = EXPORTACION_INICIAL, props = {}) => {
    const acciones = { alPedir: vi.fn(), alReintentar: vi.fn(), alDescargar: vi.fn(), alCerrar: vi.fn() };
    render(<BotonesExportar reporte="operativo" estado={estado} {...acciones} {...props} />);
    return acciones;
  };

  it('Excel, CSV y PDF, con su texto completo como nombre accesible', () => {
    dibujar();
    expect(screen.getByRole('button', { name: 'Exportar a Excel' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Exportar a CSV' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Exportar a PDF' })).toBeInTheDocument();
  });

  it('cada botón pide su formato', () => {
    const { alPedir } = dibujar();
    fireEvent.click(screen.getByRole('button', { name: 'Exportar a Excel' }));
    fireEvent.click(screen.getByRole('button', { name: 'Exportar a CSV' }));
    fireEvent.click(screen.getByRole('button', { name: 'Exportar a PDF' }));
    expect(alPedir.mock.calls.map(([f]) => f)).toEqual(['xlsx', 'csv', 'pdf']);
  });

  it('mientras se prepara: el pulsado dice «Preparando…» y los TRES quedan inactivos, sin perder el foco', () => {
    const { alPedir } = dibujar({ fase: 'preparando', formato: 'pdf', id: 'a' });
    const pdf = screen.getByRole('button', { name: /Preparando/ });
    expect(pdf).toHaveAttribute('aria-disabled', 'true');
    expect(pdf).not.toBeDisabled();   // sigue enfocable: el foco no se pierde
    expect(screen.getByRole('button', { name: 'Exportar a Excel' })).toHaveAttribute('aria-disabled', 'true');
    expect(screen.getByRole('button', { name: 'Exportar a CSV' })).toHaveAttribute('aria-disabled', 'true');

    fireEvent.click(screen.getByRole('button', { name: 'Exportar a Excel' }));
    fireEvent.click(pdf);
    expect(alPedir).not.toHaveBeenCalled();
  });

  it('anuncia «Preparando tu archivo» y luego «Tu archivo está listo» en una región de estado, con «Descargar de nuevo»', () => {
    const acciones = dibujar({ fase: 'preparando', formato: 'xlsx', id: 'a' });
    expect(screen.getByRole('status')).toHaveTextContent('Preparando tu archivo');

    render(<BotonesExportar reporte="operativo" estado={{ fase: 'lista', formato: 'xlsx', id: 'a', nombreArchivo: 'reporte-operativo-2026-11-20.xlsx' }}
      alPedir={vi.fn()} alReintentar={vi.fn()} alDescargar={acciones.alDescargar} alCerrar={vi.fn()} />);
    const regiones = screen.getAllByRole('status');
    expect(regiones.some((r) => /Tu archivo está listo/.test(r.textContent))).toBe(true);
    fireEvent.click(screen.getByRole('button', { name: /Descargar de nuevo/ }));
    expect(acciones.alDescargar).toHaveBeenCalledTimes(1);
  });

  it('un fallo es una alerta con el motivo y «Reintentar»', () => {
    const { alReintentar } = dibujar({ fase: 'fallida', formato: 'pdf', mensaje: 'No se pudo preparar el archivo.', reintentable: true });
    const alerta = screen.getByRole('alert');
    expect(alerta).toHaveTextContent('No se pudo preparar el archivo.');
    fireEvent.click(within(alerta).getByRole('button', { name: /Reintentar/ }));
    expect(alReintentar).toHaveBeenCalledTimes(1);
    // Y los botones vuelven a estar disponibles para pedir otro formato.
    expect(screen.getByRole('button', { name: 'Exportar a Excel' })).not.toHaveAttribute('aria-disabled', 'true');
  });

  it('un fallo que no se arregla reintentando (reporte muy grande, sin permiso) no ofrece «Reintentar»', () => {
    dibujar({ fase: 'fallida', formato: 'pdf', mensaje: 'Un PDF admite hasta 10.000 filas.', reintentable: false });
    expect(screen.getByRole('alert')).toHaveTextContent('10.000 filas');
    expect(screen.queryByRole('button', { name: /Reintentar/ })).toBeNull();
  });

  it('la leyenda dice qué llevan los archivos: los filtros de pantalla y el separador del CSV', () => {
    dibujar();
    expect(screen.getByText(/filtros de pantalla/i)).toBeInTheDocument();
    expect(screen.getByText(/«;»/)).toBeInTheDocument();
    expect(screen.getByText(/solo la tabla de pedidos/i)).toBeInTheDocument();
  });

  it('el foco no se pierde: al cerrar el aviso (el botón desaparece) vuelve al botón del formato', () => {
    const { rerender } = render(<BotonesExportar reporte="operativo" estado={{ fase: 'lista', formato: 'csv', reporte: 'operativo', id: 'a', nombreArchivo: 'a.csv' }}
      alPedir={vi.fn()} alReintentar={vi.fn()} alDescargar={vi.fn()} alCerrar={vi.fn()} />);
    screen.getByRole('button', { name: /Descargar de nuevo/ }).focus();

    rerender(<BotonesExportar reporte="operativo" estado={EXPORTACION_INICIAL} alPedir={vi.fn()} alReintentar={vi.fn()} alDescargar={vi.fn()} alCerrar={vi.fn()} />);

    expect(screen.queryByRole('button', { name: /Descargar de nuevo/ })).toBeNull();
    expect(document.activeElement).not.toBe(document.body);
  });

  it('el fallo dice de qué reporte y de qué formato era', () => {
    dibujar({ fase: 'fallida', formato: 'pdf', reporte: 'gerencial', mensaje: 'No se pudo.', reintentable: true });
    expect(screen.getByRole('alert')).toHaveTextContent(/Reporte gerencial en PDF/);
  });

  it('el botón «Imprimir» llama a la impresión de la página', () => {
    const imprimir = vi.spyOn(window, 'print').mockImplementation(() => {});
    dibujar();
    fireEvent.click(screen.getByRole('button', { name: 'Imprimir' }));
    expect(imprimir).toHaveBeenCalledTimes(1);
    imprimir.mockRestore();
  });
});
