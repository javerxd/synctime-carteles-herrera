import { fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import Perfil from '../paginas/panel/Perfil';
import { ProveedorAvisos } from '../avisos/Avisos';
import { almacenSesion } from '../sesion/sesion';
import authService from '../api/authService';
import { detectarCuadro, prepararMotor } from '../rostro/motorFacial';
import { guardarTokenEquipo, leerTokenEquipo } from '../rostro/tokenEquipo';
import { avanzar, crearCamara, cuadroCon, personaQueSigue, simularMovimientoReducido, simularNavegador } from './rostroAuxiliares';

vi.mock('../api/authService', () => ({
  default: {
    estadoRostro: vi.fn(),
    activarRostro: vi.fn(),
    autorizarEquipo: vi.fn(),
    desactivarRostro: vi.fn(),
  },
}));

// La sección «Avisos del taller» (fase 9) se dibuja para gestión: aquí no importa, solo no debe ir a la red.
vi.mock('../api/avisosTaller', () => ({
  avisosTaller: { obtener: vi.fn(() => Promise.resolve({ destinos: [], usaRespaldo: false, historial: [] })) },
}));

vi.mock('../rostro/motorFacial', async (original) => ({
  ...(await original()),
  prepararMotor: vi.fn(),
  detectarCuadro: vi.fn(),
}));

const EMAIL = 'dueno@letrerosherrera.cl';
const consentimiento = { version: 'v-prueba', texto: ['Sistema biométrico: prueba.', 'Finalidad: prueba.'] };
const inactivo = {
  disponible: true, activo: false, requiereEquipo: true, equipoVigente: false, consentimiento, puedeUsarRostro: true, debeRegistrarseDeNuevo: false,
};
const activo = {
  ...inactivo, activo: true, fechaConsentimiento: '2026-11-06T12:00:00+00:00', equipoVence: '2026-12-06T12:00:00+00:00', equipoVigente: true,
};

function renderPerfil(rol = 'dueño') {
  almacenSesion.aplicar({ token: 't', tipo: 'empleado', usuario: { idEmpleado: 1, nombre: 'Ana', apellido: 'Díaz', email: EMAIL, rol } });
  return render(
    <ProveedorAvisos>
      <Perfil />
    </ProveedorAvisos>
  );
}

const escribirPassword = () =>
  fireEvent.change(screen.getByLabelText(/contraseña actual/i), { target: { value: 'mi-password' } });
const registrar = () => fireEvent.click(screen.getByRole('button', { name: /registrar mi rostro/i }));

/** Acepta, escribe la contraseña y hace el flujo guiado completo de las tres muestras. */
async function capturarLasTres() {
  detectarCuadro.mockImplementation(personaQueSigue(['izquierda', 'derecha']));
  fireEvent.click(screen.getByRole('checkbox'));
  escribirPassword();
  registrar();
  await avanzar(6000);
}

describe('Mi perfil: rostro con tres muestras (solo gestión)', () => {
  let getUserMedia;

  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
    sessionStorage.clear();
    localStorage.clear();
    simularMovimientoReducido(false);
    getUserMedia = vi.fn().mockResolvedValue(crearCamara().camara);
    simularNavegador(getUserMedia);
    prepararMotor.mockResolvedValue();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it.each(['diseñador', 'operario'])('un %s no ve la sección ni consulta su estado', async (rol) => {
    authService.estadoRostro.mockResolvedValue(inactivo);
    renderPerfil(rol);
    await avanzar(10);

    expect(screen.getByRole('heading', { level: 1, name: 'Mi perfil' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: /acceso con rostro/i })).not.toBeInTheDocument();
    expect(authService.estadoRostro).not.toHaveBeenCalled();
  });

  it('muestra el consentimiento y no abre la cámara antes de registrar', async () => {
    authService.estadoRostro.mockResolvedValue(inactivo);
    renderPerfil();
    await avanzar(10);

    expect(screen.getByText('Sistema biométrico: prueba.')).toBeInTheDocument();
    expect(getUserMedia).not.toHaveBeenCalled();
  });

  it('no abre la cámara sin aceptar el consentimiento ni sin la contraseña', async () => {
    authService.estadoRostro.mockResolvedValue(inactivo);
    renderPerfil();
    await avanzar(10);
    escribirPassword();
    registrar();
    expect(screen.getByRole('checkbox')).toHaveAccessibleDescription(/debes aceptar/i);
    expect(screen.getByRole('checkbox')).toHaveFocus();

    fireEvent.click(screen.getByRole('checkbox'));
    fireEvent.change(screen.getByLabelText(/contraseña actual/i), { target: { value: '' } });
    registrar();
    expect(screen.getByLabelText(/contraseña actual/i)).toHaveAccessibleDescription(/escribe la contraseña/i);
    expect(screen.getByLabelText(/contraseña actual/i)).toHaveFocus();
    expect(getUserMedia).not.toHaveBeenCalled();
  });

  it('registra con el flujo guiado, muestra la vista previa de cada muestra y guarda las tres', async () => {
    authService.estadoRostro.mockResolvedValueOnce(inactivo).mockResolvedValue(activo);
    authService.activarRostro.mockResolvedValue({ activo: true, tokenEquipo: 'token-de-equipo' });
    renderPerfil();
    await avanzar(10);

    await capturarLasTres();

    const revision = screen.getByRole('region', { name: /revisa las tres muestras/i });
    for (const nombre of [/de frente/i, /hacia tu izquierda/i, /hacia tu derecha/i]) {
      expect(within(revision).getByRole('img', { name: nombre })).toBeInTheDocument();
    }
    expect(within(revision).getAllByRole('button', { name: /repetir esta muestra/i })).toHaveLength(3);
    expect(authService.activarRostro).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: /guardar mi rostro/i }));
    await avanzar(10);

    expect(authService.activarRostro).toHaveBeenCalledTimes(1);
    const datos = authService.activarRostro.mock.calls[0][0];
    expect(datos.password).toBe('mi-password');
    expect(datos.versionConsentimiento).toBe('v-prueba');
    expect(Object.keys(datos.muestras).sort()).toEqual(['derecha', 'frente', 'izquierda']);
    expect(datos.muestras.izquierda.descriptor).toHaveLength(128);
    expect(JSON.stringify(datos)).not.toMatch(/data:image|canvas/i);
    expect(leerTokenEquipo(EMAIL)).toBe('token-de-equipo');
    expect(screen.getByText(/este equipo está autorizado/i)).toBeInTheDocument();
  });

  it('«Repetir esta muestra» vuelve a tomar solo esa', async () => {
    authService.estadoRostro.mockResolvedValue(inactivo);
    renderPerfil();
    await avanzar(10);
    await capturarLasTres();
    const camarasAntes = getUserMedia.mock.calls.length;

    detectarCuadro.mockImplementation(async (_v, op) => cuadroCon({ g: -0.5, conDescriptor: op?.conDescriptor }));
    const revision = screen.getByRole('region', { name: /revisa las tres muestras/i });
    const figura = within(revision).getByRole('img', { name: /hacia tu derecha/i }).closest('figure');
    fireEvent.click(within(figura).getByRole('button', { name: /repetir esta muestra/i }));
    await avanzar(100);
    expect(getUserMedia.mock.calls.length).toBe(camarasAntes + 1);
    expect(screen.getByText(/paso 1 de 1/i)).toBeInTheDocument();
    await avanzar(2000);
    expect(screen.getByRole('region', { name: /revisa las tres muestras/i })).toBeInTheDocument();
    expect(getUserMedia.mock.calls.length).toBe(camarasAntes + 1);
  });

  it('muestra junto a cada muestra el error que devuelve el servidor', async () => {
    authService.estadoRostro.mockResolvedValue(inactivo);
    authService.activarRostro.mockRejectedValue({
      estado: 400, codigo: 'datos_invalidos', message: 'Revisa los datos ingresados.',
      errores: { muestras: { izquierda: 'Gira un poco más la cabeza y repite esta muestra.' } },
    });
    renderPerfil();
    await avanzar(10);
    await capturarLasTres();

    fireEvent.click(screen.getByRole('button', { name: /guardar mi rostro/i }));
    await avanzar(10);

    const figura = screen.getByRole('img', { name: /hacia tu izquierda/i }).closest('figure');
    expect(within(figura).getByText('Gira un poco más la cabeza y repite esta muestra.')).toBeInTheDocument();
  });

  it('con las tres muestras, Enter en la contraseña guarda en vez de volver a capturar', async () => {
    authService.estadoRostro.mockResolvedValue(inactivo);
    authService.activarRostro.mockResolvedValue({ activo: true, tokenEquipo: 't-equipo' });
    renderPerfil();
    await avanzar(10);
    await capturarLasTres();
    const camaras = getUserMedia.mock.calls.length;

    fireEvent.submit(screen.getByLabelText(/contraseña actual/i).closest('form'));
    await avanzar(10);

    expect(authService.activarRostro).toHaveBeenCalledTimes(1);
    expect(getUserMedia.mock.calls.length).toBe(camaras);
  });

  it('«Descartar» borra la contraseña y el consentimiento de la pantalla', async () => {
    authService.estadoRostro.mockResolvedValue(inactivo);
    renderPerfil();
    await avanzar(10);
    await capturarLasTres();

    fireEvent.click(screen.getByRole('button', { name: 'Descartar' }));

    expect(screen.getByLabelText(/contraseña actual/i)).toHaveValue('');
    expect(screen.getByRole('checkbox')).not.toBeChecked();
    expect(screen.queryByRole('region', { name: /revisa las tres muestras/i })).not.toBeInTheDocument();
  });

  it('al cancelar la captura el foco vuelve al botón para empezar, y el formulario reaparece', async () => {
    authService.estadoRostro.mockResolvedValue(inactivo);
    detectarCuadro.mockImplementation(async () => cuadroCon({ rostros: 0 }));
    renderPerfil();
    await avanzar(10);
    fireEvent.click(screen.getByRole('checkbox'));
    escribirPassword();
    registrar();
    await avanzar(100);
    expect(screen.queryByLabelText(/contraseña actual/i)).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }));
    await avanzar(10);

    expect(screen.getByRole('button', { name: /registrar mi rostro/i })).toHaveFocus();
    expect(screen.getByLabelText(/contraseña actual/i)).toHaveValue('mi-password');
  });

  it('avisa que el rostro debe registrarse de nuevo con las tres muestras', async () => {
    authService.estadoRostro.mockResolvedValue({ ...activo, debeRegistrarseDeNuevo: true });
    renderPerfil();
    await avanzar(10);

    expect(screen.getByText('Tu rostro debe registrarse de nuevo con las tres muestras')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /registrar mi rostro/i })).toBeInTheDocument();
  });

  it('avisa si este equipo no está autorizado y permite autorizarlo con la contraseña', async () => {
    authService.estadoRostro.mockResolvedValue(activo);
    authService.autorizarEquipo.mockResolvedValue({ tokenEquipo: 'nuevo-token' });
    renderPerfil();
    await avanzar(10);

    expect(screen.getByText(/este equipo no está autorizado/i)).toBeInTheDocument();
    escribirPassword();
    fireEvent.click(screen.getByRole('button', { name: /autorizar este equipo/i }));
    await avanzar(10);

    expect(leerTokenEquipo(EMAIL)).toBe('nuevo-token');
    expect(authService.autorizarEquipo).toHaveBeenCalledWith('mi-password');
  });

  it('desactiva tras confirmar y borra el token local', async () => {
    guardarTokenEquipo(EMAIL, 'token-de-equipo');
    authService.estadoRostro.mockResolvedValueOnce(activo).mockResolvedValue(inactivo);
    authService.desactivarRostro.mockResolvedValue();
    renderPerfil();
    await avanzar(10);

    fireEvent.click(screen.getByRole('button', { name: /desactivar y borrar mis datos/i }));
    expect(authService.desactivarRostro).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: /^desactivar y borrar$/i }));
    await avanzar(10);

    expect(authService.desactivarRostro).toHaveBeenCalled();
    expect(leerTokenEquipo(EMAIL)).toBeNull();
  });

  it('informa si la función no está disponible', async () => {
    authService.estadoRostro.mockResolvedValue({ ...inactivo, disponible: false });
    renderPerfil();
    await avanzar(10);

    expect(screen.getByText(/no está disponible/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /registrar mi rostro/i })).not.toBeInTheDocument();
  });
});
