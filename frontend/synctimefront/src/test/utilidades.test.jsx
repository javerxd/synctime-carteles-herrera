import { act, renderHook } from '@testing-library/react';
import { formatearFecha, formatearFechaHora, formatearPesos } from '../utilidades/formato';
import { formatearRut, validarRut } from '../utilidades/rut';
import { useDebounce } from '../utilidades/useDebounce';

describe('formatearPesos', () => {
  it('escribe pesos chilenos sin decimales y con punto de miles', () => {
    expect(formatearPesos(12990)).toBe('$12.990');
    expect(formatearPesos(1250000)).toBe('$1.250.000');
    expect(formatearPesos(0)).toBe('$0');
  });

  it('redondea, acepta texto numérico y marca la ausencia de dato', () => {
    expect(formatearPesos(9999.6)).toBe('$10.000');
    expect(formatearPesos('85000')).toBe('$85.000');
    expect(formatearPesos(null)).toBe('—');
    expect(formatearPesos(undefined)).toBe('—');
    expect(formatearPesos('abc')).toBe('—');
  });
});

describe('formatearFecha', () => {
  it('usa dd-mm-aaaa', () => {
    expect(formatearFecha('2026-11-04')).toBe('04-11-2026');
    expect(formatearFecha(new Date(2026, 0, 9))).toBe('09-01-2026');
  });

  it('una fecha sin hora no se corre un día por la zona horaria', () => {
    expect(formatearFecha('2026-03-01')).toBe('01-03-2026');
  });

  it('con hora toma el día local y puede mostrar la hora', () => {
    expect(formatearFecha('2026-11-04T23:30:00')).toBe('04-11-2026');
    expect(formatearFechaHora('2026-11-04T09:05:00')).toBe('04-11-2026 09:05');
  });

  it('marca la ausencia de dato o una fecha inválida', () => {
    expect(formatearFecha(null)).toBe('—');
    expect(formatearFecha('no es fecha')).toBe('—');
  });
});

describe('RUT', () => {
  it.each(['12.345.678-5', '12345678-5', '123456785', '7.654.321-6', '30.686.957-4', '15.682.397-K', '15682397k'])(
    'acepta %s', (rut) => expect(validarRut(rut)).toBe(true),
  );

  it.each(['12.345.678-4', '', '1-1', 'abc', '12.345.678', null])(
    'rechaza %s', (rut) => expect(validarRut(rut)).toBe(false),
  );

  it('escribe el RUT con puntos, guion y K mayúscula', () => {
    expect(formatearRut('123456785')).toBe('12.345.678-5');
    expect(formatearRut('15682397k')).toBe('15.682.397-K');
    expect(formatearRut('7654321-6')).toBe('7.654.321-6');
  });
});

describe('useDebounce', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('entrega el valor solo cuando deja de cambiar por 300 ms', () => {
    const { result, rerender } = renderHook(({ valor }) => useDebounce(valor), { initialProps: { valor: 'pan' } });
    expect(result.current).toBe('pan');
    rerender({ valor: 'panad' });
    act(() => vi.advanceTimersByTime(200));
    expect(result.current).toBe('pan');
    rerender({ valor: 'panadería' });
    act(() => vi.advanceTimersByTime(299));
    expect(result.current).toBe('pan');
    act(() => vi.advanceTimersByTime(1));
    expect(result.current).toBe('panadería');
  });
});
