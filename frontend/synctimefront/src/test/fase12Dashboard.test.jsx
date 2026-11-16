import { act, fireEvent, render, renderHook, screen, waitFor, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { indicadores } from '../api/indicadores';
import Indicadores from '../paginas/indicadores/Indicadores';
import { BLOQUES, useIndicadores } from '../paginas/indicadores/useIndicadores';
import { parametrosIndicadores } from '../paginas/indicadores/reglasIndicadores';

// Fase 12A: la pantalla de indicadores con el adaptador de demostración (VITE_INDICADORES_DEMO). «Hoy» se fija
// en el 16-11-2026 para que los datos de ejemplo no cambien.
beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date('2026-11-16T12:00:00-03:00'));
});
afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

function Ruta() {
  const { pathname, search } = useLocation();
  return <output data-testid="ruta">{`${pathname}${search}`}</output>;
}

function dibujar(entrada = '/panel/indicadores') {
  return render(
    <MemoryRouter initialEntries={[entrada]}>
      <Routes><Route path="/panel/indicadores" element={<><Indicadores /><Ruta /></>} /></Routes>
    </MemoryRouter>,
  );
}

const bloque = (titulo) => screen.getByRole('region', { name: titulo });

describe('carga en paralelo con fallo aislado (RNFP23)', () => {
  const diferida = () => {
    let resolver;
    let rechazar;
    const promesa = new Promise((si, no) => { resolver = si; rechazar = no; });
    return { promesa, resolver, rechazar };
  };

  it('pide los seis bloques a la vez, antes de que responda ninguno', () => {
    const pendientes = Object.fromEntries(BLOQUES.map((b) => [b, diferida()]));
    const consultas = Object.fromEntries(BLOQUES.map((b) => [b, vi.fn(() => pendientes[b].promesa)]));
    renderHook(() => useIndicadores({ periodo: 'mes', comparar: 'anterior', alcanceSaldo: 'periodo', pagina: 1 }, consultas));
    expect(BLOQUES).toHaveLength(6);
    BLOQUES.forEach((b) => expect(consultas[b]).toHaveBeenCalledTimes(1));
  });

  it('un bloque que falla no tumba a los demás, y «Reintentar» vuelve a pedir solo ese bloque', async () => {
    const consultas = Object.fromEntries(BLOQUES.map((b) => [b, vi.fn(() => Promise.resolve({ bloque: b }))]));
    consultas.embudo.mockRejectedValueOnce(new Error('Falló el embudo'));
    const { result } = renderHook(() => useIndicadores({ periodo: 'mes', comparar: 'anterior', alcanceSaldo: 'periodo', pagina: 1 }, consultas));

    await waitFor(() => expect(result.current.listos).toBe(true));
    expect(result.current.bloques.embudo.error.message).toBe('Falló el embudo');
    expect(result.current.bloques.resumen.datos).toEqual({ bloque: 'resumen' });
    expect(result.current.bloques.porCobrar.datos).toEqual({ bloque: 'porCobrar' });

    act(() => result.current.reintentar('embudo'));
    await waitFor(() => expect(result.current.bloques.embudo.datos).toEqual({ bloque: 'embudo' }));
    expect(consultas.embudo).toHaveBeenCalledTimes(2);
    expect(consultas.resumen).toHaveBeenCalledTimes(1);
  });

  it('cambiar de página del listado solo vuelve a pedir «por cobrar»', async () => {
    const consultas = Object.fromEntries(BLOQUES.map((b) => [b, vi.fn(() => Promise.resolve({}))]));
    const filtros = { periodo: 'mes', comparar: 'anterior', alcanceSaldo: 'periodo', pagina: 1 };
    const { rerender, result } = renderHook((f) => useIndicadores(f, consultas), { initialProps: filtros });
    await waitFor(() => expect(result.current.listos).toBe(true));
    rerender({ ...filtros, pagina: 2 });
    await waitFor(() => expect(consultas.porCobrar).toHaveBeenCalledTimes(2));
    expect(consultas.resumen).toHaveBeenCalledTimes(1);
    rerender({ ...filtros, pagina: 2, periodo: 'anio' });
    await waitFor(() => expect(consultas.resumen).toHaveBeenCalledTimes(2));
  });
});

describe('pantalla de indicadores', () => {
  it('avisa que son datos de demostración y muestra las tarjetas con su comparación', async () => {
    dibujar();
    expect(screen.getByRole('heading', { level: 1, name: 'Indicadores' })).toBeInTheDocument();
    expect(screen.getByText('Datos de demostración')).toBeInTheDocument();
    const resumen = bloque('Resumen del período');
    expect(await within(resumen).findByText('Ingresos')).toBeInTheDocument();
    ['Pedidos', 'En línea y presenciales', 'Solicitudes concretadas', 'Conversión en línea', 'Ticket promedio', 'Entregas a tiempo']
      .forEach((nombre) => expect(within(resumen).getByText(nombre)).toBeInTheDocument());
    expect(within(resumen).getAllByText(/respecto de octubre de 2026/).length).toBeGreaterThan(0);
  });

  it('si un bloque falla, los demás se ven y ese bloque ofrece «Reintentar»', async () => {
    const original = indicadores.demanda;
    const espia = vi.spyOn(indicadores, 'demanda').mockRejectedValueOnce(new Error('Sin conexión'));
    dibujar();
    const demanda = bloque('Demanda por categoría');
    expect(await within(demanda).findByRole('alert')).toHaveTextContent('No pudimos cargar este bloque');
    expect(await within(bloque('Resumen del período')).findByText('Ingresos')).toBeInTheDocument();
    expect(within(bloque('Embudo del canal web')).queryByRole('alert')).toBeNull();

    espia.mockImplementation(original);
    fireEvent.click(within(demanda).getByRole('button', { name: 'Reintentar' }));
    expect(await within(demanda).findByText('Letreros luminosos')).toBeInTheDocument();
    expect(within(demanda).queryByRole('alert')).toBeNull();
  });

  it('cada gráfico cambia a su tabla de datos', async () => {
    dibujar();
    const evolucion = bloque('Evolución comercial');
    fireEvent.click(await within(evolucion).findByRole('button', { name: 'Tabla de datos' }));
    expect(within(evolucion).getByRole('button', { name: 'Tabla de datos' })).toHaveAttribute('aria-pressed', 'true');
    const tabla = within(evolucion).getByRole('table');
    expect(within(tabla).getByRole('columnheader', { name: /Ingresos/ })).toBeInTheDocument();
  });

  it('un clic en un segmento de la cobranza filtra la tabla de pedidos por cobrar', async () => {
    dibujar('/panel/indicadores?alcanceSaldo=vigente');
    const cobranza = bloque('Cobranza');
    fireEvent.click(await within(cobranza).findByRole('button', { name: /^No pagado/ }));
    expect(screen.getByTestId('ruta')).toHaveTextContent('cobro=no_pagado');
    const tabla = bloque('Pedidos por cobrar');
    await waitFor(() => {
      const filas = within(tabla).getAllByRole('row').slice(1);
      expect(filas.length).toBeGreaterThan(0);
      filas.forEach((fila) => expect(fila).toHaveTextContent('No pagado'));
    });
    fireEvent.click(within(cobranza).getByRole('button', { name: /^No pagado/ }));
    expect(screen.getByTestId('ruta')).not.toHaveTextContent('cobro=');
  });

  it('el saldo dice si es del período o de toda la deuda vigente, y se cambia con un clic', async () => {
    dibujar();
    const resumen = bloque('Resumen del período');
    expect(await within(resumen).findByText(/Saldo del período/)).toBeInTheDocument();
    fireEvent.click(within(resumen).getByRole('button', { name: 'Ver toda la deuda vigente' }));
    expect(screen.getByTestId('ruta')).toHaveTextContent('alcanceSaldo=vigente');
    expect(await within(resumen).findByText(/Toda la deuda vigente/)).toBeInTheDocument();
    expect(within(bloque('Pedidos por cobrar')).getByText(/Toda la deuda vigente, sin límite de fecha/)).toBeInTheDocument();
  });

  it('un bloque sin datos lo dice', async () => {
    vi.spyOn(indicadores, 'demanda').mockResolvedValue({ periodo: { etiqueta: 'noviembre de 2026' }, categorias: [] });
    dibujar();
    expect(await within(bloque('Demanda por categoría')).findByText('Sin pedidos por categoría en este período')).toBeInTheDocument();
  });

  it('los filtros viven en la dirección: elegir «Año» cambia el período de todos los bloques', async () => {
    dibujar();
    fireEvent.click(await screen.findByRole('radio', { name: 'Año' }));
    expect(screen.getByTestId('ruta')).toHaveTextContent('periodo=anio');
    expect(await within(bloque('Resumen del período')).findAllByText(/respecto de 2025/)).not.toHaveLength(0);
  });
});

describe('robustez (revisión de la fase 12A)', () => {
  const BASE = { periodo: 'mes', comparar: 'anterior', alcanceSaldo: 'periodo', pagina: 1 };

  it('una respuesta vieja que llega tarde no pisa a la nueva', async () => {
    const resolvedores = [];
    const consultas = Object.fromEntries(BLOQUES.map((b) => [b, vi.fn(() => Promise.resolve({ b }))]));
    consultas.resumen = vi.fn((f) => new Promise((resolver) => resolvedores.push(() => resolver({ periodo: f.periodo }))));
    const { result, rerender } = renderHook((f) => useIndicadores(f, consultas), { initialProps: BASE });
    rerender({ ...BASE, periodo: 'anio' });
    await waitFor(() => expect(resolvedores).toHaveLength(2));
    await act(async () => { resolvedores[1](); });
    await act(async () => { resolvedores[0](); });
    expect(result.current.bloques.resumen.datos).toEqual({ periodo: 'anio' });
  });

  it('cambiar el alcance del saldo solo recarga el resumen, la cobranza y la tabla', async () => {
    const consultas = Object.fromEntries(BLOQUES.map((b) => [b, vi.fn(() => Promise.resolve({}))]));
    const { result, rerender } = renderHook((f) => useIndicadores(f, consultas), { initialProps: BASE });
    await waitFor(() => expect(result.current.listos).toBe(true));
    rerender({ ...BASE, alcanceSaldo: 'vigente' });
    await waitFor(() => expect(consultas.porCobrar).toHaveBeenCalledTimes(2));
    expect(consultas.resumen).toHaveBeenCalledTimes(2);
    expect(consultas.cobranza).toHaveBeenCalledTimes(2);
    ['evolucion', 'embudo', 'demanda'].forEach((b) => expect(consultas[b]).toHaveBeenCalledTimes(1));
  });

  it('en StrictMode cada bloque termina con sus datos, sin quedarse cargando', async () => {
    const consultas = Object.fromEntries(BLOQUES.map((b) => [b, vi.fn(() => Promise.resolve({ b }))]));
    const { result } = renderHook(() => useIndicadores(BASE, consultas), { wrapper: StrictMode });
    await waitFor(() => expect(result.current.listos).toBe(true));
    BLOQUES.forEach((b) => expect(result.current.bloques[b].datos).toEqual({ b }));
  });

  it('un bloque con una respuesta de forma inesperada muestra su aviso y los demás siguen', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(indicadores, 'embudo').mockResolvedValue({ periodo: { etiqueta: 'noviembre de 2026' }, pasos: [{ clave: 'solicitudes', cantidad: 3 }, null] });
    dibujar();
    expect(await within(bloque('Embudo del canal web')).findByRole('alert')).toHaveTextContent('No pudimos mostrar este bloque');
    expect(await within(bloque('Resumen del período')).findByText('Ingresos')).toBeInTheDocument();
  });

  it('el segmento del anillo afina el estado de pago elegido y nunca lo contradice', () => {
    expect(parametrosIndicadores({ estadoPago: 'por_cobrar', cobro: 'abonado' }, { listado: true }).estadoPago).toBe('abonado');
    expect(parametrosIndicadores({ estadoPago: 'pagado_total', cobro: 'abonado' }, { listado: true }).estadoPago).toBe('pagado_total');
  });
});
