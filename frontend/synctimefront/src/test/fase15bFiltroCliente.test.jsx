import { act, fireEvent, render, screen } from '@testing-library/react';
import { StrictMode, useState } from 'react';
import { api, ErrorApi } from '../api/cliente';
import { indicadores } from '../api/indicadores';
import { reportes } from '../api/reportes';
import { FiltroCliente } from '../paginas/indicadores/FiltrosIndicadores';

// Fase 15B (RFU27): el filtro «Cliente» de indicadores y reportes busca en el servidor (GET /clientes?q=&tamano=10) con debounce,
// en vez de traer los primeros 100 clientes y filtrarlos en el navegador.
const ESPERA = 300;
const llamadas = [];
const cambios = [];

const cliente = (idCliente, nombre, apellido = '', telefono = '9000000' + idCliente, rut = null) => ({ idCliente, nombre, apellido, telefono, rut });
const CLIENTES = [cliente(7, 'Ana', 'Pérez'), cliente(8, 'Luis', 'Soto', '912345678', '12.345.678-5'), cliente(31, 'Ana', 'Pérez')];

function Armado({ inicial = '' }) {
  const [valor, setValor] = useState(inicial);
  return (
    <>
      <FiltroCliente valor={valor} alCambiar={(nuevo) => { cambios.push(nuevo); setValor(nuevo); }} />
      <output data-testid="valor">{valor}</output>
    </>
  );
}

const campo = () => screen.getByLabelText('Cliente');
const escribir = (texto) => fireEvent.change(campo(), { target: { value: texto } });
const avanzar = (ms) => act(() => vi.advanceTimersByTimeAsync(ms));
const opciones = () => [...document.querySelectorAll('datalist option')].map((o) => o.getAttribute('value'));

beforeEach(() => {
  vi.useFakeTimers();
  llamadas.length = 0;
  cambios.length = 0;
  vi.spyOn(api, 'get').mockImplementation(async (ruta, { parametros } = {}) => {
    llamadas.push({ ruta, parametros });
    if (ruta === '/clientes') return { elementos: CLIENTES.filter((c) => `${c.nombre} ${c.apellido}`.toLowerCase().includes(String(parametros.q).toLowerCase())) };
    if (ruta.startsWith('/clientes/')) return CLIENTES.find((c) => String(c.idCliente) === ruta.split('/')[2]);
    throw new Error(`Ruta sin simular: ${ruta}`);
  });
});
afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('el filtro «Cliente» busca en el servidor (RFU27)', () => {
  it('al abrir la pantalla no pide la lista de clientes', async () => {
    render(<Armado />);
    await avanzar(2000);
    expect(llamadas).toEqual([]);
  });

  it('pide una sola búsqueda, con el texto y diez resultados, cuando se deja de escribir', async () => {
    render(<Armado />);
    escribir('a');
    await avanzar(100);
    escribir('an');
    await avanzar(100);
    escribir('ana');
    await avanzar(ESPERA - 1);
    expect(llamadas).toEqual([]);
    await avanzar(1);
    expect(llamadas).toEqual([{ ruta: '/clientes', parametros: { q: 'ana', tamano: 10 } }]);
  });

  it('con menos de dos letras no busca', async () => {
    render(<Armado />);
    escribir('a');
    await avanzar(2000);
    expect(llamadas).toEqual([]);
  });

  it('muestra los resultados en la lista y distingue a dos clientes con el mismo nombre', async () => {
    render(<Armado />);
    escribir('ana');
    await avanzar(ESPERA);
    const textos = opciones();
    expect(textos).toHaveLength(2);
    expect(new Set(textos).size).toBe(2);
    expect(textos.every((t) => t.startsWith('Ana Pérez'))).toBe(true);
    expect(textos.some((t) => t.includes('N.º 7'))).toBe(true);
    expect(textos.some((t) => t.includes('N.º 31'))).toBe(true);
  });

  it('elegir un resultado aplica el filtro con su número; escribir sin elegir no lo aplica', async () => {
    render(<Armado />);
    escribir('lui');
    await avanzar(ESPERA);
    expect(cambios).toEqual([]);
    escribir('Luis Soto');
    expect(cambios).toEqual(['8']);
    expect(screen.getByTestId('valor')).toHaveTextContent('8');
    expect(campo()).toHaveValue('Luis Soto');
  });

  it('una respuesta que llega tarde de una búsqueda anterior se descarta y la anterior se cancela', async () => {
    const pendientes = [];
    api.get.mockImplementation((ruta, { parametros, senal }) => new Promise((resolver, rechazar) => {
      pendientes.push({ q: parametros.q, resolver, senal });
      senal?.addEventListener('abort', () => rechazar(new DOMException('Cancelada', 'AbortError')));
    }));
    render(<Armado />);
    escribir('an');
    await avanzar(ESPERA);
    escribir('lui');
    await avanzar(ESPERA);
    expect(pendientes.map((p) => p.q)).toEqual(['an', 'lui']);
    expect(pendientes[0].senal.aborted).toBe(true);
    await act(async () => { pendientes[1].resolver({ elementos: [cliente(8, 'Luis', 'Soto')] }); });
    await act(async () => { pendientes[0].resolver({ elementos: [cliente(7, 'Ana', 'Pérez')] }); });
    expect(opciones()).toEqual(['Luis Soto']);
  });

  it('si la búsqueda falla lo dice con claridad y no deja la pantalla trabada', async () => {
    api.get.mockRejectedValue(new ErrorApi({ estado: 500, codigo: 'servidor', mensaje: 'El servidor tuvo un problema.' }));
    render(<Armado />);
    escribir('ana');
    await avanzar(ESPERA);
    expect(screen.getByText('No pudimos buscar clientes. Inténtalo de nuevo.')).toBeInTheDocument();
    expect(campo()).toHaveAttribute('aria-invalid', 'true');
    api.get.mockResolvedValue({ elementos: [cliente(8, 'Luis', 'Soto')] });
    escribir('luis');
    await avanzar(ESPERA);
    expect(screen.queryByText('No pudimos buscar clientes. Inténtalo de nuevo.')).not.toBeInTheDocument();
    expect(opciones()).toEqual(['Luis Soto']);
  });

  it('con un cliente que viene de la dirección pide su nombre una vez; si no lo encuentra muestra «Cliente N.º»', async () => {
    const { unmount } = render(<StrictMode><Armado inicial="8" /></StrictMode>);
    await avanzar(10);
    expect(campo()).toHaveValue('Luis Soto');
    expect(screen.getByRole('button', { name: 'Quitar el filtro por cliente: Luis Soto' })).toBeInTheDocument();
    unmount();

    api.get.mockRejectedValue(new ErrorApi({ estado: 404, codigo: 'no_encontrado', mensaje: 'No encontramos lo que buscas.' }));
    render(<Armado inicial="9" />);
    await avanzar(10);
    expect(campo()).toHaveValue('Cliente N.º 9');
    expect(screen.getByRole('button', { name: 'Quitar el filtro por cliente: Cliente N.º 9' })).toBeInTheDocument();
  });

  it('«Quitar» limpia el filtro y devuelve el foco al campo', async () => {
    render(<Armado inicial="8" />);
    await avanzar(10);
    fireEvent.click(screen.getByRole('button', { name: /Quitar el filtro por cliente/ }));
    expect(cambios).toEqual(['']);
    expect(campo()).toHaveFocus();
    expect(campo()).toHaveValue('');
  });
});

describe('lo que anuncia y lo que ofrece el filtro (revisiones de la 15B)', () => {
  const estado = () => screen.getAllByRole('status').map((e) => e.textContent).join(' | ');

  it('anuncia cuántos clientes encontró y cuándo no hay coincidencias', async () => {
    render(<Armado />);
    escribir('ana');
    await avanzar(ESPERA);
    expect(estado()).toContain('2 clientes encontrados');
    escribir('zzz');
    await avanzar(ESPERA);
    expect(estado()).toContain('Sin coincidencias');
  });

  it('anuncia que se aplicó el filtro y que se quitó, sin mover el foco del campo', async () => {
    render(<Armado />);
    escribir('lui');
    await avanzar(ESPERA);
    escribir('Luis Soto');
    expect(estado()).toContain('Filtro por cliente: Luis Soto');
    fireEvent.click(screen.getByRole('button', { name: /Quitar el filtro por cliente/ }));
    expect(estado()).toContain('Se quitó el filtro por cliente');
  });

  it('la lista muestra el teléfono y el RUT para poder buscar por ellos', async () => {
    render(<Armado />);
    escribir('lui');
    await avanzar(ESPERA);
    const opcion = document.querySelector('datalist option');
    expect(opcion.getAttribute('label')).toContain('912345678');
    expect(opcion.getAttribute('label')).toContain('12.345.678-5');
  });

  it('tras «Quitar» la lista queda vacía y no ofrece las sugerencias de antes', async () => {
    render(<Armado />);
    escribir('lui');
    await avanzar(ESPERA);
    escribir('Luis Soto');
    fireEvent.click(screen.getByRole('button', { name: /Quitar el filtro por cliente/ }));
    await avanzar(ESPERA);
    expect(opciones()).toEqual([]);
  });

  it('al elegir entre dos clientes con el mismo nombre, el campo queda con el nombre limpio', async () => {
    render(<Armado />);
    escribir('ana');
    await avanzar(ESPERA);
    escribir(opciones().find((t) => t.includes('N.º 31')));
    expect(cambios).toEqual(['31']);
    expect(campo()).toHaveValue('Ana Pérez');
  });

  it('volver a escribir la misma búsqueda tras un error no muestra el error viejo mientras llega la respuesta nueva', async () => {
    api.get.mockRejectedValueOnce(new ErrorApi({ estado: 500, codigo: 'servidor', mensaje: 'x' }));
    render(<Armado />);
    escribir('ana');
    await avanzar(ESPERA);
    expect(screen.getByText('No pudimos buscar clientes. Inténtalo de nuevo.')).toBeInTheDocument();
    escribir('an');
    await avanzar(ESPERA);
    api.get.mockImplementation(() => new Promise(() => {}));
    escribir('ana');
    await avanzar(ESPERA);
    expect(screen.queryByText('No pudimos buscar clientes. Inténtalo de nuevo.')).not.toBeInTheDocument();
  });
});

describe('ya no se trae la lista de 100 clientes (RFU27)', () => {
  it('los catálogos de indicadores piden solo las categorías', async () => {
    const get = vi.spyOn(api, 'get').mockResolvedValue({ elementos: [{ idCategoria: 1, nombre: 'Letreros' }] });
    const catalogos = await indicadores.catalogos();
    expect(get.mock.calls.map(([ruta]) => ruta)).toEqual(['/categorias']);
    expect(catalogos).toEqual({ categorias: [{ idCategoria: 1, nombre: 'Letreros' }] });
  });

  it('los reportes ya no tienen una lista de clientes para el filtro', () => {
    expect(reportes.clientes).toBeUndefined();
  });
});
