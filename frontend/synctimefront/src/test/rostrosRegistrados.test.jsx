import { fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import RostrosRegistrados from '../paginas/rostros/RostrosRegistrados';
import CalibrarRostro from '../paginas/rostros/CalibrarRostro';
import { ProveedorAvisos } from '../avisos/Avisos';
import { almacenSesion } from '../sesion/sesion';
import authService from '../api/authService';
import { rostros } from '../api/rostros';
import { personal } from '../api/mantenedores';
import { detectarCuadro, prepararMotor } from '../rostro/motorFacial';
import { avanzar, crearCamara, personaQueSigue, simularMovimientoReducido, simularNavegador } from './rostroAuxiliares';

vi.mock('../api/authService', () => ({
  default: { estadoRostro: vi.fn(), desafioFacial: vi.fn() },
}));

vi.mock('../api/rostros', () => ({
  rostros: {
    listar: vi.fn(),
    registrar: vi.fn(),
    desactivar: vi.fn(),
    calibracion: { obtener: vi.fn(), medir: vi.fn(), borrar: vi.fn() },
  },
}));

vi.mock('../api/mantenedores', () => ({
  personal: { obtener: vi.fn() },
}));

vi.mock('../rostro/motorFacial', async (original) => ({
  ...(await original()),
  prepararMotor: vi.fn(),
  detectarCuadro: vi.fn(),
}));

const consentimiento = { version: 'v-prueba', texto: ['Sistema biométrico: prueba.'] };
const FILAS = [
  {
    idEmpleado: 1, nombre: 'Ana Díaz', rol: 'dueño', estado: 'activo', fechaRegistro: '2026-11-01T12:00:00Z',
    fechaConsentimiento: '2026-11-01T12:00:00Z', registradoPor: 'Ana Díaz', fechaDesactivacion: null, historial: [],
  },
  {
    idEmpleado: 2, nombre: 'Marta Fuentes', rol: 'administrador', estado: 'sin_registro', fechaRegistro: null,
    fechaConsentimiento: null, registradoPor: null, fechaDesactivacion: '2026-10-20T12:00:00Z',
    historial: [{ fechaConsentimiento: '2026-10-01T12:00:00Z', fechaDesactivacion: '2026-10-20T12:00:00Z' }],
  },
  {
    idEmpleado: 3, nombre: 'Luis Soto', rol: 'administrador', estado: 'debe_registrarse', fechaRegistro: '2026-09-01T12:00:00Z',
    fechaConsentimiento: '2026-09-01T12:00:00Z', registradoPor: 'Ana Díaz', fechaDesactivacion: null, historial: [],
  },
];

const CALIBRACION = {
  activa: true,
  umbralVigente: 0.5,
  rangos: { frenteMaximo: 0.15, giroMinimo: 0.3, giroMaximo: 0.75 },
  mediciones: [
    {
      idMedicion: 1, idEmpleado: 1, empleado: 'Ana Díaz', etiqueta: 'propio', distanciaFrente: 0.31, distanciaPrimerGiro: 0.35,
      distanciaSegundoGiro: 0.38, distanciaMaxEntreCapturas: 0.4, giroFrente: 0.02, giroPrimero: 0.45, giroSegundo: -0.44,
      umbralVigente: 0.5, fecha: '2026-11-15T12:00:00Z',
    },
    {
      idMedicion: 2, idEmpleado: 1, empleado: 'Ana Díaz', etiqueta: 'otra_persona', distanciaFrente: 0.61, distanciaPrimerGiro: 0.66,
      distanciaSegundoGiro: 0.64, distanciaMaxEntreCapturas: 0.7, giroFrente: -0.03, giroPrimero: 0.41, giroSegundo: -0.5,
      umbralVigente: 0.5, fecha: '2026-11-15T12:05:00Z',
    },
  ],
  resumen: {
    propio: { n: 1, min: 0.38, max: 0.38, promedio: 0.38 },
    otra: { n: 1, min: 0.66, max: 0.66, promedio: 0.66 },
    umbralSugerido: 0.52,
    margen: 0.14,
  },
};

function renderCon(pagina) {
  almacenSesion.aplicar({ token: 't', tipo: 'empleado', usuario: { idEmpleado: 1, nombre: 'Ana', apellido: 'Díaz', email: 'ana@taller.cl', rol: 'dueño' } });
  return render(
    <ProveedorAvisos>
      <MemoryRouter>{pagina}</MemoryRouter>
    </ProveedorAvisos>
  );
}

function error404() {
  return Object.assign(new Error('No encontramos lo que buscas.'), { estado: 404, codigo: 'http_404' });
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.clearAllMocks();
  simularMovimientoReducido(false);
  simularNavegador(vi.fn().mockResolvedValue(crearCamara().camara));
  prepararMotor.mockResolvedValue();
  authService.estadoRostro.mockResolvedValue({ disponible: true, activo: true, consentimiento });
  rostros.listar.mockResolvedValue(FILAS);
  rostros.calibracion.obtener.mockRejectedValue(error404());
});

afterEach(() => {
  vi.useRealTimers();
});

describe('Rostros registrados (S16)', () => {
  it('lista a gestión con su estado, fechas, quién registró y el historial', async () => {
    renderCon(<RostrosRegistrados />);
    await avanzar(10);

    expect(screen.getByRole('heading', { level: 1, name: 'Rostros registrados' })).toBeInTheDocument();
    const tabla = screen.getByRole('table', { name: /rostros registrados/i });
    for (const encabezado of ['Rol', 'Estado', 'Registro', 'Consentimiento', 'Registrado por', 'Desactivación', 'Historial']) {
      expect(within(tabla).getByRole('columnheader', { name: encabezado })).toBeInTheDocument();
    }
    const marta = within(tabla).getByRole('rowheader', { name: /marta fuentes/i }).closest('tr');
    expect(within(marta).getByText('Sin registro')).toBeInTheDocument();
    expect(within(marta).getByText(/1 constancia anterior/i)).toBeInTheDocument();
    const luis = within(tabla).getByRole('rowheader', { name: /luis soto/i }).closest('tr');
    expect(within(luis).getByText('Debe registrarse de nuevo')).toBeInTheDocument();
  });

  it('sin la calibración activa no ofrece calibrar', async () => {
    renderCon(<RostrosRegistrados />);
    await avanzar(10);
    expect(screen.queryByRole('link', { name: /calibrar/i })).not.toBeInTheDocument();
  });

  it('con la calibración activa enlaza a «Calibrar el rostro»', async () => {
    rostros.calibracion.obtener.mockResolvedValue(CALIBRACION);
    renderCon(<RostrosRegistrados />);
    await avanzar(10);
    expect(screen.getByRole('link', { name: /calibrar el rostro/i })).toHaveAttribute('href', '/panel/rostros/calibrar');
  });

  it('registra el rostro de otro empleado de gestión con su contraseña y su consentimiento', async () => {
    rostros.registrar.mockResolvedValue({ activo: true });
    renderCon(<RostrosRegistrados />);
    await avanzar(10);

    fireEvent.click(screen.getByRole('button', { name: 'Registrar rostro de Marta Fuentes' }));
    await avanzar(10);
    const seccion = screen.getByRole('region', { name: 'Registrar el rostro de Marta Fuentes' });
    expect(within(seccion).getByRole('heading', { name: 'Registrar el rostro de Marta Fuentes' })).toHaveFocus();
    expect(within(seccion).getByText('Sistema biométrico: prueba.')).toBeInTheDocument();

    detectarCuadro.mockImplementation(personaQueSigue(['izquierda', 'derecha']));
    fireEvent.click(within(seccion).getByRole('checkbox', { name: /marta fuentes leyó/i }));
    fireEvent.change(within(seccion).getByLabelText(/contraseña de marta fuentes/i), { target: { value: 'clave-de-marta' } });
    fireEvent.click(within(seccion).getByRole('button', { name: /registrar el rostro/i }));
    await avanzar(6000);
    fireEvent.click(within(seccion).getByRole('button', { name: /guardar el rostro/i }));
    await avanzar(10);

    expect(rostros.registrar).toHaveBeenCalledTimes(1);
    const [id, datos] = rostros.registrar.mock.calls[0];
    expect(id).toBe(2);
    expect(datos.passwordTitular).toBe('clave-de-marta');
    expect(datos.versionConsentimiento).toBe('v-prueba');
    expect(Object.keys(datos.muestras).sort()).toEqual(['derecha', 'frente', 'izquierda']);
    expect(rostros.listar).toHaveBeenCalledTimes(2);
  });

  it('el propio rostro se registra desde Mi perfil', async () => {
    rostros.listar.mockResolvedValue([{ ...FILAS[0], estado: 'sin_registro' }]);
    renderCon(<RostrosRegistrados />);
    await avanzar(10);
    expect(screen.getByRole('link', { name: /registrar tu rostro en mi perfil/i })).toHaveAttribute('href', '/panel/perfil');
  });

  it('desactivar pide confirmación y borra las muestras', async () => {
    rostros.desactivar.mockResolvedValue(null);
    renderCon(<RostrosRegistrados />);
    await avanzar(10);

    fireEvent.click(screen.getByRole('button', { name: 'Desactivar el rostro de Luis Soto' }));
    expect(rostros.desactivar).not.toHaveBeenCalled();
    const dialogo = screen.getByRole('dialog', { name: /desactivar el rostro de luis soto/i });
    expect(within(dialogo).getByRole('button', { name: 'Cancelar' })).toHaveFocus();
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Desactivar' }));
    await avanzar(10);

    expect(rostros.desactivar).toHaveBeenCalledWith(3);
    expect(rostros.listar).toHaveBeenCalledTimes(2);
  });
});

describe('Calibrar el rostro', () => {
  it('apagada por configuración: lo explica y no ofrece intentos', async () => {
    renderCon(<CalibrarRostro />);
    await avanzar(10);

    expect(screen.getByRole('heading', { level: 1, name: 'Calibrar el rostro' })).toBeInTheDocument();
    expect(screen.getByText(/RostroFacial:Calibracion:Activa/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /hacer un intento/i })).not.toBeInTheDocument();
  });

  it('muestra las mediciones y el umbral sugerido con su margen, que se copia a mano', async () => {
    rostros.calibracion.obtener.mockResolvedValue(CALIBRACION);
    renderCon(<CalibrarRostro />);
    await avanzar(10);

    const tabla = screen.getByRole('table', { name: /mediciones/i });
    expect(within(tabla).getAllByRole('row')).toHaveLength(3);
    expect(within(tabla).getByText('Soy yo')).toBeInTheDocument();
    expect(within(tabla).getByText('Otra persona')).toBeInTheDocument();
    const resumen = screen.getByRole('region', { name: /resumen/i });
    expect(within(resumen).getByText('0,520')).toBeInTheDocument();
    expect(within(resumen).getByText('0,140')).toBeInTheDocument();
    expect(screen.getByText(/RostroFacial:UmbralDistancia/)).toBeInTheDocument();
  });

  it('hace un intento guiado: pide el desafío con el email del empleado y guarda solo la medición', async () => {
    rostros.calibracion.obtener.mockResolvedValue(CALIBRACION);
    rostros.calibracion.medir.mockResolvedValue({ medicion: CALIBRACION.mediciones[0], habriaEntrado: true });
    personal.obtener.mockResolvedValue({ idEmpleado: 1, email: 'ana@taller.cl' });
    authService.desafioFacial.mockResolvedValue({ desafio: 'd-cal', orden: ['derecha', 'izquierda'], venceEnSegundos: 30 });
    detectarCuadro.mockImplementation(personaQueSigue(['derecha', 'izquierda']));
    renderCon(<CalibrarRostro />);
    await avanzar(10);

    fireEvent.change(screen.getByLabelText(/empleado/i), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('radio', { name: /^soy yo/i }));
    fireEvent.click(screen.getByRole('button', { name: /hacer un intento/i }));
    await avanzar(6000);

    expect(authService.desafioFacial).toHaveBeenCalledWith('ana@taller.cl', expect.any(AbortSignal));
    expect(rostros.calibracion.medir).toHaveBeenCalledTimes(1);
    const datos = rostros.calibracion.medir.mock.calls[0][0];
    expect(datos).toMatchObject({ idEmpleado: 1, etiqueta: 'propio', desafio: 'd-cal' });
    expect(datos.capturas.map((c) => c.paso)).toEqual(['frente', 'derecha', 'izquierda']);
    expect(screen.getByText(/habría entrado/i)).toBeInTheDocument();
    expect(rostros.calibracion.obtener).toHaveBeenCalledTimes(2);
  });

  it('exige elegir el empleado y la etiqueta antes de encender la cámara', async () => {
    rostros.calibracion.obtener.mockResolvedValue(CALIBRACION);
    renderCon(<CalibrarRostro />);
    await avanzar(10);

    fireEvent.click(screen.getByRole('button', { name: /hacer un intento/i }));
    await avanzar(10);

    expect(screen.getByText(/elige el empleado/i)).toBeInTheDocument();
    expect(screen.getByText(/elige si eres tú u otra persona/i)).toBeInTheDocument();
    expect(navigator.mediaDevices.getUserMedia).not.toHaveBeenCalled();
  });

  it('«Borrar mediciones» pide confirmación', async () => {
    rostros.calibracion.obtener.mockResolvedValue(CALIBRACION);
    rostros.calibracion.borrar.mockResolvedValue(null);
    renderCon(<CalibrarRostro />);
    await avanzar(10);

    fireEvent.click(screen.getByRole('button', { name: 'Borrar mediciones' }));
    expect(rostros.calibracion.borrar).not.toHaveBeenCalled();
    const dialogo = screen.getByRole('dialog', { name: /borrar las mediciones/i });
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Borrar' }));
    await avanzar(10);

    expect(rostros.calibracion.borrar).toHaveBeenCalled();
  });
});
