import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { auditoria } from '../api/auditoria';
import { ErrorApi } from '../api/cliente';
import { empleados } from '../api/pedidos';
import { ProveedorAvisos } from '../avisos/Avisos';
import Auditoria, { ConsentimientosAuditoria } from '../paginas/auditoria/Auditoria';
import { detalleLegible, nombreUsuario } from '../paginas/auditoria/reglasAuditoria';

vi.mock('../api/authService', () => ({ COOKIE_CSRF: 'synctime_csrf', default: { renovar: vi.fn(), salir: vi.fn() } }));
vi.mock('../api/auditoria', () => ({ auditoria: { listar: vi.fn(), consentimientos: vi.fn() } }));
vi.mock('../api/pedidos', () => ({ empleados: { listar: vi.fn() } }));

const MODIFICO = {
  idAuditoria: 900, fecha: '2026-11-15T09:30:00', usuario: { tipo: 'empleado', id: 1, nombre: 'Marta Fuentes' }, rol: 'dueño',
  entidad: 'Pedido', entidadTexto: 'Pedido', idEntidad: '7', accion: 'modificar', accionTexto: 'Modificó',
  detalle: { campos: [{ campo: 'estadoPedido', antes: 'aceptado', despues: 'en_proceso' }, { campo: 'direccionInstalacion' }] }, ip: '10.0.0.1',
};
const FALLO = {
  idAuditoria: 899, fecha: '2026-11-15T09:00:00', usuario: { tipo: 'sistema', id: null, nombre: null }, rol: 'sistema',
  entidad: 'CorreoSaliente', entidadTexto: 'Correo', idEntidad: '51', accion: 'correo_fallido', accionTexto: 'Correo no enviado',
  detalle: { tipo: 'aviso_solicitud_nueva', referencia: 'solicitud:123', destinoEnmascarado: 'c•••o@gmail.com', intentos: 6, motivo: 'rechazado' }, ip: null,
};
const pagina = (elementos, extra = {}) => ({ elementos, total: elementos.length, pagina: 1, tamano: 20, totalPaginas: 1, ...extra });

function dibujar(entrada, elemento) {
  return render(
    <ProveedorAvisos>
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          <Route path="/panel/auditoria" element={elemento} />
          <Route path="/panel/auditoria/consentimientos" element={elemento} />
        </Routes>
      </MemoryRouter>
    </ProveedorAvisos>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  empleados.listar.mockResolvedValue([{ idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', rol: 'dueño' }]);
});

describe('reglas de la auditoría', () => {
  it('el detalle se lee como campos cambiados, con antes y después solo si vienen', () => {
    const { cambios, otros } = detalleLegible(MODIFICO.detalle);
    expect(cambios).toEqual([
      { campo: 'estadoPedido', etiqueta: 'Estado del pedido', antes: 'aceptado', despues: 'en_proceso', conValores: true },
      { campo: 'direccionInstalacion', etiqueta: 'Dirección de instalación', antes: null, despues: null, conValores: false },
    ]);
    expect(otros).toEqual([]);
  });

  it('acepta también una lista de nombres o un objeto de cambios, y lo demás como datos sueltos', () => {
    expect(detalleLegible({ campos: ['monto', 'activo'] }).cambios.map((c) => [c.etiqueta, c.conValores])).toEqual([['Monto', false], ['Activo', false]]);
    expect(detalleLegible({ cambios: { activo: { antes: true, despues: false } } }).cambios[0]).toMatchObject({ antes: 'Sí', despues: 'No', conValores: true });
    const { otros } = detalleLegible(FALLO.detalle);
    expect(otros).toContainEqual({ etiqueta: 'Destino', valor: 'c•••o@gmail.com' });
    expect(otros).toContainEqual({ etiqueta: 'Intentos', valor: '6' });
    expect(detalleLegible(null)).toEqual({ cambios: [], otros: [] });
    expect(detalleLegible('texto')).toEqual({ cambios: [], otros: [{ etiqueta: 'Detalle', valor: 'texto' }] });
  });

  it('nombra al usuario, al sistema o al cliente', () => {
    expect(nombreUsuario(MODIFICO.usuario)).toBe('Marta Fuentes');
    expect(nombreUsuario(FALLO.usuario)).toBe('Sistema');
    expect(nombreUsuario({ tipo: 'cliente', id: 9, nombre: null })).toBe('Cliente #9');
  });
});

describe('/panel/auditoria', () => {
  it('muestra la bitácora con quién, qué y el detalle legible', async () => {
    auditoria.listar.mockResolvedValue(pagina([MODIFICO, FALLO]));
    dibujar('/panel/auditoria', <Auditoria />);
    const fila = (await screen.findByRole('rowheader', { name: /15-11-2026 09:30/ })).closest('tr');
    expect(within(fila).getByText('Marta Fuentes')).toBeInTheDocument();
    expect(within(fila).getByText('Modificó')).toBeInTheDocument();
    expect(within(fila).getByText('Pedido #7')).toBeInTheDocument();
    const detalle = within(fila).getByText('Ver detalle').closest('details');
    expect(within(detalle).getByText('Estado del pedido')).toBeInTheDocument();
    expect(within(detalle).getByText('aceptado')).toBeInTheDocument();
    expect(within(detalle).getByText('en_proceso')).toBeInTheDocument();
    expect(within(detalle).getByText('cambió')).toBeInTheDocument();
    const sistema = screen.getByRole('rowheader', { name: /15-11-2026 09:00/ }).closest('tr');
    expect(within(sistema).getByText('Sistema')).toBeInTheDocument();
    expect(within(sistema).getByText('c•••o@gmail.com')).toBeInTheDocument();
  });

  it('los filtros y la página viajan a la API', async () => {
    auditoria.listar.mockResolvedValue(pagina([MODIFICO], { total: 30, totalPaginas: 2 }));
    dibujar('/panel/auditoria', <Auditoria />);
    await screen.findByRole('rowheader', { name: /09:30/ });
    fireEvent.change(screen.getByRole('combobox', { name: 'Acción' }), { target: { value: 'eliminar' } });
    await waitFor(() => expect(auditoria.listar).toHaveBeenLastCalledWith(expect.objectContaining({ accion: 'eliminar', pagina: 1 }), expect.anything()));
    fireEvent.change(screen.getByRole('combobox', { name: 'Entidad' }), { target: { value: 'Pago' } });
    await waitFor(() => expect(auditoria.listar).toHaveBeenLastCalledWith(expect.objectContaining({ entidad: 'Pago', accion: 'eliminar' }), expect.anything()));
    const usuario = screen.getByRole('combobox', { name: 'Usuario' });
    await waitFor(() => expect(within(usuario).getByRole('option', { name: 'Marta Fuentes' })).toBeInTheDocument());
    fireEvent.change(usuario, { target: { value: '1' } });
    await waitFor(() => expect(auditoria.listar).toHaveBeenLastCalledWith(expect.objectContaining({ idEmpleado: '1' }), expect.anything()));
    fireEvent.click(await screen.findByRole('button', { name: 'Página 2' }));
    await waitFor(() => expect(auditoria.listar).toHaveBeenLastCalledWith(expect.objectContaining({ pagina: 2 }), expect.anything()));
  });

  it('vacío y error', async () => {
    auditoria.listar.mockResolvedValue(pagina([]));
    const { unmount } = dibujar('/panel/auditoria', <Auditoria />);
    expect(await screen.findByText('Aún no hay registros en la bitácora')).toBeInTheDocument();
    unmount();
    auditoria.listar.mockRejectedValue(new ErrorApi({ estado: 500, mensaje: 'El servidor tuvo un problema.' }));
    dibujar('/panel/auditoria', <Auditoria />);
    expect(await screen.findByText('No pudimos cargar la bitácora')).toBeInTheDocument();
  });

  it('las dos vistas se enlazan como pestañas, con la actual marcada', async () => {
    auditoria.listar.mockResolvedValue(pagina([]));
    dibujar('/panel/auditoria', <Auditoria />);
    const vistas = screen.getByRole('navigation', { name: 'Vistas de la auditoría' });
    expect(within(vistas).getByRole('link', { name: 'Bitácora' })).toHaveAttribute('aria-current', 'page');
    expect(within(vistas).getByRole('link', { name: 'Consentimientos' })).toHaveAttribute('href', '/panel/auditoria/consentimientos');
  });
});

describe('/panel/auditoria/consentimientos', () => {
  it('muestra quién aceptó o retiró, qué versión y cuándo, con filtro por tipo', async () => {
    auditoria.consentimientos.mockResolvedValue(pagina([
      { titular: { tipo: 'cliente', id: 9, nombre: 'Rosa Pino' }, tipo: 'marketing', version: '1.0', acepta: true, fecha: '2026-11-10T12:00:00' },
      { titular: { tipo: 'cliente', id: 9, nombre: 'Rosa Pino' }, tipo: 'avisos_estado', version: '1.0', acepta: false, fecha: '2026-11-12T12:00:00' },
    ]));
    dibujar('/panel/auditoria/consentimientos', <ConsentimientosAuditoria />);
    const fila = (await screen.findByRole('rowheader', { name: /10-11-2026 12:00/ })).closest('tr');
    expect(within(fila).getByText('Rosa Pino')).toBeInTheDocument();
    expect(within(fila).getByText('Novedades y promociones')).toBeInTheDocument();
    expect(within(fila).getByText('Aceptó')).toBeInTheDocument();
    const retiro = screen.getByRole('rowheader', { name: /12-11-2026 12:00/ }).closest('tr');
    expect(within(retiro).getByText('Retiró')).toBeInTheDocument();
    expect(within(retiro).getByText('Avisos de estado')).toBeInTheDocument();
    fireEvent.change(screen.getByRole('combobox', { name: 'Tipo' }), { target: { value: 'privacidad' } });
    await waitFor(() => expect(auditoria.consentimientos).toHaveBeenLastCalledWith(expect.objectContaining({ tipo: 'privacidad' }), expect.anything()));
    expect(within(screen.getByRole('navigation', { name: 'Vistas de la auditoría' })).getByRole('link', { name: 'Consentimientos' }))
      .toHaveAttribute('aria-current', 'page');
  });
});

describe('detalle con el formato de la API (campos + valores)', () => {
  it('une los valores permitidos a sus campos y deja el resto como contexto', async () => {
    const { detalleLegible } = await import('../paginas/auditoria/reglasAuditoria');
    const r = detalleLegible({
      campos: ['estado', 'passwordHash', 'observacion'],
      valores: { estado: { antes: 'solicitado', despues: 'cotizado' }, passwordHash: { antes: 'x', despues: 'y' }, monto: { antes: null, despues: 1000 } },
    });
    const estado = r.cambios.find((c) => c.campo === 'estado' || c.clave === 'estado' || String(c.etiqueta ?? '').toLowerCase().includes('estado'));
    expect(JSON.stringify(estado)).toContain('cotizado');
    expect(JSON.stringify(r)).not.toContain('"y"');
    expect(r.otros.some((o) => String(o.valor).includes('1000') || String(o.valor).includes('1.000'))).toBe(true);
    expect(r.otros.some((o) => o.etiqueta === 'Valores')).toBe(false);
  });
});
