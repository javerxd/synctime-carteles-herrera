import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import authService from '../api/authService';
import { ErrorApi } from '../api/cliente';
import { cuenta } from '../api/cuenta';
import { publico } from '../api/publico';
import { misSolicitudes, solicitudes } from '../api/solicitudes';
import { ProveedorAvisos } from '../avisos/Avisos';
import { destinoSolicitud } from '../estructuras/navegacion';
import NuevaSolicitud from '../paginas/solicitudes/NuevaSolicitud';
import BandejaSolicitudes from '../paginas/solicitudes/panel/BandejaSolicitudes';
import FichaSolicitud from '../paginas/solicitudes/panel/FichaSolicitud';
import { parametrosApi } from '../paginas/solicitudes/panel/reglasBandeja';
import {
  areaItem, detallesParaApi, erroresDesdeApi, itemVacio, validarSolicitud,
} from '../paginas/solicitudes/reglasSolicitud';
import { almacenSesion } from '../sesion/sesion';

vi.mock('../api/authService', () => ({
  COOKIE_CSRF: 'synctime_csrf',
  default: { reenviarVerificacion: vi.fn(), renovar: vi.fn(), salir: vi.fn() },
}));
vi.mock('../api/cuenta', () => ({ cuenta: { obtener: vi.fn() } }));
vi.mock('../api/publico', () => ({ publico: { productos: vi.fn(), estadoLocal: vi.fn() } }));
vi.mock('../api/solicitudes', () => ({
  misSolicitudes: { subirAdjunto: vi.fn(), quitarAdjunto: vi.fn(), enviar: vi.fn(), borradores: vi.fn() },
  adjuntos: { miniatura: vi.fn(), archivo: vi.fn() },
  solicitudes: {
    listar: vi.fn(), obtener: vi.fn(), editar: vi.fn(), cambiarEstado: vi.fn(), eliminar: vi.fn(), subirAdjunto: vi.fn(), quitarAdjunto: vi.fn(),
  },
  clientes: {},
}));

const PRODUCTOS = [{ idProducto: 1, nombre: 'Letrero luminoso' }, { idProducto: 2, nombre: 'Letras corpóreas' }];
const MI_CUENTA = { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', correo: 'rosa@correo.test', correoVerificado: true };
const COMPROBANTE = {
  idSolicitud: 123, numero: '#123', fechaChile: '2026-11-14T10:32:00', origen: 'online', estado: 'solicitado',
  cliente: { nombre: 'Rosa', apellido: 'Pino', telefono: '912345678', correo: 'rosa@correo.test' },
  detalles: [{ idProducto: 2, producto: 'Letras corpóreas', descripcion: null, anchoMm: 1200, altoMm: 600, cantidad: 2, areaM2: 1.44 }],
  areaTotalM2: 1.44, descripcionGeneral: 'Letras para la fachada del local', rowVersion: 'AAAAAAAAB9E=',
  adjuntos: [{ idAdjunto: 7, tipo: 'pdf', nombre: 'logo.pdf', bytes: 2048, urlMiniatura: null }],
};

/** Muestra la ruta a la que se llegó (para seguir redirecciones). */
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

const escribir = (control, valor) => fireEvent.change(control, { target: { value: valor } });

beforeEach(() => {
  vi.clearAllMocks();
  URL.createObjectURL = vi.fn(() => 'blob:prueba');
  URL.revokeObjectURL = vi.fn();
  publico.productos.mockResolvedValue(PRODUCTOS);
  publico.estadoLocal.mockResolvedValue({
    abiertoAhora: true, motivo: 'abierto', hoy: { abierto: true, horaInicio: '09:00:00', horaFin: '18:30:00' }, cartel: { mensajeEstado: 'Atendiendo' },
  });
  cuenta.obtener.mockResolvedValue(MI_CUENTA);
  misSolicitudes.borradores.mockResolvedValue([]);
  sessionStorage.clear();
  adjuntosMiniatura();
});

function adjuntosMiniatura() {
  return import('../api/solicitudes').then(({ adjuntos }) => adjuntos.miniatura.mockResolvedValue(new Blob(['x'])));
}

describe('reglas de la solicitud', () => {
  it('calcula el área en m² con las medidas en cm y la cantidad', () => {
    expect(areaItem({ ancho: '120', alto: '60', cantidad: '2' })).toBe(1.44);
    expect(areaItem({ ancho: '120,5', alto: '100', cantidad: '1' })).toBe(1.21);
    expect(areaItem({ ancho: '', alto: '60', cantidad: '1' })).toBeNull();
  });

  it('convierte los centímetros a milímetros y deja sin medidas si faltan las dos', () => {
    expect(detallesParaApi([{ ...itemVacio(3), ancho: '120,5', alto: '60', cantidad: '2', descripcion: ' Rojo ' }]))
      .toEqual([{ idProducto: 3, descripcion: 'Rojo', anchoMm: 1205, altoMm: 600, cantidad: 2 }]);
    expect(detallesParaApi([{ ...itemVacio(3), cantidad: '1' }])[0]).toMatchObject({ anchoMm: null, altoMm: null, descripcion: null });
  });

  it('exige el tipo, ambas medidas o ninguna, la cantidad de 1 a 999 y 10 letras de descripción', () => {
    const errores = validarSolicitud({ items: [{ ...itemVacio(), ancho: '100', cantidad: '0' }], descripcionGeneral: 'Hola' });
    expect(Object.keys(errores).sort()).toEqual(['descripcionGeneral', 'detalles.0.alto', 'detalles.0.cantidad', 'detalles.0.idProducto']);
    expect(validarSolicitud({ items: [{ ...itemVacio(1), ancho: '6000', alto: '10', cantidad: '1' }], descripcionGeneral: 'Un letrero grande' }))
      .toEqual({ 'detalles.0.ancho': expect.stringMatching(/entre 0,1 y 5\.000/) });
  });

  it('traduce los errores por campo de la API a los nombres del formulario', () => {
    expect(erroresDesdeApi({ 'detalles[1].AnchoMm': ['Muy ancho'], DescripcionGeneral: ['Muy corta'], adjuntos: ['Ya no existe'] }))
      .toEqual({ 'detalles.1.ancho': 'Muy ancho', descripcionGeneral: 'Muy corta', adjuntos: 'Ya no existe' });
  });

  it('«Cotizar uno similar» lleva el tipo de letrero y la referencia; el personal va al registro presencial', () => {
    expect(destinoSolicitud({ trabajo: { idProducto: 4, titulo: 'Letrero para panadería' } }))
      .toBe('/cuenta/solicitud?producto=4&referencia=Letrero+para+panader%C3%ADa');
    expect(destinoSolicitud()).toBe('/cuenta/solicitud');
    expect(destinoSolicitud({ esPersonal: true })).toBe('/panel/solicitudes/nueva');
  });
});

describe('formulario de solicitud del cliente', () => {
  const abrir = (entrada = '/cuenta/solicitud') => dibujar(entrada, '/cuenta/solicitud', <NuevaSolicitud />);

  async function llenarMinimo() {
    const tipo = await screen.findByRole('combobox', { name: /Tipo de letrero/ });
    await waitFor(() => expect(within(tipo).getByRole('option', { name: 'Letras corpóreas' })).toBeInTheDocument());
    escribir(tipo, '2');
    escribir(screen.getByRole('textbox', { name: /^Ancho/ }), '120');
    escribir(screen.getByRole('textbox', { name: /^Alto/ }), '60');
    escribir(screen.getByRole('textbox', { name: /^Cantidad/ }), '2');
    escribir(screen.getByRole('textbox', { name: /^Descripción/ }), 'Letras para la fachada del local');
  }

  it('precarga el tipo de letrero de «Cotizar uno similar» y la referencia', async () => {
    abrir('/cuenta/solicitud?producto=2&referencia=Letras%20doradas');
    const tipo = await screen.findByRole('combobox', { name: /Tipo de letrero/ });
    await waitFor(() => expect(tipo).toHaveValue('2'));
    expect(screen.getByText('Letras doradas', { selector: 'strong' })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^Descripción/ })).toHaveValue('Me interesa un trabajo similar a «Letras doradas». ');
  });

  it('agrega y quita letreros, con el área estimada en vivo', async () => {
    abrir();
    await llenarMinimo();
    expect(screen.getByText('1,44 m²', { selector: '[class*="area"] strong' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Agregar otro letrero' }));
    const segundo = screen.getByRole('group', { name: 'Letrero 2' });
    await waitFor(() => expect(within(segundo).getByRole('combobox', { name: /Tipo de letrero/ })).toHaveFocus());
    escribir(within(segundo).getByRole('textbox', { name: /^Ancho/ }), '100');
    escribir(within(segundo).getByRole('textbox', { name: /^Alto/ }), '100');
    expect(screen.getByText(/Área total estimada/)).toHaveTextContent('2,44 m²');
    fireEvent.click(within(segundo).getByRole('button', { name: 'Quitar el letrero 2' }));
    expect(screen.queryByRole('group', { name: 'Letrero 2' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Agregar otro letrero' })).toHaveFocus();
  });

  it('al enviar con errores enfoca el primero y no llama a la API', async () => {
    abrir();
    await screen.findByRole('combobox', { name: /Tipo de letrero/ });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar solicitud' }));
    const tipo = screen.getByRole('combobox', { name: /Tipo de letrero/ });
    expect(tipo).toHaveFocus();
    expect(tipo).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('textbox', { name: /^Descripción/ })).toHaveAccessibleDescription(/al menos 10 letras/);
    expect(misSolicitudes.enviar).not.toHaveBeenCalled();
  });

  it('envía los letreros en mm con los ids de los adjuntos y muestra el comprobante', async () => {
    misSolicitudes.subirAdjunto.mockImplementation((archivo) => Promise.resolve({ idAdjunto: 7, tipo: 'pdf', nombre: archivo.name, bytes: 2048, urlMiniatura: null }));
    misSolicitudes.enviar.mockResolvedValue(COMPROBANTE);
    abrir();
    await llenarMinimo();
    fireEvent.change(screen.getByTestId('entrada-adjuntos'), { target: { files: [new File(['%PDF-'], 'logo.pdf', { type: 'application/pdf' })] } });
    await screen.findByRole('button', { name: 'Quitar logo.pdf' });
    await waitFor(() => expect(screen.getByRole('button', { name: 'Quitar logo.pdf' }).closest('li').querySelector('progress')).toBeNull());
    // El aviso al formulario («ya no queda nada subiendo») llega en un efecto: se deja terminar antes de enviar.
    await act(async () => { await new Promise((resolver) => { setTimeout(resolver, 0); }); });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar solicitud' }));
    expect(screen.queryByText(/Espera a que terminen/)).not.toBeInTheDocument();
    await waitFor(() => expect(misSolicitudes.enviar).toHaveBeenCalledWith({
      descripcionGeneral: 'Letras para la fachada del local',
      detalles: [{ idProducto: 2, descripcion: null, anchoMm: 1200, altoMm: 600, cantidad: 2 }],
      adjuntos: [7],
    }), { timeout: 3000 });
    const titulo = await screen.findByRole('heading', { level: 1, name: 'Recibimos tu solicitud #123' });
    await waitFor(() => expect(titulo).toHaveFocus());
    expect(screen.getByText('14-11-2026 10:32')).toBeInTheDocument();
    expect(screen.getByRole('rowheader', { name: 'Letras corpóreas' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Descargar logo.pdf' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver mis solicitudes' })).toHaveAttribute('href', '/cuenta/solicitudes');
  });

  it('no deja enviar mientras un archivo sigue subiendo', async () => {
    misSolicitudes.subirAdjunto.mockImplementation(() => new Promise(() => {}));
    abrir();
    await llenarMinimo();
    fireEvent.change(screen.getByTestId('entrada-adjuntos'), { target: { files: [new File(['x'], 'foto.jpg', { type: 'image/jpeg' })] } });
    await screen.findByRole('progressbar', { name: 'Subiendo foto.jpg' });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar solicitud' }));
    expect(await screen.findByText('Espera a que terminen de subir tus archivos y vuelve a enviar.')).toBeInTheDocument();
    expect(misSolicitudes.enviar).not.toHaveBeenCalled();
  });

  it('403 correo sin confirmar: ofrece reenviar el enlace y conserva lo escrito', async () => {
    misSolicitudes.enviar.mockRejectedValue(new ErrorApi({ estado: 403, codigo: 'correo_sin_confirmar', mensaje: 'Confirma tu correo.' }));
    authService.reenviarVerificacion.mockResolvedValue({ mensaje: 'Te enviamos un enlace nuevo.' });
    abrir();
    await llenarMinimo();
    fireEvent.click(screen.getByRole('button', { name: 'Enviar solicitud' }));
    expect(await screen.findByText('Confirma tu correo para enviar solicitudes')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^Descripción/ })).toHaveValue('Letras para la fachada del local');
    fireEvent.click(screen.getByRole('button', { name: 'Reenviar enlace' }));
    await waitFor(() => expect(authService.reenviarVerificacion).toHaveBeenCalledTimes(1));
  });

  it('con el correo sin confirmar no muestra el formulario, solo el aviso', async () => {
    cuenta.obtener.mockResolvedValue({ ...MI_CUENTA, correoVerificado: false });
    abrir();
    expect(await screen.findByText('Confirma tu correo para enviar solicitudes')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Enviar solicitud' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ya lo confirmé' })).toBeInTheDocument();
  });

  it('409 contacto directo: muestra el horario, el estado del local, WhatsApp y teléfono sin perder lo escrito', async () => {
    const mensaje = 'Para esta solicitud es preferible contactarse directamente con el local.';
    misSolicitudes.enviar.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'contacto_directo', mensaje }));
    abrir();
    await llenarMinimo();
    fireEvent.click(screen.getByRole('button', { name: 'Enviar solicitud' }));
    expect(await screen.findByText('Para esta solicitud es preferible contactarse directamente con el local')).toBeInTheDocument();
    expect(screen.getByText(/Escríbenos por WhatsApp o llámanos/)).toBeInTheDocument();
    expect(await screen.findByText('Hoy: 9:00 a 18:30 (hora de Chile)')).toBeInTheDocument();
    expect(screen.getByText('Abierto / Disponible')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Escribir por WhatsApp/ })).toHaveAttribute('href', expect.stringMatching(/^https:\/\/wa\.me\//));
    expect(screen.getByRole('link', { name: /^Llamar al/ })).toHaveAttribute('href', expect.stringMatching(/^tel:\+56/));
    expect(screen.getByRole('textbox', { name: /^Descripción/ })).toHaveValue('Letras para la fachada del local');
  });

  it('503 ocupado (ya reintentado): explica que vuelva en unos segundos sin perder lo escrito', async () => {
    misSolicitudes.enviar.mockRejectedValue(new ErrorApi({
      estado: 503, codigo: 'servicio_ocupado', mensaje: 'Estamos recibiendo muchas solicitudes; vuelve a intentarlo en unos segundos.',
    }));
    abrir();
    await llenarMinimo();
    fireEvent.click(screen.getByRole('button', { name: 'Enviar solicitud' }));
    expect(await screen.findByText('Estamos recibiendo muchas solicitudes; vuelve a intentarlo en unos segundos.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^Descripción/ })).toHaveValue('Letras para la fachada del local');
    expect(screen.getByRole('button', { name: 'Enviar solicitud' })).toBeInTheDocument();
  });

  it('400 con errores por campo los pone junto a su campo', async () => {
    misSolicitudes.enviar.mockRejectedValue(new ErrorApi({ estado: 400, mensaje: 'Revisa', errores: { 'detalles[0].anchoMm': ['El ancho va de 1 a 50.000 mm.'] } }));
    abrir();
    await llenarMinimo();
    fireEvent.click(screen.getByRole('button', { name: 'Enviar solicitud' }));
    const ancho = screen.getByRole('textbox', { name: /^Ancho/ });
    await waitFor(() => expect(ancho).toHaveAttribute('aria-invalid', 'true'));
    expect(ancho).toHaveAccessibleDescription('El ancho va de 1 a 50.000 mm.');
    await waitFor(() => expect(ancho).toHaveFocus());
  });
});

describe('bandeja de solicitudes', () => {
  const PAGINA = {
    elementos: [{
      idSolicitud: 123, numero: '#123', fechaChile: '2026-11-14T10:32:00', estado: 'solicitado', origen: 'online',
      cliente: { nombre: 'Rosa', apellido: 'Pino', telefono: '912345678' }, cantidadItems: 2, cantidadAdjuntos: 1, registradaPor: null,
    }],
    total: 1, pagina: 1, tamano: 20, totalPaginas: 1,
  };

  beforeEach(() => solicitudes.listar.mockResolvedValue(PAGINA));

  it('los filtros se convierten en parámetros de la API; un rango de fechas al revés no se envía', () => {
    expect(parametrosApi({ estado: 'rechazado', origen: '', desde: '2026-11-01', hasta: '2026-11-30', q: ' 9123 ', orden: 'fecha_asc', pagina: 2 }))
      .toEqual({ estado: 'rechazado', origen: undefined, desde: '2026-11-01', hasta: '2026-11-30', q: '9123', orden: 'fecha_asc', pagina: 2, tamano: 20 });
    expect(parametrosApi({ estado: '', origen: '', desde: '2026-11-30', hasta: '2026-11-01', q: '', orden: 'fecha_desc', pagina: 1 }))
      .toMatchObject({ desde: undefined, hasta: undefined });
  });

  it('pide la primera página y cada filtro vuelve a pedir desde la página 1', async () => {
    dibujar('/panel/solicitudes?pagina=3', '/panel/solicitudes', <BandejaSolicitudes />);
    await waitFor(() => expect(solicitudes.listar).toHaveBeenLastCalledWith(expect.objectContaining({ pagina: 3, orden: 'fecha_desc', tamano: 20 })));
    expect(await screen.findByRole('link', { name: 'Solicitud #123' })).toHaveAttribute('href', '/panel/solicitudes/123');

    escribir(screen.getByRole('combobox', { name: 'Estado' }), 'rechazado');
    await waitFor(() => expect(solicitudes.listar).toHaveBeenLastCalledWith(expect.objectContaining({ estado: 'rechazado', pagina: 1 })));
    escribir(screen.getByRole('combobox', { name: 'Origen' }), 'presencial');
    escribir(screen.getByLabelText('Desde'), '2026-11-01');
    escribir(screen.getByLabelText('Hasta'), '2026-11-30');
    escribir(screen.getByRole('combobox', { name: 'Orden' }), 'fecha_asc');
    await waitFor(() => expect(solicitudes.listar).toHaveBeenLastCalledWith({
      estado: 'rechazado', origen: 'presencial', desde: '2026-11-01', hasta: '2026-11-30', q: undefined, orden: 'fecha_asc', pagina: 1, tamano: 20,
    }));
    expect(screen.getByTestId('ruta')).toHaveTextContent('/panel/solicitudes?estado=rechazado&origen=presencial&desde=2026-11-01&hasta=2026-11-30&orden=fecha_asc');
  });

  it('un parámetro inválido en la dirección se ignora y se limpia, sin mostrar error', async () => {
    dibujar('/panel/solicitudes?estado=borrado&origen=presencial&orden=azar&desde=ayer&pagina=x', '/panel/solicitudes', <BandejaSolicitudes />);
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent(/^\/panel\/solicitudes\?origen=presencial$/));
    expect(solicitudes.listar).toHaveBeenCalledWith({
      estado: undefined, origen: 'presencial', desde: undefined, hasta: undefined, q: undefined, orden: 'fecha_desc', pagina: 1, tamano: 20,
    });
    expect(screen.getByRole('combobox', { name: 'Estado' })).toHaveValue('');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('si la API rechaza un filtro con 400, se quita de la dirección y se vuelve a pedir', async () => {
    solicitudes.listar
      .mockRejectedValueOnce(new ErrorApi({ estado: 400, codigo: 'datos_invalidos', mensaje: 'x', errores: { origen: ['Origen no válido.'] } }));
    dibujar('/panel/solicitudes?origen=online', '/panel/solicitudes', <BandejaSolicitudes />);
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent(/^\/panel\/solicitudes$/));
    expect(await screen.findByRole('link', { name: 'Solicitud #123' })).toBeInTheDocument();
    expect(screen.queryByText('No pudimos cargar las solicitudes')).not.toBeInTheDocument();
  });

  it('la búsqueda se aplica al dejar de escribir y «Limpiar filtros» vuelve a todo', async () => {
    dibujar('/panel/solicitudes?estado=solicitado', '/panel/solicitudes', <BandejaSolicitudes />);
    await screen.findByRole('link', { name: 'Solicitud #123' });
    escribir(screen.getByRole('searchbox', { name: 'Buscar' }), '912345678');
    await waitFor(() => expect(solicitudes.listar).toHaveBeenLastCalledWith(expect.objectContaining({ q: '912345678', estado: 'solicitado' })), { timeout: 2000 });
    fireEvent.click(screen.getByRole('button', { name: 'Limpiar filtros' }));
    await waitFor(() => expect(solicitudes.listar).toHaveBeenLastCalledWith({
      estado: undefined, origen: undefined, desde: undefined, hasta: undefined, q: undefined, orden: 'fecha_desc', pagina: 1, tamano: 20,
    }));
  });
});

describe('ficha de la solicitud', () => {
  const FICHA = {
    ...COMPROBANTE,
    adjuntos: [{ idAdjunto: 8, tipo: 'imagen', nombre: 'fachada.jpg', bytes: 4096, urlMiniatura: '/api/v1/adjuntos/8/miniatura' }],
    historial: [
      { estadoAnterior: null, estadoNuevo: 'solicitado', motivo: null, fechaChile: '2026-11-14T10:32:00', actor: 'cliente', por: 'Rosa Pino' },
      { estadoAnterior: 'solicitado', estadoNuevo: 'solicitado', motivo: 'Se quitó el adjunto boceto.png', fechaChile: '2026-11-14T11:00:00', actor: 'personal', por: 'Hugo Vera' },
    ],
    registradaPor: null,
  };

  function entrarComo(rol) {
    act(() => almacenSesion.aplicar({ token: 'jwt', tipo: 'empleado', usuario: { idEmpleado: 1, nombre: 'Marta', apellido: 'Fuentes', rol } }));
  }

  const abrir = () => dibujar('/panel/solicitudes/123', '/panel/solicitudes/:id', <FichaSolicitud />);

  beforeEach(() => {
    solicitudes.obtener.mockResolvedValue(FICHA);
  });
  afterEach(() => act(() => almacenSesion.cerrar()));

  it('muestra el comprobante, los adjuntos que se abren en grande, el historial y quién la registró', async () => {
    entrarComo('operario');
    abrir();
    expect(await screen.findByRole('heading', { level: 1, name: 'Solicitud #123' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '9 1234 5678' })).toHaveAttribute('href', 'tel:912345678');
    expect(screen.getByRole('button', { name: 'Ver fachada.jpg en grande' })).toBeInTheDocument();
    expect(screen.getByText(/Creada como/)).toHaveTextContent('Creada como Solicitada');
    expect(screen.getByText('El cliente, en línea')).toBeInTheDocument();
    // Quitar un adjunto queda como evento, no como cambio de estado.
    expect(screen.getByText('Se quitó el adjunto boceto.png')).toBeInTheDocument();
    expect(screen.queryByText(/Solicitada → /)).not.toBeInTheDocument();
    expect(screen.queryByText(/Motivo: Se quitó/)).not.toBeInTheDocument();
    // RFU11: todo el personal edita, rechaza y elimina (borrado lógico).
    expect(screen.getByRole('button', { name: 'Editar solicitud' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rechazar con motivo' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Eliminar solicitud' })).toBeInTheDocument();
  });

  it('rechazar exige un motivo de al menos 10 caracteres y lo envía', async () => {
    entrarComo('dueño');
    solicitudes.cambiarEstado.mockResolvedValue({});
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Rechazar con motivo' }));
    const dialogo = screen.getByRole('dialog', { name: 'Rechazar la solicitud #123' });
    const motivo = within(dialogo).getByRole('textbox', { name: /Motivo del rechazo/ });
    escribir(motivo, 'Corto');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Rechazar solicitud' }));
    expect(motivo).toHaveAttribute('aria-invalid', 'true');
    expect(motivo).toHaveFocus();
    expect(solicitudes.cambiarEstado).not.toHaveBeenCalled();

    escribir(motivo, 'No fabricamos letreros de ese tamaño');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Rechazar solicitud' }));
    await waitFor(() => expect(solicitudes.cambiarEstado).toHaveBeenCalledWith(
      '123', { estado: 'rechazado', motivo: 'No fabricamos letreros de ese tamaño' }, 'AAAAAAAAB9E=',
    ));
    await waitFor(() => expect(solicitudes.obtener).toHaveBeenCalledTimes(2));
  });

  it('un 409 version_obsoleta al rechazar recarga la ficha y ofrece ver la versión actual', async () => {
    entrarComo('diseñador');
    solicitudes.cambiarEstado.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'version_obsoleta', mensaje: 'Otra persona modificó esta solicitud mientras la veías.' }));
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Rechazar con motivo' }));
    const dialogo = screen.getByRole('dialog');
    escribir(within(dialogo).getByRole('textbox', { name: /Motivo/ }), 'El cliente desistió del trabajo');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Rechazar solicitud' }));
    expect(await within(dialogo).findByText('Otra persona modificó esta solicitud')).toBeInTheDocument();
    // La ficha ya se recargó detrás del diálogo.
    await waitFor(() => expect(solicitudes.obtener).toHaveBeenCalledTimes(2));
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Ver versión actual' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    // Al reabrirlo, el diálogo empieza sin el error anterior.
    fireEvent.click(screen.getByRole('button', { name: 'Rechazar con motivo' }));
    expect(within(screen.getByRole('dialog')).queryByText('Otra persona modificó esta solicitud')).not.toBeInTheDocument();
  });

  it('al llegar los datos, el foco va al título de la ficha', async () => {
    entrarComo('operario');
    abrir();
    const titulo = await screen.findByRole('heading', { level: 1, name: 'Solicitud #123' });
    await waitFor(() => expect(titulo).toHaveFocus());
  });

  it('el comprobante muestra dos letreros iguales como filas distintas', async () => {
    entrarComo('operario');
    const igual = { idProducto: 2, producto: 'Letras corpóreas', descripcion: null, anchoMm: 100, altoMm: 100, cantidad: 1, areaM2: 0.01 };
    solicitudes.obtener.mockResolvedValue({ ...FICHA, detalles: [igual, igual] });
    abrir();
    await screen.findByRole('heading', { level: 1, name: 'Solicitud #123' });
    expect(screen.getAllByRole('rowheader', { name: 'Letras corpóreas' })).toHaveLength(2);
  });

  it('eliminar envía la versión vista', async () => {
    entrarComo('operario');
    solicitudes.eliminar.mockResolvedValue();
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Eliminar solicitud' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Eliminar solicitud' }));
    await waitFor(() => expect(solicitudes.eliminar).toHaveBeenCalledWith('123', 'AAAAAAAAB9E='));
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent(/^\/panel\/solicitudes$/));
  });

  it('un 409 al rechazar se explica dentro del diálogo', async () => {
    entrarComo('administrador');
    solicitudes.cambiarEstado.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'transicion_invalida', mensaje: 'La solicitud ya fue aceptada y no se puede rechazar.' }));
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Rechazar con motivo' }));
    const dialogo = screen.getByRole('dialog');
    escribir(within(dialogo).getByRole('textbox', { name: /Motivo/ }), 'El cliente desistió del trabajo');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Rechazar solicitud' }));
    expect(await within(dialogo).findByText('La solicitud ya fue aceptada y no se puede rechazar.')).toBeInTheDocument();
  });

  it('editar envía la versión vista (If-Match) y un 409 avisa que otra persona la modificó, sin perder lo escrito', async () => {
    entrarComo('diseñador');
    publico.productos.mockResolvedValue(PRODUCTOS);
    solicitudes.editar.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'version_obsoleta', mensaje: 'Otra persona modificó esta solicitud mientras la veías. Recarga para ver la última versión.' }));
    abrir();
    fireEvent.click(await screen.findByRole('button', { name: 'Editar solicitud' }));
    const descripcion = await screen.findByRole('textbox', { name: /^Descripción/ });
    await waitFor(() => expect(screen.getByRole('combobox', { name: /Tipo de letrero/ })).toHaveValue('2'));
    expect(screen.getByRole('textbox', { name: /^Ancho/ })).toHaveValue('120');
    escribir(descripcion, 'Letras doradas para la fachada');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    await waitFor(() => expect(solicitudes.editar).toHaveBeenCalledWith(123, {
      descripcionGeneral: 'Letras doradas para la fachada',
      detalles: [{ idProducto: 2, descripcion: null, anchoMm: 1200, altoMm: 600, cantidad: 2 }],
    }, 'AAAAAAAAB9E='));
    expect(await screen.findByText('Otra persona modificó esta solicitud')).toBeInTheDocument();
    expect(descripcion).toHaveValue('Letras doradas para la fachada');
    fireEvent.click(screen.getByRole('button', { name: 'Cargar la versión actual' }));
    await waitFor(() => expect(solicitudes.obtener).toHaveBeenCalledTimes(2));
    expect(screen.queryByRole('button', { name: 'Guardar cambios' })).not.toBeInTheDocument();
  });

describe('revisión ECC del formulario y la bandeja', () => {
  const abrir = (entrada = '/cuenta/solicitud') => dibujar(entrada, '/cuenta/solicitud', <NuevaSolicitud />);

  beforeEach(() => act(() => almacenSesion.aplicar({ token: 'jwt', tipo: 'cliente', usuario: { idCliente: 9, nombre: 'Rosa', apellido: 'Pino' } })));
  afterEach(() => act(() => almacenSesion.cerrar()));

  async function tipoListo() {
    const tipo = await screen.findByRole('combobox', { name: /Tipo de letrero/ });
    await waitFor(() => expect(within(tipo).getByRole('option', { name: 'Letras corpóreas' })).toBeInTheDocument());
    return tipo;
  }

  it('muestra los borradores que el cliente subió antes y los envía con la solicitud', async () => {
    misSolicitudes.borradores.mockResolvedValue([{ idAdjunto: 31, tipo: 'pdf', nombre: 'logo-antes.pdf', bytes: 900, urlMiniatura: null }]);
    misSolicitudes.enviar.mockResolvedValue(COMPROBANTE);
    abrir();
    expect(await screen.findByRole('button', { name: 'Quitar logo-antes.pdf' })).toBeInTheDocument();
    expect(screen.getByText(/Recuperamos el archivo que subiste antes/)).toBeInTheDocument();
    escribir(await tipoListo(), '2');
    escribir(screen.getByRole('textbox', { name: /^Descripción/ }), 'Letras para la fachada del local');
    await act(async () => { await new Promise((r) => { setTimeout(r, 0); }); });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar solicitud' }));
    await waitFor(() => expect(misSolicitudes.enviar).toHaveBeenCalledWith(expect.objectContaining({ adjuntos: [31] })));
  });

  it('409 tope_adjuntos se explica junto al archivo', async () => {
    misSolicitudes.subirAdjunto.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'tope_adjuntos', mensaje: 'x' }));
    abrir();
    await tipoListo();
    fireEvent.change(screen.getByTestId('entrada-adjuntos'), { target: { files: [new File(['x'], 'sexto.png', { type: 'image/png' })] } });
    await waitFor(() => expect(screen.getByText('sexto.png', { selector: 'strong' }).closest('li'))
      .toHaveTextContent(/contando los que subiste antes y no enviaste/));
  });

  it('guarda lo escrito en la pestaña, lo recupera al volver y lo borra al enviar', async () => {
    const { unmount } = abrir();
    escribir(await tipoListo(), '2');
    escribir(screen.getByRole('textbox', { name: /^Ancho/ }), '90');
    escribir(screen.getByRole('textbox', { name: /^Descripción/ }), 'Un letrero con mi logo');
    unmount();

    misSolicitudes.enviar.mockResolvedValue(COMPROBANTE);
    abrir();
    expect(await screen.findByText('Recuperamos lo que habías escrito en esta pestaña.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^Descripción/ })).toHaveValue('Un letrero con mi logo');
    expect(screen.getByRole('textbox', { name: /^Ancho/ })).toHaveValue('90');
    escribir(screen.getByRole('textbox', { name: /^Alto/ }), '40');
    await act(async () => { await new Promise((r) => { setTimeout(r, 0); }); });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar solicitud' }));
    await screen.findByRole('heading', { level: 1, name: 'Recibimos tu solicitud #123' });
    expect(sessionStorage.getItem('synctime_solicitud_9')).toBeNull();
  });

  it('un doble clic en «Enviar solicitud» envía una sola vez', async () => {
    let terminar;
    misSolicitudes.enviar.mockImplementation(() => new Promise((r) => { terminar = () => r(COMPROBANTE); }));
    abrir();
    escribir(await tipoListo(), '2');
    escribir(screen.getByRole('textbox', { name: /^Descripción/ }), 'Letras para la fachada del local');
    await act(async () => { await new Promise((r) => { setTimeout(r, 0); }); });
    const boton = screen.getByRole('button', { name: 'Enviar solicitud' });
    fireEvent.click(boton);
    fireEvent.click(boton);
    await waitFor(() => expect(misSolicitudes.enviar).toHaveBeenCalledTimes(1));
    await act(async () => terminar());
    expect(misSolicitudes.enviar).toHaveBeenCalledTimes(1);
  });

  it('un letrero recién agregado no muestra errores hasta tocarlo, y quitar uno se anuncia', async () => {
    abrir();
    await tipoListo();
    fireEvent.click(screen.getByRole('button', { name: 'Enviar solicitud' }));
    expect(screen.getByRole('combobox', { name: /Tipo de letrero/ })).toHaveAttribute('aria-invalid', 'true');
    fireEvent.click(screen.getByRole('button', { name: 'Agregar otro letrero' }));
    const segundo = screen.getByRole('group', { name: 'Letrero 2' });
    expect(within(segundo).getByRole('combobox', { name: /Tipo de letrero/ })).not.toHaveAttribute('aria-invalid');
    fireEvent.blur(within(segundo).getByRole('combobox', { name: /Tipo de letrero/ }));
    expect(within(segundo).getByRole('combobox', { name: /Tipo de letrero/ })).toHaveAttribute('aria-invalid', 'true');
    fireEvent.click(within(segundo).getByRole('button', { name: 'Quitar el letrero 2' }));
    expect(screen.getByText('Quitaste el letrero 2. Quedan 1.')).toBeInTheDocument();
  });
});

describe('bandeja: búsqueda y páginas', () => {
  beforeEach(() => solicitudes.listar.mockResolvedValue({ elementos: [], total: 3, pagina: 1, tamano: 20, totalPaginas: 1 }));

  it('una página fuera de rango lleva a la última', async () => {
    dibujar('/panel/solicitudes?pagina=9', '/panel/solicitudes', <BandejaSolicitudes />);
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent(/^\/panel\/solicitudes$/));
    await waitFor(() => expect(solicitudes.listar).toHaveBeenLastCalledWith(expect.objectContaining({ pagina: 1 })));
  });

  it('el campo de búsqueda sigue a la dirección al navegar, sin volver a aplicar lo anterior', async () => {
    const { Link } = await import('react-router-dom');
    render(
      <ProveedorAvisos>
        <MemoryRouter initialEntries={['/panel/solicitudes?q=rosa']}>
          <Routes>
            <Route path="/panel/solicitudes" element={<><Link to="/panel/solicitudes">Menú: Solicitudes</Link><BandejaSolicitudes /><Ruta /></>} />
          </Routes>
        </MemoryRouter>
      </ProveedorAvisos>,
    );
    const buscar = screen.getByRole('searchbox', { name: 'Buscar' });
    expect(buscar).toHaveValue('rosa');
    fireEvent.click(screen.getByRole('link', { name: 'Menú: Solicitudes' }));
    await waitFor(() => expect(buscar).toHaveValue(''));
    await new Promise((r) => { setTimeout(r, 450); });
    expect(screen.getByTestId('ruta')).toHaveTextContent(/^\/panel\/solicitudes$/);
  });
});
});
