import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { publico } from '../api/publico';
import { EstructuraPublica } from '../estructuras/EstructuraPublica';
import { CartelGrande } from '../paginas/publicas/cartel/CartelLocal';
import Consultas from '../paginas/publicas/Consultas';
import Portada from '../paginas/publicas/Portada';
import SelectorIngreso from '../paginas/publicas/SelectorIngreso';
import { Vitrina } from '../paginas/publicas/vitrina/Vitrina';
import { almacenSesion } from '../sesion/sesion';
import { simularMovimientoReducido } from './rostroAuxiliares';

// Fase 12C: barra, cartel, consultas, vitrina e ingreso del portal público.
vi.mock('../api/publico', () => ({
  publico: {
    estadoLocal: vi.fn(),
    categorias: vi.fn(),
    catalogoCompleto: vi.fn(),
    atencion: vi.fn(() => Promise.resolve([])),
    documentosLegales: vi.fn(() => Promise.resolve([{ tipo: 'privacidad', version: '1.0' }, { tipo: 'cookies', version: '1.0' }])),
    enviarContacto: vi.fn(),
    aceptarCookies: vi.fn(),
    retirarCookies: vi.fn(),
  },
}));

const SEMANA = [1, 2, 3, 4, 5, 6, 7].map((d) => ({
  diaSemana: d, nombreDia: ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'][d - 1],
  abierto: d < 7, horaInicio: d < 7 ? '09:00:00' : null, horaFin: d < 7 ? '18:00:00' : null,
}));
const ESTADO_CERRADO = {
  abiertoAhora: false, motivo: 'fuera_de_horario', dentroDeHorario: false, horaChile: '2026-11-20T07:30:00',
  hoy: SEMANA[4], proximaApertura: { diaSemana: 5, nombreDia: 'Viernes', hora: '09:00:00', esHoy: true },
  cartel: { abiertoEnLocal: false, mensajeEstado: 'Cerrado', fechaUltimaModificacion: '2026-11-19T18:00:00' }, semana: SEMANA,
};
const ESTADO_ABIERTO = {
  ...ESTADO_CERRADO, abiertoAhora: true, motivo: 'abierto', dentroDeHorario: true, proximaApertura: null,
  cartel: { abiertoEnLocal: true, mensajeEstado: 'En el taller ahora.', fechaUltimaModificacion: '2026-11-20T09:00:00' },
};
const trabajo = (id, titulo, categoria, tipoLetrero = 'Letrero') => ({
  idCatalogo: id, titulo, tipoLetrero, idProducto: id, descripcion: null, precioReferencial: 0, imagen: null,
  categoria: { idCategoria: categoria === 'Vinilos' ? 2 : 1, nombre: categoria },
});
const TRABAJOS = [
  trabajo(1, 'Letrero luminoso para panadería', 'Luminosos', 'Caja de luz'),
  trabajo(2, 'Señalización de bodega', 'Luminosos'),
  trabajo(3, 'Vinilo para vitrina', 'Vinilos', 'Vinilo de corte'),
];
const CATEGORIAS = [{ idCategoria: 1, nombre: 'Luminosos', activo: true }, { idCategoria: 2, nombre: 'Vinilos', activo: true }];

function Ruta({ nombre }) {
  const { state, pathname } = useLocation();
  return <p>{nombre} {pathname} {state?.desde?.pathname ?? ''}</p>;
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false, fecha: new Date().toISOString() }));
  localStorage.removeItem('synctime.cartel.movimiento');
  almacenSesion.cerrar();
  publico.estadoLocal.mockResolvedValue(ESTADO_CERRADO);
  publico.categorias.mockResolvedValue(CATEGORIAS);
  publico.catalogoCompleto.mockResolvedValue(TRABAJOS);
});

function estructura(entrada = '/') {
  return render(
    <MemoryRouter initialEntries={[entrada]}>
      <Routes>
        <Route element={<EstructuraPublica />}>
          <Route index element={<Ruta nombre="Portada" />} />
          <Route path="consultas" element={<Ruta nombre="Consultas" />} />
          <Route path="ingresar" element={<Ruta nombre="Ingresar" />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe('barra superior', () => {
  it('quita «Cómo llegar» y «Contacto»: quedan Trabajos, Horario y Consultas', async () => {
    estructura();
    const menu = screen.getByRole('navigation', { name: 'Principal' });
    expect(within(menu).getByRole('link', { name: 'Trabajos' })).toBeInTheDocument();
    expect(within(menu).getByRole('link', { name: 'Horario' })).toBeInTheDocument();
    expect(within(menu).getByRole('link', { name: 'Consultas' })).toHaveAttribute('href', '/consultas');
    expect(within(menu).queryByRole('link', { name: 'Cómo llegar' })).not.toBeInTheDocument();
    expect(within(menu).queryByRole('link', { name: 'Contacto' })).not.toBeInTheDocument();
    await screen.findByText('Cerrado');
  });

  it('«Ingresar» lleva el icono de usuario y «Solicitar cotización» es el botón principal', async () => {
    estructura();
    const ingresar = screen.getByRole('link', { name: 'Ingresar' });
    expect(ingresar).toHaveAttribute('href', '/ingresar');
    expect(ingresar.querySelector('svg.lucide-user-round')).not.toBeNull();
    const solicitar = screen.getByRole('link', { name: 'Solicitar cotización' });
    expect(solicitar.className).toMatch(/primario/);
    expect(ingresar.className).toMatch(/secundario/);
    await screen.findByText('Cerrado');
  });

  it('la zona pública es animada (los botones del panel del taller no lo son)', () => {
    const { container } = estructura();
    expect(container.querySelector('[data-animar]')).not.toBeNull();
  });

  it('el cartel pequeño de la esquina muestra el estado del local', async () => {
    estructura();
    expect(await screen.findByText('Cerrado')).toBeInTheDocument();
    publico.estadoLocal.mockResolvedValue(ESTADO_ABIERTO);
  });

  describe('al tocar la marca, «Consultas» o «Ingresar» vuelve arriba', () => {
    let desplazar;
    beforeEach(() => { desplazar = vi.spyOn(window, 'scrollTo').mockImplementation(() => {}); });
    afterEach(() => { desplazar.mockRestore(); });

    it('la marca lleva arriba desde otra página y también desde la misma', async () => {
      estructura('/consultas');
      expect(desplazar).not.toHaveBeenCalled(); // la primera carga no mueve la página
      fireEvent.click(screen.getAllByRole('link', { name: 'Letreros Herrera' })[0]);
      await waitFor(() => expect(desplazar).toHaveBeenCalledTimes(1));
      expect(desplazar).toHaveBeenLastCalledWith({ top: 0, left: 0, behavior: 'auto' });
      fireEvent.click(screen.getAllByRole('link', { name: 'Letreros Herrera' })[0]); // ya estamos en «/»
      await waitFor(() => expect(desplazar).toHaveBeenCalledTimes(2));
    });

    it('«Consultas» e «Ingresar» también, incluso estando ya en ellas', async () => {
      estructura('/consultas');
      fireEvent.click(within(screen.getByRole('navigation', { name: 'Principal' })).getByRole('link', { name: 'Consultas' }));
      await waitFor(() => expect(desplazar).toHaveBeenCalledTimes(1));
      fireEvent.click(screen.getByRole('link', { name: 'Ingresar' }));
      await waitFor(() => expect(desplazar).toHaveBeenCalledTimes(2));
    });

    it('un ancla de la portada (/#vitrina) no sube: baja a su sección', async () => {
      estructura('/consultas');
      fireEvent.click(within(screen.getByRole('navigation', { name: 'Principal' })).getByRole('link', { name: 'Trabajos' }));
      await screen.findByText(/Portada/);
      expect(desplazar).not.toHaveBeenCalled();
    });
  });
});

describe('cartel digital', () => {
  const estado = (datos) => ({ datos, cargando: !datos, error: null, recargar: vi.fn() });

  it('cerrado: letrero rojo con los textos reales adentro y solo «Cerrado»', () => {
    const { container } = render(<CartelGrande estado={estado(ESTADO_CERRADO)} />);
    expect(container.querySelector('[data-estado="cerrado"]')).not.toBeNull();
    expect(container.querySelector('img')).toBeNull();
    expect(screen.getByRole('heading', { level: 2, name: 'En el taller ahora.' })).toBeInTheDocument();
    expect(screen.getByText('Cerrado')).toBeInTheDocument();
    expect(screen.queryByText(/Ausente|Disponible/)).not.toBeInTheDocument();
    expect(screen.getByText('Cerrado. Abrimos hoy a las 9:00.')).toBeInTheDocument();
    expect(screen.getByText(/Cartel actualizado el/)).toBeInTheDocument();
  });

  it('abierto: letrero verde con solo «Abierto» y el mensaje del cartel', () => {
    const { container } = render(<CartelGrande estado={estado(ESTADO_ABIERTO)} />);
    expect(container.querySelector('[data-estado="abierto"]')).not.toBeNull();
    expect(screen.getByText('Abierto')).toBeInTheDocument();
    expect(screen.getByText('En el taller ahora.', { selector: 'span' })).toBeInTheDocument();
  });

  it('el dibujo es decorativo y los textos van en una región viva', () => {
    const { container } = render(<CartelGrande estado={estado(ESTADO_CERRADO)} />);
    expect(container.querySelector('svg[aria-hidden="true"]')).not.toBeNull();
    expect(container.querySelector('[aria-live="polite"][aria-atomic="true"]')).not.toBeNull();
  });

  it('al cambiar el estado cambia el color sin volver a montar el cartel', () => {
    const { container, rerender } = render(<CartelGrande estado={estado(ESTADO_CERRADO)} />);
    const cartel = container.firstChild;
    rerender(<CartelGrande estado={estado(ESTADO_ABIERTO)} />);
    expect(container.firstChild).toBe(cartel);
    expect(container.querySelector('[data-estado="abierto"]')).not.toBeNull();
  });

  it('si el estado no carga, lo dice y permite reintentar', () => {
    const recargar = vi.fn();
    render(<CartelGrande estado={{ datos: undefined, cargando: false, error: new Error('x'), recargar }} />);
    fireEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(recargar).toHaveBeenCalled();
  });

  describe('balanceo continuo', () => {
    afterEach(() => { delete window.matchMedia; });
    const grande = (container) => container.firstChild;
    const boton = (nombre) => screen.getByRole('button', { name: nombre });

    it('por defecto se balancea y un botón permite pausarlo (WCAG 2.2.2)', () => {
      const { container } = render(<CartelGrande estado={estado(ESTADO_CERRADO)} />);
      expect(grande(container)).toHaveAttribute('data-movimiento', 'si');
      fireEvent.click(boton('Pausar el balanceo'));
      expect(grande(container)).toHaveAttribute('data-movimiento', 'no');
      expect(boton('Balancear el cartel')).toBeInTheDocument();
    });

    it('con «reducir movimiento» en el sistema arranca quieto, pero la persona puede encenderlo', () => {
      simularMovimientoReducido(true);
      const { container } = render(<CartelGrande estado={estado(ESTADO_CERRADO)} />);
      expect(grande(container)).toHaveAttribute('data-movimiento', 'no');
      fireEvent.click(boton('Balancear el cartel'));
      expect(grande(container)).toHaveAttribute('data-movimiento', 'si');
    });

    it('recuerda la elección en el navegador', () => {
      const primera = render(<CartelGrande estado={estado(ESTADO_CERRADO)} />);
      fireEvent.click(boton('Pausar el balanceo'));
      expect(localStorage.getItem('synctime.cartel.movimiento')).toBe('no');
      primera.unmount();
      const segunda = render(<CartelGrande estado={estado(ESTADO_CERRADO)} />);
      expect(grande(segunda.container)).toHaveAttribute('data-movimiento', 'no');
    });

    it('si el navegador no deja guardar, igual alterna', () => {
      const guardar = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('lleno'); });
      const { container } = render(<CartelGrande estado={estado(ESTADO_CERRADO)} />);
      fireEvent.click(boton('Pausar el balanceo'));
      expect(grande(container)).toHaveAttribute('data-movimiento', 'no');
      guardar.mockRestore();
    });
  });

  it('el botón flotante de WhatsApp lleva la imagen del taller, no el icono genérico', () => {
    const { container } = estructura();
    const flotante = container.querySelector('a[href*="wa.me"][class*="whatsapp"]');
    expect(flotante.querySelector('img').getAttribute('src')).toBe('/imagenes/icono-whatsapp.webp');
    expect(flotante.querySelector('svg')).toBeNull();
  });
});

describe('Consultas', () => {
  it('es una pantalla propia con el formulario «Escríbenos»', () => {
    render(<MemoryRouter><Consultas /></MemoryRouter>);
    expect(screen.getByRole('heading', { level: 1, name: 'Escríbenos' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Enviar consulta' })).toBeInTheDocument();
    expect(screen.getByLabelText(/^Mensaje/)).toBeInTheDocument();
  });

  it('el icono de WhatsApp es una imagen decorativa y el botón conserva su nombre', () => {
    render(<MemoryRouter><Consultas /></MemoryRouter>);
    const boton = screen.getByRole('link', { name: /Escribir por WhatsApp/ });
    const icono = boton.querySelector('img');
    expect(icono.getAttribute('src')).toBe('/imagenes/icono-whatsapp.webp');
    expect(icono.getAttribute('alt')).toBe('');
  });
});

describe('portada', () => {
  function portada(entrada = '/') {
    return render(
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          <Route element={<EstructuraPublica />}>
            <Route index element={<Portada />} />
            <Route path="consultas" element={<Ruta nombre="Consultas" />} />
          </Route>
        </Routes>
      </MemoryRouter>,
    );
  }

  it('el formulario sale de la portada: solo los canales directos y un botón a /consultas', async () => {
    portada();
    const escribir = await screen.findByRole('link', { name: 'Escribir una consulta' });
    expect(escribir).toHaveAttribute('href', '/consultas');
    expect(screen.queryByRole('button', { name: 'Enviar consulta' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: /Escribir por WhatsApp/ }).length).toBeGreaterThan(0);
  });

  it('el enlace antiguo /#contacto lleva a /consultas', async () => {
    portada('/#contacto');
    expect(await screen.findByText(/Consultas \/consultas/)).toBeInTheDocument();
  });

  it('muestra el cartel grande en el lugar del anterior', async () => {
    portada();
    expect(await screen.findByText('Cerrado. Abrimos hoy a las 9:00.')).toBeInTheDocument();
  });
});

describe('vitrina: búsqueda, categorías y contador', () => {
  async function mostrar() {
    render(<MemoryRouter><Vitrina /></MemoryRouter>);
    await screen.findByRole('heading', { level: 3, name: 'Letrero luminoso para panadería' });
  }
  const tarjetas = () => screen.getAllByRole('heading', { level: 3 }).map((h) => h.textContent);

  it('al escribir filtra al instante, sin tildes, y anuncia cuántos trabajos hay', async () => {
    await mostrar();
    expect(screen.getByRole('status')).toHaveTextContent('3 trabajos');
    fireEvent.change(screen.getByRole('searchbox', { name: /Buscar/ }), { target: { value: 'senal' } });
    expect(tarjetas()).toEqual(['Señalización de bodega']);
    expect(screen.getByRole('status')).toHaveTextContent('1 trabajo');
    // Se filtra en el navegador: no hay consultas nuevas por cada letra.
    expect(publico.catalogoCompleto).toHaveBeenCalledTimes(1);
  });

  it('la categoría es una lista desplegable con «Todas» y se combina con la búsqueda', async () => {
    await mostrar();
    const lista = screen.getByRole('combobox', { name: 'Categoría' });
    expect(within(lista).getByRole('option', { name: 'Todas' })).toBeInTheDocument();
    fireEvent.change(lista, { target: { value: '2' } });
    expect(tarjetas()).toEqual(['Vinilo para vitrina']);
    fireEvent.change(screen.getByRole('searchbox', { name: /Buscar/ }), { target: { value: 'panaderia' } });
    expect(screen.getByRole('heading', { level: 3, name: 'No hay trabajos con esa búsqueda' })).toBeInTheDocument();
  });

  it('sin resultados lo dice y «Limpiar» devuelve todos', async () => {
    await mostrar();
    fireEvent.change(screen.getByRole('searchbox', { name: /Buscar/ }), { target: { value: 'zzz' } });
    expect(screen.getByRole('status')).toHaveTextContent('0 trabajos');
    fireEvent.click(screen.getByRole('button', { name: 'Limpiar' }));
    expect(tarjetas()).toHaveLength(3);
    expect(screen.getByRole('searchbox', { name: /Buscar/ })).toHaveValue('');
    expect(screen.getByRole('searchbox', { name: /Buscar/ })).toHaveFocus();
  });

  it('con la vitrina vacía no ofrece buscar y explica que aún no hay trabajos', async () => {
    publico.catalogoCompleto.mockResolvedValue([]);
    render(<MemoryRouter><Vitrina /></MemoryRouter>);
    expect(await screen.findByText('Aún no hay trabajos publicados')).toBeInTheDocument();
    expect(screen.queryByRole('searchbox')).not.toBeInTheDocument();
  });

  it('si no carga, lo dice y permite reintentar', async () => {
    publico.catalogoCompleto.mockRejectedValueOnce(new Error('sin red'));
    render(<MemoryRouter><Vitrina /></MemoryRouter>);
    fireEvent.click(await screen.findByRole('button', { name: 'Reintentar' }));
    expect(await screen.findByRole('heading', { level: 3, name: 'Señalización de bodega' })).toBeInTheDocument();
  });
});

describe('ingreso: ¿quién está iniciando sesión?', () => {
  function selector(entrada) {
    return render(
      <MemoryRouter initialEntries={[entrada]}>
        <Routes>
          <Route path="/ingresar" element={<SelectorIngreso />} />
          <Route path="/ingresar/cliente" element={<Ruta nombre="Ingreso del cliente" />} />
          <Route path="/personal/ingresar" element={<Ruta nombre="Ingreso del personal" />} />
        </Routes>
      </MemoryRouter>,
    );
  }

  it('muestra dos tarjetas-enlace: cliente primero, personal del taller después', () => {
    selector('/ingresar');
    expect(screen.getByRole('heading', { level: 1, name: '¿Quién está iniciando sesión?' })).toBeInTheDocument();
    const enlaces = screen.getAllByRole('link');
    expect(enlaces).toHaveLength(2);
    expect(enlaces[0]).toHaveAccessibleName(/Cliente/);
    expect(enlaces[0]).toHaveAttribute('href', '/ingresar/cliente');
    expect(enlaces[1]).toHaveAccessibleName(/Personal del taller/);
    expect(enlaces[1]).toHaveAttribute('href', '/personal/ingresar');
  });

  it('conserva la dirección a la que volver al elegir cada tarjeta', async () => {
    const desde = { pathname: '/cuenta/solicitud', search: '?producto=2' };
    const { unmount } = render(
      <MemoryRouter initialEntries={[{ pathname: '/ingresar', state: { desde } }]}>
        <Routes>
          <Route path="/ingresar" element={<SelectorIngreso />} />
          <Route path="/ingresar/cliente" element={<Ruta nombre="Ingreso del cliente" />} />
        </Routes>
      </MemoryRouter>,
    );
    fireEvent.click(screen.getByRole('link', { name: /Cliente/ }));
    expect(await screen.findByText(/Ingreso del cliente \/ingresar\/cliente \/cuenta\/solicitud/)).toBeInTheDocument();
    unmount();

    render(
      <MemoryRouter initialEntries={[{ pathname: '/ingresar', state: { desde: { pathname: '/panel/pedidos' } } }]}>
        <Routes>
          <Route path="/ingresar" element={<SelectorIngreso />} />
          <Route path="/personal/ingresar" element={<Ruta nombre="Ingreso del personal" />} />
        </Routes>
      </MemoryRouter>,
    );
    fireEvent.click(screen.getByRole('link', { name: /Personal del taller/ }));
    expect(await screen.findByText(/Ingreso del personal \/personal\/ingresar \/panel\/pedidos/)).toBeInTheDocument();
  });

  it('sin sesión previa los enlaces antiguos siguen mostrando el selector', () => {
    selector('/ingresar');
    expect(screen.getByRole('heading', { name: '¿Quién está iniciando sesión?' })).toBeInTheDocument();
  });
});

describe('estado compartido', () => {
  it('la barra y la portada comparten una sola consulta del estado del local', async () => {
    render(
      <MemoryRouter>
        <Routes>
          <Route element={<EstructuraPublica />}>
            <Route index element={<Portada />} />
          </Route>
        </Routes>
      </MemoryRouter>,
    );
    await screen.findByText('Cerrado. Abrimos hoy a las 9:00.');
    await act(async () => {});
    await waitFor(() => expect(publico.estadoLocal).toHaveBeenCalledTimes(1));
  });
});
