import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { ErrorApi } from '../api/cliente';
import { categorias, personal, vitrina } from '../api/mantenedores';
import { ProveedorAvisos } from '../avisos/Avisos';
import EditorMantenedor from '../paginas/mantenedores/EditorMantenedor';
import ListaMantenedor from '../paginas/mantenedores/ListaMantenedor';
import Mantenedores from '../paginas/mantenedores/Mantenedores';
import {
  empleadoParaApi, erroresDeApi, requierePasswordEmpleado, FILTROS_MANTENEDOR, logoVigente, parametrosMantenedor, publicacionParaApi, puedeSerVisible, textoConflicto,
  validarCategoria, validarEmpleado, validarMaterial, validarProducto, validarPublicacion,
} from '../paginas/mantenedores/reglasMantenedores';
import { almacenSesion } from '../sesion/sesion';
import { filtrosDesde, parametrosInvalidos } from '../utilidades/filtrosUrl';

vi.mock('../api/authService', () => ({ COOKIE_CSRF: 'synctime_csrf', default: { renovar: vi.fn(), salir: vi.fn() } }));
vi.mock('../api/mantenedores', () => {
  const crud = () => ({ listar: vi.fn(), obtener: vi.fn(), crear: vi.fn(), editar: vi.fn(), cambiarActivo: vi.fn() });
  return {
    categorias: crud(), productos: crud(), materialesTaller: crud(), personal: crud(),
    vitrina: { ...crud(), cambiarVisible: vi.fn(), autorizarLogo: vi.fn(), subirImagen: vi.fn() },
    todasLasPaginas: vi.fn(() => Promise.resolve([{ idProducto: 1, nombre: 'Letrero luminoso' }])),
  };
});

const escribir = (control, valor) => fireEvent.change(control, { target: { value: valor } });
const pagina = (elementos) => ({ elementos, total: elementos.length, pagina: 1, tamano: 20, totalPaginas: 1 });

function Ruta() {
  const { pathname, search } = useLocation();
  return <p data-testid="ruta">{pathname}{search}</p>;
}

function abrir(entrada) {
  return render(
    <ProveedorAvisos>
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          <Route path="/panel/mantenedores" element={<Mantenedores />}>
            <Route path=":tipo" element={<ListaMantenedor />} />
          </Route>
          <Route path="/panel/mantenedores/:tipo/nuevo" element={<EditorMantenedor />} />
          <Route path="/panel/mantenedores/:tipo/:id" element={<EditorMantenedor />} />
          <Route path="/panel/empleados/nuevo" element={<EditorMantenedor tipo="personal" />} />
          <Route path="/panel/empleados/:id" element={<EditorMantenedor tipo="personal" />} />
        </Routes>
        <Ruta />
      </MemoryRouter>
    </ProveedorAvisos>,
  );
}

function entrarComo(rol, idEmpleado = 1) {
  act(() => almacenSesion.aplicar({ token: 'jwt', tipo: 'empleado', usuario: { idEmpleado, nombre: 'Marta', apellido: 'Fuentes', rol } }));
}

beforeEach(() => {
  vi.clearAllMocks();
  entrarComo('dueño');
});
afterEach(() => act(() => almacenSesion.cerrar()));

describe('reglas de los mantenedores', () => {
  it('filtros en la dirección: activos por defecto, «todos» no viaja y lo inválido se limpia', () => {
    const filtros = filtrosDesde(new URLSearchParams('q=acrilico&activo=nada&pagina=2'), FILTROS_MANTENEDOR);
    expect(filtros).toEqual({ q: 'acrilico', activo: 'true', pagina: 2 });
    expect(parametrosInvalidos(new URLSearchParams('activo=nada&x=1'), FILTROS_MANTENEDOR)).toEqual(['activo', 'x']);
    expect(parametrosMantenedor(filtros)).toEqual({ q: 'acrilico', activo: 'true', pagina: 2, tamano: 20 });
    expect(parametrosMantenedor({ q: '', activo: 'todos', pagina: 1 })).toEqual({ q: undefined, activo: undefined, pagina: 1, tamano: 20 });
  });

  it('valida categorías, tipos de letrero y materiales con los límites de la API', () => {
    expect(validarCategoria({ nombre: 'L', descripcion: '' })).toEqual({ nombre: 'El nombre debe tener al menos 2 caracteres.' });
    expect(validarCategoria({ nombre: 'Luminosos', descripcion: 'x'.repeat(301) })).toHaveProperty('descripcion');
    expect(validarProducto({ idCategoria: '', nombre: 'Caja de luz', descripcion: '', precioBaseRef: '12,5' })).toEqual({
      idCategoria: 'Elige la categoría.', precioBaseRef: 'Escribe un monto en pesos, sin decimales; por ejemplo 12.990.',
    });
    expect(validarProducto({ idCategoria: '1', nombre: 'Caja de luz', descripcion: '', precioBaseRef: '45.000' })).toEqual({});
    expect(validarMaterial({ nombre: 'Acrílico', unidadMedida: '', costoUnitarioAprox: '' })).toEqual({
      unidadMedida: 'Escribe la unidad de medida, por ejemplo m2.', costoUnitarioAprox: 'Escribe el costo unitario aproximado.',
    });
  });

  it('una publicación con logo sin autorización no puede quedar visible', () => {
    const base = { idProducto: '1', titulo: 'Panadería', descripcion: '', ancho: '120', alto: '60', precioReferencial: '', incluyeLogoCliente: true, visible: true };
    expect(puedeSerVisible({ incluyeLogoCliente: true, logoAutorizado: false })).toBe(false);
    expect(puedeSerVisible({ incluyeLogoCliente: false })).toBe(true);
    expect(validarPublicacion(base)).toHaveProperty('visible');
    expect(validarPublicacion(base, { logoAutorizado: true })).toEqual({});
    expect(validarPublicacion({ ...base, alto: '' }, { logoAutorizado: true })).toEqual({ alto: 'Escribe el ancho y el alto, o deja los dos en blanco.' });
    expect(publicacionParaApi({ ...base, ancho: '120,5', imagen: { idImagen: 9 } })).toMatchObject({ anchoMm: 1205, altoMm: 600, idImagen: 9, precioReferencial: null });
    // La autorización vale para la pieza que el cliente vio: otra imagen la anula.
    const registro = { incluyeLogoCliente: true, logoAutorizado: true, idImagen: 9 };
    expect(logoVigente({ incluyeLogoCliente: true, imagen: { idImagen: 9 } }, registro)).toBe(true);
    expect(logoVigente({ incluyeLogoCliente: true, imagen: { idImagen: 10 } }, registro)).toBe(false);
  });

  it('personal: RUT válido, rol de los cuatro y, al dar el alta, contraseña inicial y la propia', () => {
    const datos = { rut: '12.345.678-5', nombre: 'Hugo', apellido: 'Vera', telefono: '9 1234 5678', email: 'Hugo@Taller.cl', rol: 'operario', password: '', passwordActual: '' };
    expect(validarEmpleado(datos)).toEqual({});
    expect(validarEmpleado({ ...datos, rut: '12.345.678-9', rol: 'jefe' })).toMatchObject({ rut: expect.any(String), rol: 'Elige el rol.' });
    expect(Object.keys(validarEmpleado(datos, { nuevo: true }))).toEqual(['password', 'passwordActual']);
    expect(empleadoParaApi({ ...datos, password: 'secreta123', passwordActual: 'mia12345' }, { nuevo: true })).toEqual({
      rut: '12.345.678-5', nombre: 'Hugo', apellido: 'Vera', telefono: '912345678', email: 'hugo@taller.cl', rol: 'operario', password: 'secreta123', passwordActual: 'mia12345',
    });
    expect(empleadoParaApi(datos)).not.toHaveProperty('password');
  });

  it('cambiar el correo o el rol de otra persona pide la contraseña de quien lo cambia', () => {
    const registro = { email: 'hugo@taller.cl', rol: 'operario' };
    const datos = { rut: '12.345.678-5', nombre: 'Hugo', apellido: 'Vera', telefono: '912345678', email: 'hugo@taller.cl', rol: 'operario', passwordActual: '' };
    expect(requierePasswordEmpleado({ ...datos, nombre: 'Hugo Andrés' }, registro)).toBe(false);
    expect(requierePasswordEmpleado({ ...datos, email: ' HUGO@taller.cl ' }, registro)).toBe(false);
    expect(requierePasswordEmpleado({ ...datos, email: 'otro@taller.cl' }, registro)).toBe(true);
    expect(requierePasswordEmpleado({ ...datos, rol: 'diseñador' }, registro)).toBe(true);
    expect(validarEmpleado({ ...datos, rol: 'diseñador' }, { requierePassword: true })).toEqual({
      passwordActual: 'Escribe tu contraseña para confirmar el cambio de correo o de rol.',
    });
    expect(empleadoParaApi({ ...datos, passwordActual: 'mia' }, { requierePassword: true })).toHaveProperty('passwordActual', 'mia');
    expect(empleadoParaApi({ ...datos, passwordActual: 'mia' })).not.toHaveProperty('passwordActual');
  });

  it('traduce los errores de la API', () => {
    expect(erroresDeApi({ Nombre: ['Ya existe'], 'Datos.AnchoMm': ['Muy ancho'] })).toEqual({ nombre: 'Ya existe', ancho: 'Muy ancho' });
    expect(textoConflicto(new ErrorApi({ estado: 409, codigo: 'ultimo_dueno', mensaje: 'Otra persona cambió este registro. Recarga para ver la última versión.' })))
      .toMatch(/al menos un dueño activo/);
    expect(textoConflicto(new ErrorApi({ estado: 409, codigo: 'propio', mensaje: 'No puedes desactivarte.' }))).toBe('No puedes desactivarte.');
  });
});

describe('listado de un mantenedor', () => {
  it('lee los filtros de la dirección, los envía a la API y los cambia en la dirección', async () => {
    categorias.listar.mockResolvedValue(pagina([{ idCategoria: 1, nombre: 'Luminosos', descripcion: null, activo: false }]));
    abrir('/panel/mantenedores/categorias?activo=false&q=lum');
    expect(await screen.findByRole('link', { name: 'Luminosos' })).toHaveAttribute('href', '/panel/mantenedores/categorias/1');
    expect(categorias.listar).toHaveBeenCalledWith({ q: 'lum', activo: 'false', pagina: 1, tamano: 20 }, expect.anything());
    expect(screen.getByText('Inactivo')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Categorías' })).toHaveAttribute('aria-current', 'page');
    fireEvent.change(screen.getByRole('combobox', { name: 'Estado' }), { target: { value: 'todos' } });
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent('/panel/mantenedores/categorias?q=lum&activo=todos'));
    await waitFor(() => expect(categorias.listar).toHaveBeenLastCalledWith({ q: 'lum', activo: undefined, pagina: 1, tamano: 20 }, expect.anything()));
    expect(screen.queryByText(/CRUD/)).not.toBeInTheDocument();
  });

  it('en la vitrina, el interruptor de una publicación con logo sin autorización queda bloqueado y lo explica', async () => {
    vitrina.listar.mockResolvedValue(pagina([
      { idCatalogo: 3, titulo: 'Clínica Sonríe', tipoLetrero: 'Luminoso', visible: false, incluyeLogoCliente: true, logoAutorizado: false, rowVersion: 'v3' },
      { idCatalogo: 4, titulo: 'Panadería', tipoLetrero: 'Vinilo', visible: false, incluyeLogoCliente: false, logoAutorizado: false, rowVersion: 'v4' },
    ]));
    vitrina.cambiarVisible.mockResolvedValue({});
    abrir('/panel/mantenedores/vitrina');
    const bloqueado = await screen.findByRole('switch', { name: 'Mostrar «Clínica Sonríe» en la vitrina' });
    // El nombre accesible empieza con el texto visible del control (WCAG 2.5.3).
    expect(bloqueado).toHaveTextContent('Mostrar');
    expect(bloqueado).toHaveAttribute('aria-disabled', 'true');
    expect(bloqueado).toHaveAccessibleDescription(/registra su autorización/);
    fireEvent.click(bloqueado);
    expect(vitrina.cambiarVisible).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('switch', { name: 'Mostrar «Panadería» en la vitrina' }));
    await waitFor(() => expect(vitrina.cambiarVisible).toHaveBeenCalledWith(4, true, 'v4'));
  });
});

describe('ficha de un mantenedor', () => {
  it('nueva publicación: con logo, «Mostrar en la vitrina» queda bloqueado y explica cómo seguir', async () => {
    abrir('/panel/mantenedores/vitrina/nuevo');
    const mostrar = await screen.findByRole('checkbox', { name: 'Mostrar en la vitrina' });
    fireEvent.click(mostrar);
    expect(mostrar).toBeChecked();
    fireEvent.click(screen.getByRole('checkbox', { name: 'Incluye el logo de un cliente' }));
    expect(mostrar).not.toBeChecked();
    expect(mostrar).toBeDisabled();
    expect(mostrar).toHaveAccessibleDescription(/Guárdala oculta/);
  });

  it('versión vieja: lo escrito se queda y se puede guardar sobre la versión actual', async () => {
    categorias.obtener
      .mockResolvedValueOnce({ idCategoria: 1, nombre: 'Luminosos', descripcion: null, activo: true, rowVersion: 'v1' })
      .mockResolvedValue({ idCategoria: 1, nombre: 'Luminosos LED', descripcion: null, activo: true, rowVersion: 'v2' });
    categorias.editar
      .mockRejectedValueOnce(new ErrorApi({ estado: 409, codigo: 'version_obsoleta', mensaje: 'Otra persona la cambió.' }))
      .mockResolvedValue({});
    abrir('/panel/mantenedores/categorias/1');
    const nombre = await screen.findByRole('textbox', { name: /Nombre/ });
    escribir(nombre, 'Cajas de luz');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    expect(await screen.findByText('Otra persona modificó esta categoría mientras lo editabas')).toBeInTheDocument();
    expect(await screen.findByText('Versión actual: Luminosos LED.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /Nombre/ })).toHaveValue('Cajas de luz');
    expect(categorias.editar).toHaveBeenCalledWith('1', { nombre: 'Cajas de luz', descripcion: null }, 'v1');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar sobre la versión actual' }));
    await waitFor(() => expect(categorias.editar).toHaveBeenLastCalledWith('1', { nombre: 'Cajas de luz', descripcion: null }, 'v2'));
  });

  it('con cambios sin guardar, los botones de estado esperan y dicen por qué; sin cambios, la ficha se actualiza', async () => {
    categorias.obtener
      .mockResolvedValueOnce({ idCategoria: 1, nombre: 'Luminosos', descripcion: null, activo: true, rowVersion: 'v1' })
      .mockResolvedValue({ idCategoria: 1, nombre: 'Luminosos', descripcion: null, activo: false, rowVersion: 'v2' });
    categorias.cambiarActivo.mockResolvedValue({});
    categorias.editar.mockResolvedValue({});
    abrir('/panel/mantenedores/categorias/1');
    const nombre = await screen.findByRole('textbox', { name: /Nombre/ });
    escribir(nombre, 'Cajas de luz');
    const desactivar = screen.getByRole('button', { name: 'Desactivar categoría' });
    expect(desactivar).toHaveAttribute('aria-disabled', 'true');
    expect(desactivar).toHaveAccessibleDescription(/Guarda o descarta tus cambios/);
    fireEvent.click(desactivar);
    expect(categorias.cambiarActivo).not.toHaveBeenCalled();
    // Vuelve a lo guardado: el botón se libera, desactiva con la versión vista y la ficha toma la versión nueva.
    escribir(nombre, 'Luminosos');
    expect(desactivar).not.toHaveAttribute('aria-disabled');
    fireEvent.click(desactivar);
    await waitFor(() => expect(categorias.cambiarActivo).toHaveBeenCalledWith('1', false, 'v1'));
    expect(await screen.findByRole('button', { name: 'Activar categoría' })).toBeInTheDocument();
    // Guardar después usa la versión nueva, sin desmontar el formulario.
    escribir(screen.getByRole('textbox', { name: /Nombre/ }), 'Cajas de luz');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    await waitFor(() => expect(categorias.editar).toHaveBeenCalledWith('1', { nombre: 'Cajas de luz', descripcion: null }, 'v2'));
  });

  it('mientras sube la imagen no se guarda, y se anuncia «Imagen subida» al terminar', async () => {
    vitrina.obtener.mockResolvedValue({
      idCatalogo: 3, titulo: 'Panadería', descripcion: '', idProducto: 1, visible: false, incluyeLogoCliente: false, logoAutorizado: false, rowVersion: 'v3',
    });
    let terminar;
    vitrina.subirImagen.mockImplementation(() => new Promise((resolver) => { terminar = resolver; }));
    vitrina.editar.mockResolvedValue({});
    const { container } = abrir('/panel/mantenedores/vitrina/3');
    await screen.findByRole('textbox', { name: /Título/ });
    const archivo = new File(['x'], 'foto.webp', { type: 'image/webp' });
    fireEvent.change(container.querySelector('input[type="file"]'), { target: { files: [archivo] } });
    const guardar = await screen.findByRole('button', { name: 'Guardar cambios' });
    await waitFor(() => expect(guardar).toHaveAttribute('aria-disabled', 'true'));
    expect(guardar).toHaveAccessibleDescription('Espera a que termine de subir la imagen.');
    expect(vitrina.subirImagen).toHaveBeenCalledWith(archivo, expect.objectContaining({ senal: expect.any(AbortSignal) }));
    fireEvent.click(guardar);
    expect(vitrina.editar).not.toHaveBeenCalled();
    await act(async () => terminar({ idImagen: 12, urlMiniatura: '/api/v1/imagenes/a.webp', urlGrande: '/api/v1/imagenes/b.webp' }));
    expect(await screen.findByText('Imagen subida')).toBeInTheDocument();
    expect(guardar).not.toHaveAttribute('aria-disabled');
    fireEvent.click(guardar);
    await waitFor(() => expect(vitrina.editar).toHaveBeenCalledWith('3', expect.objectContaining({ idImagen: 12 }), 'v3'));
  });

  it('al salir de la ficha, la subida en curso se corta', async () => {
    vitrina.obtener.mockResolvedValue({ idCatalogo: 3, titulo: 'Panadería', idProducto: 1, visible: false, incluyeLogoCliente: false, rowVersion: 'v3' });
    let senal;
    vitrina.subirImagen.mockImplementation((_, opciones) => {
      senal = opciones.senal;
      return new Promise(() => {});
    });
    const { container, unmount } = abrir('/panel/mantenedores/vitrina/3');
    await screen.findByRole('textbox', { name: /Título/ });
    fireEvent.change(container.querySelector('input[type="file"]'), { target: { files: [new File(['x'], 'f.png', { type: 'image/png' })] } });
    await waitFor(() => expect(senal).toBeDefined());
    unmount();
    expect(senal.aborted).toBe(true);
  });

  it('cambiar el correo de otra persona pide la contraseña y la envía; una equivocada se marca en su campo', async () => {
    personal.obtener.mockResolvedValue({
      idEmpleado: 5, rut: '12.345.678-5', nombre: 'Hugo', apellido: 'Vera', telefono: '912345678', email: 'hugo@taller.cl', rol: 'operario', activo: true, rowVersion: 'e5',
    });
    personal.editar
      .mockRejectedValueOnce(new ErrorApi({ estado: 400, codigo: 'datos_invalidos', mensaje: 'La contraseña actual no es correcta.' }))
      .mockResolvedValue({});
    abrir('/panel/empleados/5');
    escribir(await screen.findByRole('textbox', { name: /^Nombre/ }), 'Hugo Andrés');
    expect(screen.queryByLabelText(/Tu contraseña, para confirmar el cambio/)).not.toBeInTheDocument();
    escribir(screen.getByRole('textbox', { name: /^Correo/ }), 'hugo.vera@taller.cl');
    const password = screen.getByLabelText(/Tu contraseña, para confirmar el cambio/);
    expect(screen.getByText(/se cerrarán las sesiones abiertas de esta persona/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    expect(await screen.findByText('Escribe tu contraseña para confirmar el cambio de correo o de rol.')).toBeInTheDocument();
    expect(personal.editar).not.toHaveBeenCalled();
    escribir(password, 'equivocada');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    expect(await screen.findByText('La contraseña actual no es correcta.')).toBeInTheDocument();
    expect(password).toHaveValue('');
    escribir(password, 'mia12345');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    await waitFor(() => expect(personal.editar).toHaveBeenLastCalledWith('5', expect.objectContaining({
      email: 'hugo.vera@taller.cl', nombre: 'Hugo Andrés', passwordActual: 'mia12345',
    }), 'e5'));
  });

  it('desactivar una categoría envía la versión vista', async () => {
    categorias.obtener.mockResolvedValue({ idCategoria: 1, nombre: 'Luminosos', activo: true, rowVersion: 'v1' });
    categorias.cambiarActivo.mockResolvedValue({});
    abrir('/panel/mantenedores/categorias/1');
    fireEvent.click(await screen.findByRole('button', { name: 'Desactivar categoría' }));
    await waitFor(() => expect(categorias.cambiarActivo).toHaveBeenCalledWith('1', false, 'v1'));
  });

  it('vitrina: sin autorización, «Mostrar en la vitrina» queda bloqueado; el 409 logo_sin_autorizacion se explica', async () => {
    const publicacion = {
      idCatalogo: 3, titulo: 'Clínica Sonríe', descripcion: '', idProducto: 1, tipoLetrero: 'Letrero luminoso', anchoMm: null, altoMm: null,
      precioReferencial: null, imagen: null, visible: false, incluyeLogoCliente: true, logoAutorizado: false, rowVersion: 'v3',
    };
    vitrina.obtener.mockResolvedValue(publicacion);
    vitrina.editar.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'logo_sin_autorizacion', mensaje: 'Una publicación con el logotipo de un cliente necesita su autorización.' }));
    abrir('/panel/mantenedores/vitrina/3');
    const zona = await screen.findByRole('region', { name: 'Autorización y visibilidad' });
    expect(within(zona).getByText('Falta la autorización del cliente')).toBeInTheDocument();
    const mostrar = within(zona).getByRole('button', { name: 'Mostrar en la vitrina' });
    expect(mostrar).toHaveAttribute('aria-disabled', 'true');
    expect(mostrar).toHaveAccessibleDescription(/registra su autorización/);
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    expect(await screen.findByText('Una publicación con el logotipo de un cliente necesita su autorización.')).toBeInTheDocument();
  });

  it('vitrina: registrar la autorización la pide confirmar y la envía', async () => {
    vitrina.obtener.mockResolvedValue({
      idCatalogo: 3, titulo: 'Clínica Sonríe', idProducto: 1, visible: false, incluyeLogoCliente: true, logoAutorizado: false, rowVersion: 'v3',
    });
    vitrina.autorizarLogo.mockResolvedValue({});
    abrir('/panel/mantenedores/vitrina/3');
    fireEvent.click(await screen.findByRole('button', { name: 'Registrar autorización del cliente' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Registrar la autorización del cliente' });
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Registrar autorización' }));
    await waitFor(() => expect(vitrina.autorizarLogo).toHaveBeenCalledWith('3', true, 'v3'));
  });

  const HUGO = { idEmpleado: 5, rut: '12.345.678-5', nombre: 'Hugo', apellido: 'Vera', telefono: '912345678', email: 'hugo@taller.cl', rol: 'operario', activo: true, rowVersion: 'e5' };

  it('desactivar a una persona pide confirmar: se borra su rostro y se cierran sus sesiones', async () => {
    personal.obtener.mockResolvedValue(HUGO);
    personal.cambiarActivo.mockResolvedValue({});
    abrir('/panel/empleados/5');
    fireEvent.click(await screen.findByRole('button', { name: 'Desactivar a esta persona' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Desactivar a Hugo Vera' });
    expect(dialogo).toHaveAccessibleDescription('Se borrará su acceso con rostro y se cerrarán sus sesiones.');
    expect(within(dialogo).getByRole('button', { name: 'Cancelar' })).toHaveFocus();
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Desactivar' }));
    await waitFor(() => expect(personal.cambiarActivo).toHaveBeenCalledWith('5', false, 'e5'));
  });

  it('si es el último dueño (409 ultimo_dueno), el diálogo lo explica', async () => {
    personal.obtener.mockResolvedValue({ ...HUGO, rol: 'dueño' });
    personal.cambiarActivo.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'ultimo_dueno', mensaje: 'Otra persona cambió este registro. Recarga para ver la última versión.' }));
    abrir('/panel/empleados/5');
    fireEvent.click(await screen.findByRole('button', { name: 'Desactivar a esta persona' }));
    const dialogo = await screen.findByRole('dialog');
    fireEvent.click(within(dialogo).getByRole('button', { name: 'Desactivar' }));
    expect(await within(dialogo).findByText(/al menos un dueño activo/)).toBeInTheDocument();
  });

  it('nadie se desactiva ni se cambia el rol a sí mismo', async () => {
    entrarComo('administrador', 5);
    personal.obtener.mockResolvedValue({ ...HUGO, rol: 'administrador' });
    abrir('/panel/empleados/5');
    const boton = await screen.findByRole('button', { name: 'Desactivar a esta persona' });
    expect(boton).toHaveAttribute('aria-disabled', 'true');
    expect(boton).toHaveAccessibleDescription(/No puedes desactivarte a ti mismo/);
    fireEvent.click(boton);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: /Rol/ })).toBeDisabled();
  });

  it('el alta lleva la contraseña inicial que escribe quien la da y su propia contraseña', async () => {
    personal.crear.mockResolvedValue({ ...HUGO, idEmpleado: 9 });
    abrir('/panel/empleados/nuevo');
    escribir(await screen.findByRole('textbox', { name: /^Nombre/ }), 'Hugo');
    escribir(screen.getByRole('textbox', { name: /^Apellido/ }), 'Vera');
    escribir(screen.getByRole('textbox', { name: /^RUT/ }), '12.345.678-5');
    escribir(screen.getByRole('textbox', { name: /^Teléfono/ }), '912345678');
    escribir(screen.getByRole('textbox', { name: /^Correo/ }), 'hugo@taller.cl');
    escribir(screen.getByRole('combobox', { name: /^Rol/ }), 'operario');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    expect(await screen.findByText('Escribe la contraseña inicial.')).toBeInTheDocument();
    expect(personal.crear).not.toHaveBeenCalled();
    escribir(screen.getByLabelText(/^Contraseña inicial/), 'inicial123');
    escribir(screen.getByLabelText(/^Tu contraseña actual/), 'mia12345');
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    await waitFor(() => expect(personal.crear).toHaveBeenCalledWith(expect.objectContaining({ password: 'inicial123', passwordActual: 'mia12345', rol: 'operario' })));
    await waitFor(() => expect(screen.getByTestId('ruta')).toHaveTextContent('/panel/empleados/9'));
  });
});
