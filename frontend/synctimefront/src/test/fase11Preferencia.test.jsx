import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { ErrorApi } from '../api/cliente';
import { avisosTaller } from '../api/avisosTaller';
import { ProveedorAvisos } from '../avisos/Avisos';
import { SeccionAvisosTaller } from '../paginas/avisosTaller/SeccionAvisosTaller';

vi.mock('../api/authService', () => ({ COOKIE_CSRF: 'synctime_csrf', default: { renovar: vi.fn(), salir: vi.fn() } }));
vi.mock('../api/avisosTaller', () => ({
  avisosTaller: {
    obtener: vi.fn(), agregar: vi.fn(), reenviar: vi.fn(), cancelar: vi.fn(), quitar: vi.fn(), confirmar: vi.fn(), probar: vi.fn(), estadoPrueba: vi.fn(),
    preferencia: vi.fn(),
  },
}));

const ACTIVO = {
  idDestinatario: 1, correo: 'contacto@gmail.com', estado: 'activo', reemplazaA: null, fechaSolicitudChile: '2026-11-01T10:00:00',
  fechaConfirmacionChile: '2026-11-01T10:05:00', venceChile: null, recibeEnLinea: true, recibePresenciales: false, rowVersion: 'AAAAAAAAPR1=',
};
const datos = (destino = ACTIVO) => ({ destinos: [destino], usaRespaldo: false, historial: [] });

function abrir() {
  render(<ProveedorAvisos><SeccionAvisosTaller /></ProveedorAvisos>);
  return screen.findByRole('group', { name: 'Avisos que recibe contacto@gmail.com' });
}

beforeEach(() => {
  vi.clearAllMocks();
  avisosTaller.obtener.mockResolvedValue(datos());
});

describe('preferencia de avisos por destino', () => {
  it('muestra las dos casillas con lo guardado', async () => {
    const grupo = await abrir();
    expect(within(grupo).getByRole('checkbox', { name: 'Solicitudes en línea' })).toBeChecked();
    expect(within(grupo).getByRole('checkbox', { name: 'Solicitudes presenciales' })).not.toBeChecked();
  });

  it('con las dos apagadas muestra el error junto al grupo y no envía', async () => {
    const grupo = await abrir();
    fireEvent.click(within(grupo).getByRole('checkbox', { name: 'Solicitudes en línea' }));
    fireEvent.click(within(grupo).getByRole('button', { name: 'Guardar preferencia' }));
    expect(await within(grupo).findByText('Marca al menos un tipo de solicitud.')).toBeInTheDocument();
    expect(within(grupo).getByRole('checkbox', { name: 'Solicitudes en línea' })).toHaveAccessibleDescription(/al menos un tipo/);
    expect(avisosTaller.preferencia).not.toHaveBeenCalled();
  });

  it('guarda con la versión vista (If-Match) y lo anuncia', async () => {
    avisosTaller.preferencia.mockResolvedValue({ ...ACTIVO, recibePresenciales: true, rowVersion: 'AAAAAAAAPR2=' });
    const grupo = await abrir();
    fireEvent.click(within(grupo).getByRole('checkbox', { name: 'Solicitudes presenciales' }));
    fireEvent.click(within(grupo).getByRole('button', { name: 'Guardar preferencia' }));
    await waitFor(() => expect(avisosTaller.preferencia).toHaveBeenCalledWith(1, { recibeEnLinea: true, recibePresenciales: true }, 'AAAAAAAAPR1='));
    expect(await screen.findByText('Preferencia guardada')).toBeInTheDocument();
  });

  it('un 409 conserva lo marcado y ofrece guardar sobre la versión actual', async () => {
    avisosTaller.preferencia
      .mockRejectedValueOnce(new ErrorApi({ estado: 409, codigo: 'version_obsoleta', mensaje: 'Otra persona cambió este registro.' }))
      .mockResolvedValueOnce({ ...ACTIVO, recibePresenciales: true, rowVersion: 'AAAAAAAAPR3=' });
    const grupo = await abrir();
    avisosTaller.obtener.mockResolvedValue(datos({ ...ACTIVO, recibeEnLinea: false, recibePresenciales: true, rowVersion: 'AAAAAAAAPR2=' }));
    fireEvent.click(within(grupo).getByRole('checkbox', { name: 'Solicitudes presenciales' }));
    fireEvent.click(within(grupo).getByRole('button', { name: 'Guardar preferencia' }));
    const sobre = await screen.findByRole('button', { name: 'Guardar sobre la versión actual' });
    expect(screen.getByText(/Otra persona modificó este destino/)).toBeInTheDocument();
    expect(within(grupo).getByRole('checkbox', { name: 'Solicitudes en línea' })).toBeChecked();
    await waitFor(() => expect(sobre).toHaveFocus());
    fireEvent.click(sobre);
    await waitFor(() => expect(avisosTaller.preferencia).toHaveBeenLastCalledWith(1, { recibeEnLinea: true, recibePresenciales: true }, 'AAAAAAAAPR2='));
  });
});
