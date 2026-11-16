import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { ErrorApi } from '../api/cliente';
import { bajaCorreos, correos } from '../api/correos';
import { ProveedorAvisos } from '../avisos/Avisos';
import { AvisoCorreosNoEnviados } from '../paginas/correos/AvisoCorreosNoEnviados';
import BajaCorreos from '../paginas/correos/BajaCorreos';
import CorreosNoEnviados from '../paginas/correos/CorreosNoEnviados';
import { destinoDelReenvio, enlaceSeguro, esAvisoDelTaller } from '../paginas/correos/reglasCorreos';

vi.mock('../api/authService', () => ({ COOKIE_CSRF: 'synctime_csrf', default: { renovar: vi.fn(), salir: vi.fn() } }));
vi.mock('../api/correos', () => ({
  correos: { noEnviados: vi.fn(), resumen: vi.fn(), reenviar: vi.fn(), marcarRevisado: vi.fn() },
  bajaCorreos: { consultar: vi.fn(), confirmar: vi.fn() },
}));

const fila = (extra) => ({
  idCorreo: 51, tipo: 'aviso_solicitud_nueva', tipoTexto: 'Aviso de solicitud nueva', referencia: 'solicitud:123', enlace: '/panel/solicitudes/123',
  destinoEnmascarado: 'c•••o@gmail.com', intentos: 6, motivo: 'El servidor de correo no aceptó el envío', fechaCreacion: '2026-11-14T10:00:00',
  fechaFallo: '2026-11-14T10:30:00', revisado: false, reenviable: true, reenviadoComo: null, ...extra,
});
const AVISO = fila();
const AL_CLIENTE = fila({
  idCorreo: 52, tipo: 'estado_pedido', tipoTexto: 'Estado del pedido', referencia: 'pedido:7', enlace: '/panel/pedidos/7', destinoEnmascarado: 'r•••a@correo.test',
});
const PURGADO = fila({ idCorreo: 53, tipo: 'cotizacion_lista', tipoTexto: 'Cotización lista', referencia: 'cotizacion:45', enlace: null, reenviable: false, revisado: true });
const pagina = (elementos, extra = {}) => ({ elementos, total: elementos.length, pagina: 1, tamano: 20, totalPaginas: 1, ...extra });

function Ruta() {
  const { pathname, search } = useLocation();
  return <p data-testid="ruta">{pathname}{search}</p>;
}

function dibujar(entrada, ruta, elemento) {
  return render(
    <ProveedorAvisos>
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          <Route path={ruta} element={<>{elemento}<Ruta /></>} />
          <Route path="*" element={<Ruta />} />
        </Routes>
      </MemoryRouter>
    </ProveedorAvisos>,
  );
}

const abrirLista = (entrada = '/panel/correos-no-enviados') => dibujar(entrada, '/panel/correos-no-enviados', <CorreosNoEnviados />);
const filaDe = (texto) => screen.getByRole('rowheader', { name: new RegExp(texto) }).closest('tr');

beforeEach(() => {
  vi.clearAllMocks();
});

describe('reglas de los correos no enviados', () => {
  it('reconoce los avisos del taller y dice a quién irá el reenvío', () => {
    expect(esAvisoDelTaller('aviso_solicitud_nueva')).toBe(true);
    expect(esAvisoDelTaller('aviso_tope_solicitudes')).toBe(true);
    expect(esAvisoDelTaller('consulta_contacto')).toBe(true);
    expect(esAvisoDelTaller('estado_pedido')).toBe(false);
    expect(destinoDelReenvio(AVISO)).toMatch(/destinos activos de hoy/);
    expect(destinoDelReenvio(AL_CLIENTE)).toMatch(/misma dirección.*r•••a@correo\.test/);
  });

  it('solo acepta enlaces internos del panel', () => {
    expect(enlaceSeguro('/panel/solicitudes/123')).toBe('/panel/solicitudes/123');
    expect(enlaceSeguro('javascript:alert(1)')).toBeNull();
    expect(enlaceSeguro('https://otro.sitio/panel/x')).toBeNull();
    expect(enlaceSeguro('//otro.sitio/panel')).toBeNull();
    expect(enlaceSeguro(null)).toBeNull();
  });
});

describe('/panel/correos-no-enviados', () => {
  it('mientras carga lo anuncia; luego muestra cada correo con su referencia enlazada y el destino enmascarado', async () => {
    let resolver;
    correos.noEnviados.mockReturnValue(new Promise((r) => { resolver = r; }));
    abrirLista();
    expect(screen.getByText('Cargando correos no enviados…')).toBeInTheDocument();
    await act(async () => resolver(pagina([AVISO, AL_CLIENTE, PURGADO])));
    const aviso = filaDe('Aviso de solicitud nueva');
    expect(within(aviso).getByRole('link', { name: 'Solicitud #123' })).toHaveAttribute('href', '/panel/solicitudes/123');
    expect(within(aviso).getByText('c•••o@gmail.com')).toBeInTheDocument();
    expect(within(aviso).getByText('El servidor de correo no aceptó el envío')).toBeInTheDocument();
    expect(within(aviso).getByText('Sin revisar')).toBeInTheDocument();
    // Sin enlace, la referencia se lee como texto.
    expect(within(filaDe('Cotización lista')).getByText('Cotización #45')).toBeInTheDocument();
    expect(within(filaDe('Cotización lista')).queryByRole('link')).not.toBeInTheDocument();
  });

  it('sin correos fallidos muestra el estado vacío; con filtros, cómo quitarlos', async () => {
    correos.noEnviados.mockResolvedValue(pagina([]));
    const { unmount } = abrirLista();
    expect(await screen.findByText('No hay correos sin enviar')).toBeInTheDocument();
    unmount();
    abrirLista('/panel/correos-no-enviados?revisado=false');
    expect(await screen.findByText('Ningún correo coincide con estos filtros')).toBeInTheDocument();
  });

  it('un error al cargar se explica y deja reintentar', async () => {
    correos.noEnviados.mockRejectedValueOnce(new ErrorApi({ estado: 500, mensaje: 'El servidor tuvo un problema.' }));
    correos.noEnviados.mockResolvedValue(pagina([AVISO]));
    abrirLista();
    expect(await screen.findByText('No pudimos cargar los correos no enviados')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(await screen.findByRole('rowheader', { name: /Aviso de solicitud nueva/ })).toBeInTheDocument();
  });

  it('los filtros viajan a la API: tipo, fechas y «sin revisar»', async () => {
    correos.noEnviados.mockResolvedValue(pagina([AVISO]));
    abrirLista();
    await screen.findByRole('rowheader', { name: /Aviso de solicitud nueva/ });
    fireEvent.change(screen.getByRole('combobox', { name: 'Revisión' }), { target: { value: 'false' } });
    await waitFor(() => expect(correos.noEnviados).toHaveBeenLastCalledWith(expect.objectContaining({ revisado: 'false', pagina: 1 }), expect.anything()));
    fireEvent.change(screen.getByRole('combobox', { name: 'Tipo de correo' }), { target: { value: 'aviso_solicitud_nueva' } });
    await waitFor(() => expect(correos.noEnviados).toHaveBeenLastCalledWith(
      expect.objectContaining({ tipo: 'aviso_solicitud_nueva', revisado: 'false' }), expect.anything(),
    ));
    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2026-11-01' } });
    await waitFor(() => expect(correos.noEnviados).toHaveBeenLastCalledWith(expect.objectContaining({ desde: '2026-11-01' }), expect.anything()));
    expect(screen.getByTestId('ruta')).toHaveTextContent('revisado=false');
  });

  it('reenviar un aviso del taller pide confirmar y dice que irá a los destinos activos de hoy', async () => {
    correos.noEnviados.mockResolvedValue(pagina([AVISO, AL_CLIENTE]));
    correos.reenviar.mockResolvedValue({ idCorreoNuevo: 77 });
    abrirLista();
    const aviso = await screen.findByRole('rowheader', { name: /Aviso de solicitud nueva/ });
    fireEvent.click(within(aviso.closest('tr')).getByRole('button', { name: /^Reenviar/ }));
    const dialogo = await screen.findByRole('dialog', { name: 'Reenviar este correo' });
    expect(dialogo).toHaveTextContent(/destinos activos de hoy/);
    expect(correos.reenviar).not.toHaveBeenCalled();
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Reenviar correo' }));
    await waitFor(() => expect(correos.reenviar).toHaveBeenCalledWith(51));
    expect(await screen.findByText('Correo reenviado')).toBeInTheDocument();
    await waitFor(() => expect(correos.noEnviados).toHaveBeenCalledTimes(2));
  });

  it('reenviar un correo al cliente dice que irá a la misma dirección, enmascarada', async () => {
    correos.noEnviados.mockResolvedValue(pagina([AVISO, AL_CLIENTE]));
    abrirLista();
    const cliente = await screen.findByRole('rowheader', { name: /Estado del pedido/ });
    fireEvent.click(within(cliente.closest('tr')).getByRole('button', { name: /^Reenviar/ }));
    const dialogo = await screen.findByRole('dialog', { name: 'Reenviar este correo' });
    expect(dialogo).toHaveTextContent(/misma dirección/);
    expect(dialogo).toHaveTextContent('r•••a@correo.test');
    expect(dialogo).not.toHaveTextContent(/destinos activos/);
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Cancelar' }));
    expect(correos.reenviar).not.toHaveBeenCalled();
  });

  it('si ya se reenvió (409), lo dice y recarga el listado', async () => {
    correos.noEnviados.mockResolvedValue(pagina([AVISO]));
    correos.reenviar.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'ya_reenviado', mensaje: 'Otra persona cambió este registro. Recarga para ver la última versión.' }));
    abrirLista();
    const aviso = await screen.findByRole('rowheader', { name: /Aviso de solicitud nueva/ });
    fireEvent.click(within(aviso.closest('tr')).getByRole('button', { name: /^Reenviar/ }));
    const dialogo = await screen.findByRole('dialog', { name: 'Reenviar este correo' });
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Reenviar correo' }));
    expect(await within(dialogo).findByText(/Este correo ya se reenvió/)).toBeInTheDocument();
    await waitFor(() => expect(correos.noEnviados).toHaveBeenCalledTimes(2));
  });

  it('un correo que ya no se puede reenviar explica por qué y no abre la confirmación', async () => {
    correos.noEnviados.mockResolvedValue(pagina([PURGADO]));
    abrirLista();
    const purgado = (await screen.findByRole('rowheader', { name: /Cotización lista/ })).closest('tr');
    const boton = within(purgado).getByRole('button', { name: /^Reenviar/ });
    expect(boton).toHaveAttribute('aria-disabled', 'true');
    expect(boton).toHaveAccessibleDescription(/ya no se puede reenviar/i);
    fireEvent.click(boton);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('«Marcar como revisado» lo marca sin reenviar y recarga', async () => {
    correos.noEnviados.mockResolvedValue(pagina([AVISO]));
    correos.marcarRevisado.mockResolvedValue(undefined);
    abrirLista();
    const aviso = (await screen.findByRole('rowheader', { name: /Aviso de solicitud nueva/ })).closest('tr');
    fireEvent.click(within(aviso).getByRole('button', { name: /Marcar como revisado/ }));
    await waitFor(() => expect(correos.marcarRevisado).toHaveBeenCalledWith(51));
    expect(correos.reenviar).not.toHaveBeenCalled();
    await waitFor(() => expect(correos.noEnviados).toHaveBeenCalledTimes(2));
  });
});

describe('aviso en el inicio del panel', () => {
  const abrirAviso = () => dibujar('/panel', '/panel', <AvisoCorreosNoEnviados />);

  it('con correos sin revisar avisa cuántos y enlaza a la vista filtrada', async () => {
    correos.resumen.mockResolvedValue({ sinRevisar: 3 });
    abrirAviso();
    const enlace = await screen.findByRole('link', { name: /Ver los correos no enviados/ });
    expect(enlace).toHaveAttribute('href', '/panel/correos-no-enviados?revisado=false');
    expect(screen.getByText('3 correos no se pudieron enviar')).toBeInTheDocument();
  });

  it('uno solo, en singular; ninguno, no muestra nada', async () => {
    correos.resumen.mockResolvedValue({ sinRevisar: 1 });
    const { unmount } = abrirAviso();
    expect(await screen.findByText('1 correo no se pudo enviar')).toBeInTheDocument();
    unmount();
    correos.resumen.mockResolvedValue({ sinRevisar: 0 });
    abrirAviso();
    await waitFor(() => expect(correos.resumen).toHaveBeenCalledTimes(2));
    expect(screen.queryByText(/no se pud/)).not.toBeInTheDocument();
  });
});

describe('/baja (enlace de baja del correo)', () => {
  const abrirBaja = (entrada) => dibujar(entrada, '/baja', <BajaCorreos />);
  const ACTIVAS = { correoEnmascarado: 'r•••a@correo.test', avisosEstado: true, marketing: true };

  it('al cargar solo consulta: no da de baja, saca el token de la dirección y no lo filtra en el Referer', async () => {
    bajaCorreos.consultar.mockResolvedValue(ACTIVAS);
    abrirBaja('/baja?t=tok-123');
    expect(await screen.findByText('r•••a@correo.test')).toBeInTheDocument();
    expect(bajaCorreos.consultar).toHaveBeenCalledWith('tok-123');
    expect(bajaCorreos.confirmar).not.toHaveBeenCalled();
    expect(screen.getByTestId('ruta')).toHaveTextContent(/^\/baja$/);
    expect(document.head.querySelector('meta[name="referrer"][content="no-referrer"]')).not.toBeNull();
  });

  it('«Confirmar baja» apaga lo marcado y muestra el resultado', async () => {
    bajaCorreos.consultar.mockResolvedValue(ACTIVAS);
    bajaCorreos.confirmar.mockResolvedValue({ avisosEstado: false, marketing: false });
    abrirBaja('/baja?t=tok-123');
    await screen.findByText('r•••a@correo.test');
    expect(screen.getByRole('checkbox', { name: /estado de tus solicitudes y pedidos/ })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: /novedades y promociones/i })).toBeChecked();
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar baja' }));
    await waitFor(() => expect(bajaCorreos.confirmar).toHaveBeenCalledWith({ t: 'tok-123', avisosEstado: false, marketing: false }));
    expect(await screen.findByRole('heading', { level: 1, name: 'Listo, te diste de baja' })).toBeInTheDocument();
  });

  it('se puede elegir solo uno', async () => {
    bajaCorreos.consultar.mockResolvedValue(ACTIVAS);
    bajaCorreos.confirmar.mockResolvedValue({ avisosEstado: false, marketing: true });
    abrirBaja('/baja?t=tok-123');
    await screen.findByText('r•••a@correo.test');
    fireEvent.click(screen.getByRole('checkbox', { name: /novedades y promociones/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar baja' }));
    await waitFor(() => expect(bajaCorreos.confirmar).toHaveBeenCalledWith({ t: 'tok-123', avisosEstado: false }));
    expect(await screen.findByText(/Seguirás recibiendo las novedades y promociones/)).toBeInTheDocument();
  });

  it('sin nada marcado no envía y lo explica junto a las casillas', async () => {
    bajaCorreos.consultar.mockResolvedValue(ACTIVAS);
    abrirBaja('/baja?t=tok-123');
    await screen.findByText('r•••a@correo.test');
    fireEvent.click(screen.getByRole('checkbox', { name: /estado de tus solicitudes y pedidos/ }));
    fireEvent.click(screen.getByRole('checkbox', { name: /novedades y promociones/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar baja' }));
    expect(await screen.findByText('Marca al menos un tipo de correo.')).toBeInTheDocument();
    expect(bajaCorreos.confirmar).not.toHaveBeenCalled();
  });

  it('un enlace inválido (404) lo dice sin dar detalles y no ofrece confirmar', async () => {
    bajaCorreos.consultar.mockRejectedValue(new ErrorApi({ estado: 404, mensaje: 'No encontramos lo que buscas.' }));
    abrirBaja('/baja?t=alterado');
    expect(await screen.findByRole('heading', { level: 1, name: 'Este enlace no sirve' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Confirmar baja' })).not.toBeInTheDocument();
  });

  it('sin token dice que el enlace está incompleto y no consulta', async () => {
    abrirBaja('/baja');
    expect(await screen.findByRole('heading', { level: 1, name: 'Este enlace no sirve' })).toBeInTheDocument();
    expect(screen.getByText(/incompleto/)).toBeInTheDocument();
    expect(bajaCorreos.consultar).not.toHaveBeenCalled();
  });

  it('si ya no recibe correos opcionales, lo dice sin botón', async () => {
    bajaCorreos.consultar.mockResolvedValue({ correoEnmascarado: 'r•••a@correo.test', avisosEstado: false, marketing: false });
    abrirBaja('/baja?t=tok-123');
    expect(await screen.findByText(/Ya no recibes correos opcionales/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Confirmar baja' })).not.toBeInTheDocument();
  });
});
