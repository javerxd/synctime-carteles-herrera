import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import authService from '../api/authService';
import { ErrorApi } from '../api/cliente';
import { cuenta } from '../api/cuenta';
import { publico } from '../api/publico';
import { ProveedorAvisos } from '../avisos/Avisos';
import { ComprobanteBaja } from '../paginas/cuenta/ComprobanteBaja';
import { CorreosQueRecibo } from '../paginas/cuenta/CorreosQueRecibo';
import { MisDatos } from '../paginas/cuenta/MisDatos';
import { nombreArchivoDatos } from '../paginas/cuenta/reglasCuenta';
import { almacenSesion } from '../sesion/sesion';

vi.mock('../api/authService', () => ({ COOKIE_CSRF: 'synctime_csrf', default: { renovar: vi.fn(), salir: vi.fn() } }));
vi.mock('../api/cuenta', () => ({ cuenta: { preferencias: vi.fn(), descargarDatos: vi.fn(), darDeBaja: vi.fn() } }));
vi.mock('../api/publico', () => ({ publico: { documentosLegales: vi.fn() } }));

const DOCUMENTOS = [{ tipo: 'privacidad', version: 'p-1' }, { tipo: 'marketing', version: 'm-1' }];
const CUENTA = { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', correo: 'rosa@correo.test', correoVerificado: true, avisosEstado: true, marketing: false };

function Ruta() {
  const { pathname, state } = useLocation();
  return <p data-testid="ruta" data-estado={JSON.stringify(state ?? null)}>{pathname}</p>;
}

function dibujar(elemento, entrada = '/cuenta') {
  return render(
    <ProveedorAvisos>
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          <Route path="/cuenta" element={<>{elemento}<Ruta /></>} />
          <Route path="/" element={<><ComprobanteBaja /><Ruta /></>} />
        </Routes>
      </MemoryRouter>
    </ProveedorAvisos>,
  );
}

const escribir = (control, valor) => fireEvent.change(control, { target: { value: valor } });

beforeEach(() => {
  vi.clearAllMocks();
  publico.documentosLegales.mockResolvedValue(DOCUMENTOS);
  authService.salir.mockResolvedValue(undefined);
  act(() => almacenSesion.aplicar({ token: 'jwt', tipo: 'cliente', usuario: { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', email: 'rosa@correo.test', correoVerificado: true } }));
});
afterEach(() => act(() => almacenSesion.cerrar()));

describe('Mi cuenta → Correos que recibo', () => {
  it('muestra cada correo opcional y que los de seguridad siempre llegan', () => {
    dibujar(<CorreosQueRecibo datosCuenta={CUENTA} alGuardar={vi.fn()} />);
    expect(screen.getByRole('heading', { name: 'Correos que recibo' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /estado de mis solicitudes y pedidos/ })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: /novedades y promociones/i })).not.toBeChecked();
    expect(screen.getByText('Los correos de seguridad de tu cuenta siempre se envían.')).toBeInTheDocument();
  });

  it('apagar los avisos de estado guarda las dos preferencias juntas', async () => {
    const alGuardar = vi.fn();
    cuenta.preferencias.mockResolvedValue({ avisosEstado: false, marketing: false });
    dibujar(<CorreosQueRecibo datosCuenta={CUENTA} alGuardar={alGuardar} />);
    fireEvent.click(screen.getByRole('checkbox', { name: /estado de mis solicitudes y pedidos/ }));
    await waitFor(() => expect(cuenta.preferencias).toHaveBeenCalledWith({ avisosEstado: false, marketing: false }));
    expect(await screen.findByText(/ya no te enviaremos avisos del estado/i)).toBeInTheDocument();
    expect(alGuardar).toHaveBeenCalled();
  });

  it('aceptar novedades envía la versión vigente del texto', async () => {
    cuenta.preferencias.mockResolvedValue({ avisosEstado: true, marketing: true });
    dibujar(<CorreosQueRecibo datosCuenta={CUENTA} alGuardar={vi.fn()} />);
    await waitFor(() => expect(publico.documentosLegales).toHaveBeenCalled());
    await act(async () => {});
    fireEvent.click(screen.getByRole('checkbox', { name: /novedades y promociones/i }));
    await waitFor(() => expect(cuenta.preferencias).toHaveBeenCalledWith({ avisosEstado: true, marketing: true, versionMarketing: 'm-1' }));
  });

  it('si falla, lo dice y la casilla vuelve a estar disponible', async () => {
    cuenta.preferencias.mockRejectedValue(new ErrorApi({ estado: 0, codigo: 'sin_conexion', mensaje: 'No pudimos conectar con el servidor.' }));
    dibujar(<CorreosQueRecibo datosCuenta={CUENTA} alGuardar={vi.fn()} />);
    const casilla = screen.getByRole('checkbox', { name: /estado de mis solicitudes y pedidos/ });
    fireEvent.click(casilla);
    expect(await screen.findByText('No pudimos conectar con el servidor.')).toBeInTheDocument();
    expect(casilla).toBeEnabled();
    expect(casilla).toBeChecked();
  });
});

describe('Mi cuenta → Mis datos', () => {
  it('el archivo se nombra con la fecha', () => {
    expect(nombreArchivoDatos(new Date(2026, 10, 5))).toBe('mis-datos-letreros-herrera-20261105.json');
  });

  it('descargar pide la contraseña antes de llamar a la API', async () => {
    dibujar(<MisDatos />);
    fireEvent.click(screen.getByRole('button', { name: 'Descargar mis datos' }));
    expect(await screen.findByText('Escribe tu contraseña para descargar tus datos.')).toBeInTheDocument();
    expect(cuenta.descargarDatos).not.toHaveBeenCalled();
  });

  it('con la contraseña guarda el archivo JSON y la borra del campo', async () => {
    URL.createObjectURL = vi.fn(() => 'blob:datos');
    URL.revokeObjectURL = vi.fn();
    const clic = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    cuenta.descargarDatos.mockResolvedValue(new Blob(['{}'], { type: 'application/json' }));
    dibujar(<MisDatos />);
    const seccion = screen.getByRole('region', { name: 'Mis datos' });
    const campo = within(seccion).getAllByLabelText(/Contraseña/)[0];
    escribir(campo, 'secreta123');
    fireEvent.click(screen.getByRole('button', { name: 'Descargar mis datos' }));
    await waitFor(() => expect(cuenta.descargarDatos).toHaveBeenCalledWith('secreta123'));
    await waitFor(() => expect(clic).toHaveBeenCalled());
    const enlace = clic.mock.contexts[0];
    expect(enlace.download).toMatch(/^mis-datos-letreros-herrera-\d{8}\.json$/);
    expect(enlace.href).toBe('blob:datos');
    expect(campo).toHaveValue('');
    expect(await screen.findByText(/Descargamos tus datos/)).toBeInTheDocument();
    clic.mockRestore();
  });

  it('una contraseña incorrecta se explica junto al campo', async () => {
    cuenta.descargarDatos.mockRejectedValue(new ErrorApi({ estado: 400, codigo: 'datos_invalidos', mensaje: 'La contraseña no es correcta.', errores: { password: ['La contraseña no es correcta.'] } }));
    dibujar(<MisDatos />);
    const campo = within(screen.getByRole('region', { name: 'Mis datos' })).getAllByLabelText(/Contraseña/)[0];
    escribir(campo, 'mala');
    fireEvent.click(screen.getByRole('button', { name: 'Descargar mis datos' }));
    await waitFor(() => expect(campo).toHaveAccessibleDescription(/La contraseña no es correcta/));
    await waitFor(() => expect(campo).toHaveFocus());
  });

  it('dar de baja explica qué se conserva y exige la contraseña y escribir DAR DE BAJA', async () => {
    dibujar(<MisDatos />);
    fireEvent.click(screen.getByRole('button', { name: 'Dar de baja mi cuenta' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Dar de baja mi cuenta' });
    expect(dialogo).toHaveTextContent(/se conservan tus solicitudes, cotizaciones, pedidos y abonos/i);
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Dar de baja' }));
    expect(await within(dialogo).findByText('Escribe tu contraseña para confirmar.')).toBeInTheDocument();
    escribir(within(dialogo).getByLabelText(/Contraseña/), 'secreta123');
    escribir(within(dialogo).getByLabelText(/Escribe DAR DE BAJA/), 'dar de baja');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Dar de baja' }));
    expect(await within(dialogo).findByText('Escribe DAR DE BAJA, en mayúsculas, para confirmar.')).toBeInTheDocument();
    expect(cuenta.darDeBaja).not.toHaveBeenCalled();
  });

  it('al dar de baja muestra el comprobante, cierra la sesión y lleva a la portada', async () => {
    cuenta.darDeBaja.mockResolvedValue({ comprobante: { numero: 'BAJA-9-20261115', fecha: '2026-11-15T10:00:00' } });
    dibujar(<MisDatos />);
    fireEvent.click(screen.getByRole('button', { name: 'Dar de baja mi cuenta' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Dar de baja mi cuenta' });
    escribir(within(dialogo).getByLabelText(/Contraseña/), 'secreta123');
    escribir(within(dialogo).getByLabelText(/Escribe DAR DE BAJA/), 'DAR DE BAJA');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Dar de baja' }));
    await waitFor(() => expect(cuenta.darDeBaja).toHaveBeenCalledWith({ password: 'secreta123', confirmacion: 'DAR DE BAJA' }));
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent(/^\/$/));
    await waitFor(() => expect(authService.salir).toHaveBeenCalled());
    await waitFor(() => expect(almacenSesion.obtener().usuario).toBeNull());
    const comprobante = await screen.findByRole('region', { name: 'Tu cuenta quedó dada de baja' });
    expect(comprobante).toHaveTextContent('BAJA-9-20261115');
    expect(comprobante).toHaveTextContent('15-11-2026');
  });

  it('una contraseña incorrecta al dar de baja no cierra el diálogo', async () => {
    cuenta.darDeBaja.mockRejectedValue(new ErrorApi({ estado: 400, codigo: 'datos_invalidos', mensaje: 'La contraseña no es correcta.' }));
    dibujar(<MisDatos />);
    fireEvent.click(screen.getByRole('button', { name: 'Dar de baja mi cuenta' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Dar de baja mi cuenta' });
    escribir(within(dialogo).getByLabelText(/Contraseña/), 'mala');
    escribir(within(dialogo).getByLabelText(/Escribe DAR DE BAJA/), 'DAR DE BAJA');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Dar de baja' }));
    expect(await within(dialogo).findByText('La contraseña no es correcta.')).toBeInTheDocument();
    expect(screen.getByTestId('ruta')).toHaveTextContent('/cuenta');
    expect(authService.salir).not.toHaveBeenCalled();
  });
});
