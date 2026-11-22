import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import authService from '../api/authService';
import { ErrorApi } from '../api/cliente';
import { cuenta } from '../api/cuenta';
import { productos } from '../api/mantenedores';
import { ProveedorAvisos } from '../avisos/Avisos';
import { EstructuraCuenta } from '../estructuras/EstructuraCuenta';
import { EstructuraPanel } from '../estructuras/EstructuraPanel';
import { GESTION, MENU_CUENTA, menuPara } from '../estructuras/navegacion';
import MiCuenta from '../paginas/cuenta/MiCuenta';
import { MedidasYOpciones } from '../paginas/mantenedores/MedidasYOpciones';
import InicioPanel from '../paginas/panel/InicioPanel';
import { OrigenSolicitud } from '../paginas/solicitudes/OrigenSolicitud';
import { resumenProducto } from '../paginas/solicitudes/panel/reglasBandeja';
import { almacenSesion } from '../sesion/sesion';

vi.mock('../api/authService', () => ({ COOKIE_CSRF: 'synctime_csrf', default: { renovar: vi.fn(), salir: vi.fn().mockResolvedValue(undefined) } }));
vi.mock('../api/cuenta', () => ({
  cuenta: {
    obtener: vi.fn(), actualizar: vi.fn(), cambiarCorreo: vi.fn(), cambiarPassword: vi.fn(), marketing: vi.fn(), preferencias: vi.fn(),
    descargarDatos: vi.fn(), darDeBaja: vi.fn(),
  },
}));
vi.mock('../api/mantenedores', () => ({
  productos: { editarMedidasYOpciones: vi.fn() },
  todasLasPaginas: vi.fn(() => Promise.resolve([])),
}));
// El inicio se prueba por su estructura: cada bloque trae sus propias consultas y tiene sus propias pruebas.
vi.mock('../paginas/solicitudes/panel/SolicitudesPorAtender', () => ({
  SolicitudesPorAtender: () => <section aria-label="Solicitudes por atender">solicitudes</section>,

}));
vi.mock('../paginas/indicadores/MetricasInicio', () => ({ MetricasInicio: () => <section aria-label="Indicadores del mes">indicadores</section> }));
vi.mock('../paginas/pedidos/AlertasGestion', () => ({ AlertasGestion: () => null }));
vi.mock('../paginas/correos/AvisoCorreosNoEnviados', () => ({ AvisoCorreosNoEnviados: () => null }));
vi.mock('../api/pedidos', () => ({ pedidos: { listar: vi.fn(() => Promise.resolve({ total: 0 })) } }));
vi.mock('../api/cotizaciones', () => ({ cotizaciones: { listar: vi.fn(() => Promise.resolve({ total: 0 })) } }));
vi.mock('../api/publico', () => ({ publico: { cartel: vi.fn(() => Promise.resolve({ abiertoEnLocal: true })) } }));

const entrarComoPersonal = (rol, idEmpleado = 1) =>
  act(() => almacenSesion.aplicar({ token: 'jwt', tipo: 'empleado', usuario: { idEmpleado, nombre: 'Marta', apellido: 'Fuentes', rol } }));
const entrarComoCliente = () =>
  act(() => almacenSesion.aplicar({ token: 'jwt-cliente', tipo: 'cliente', usuario: { idCliente: 9, nombre: 'Rosa', apellido: 'Pino', telefono: '912345678' } }));

beforeEach(() => vi.clearAllMocks());
afterEach(() => act(() => almacenSesion.cerrar()));

const etiquetas = (menu) => menu.flatMap((g) => g.entradas.map((e) => e.etiqueta));

describe('menú del panel del taller por rol', () => {
  it.each([['dueño', true], ['administrador', true], ['diseñador', false], ['operario', false]])('%s ve «Empleados»: %s', (rol, ve) => {
    const tieneRol = (roles) => roles.includes(rol);
    expect(etiquetas(menuPara(tieneRol)).includes('Empleados')).toBe(ve);
  });

  it('«Empleados» lleva a /panel/empleados y es solo de gestión', () => {
    const entrada = menuPara(() => true).flatMap((g) => g.entradas).find((e) => e.etiqueta === 'Empleados');
    expect(entrada.ruta).toBe('/panel/empleados');
    expect(entrada.roles).toEqual(GESTION);
  });

  it('el menú de la cuenta del cliente tiene las tres opciones', () => {
    expect(MENU_CUENTA.flatMap((g) => g.entradas).map((e) => [e.etiqueta, e.ruta])).toEqual([
      ['Mis solicitudes', '/cuenta/solicitudes'], ['Solicitar cotización', '/cuenta/solicitud'], ['Mi cuenta', '/cuenta'],
    ]);
  });
});

function montar(elemento, entrada) {
  return render(
    <ProveedorAvisos>
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          {elemento}
          <Route path="/" element={<p>Página principal</p>} />
          <Route path="/personal/ingresar" element={<p>Ingreso del personal</p>} />
        </Routes>
      </MemoryRouter>
    </ProveedorAvisos>,
  );
}

describe('estructura del panel del taller', () => {
  const panel = () => montar(<Route path="/panel" element={<EstructuraPanel />}><Route index element={<h1>Inicio del panel</h1>} /></Route>, '/panel');

  it('el menú lateral trae «Empleados» para el dueño y el pie anclado con volver y cerrar sesión', () => {
    entrarComoPersonal('dueño');
    panel();

    const menu = screen.getAllByRole('navigation', { name: 'Panel del taller' })[0];
    expect(within(menu).getByRole('link', { name: 'Empleados' })).toHaveAttribute('href', '/panel/empleados');
    expect(screen.getAllByRole('link', { name: 'Volver a la página principal' })[0]).toHaveAttribute('href', '/');
    expect(screen.getAllByRole('button', { name: 'Cerrar sesión' }).length).toBeGreaterThan(0);
    expect(screen.queryByText('Salir')).not.toBeInTheDocument();
  });

  it('el operario no ve «Empleados»', () => {
    entrarComoPersonal('operario');
    panel();

    expect(within(screen.getAllByRole('navigation', { name: 'Panel del taller' })[0]).queryByRole('link', { name: 'Empleados' })).not.toBeInTheDocument();
  });

  it('«Cerrar sesión» cierra la sesión y lleva al ingreso del personal', async () => {
    entrarComoPersonal('dueño');
    panel();

    fireEvent.click(screen.getAllByRole('button', { name: 'Cerrar sesión' })[0]);

    expect(await screen.findByText('Ingreso del personal')).toBeInTheDocument();
    await waitFor(() => expect(authService.salir).toHaveBeenCalled());
  });

  it('el panel del taller no lleva la zona animada ni el fondo de la cuenta', () => {
    entrarComoPersonal('dueño');
    panel();

    expect(document.querySelector('[data-animar]')).toBeNull();
  });
});

describe('estructura de la cuenta del cliente', () => {
  const cliente = () => montar(<Route path="/cuenta" element={<EstructuraCuenta />}><Route index element={<h1>Mi cuenta</h1>} /></Route>, '/cuenta');

  it('el menú trae sus tres opciones y el pie con volver y cerrar sesión', () => {
    entrarComoCliente();
    cliente();

    const menu = screen.getAllByRole('navigation', { name: 'Mi cuenta' })[0];
    expect(within(menu).getByRole('link', { name: 'Mis solicitudes' })).toHaveAttribute('href', '/cuenta/solicitudes');
    expect(within(menu).getByRole('link', { name: 'Solicitar cotización' })).toHaveAttribute('href', '/cuenta/solicitud');
    expect(within(menu).getByRole('link', { name: 'Mi cuenta' })).toHaveAttribute('href', '/cuenta');
    expect(screen.getAllByRole('link', { name: 'Volver a la página principal' })[0]).toHaveAttribute('href', '/');
    expect(screen.getAllByRole('button', { name: 'Cerrar sesión' }).length).toBeGreaterThan(0);
  });

  it('esta zona sí lleva los botones animados y el fondo', () => {
    entrarComoCliente();
    cliente();

    expect(document.querySelector('[data-animar]')).not.toBeNull();
  });

  it('«Cerrar sesión» lleva a la página principal y cierra la sesión', async () => {
    entrarComoCliente();
    cliente();

    fireEvent.click(screen.getAllByRole('button', { name: 'Cerrar sesión' })[0]);

    expect(await screen.findByText('Página principal')).toBeInTheDocument();
    await waitFor(() => expect(authService.salir).toHaveBeenCalled());
  });
});

describe('«Mi cuenta» sin los accesos que pasaron al menú', () => {
  it('queda sin «Mis solicitudes», «Solicitar una cotización» ni «Cerrar sesión»; conserva sus formularios', async () => {
    entrarComoCliente();
    cuenta.obtener.mockResolvedValue({
      idCliente: 9, nombre: 'Rosa', apellido: 'Pino', rut: null, telefono: '912345678', correo: 'rosa@correo.test', correoVerificado: true,
      direccion: null, recibeAvisosEstado: true, recibeMarketing: false,
    });
    montar(<Route path="/cuenta" element={<MiCuenta />} />, '/cuenta');

    expect(await screen.findByRole('heading', { name: 'Tus datos' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Mis solicitudes/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Solicitar una cotización/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cerrar sesión' })).not.toBeInTheDocument();
  });
});

describe('etiquetas de origen', () => {
  it('«En línea» y «Presencial» llevan texto e iconos distintos', () => {
    const { container } = render(<><OrigenSolicitud origen="online" /><OrigenSolicitud origen="presencial" /></>);

    expect(screen.getByText('En línea')).toBeInTheDocument();
    expect(screen.getByText('Presencial')).toBeInTheDocument();
    const iconos = [...container.querySelectorAll('svg')].map((s) => s.getAttribute('class'));
    expect(iconos).toHaveLength(2);
    expect(iconos[0]).not.toBe(iconos[1]);
  });

  it('un origen desconocido se muestra tal cual, sin inventar', () => {
    render(<OrigenSolicitud origen="telefono" />);
    expect(screen.getByText('telefono')).toBeInTheDocument();
  });
});

describe('fila corta de «Solicitudes por atender»', () => {
  it('el producto va solo o con «y N más»', async () => {
    const real = resumenProducto;
    expect(real({ primerProducto: 'Banners', cantidadItems: 1 })).toBe('Banners');
    expect(real({ primerProducto: 'Banners', cantidadItems: 3 })).toBe('Banners y 2 más');
    expect(real({ primerProducto: null, cantidadItems: 2 })).toBe('2 letreros');
    expect(real({ primerProducto: null, cantidadItems: 0 })).toBe('');
  });
});

describe('inicio del panel: dos columnas', () => {
  const inicio = () => montar(<Route path="/panel" element={<InicioPanel />} />, '/panel');

  it('la gestión ve las solicitudes y los indicadores en la misma sección, con las solicitudes primero', async () => {
    entrarComoPersonal('dueño');
    inicio();

    const solicitudes = await screen.findByLabelText('Solicitudes por atender');
    const indicadores = screen.getByLabelText('Indicadores del mes');
    expect(solicitudes.parentElement).toBe(indicadores.parentElement);
    expect(solicitudes.compareDocumentPosition(indicadores) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('el diseñador y el operario ven solo las solicitudes, a todo el ancho', async () => {
    entrarComoPersonal('diseñador');
    inicio();

    await screen.findByLabelText('Solicitudes por atender');
    expect(screen.queryByLabelText('Indicadores del mes')).not.toBeInTheDocument();
  });

  it('el menú general ofrece la tarjeta «Empleados» solo a la gestión', async () => {
    entrarComoPersonal('administrador');
    inicio();
    expect(await screen.findByRole('link', { name: 'Empleados' })).toHaveAttribute('href', '/panel/empleados');
  });
});

// ── Editor de medidas y opciones ──

const PRODUCTO = {
  idProducto: 5, nombre: 'Roll Up', rowVersion: 'v1',
  anchoMinMm: 800, anchoMaxMm: 1500, altoMinMm: 2000, altoMaxMm: 3000, medidasFijas: [],
  opciones: [{ grupo: 'Diseño', valor: 'Del cliente' }, { grupo: 'Diseño', valor: 'Desarrollado por la oficina' }],
};

function editor(producto = PRODUCTO, alGuardar = vi.fn()) {
  render(<ProveedorAvisos><MedidasYOpciones registro={producto} alGuardar={alGuardar} /></ProveedorAvisos>);
  return alGuardar;
}

describe('editor de medidas y opciones', () => {
  it('muestra los límites en centímetros y las opciones por grupo', () => {
    editor();

    expect(screen.getByRole('textbox', { name: /Ancho mínimo/ })).toHaveValue('80');
    expect(screen.getByRole('textbox', { name: /Alto máximo/ })).toHaveValue('300');
    expect(screen.getByRole('textbox', { name: 'Nombre del grupo 1' })).toHaveValue('Diseño');
    expect(screen.getByRole('textbox', { name: 'Valor 2 de Diseño' })).toHaveValue('Desarrollado por la oficina');
  });

  it('guarda en milímetros, con la versión vista, y recarga la ficha', async () => {
    productos.editarMedidasYOpciones.mockResolvedValue({});
    const alGuardar = editor();

    fireEvent.change(screen.getByRole('textbox', { name: /Ancho mínimo/ }), { target: { value: '70,5' } });
    fireEvent.click(screen.getByRole('button', { name: /^Agregar valor/ }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Valor 3 de Diseño' }), { target: { value: 'Mixto' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar medidas y opciones' }));

    await waitFor(() => expect(productos.editarMedidasYOpciones).toHaveBeenCalledWith(5, {
      anchoMinMm: 705, anchoMaxMm: 1500, altoMinMm: 2000, altoMaxMm: 3000, medidasFijas: [],
      opciones: [{ grupo: 'Diseño', valores: ['Del cliente', 'Desarrollado por la oficina', 'Mixto'] }],
    }, 'v1'));
    await waitFor(() => expect(alGuardar).toHaveBeenCalled());
  });

  it('un mínimo mayor que el máximo se marca en su campo y no se envía', async () => {
    editor();

    fireEvent.change(screen.getByRole('textbox', { name: /Ancho mínimo/ }), { target: { value: '200' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar medidas y opciones' }));

    expect(await screen.findByText('El ancho máximo no puede ser menor que el mínimo.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /Ancho máximo/ })).toHaveAttribute('aria-invalid', 'true');
    expect(productos.editarMedidasYOpciones).not.toHaveBeenCalled();
  });

  it('pasar a medidas fijas muestra la lista y quita los límites; sin ninguna medida no se guarda', async () => {
    editor();

    fireEvent.click(screen.getByRole('radio', { name: /Medidas fijas/ }));
    expect(screen.queryByRole('textbox', { name: /Ancho mínimo/ })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Guardar medidas y opciones' }));
    expect(await screen.findByText(/Agrega al menos una medida fija/)).toBeInTheDocument();
    expect(productos.editarMedidasYOpciones).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Agregar medida' }));
    fireEvent.change(screen.getByRole('textbox', { name: /Ancho de la medida 1/ }), { target: { value: '80' } });
    fireEvent.change(screen.getByRole('textbox', { name: /Alto de la medida 1/ }), { target: { value: '200' } });
    productos.editarMedidasYOpciones.mockResolvedValue({});
    fireEvent.click(screen.getByRole('button', { name: 'Guardar medidas y opciones' }));

    await waitFor(() => expect(productos.editarMedidasYOpciones).toHaveBeenCalledWith(5, expect.objectContaining({
      anchoMinMm: null, medidasFijas: [{ anchoMm: 800, altoMm: 2000 }],
    }), 'v1'));
  });

  it('un grupo sin valores y un valor repetido se rechazan nombrando el grupo', async () => {
    editor();

    fireEvent.click(screen.getByRole('button', { name: 'Agregar grupo' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Nombre del grupo 2' }), { target: { value: 'Material' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar medidas y opciones' }));
    expect(await screen.findByText(/El grupo «Material» necesita al menos un valor/)).toBeInTheDocument();

    fireEvent.change(screen.getByRole('textbox', { name: 'Valor 1 de Material' }), { target: { value: 'A' } });
    fireEvent.click(screen.getAllByRole('button', { name: /^Agregar valor/ })[1]);
    fireEvent.change(screen.getByRole('textbox', { name: 'Valor 2 de Material' }), { target: { value: ' a ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar medidas y opciones' }));
    expect(await screen.findByText(/el valor «a» está repetido/)).toBeInTheDocument();
    expect(productos.editarMedidasYOpciones).not.toHaveBeenCalled();
  });

  it('si la API devuelve errores por campo, los muestra junto a su campo', async () => {
    productos.editarMedidasYOpciones.mockRejectedValue(new ErrorApi({
      estado: 400, codigo: 'datos_invalidos', mensaje: 'Revisa los datos.', errores: { anchoMaxMm: ['El ancho máximo debe estar entre 0,1 y 5000 cm.'] },
    }));
    editor();

    fireEvent.click(screen.getByRole('button', { name: 'Guardar medidas y opciones' }));

    expect(await screen.findByText('El ancho máximo debe estar entre 0,1 y 5000 cm.')).toBeInTheDocument();
  });

  it('si otra persona lo cambió, avisa y recarga la versión vigente', async () => {
    productos.editarMedidasYOpciones.mockRejectedValue(new ErrorApi({ estado: 409, codigo: 'version_obsoleta', mensaje: 'Otra persona lo modificó.' }));
    const alGuardar = editor();

    fireEvent.click(screen.getByRole('button', { name: 'Guardar medidas y opciones' }));

    expect(await screen.findByText(/Otra persona modificó este tipo de letrero/)).toBeInTheDocument();
    expect(alGuardar).toHaveBeenCalled();
  });
});
