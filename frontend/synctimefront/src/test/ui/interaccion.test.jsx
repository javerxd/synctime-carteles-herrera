import { useState } from 'react';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { BarraFiltros, Dialogo, Paginacion, Pestanas, TablaAdaptable } from '../../componentes/ui';

describe('Dialogo', () => {
  function Prueba({ alCerrarExtra = () => {} }) {
    const [abierto, setAbierto] = useState(false);
    return (
      <>
        <button type="button" onClick={() => setAbierto(true)}>Solicitar este trabajo</button>
        <Dialogo abierto={abierto} titulo="Nueva solicitud" descripcion="Cuéntanos qué necesitas."
          alCerrar={() => { setAbierto(false); alCerrarExtra(); }}>
          <label>Detalle <input /></label>
        </Dialogo>
      </>
    );
  }

  it('se abre con su título como nombre accesible', () => {
    render(<Prueba />);
    fireEvent.click(screen.getByRole('button', { name: 'Solicitar este trabajo' }));
    const dialogo = screen.getByRole('dialog', { name: 'Nueva solicitud' });
    expect(dialogo).toHaveAttribute('open');
    expect(dialogo).toHaveAccessibleDescription('Cuéntanos qué necesitas.');
  });

  it('Esc (evento cancel del navegador) lo cierra una sola vez', () => {
    const alCerrar = vi.fn();
    render(<Prueba alCerrarExtra={alCerrar} />);
    fireEvent.click(screen.getByRole('button', { name: 'Solicitar este trabajo' }));
    fireEvent(screen.getByRole('dialog'), new Event('cancel', { cancelable: true }));
    expect(alCerrar).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('cerrado no monta su contenido', () => {
    render(<Prueba />);
    expect(screen.queryByRole('textbox', { name: 'Detalle', hidden: true })).toBeNull();
  });

  it('un clic en el telón lo cierra, pero no si el gesto empezó dentro (al seleccionar texto)', () => {
    const alCerrar = vi.fn();
    render(<Dialogo abierto titulo="Nueva solicitud" alCerrar={alCerrar}><input aria-label="Detalle" /></Dialogo>);
    const dialogo = screen.getByRole('dialog');
    fireEvent.pointerDown(screen.getByRole('textbox', { name: 'Detalle' }));
    fireEvent.click(dialogo);
    expect(alCerrar).not.toHaveBeenCalled();
    fireEvent.pointerDown(dialogo);
    fireEvent.click(dialogo);
    expect(alCerrar).toHaveBeenCalledTimes(1);
  });

  it('el botón Cerrar lo cierra y el foco vuelve a quien lo abrió', () => {
    render(<Prueba />);
    const disparador = screen.getByRole('button', { name: 'Solicitar este trabajo' });
    disparador.focus();
    fireEvent.click(disparador);
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cerrar' }));
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(disparador).toHaveFocus();
  });
});

describe('Pestanas', () => {
  const pestanas = [
    { id: 'semana', etiqueta: 'Semana', contenido: <p>Pedidos de la semana</p> },
    { id: 'mes', etiqueta: 'Mes', contenido: <p>Pedidos del mes</p> },
    { id: 'anio', etiqueta: 'Año', contenido: <p>Pedidos del año</p> },
  ];

  it('sigue el patrón de pestañas: lista con nombre, una seleccionada y su panel asociado', () => {
    render(<Pestanas etiqueta="Periodo" pestanas={pestanas} />);
    expect(screen.getByRole('tablist', { name: 'Periodo' })).toBeInTheDocument();
    const primera = screen.getByRole('tab', { name: 'Semana' });
    expect(primera).toHaveAttribute('aria-selected', 'true');
    expect(primera).toHaveAttribute('tabindex', '0');
    expect(screen.getByRole('tab', { name: 'Mes' })).toHaveAttribute('tabindex', '-1');
    expect(screen.getByRole('tabpanel', { name: 'Semana' })).toHaveTextContent('Pedidos de la semana');
  });

  it('cada pestaña apunta a un panel que existe, y los inactivos quedan ocultos', () => {
    render(<Pestanas etiqueta="Periodo" pestanas={pestanas} />);
    for (const pestana of screen.getAllByRole('tab')) {
      expect(document.getElementById(pestana.getAttribute('aria-controls'))).not.toBeNull();
    }
    expect(screen.getAllByRole('tabpanel')).toHaveLength(1);
  });

  it('si la pestaña activa no existe, la primera sigue alcanzable con el teclado', () => {
    render(<Pestanas etiqueta="Periodo" pestanas={pestanas} activa="inexistente" alCambiar={() => {}} />);
    expect(screen.getByRole('tab', { name: 'Semana' })).toHaveAttribute('tabindex', '0');
  });

  it('las flechas, Inicio y Fin mueven el foco y la selección', () => {
    render(<Pestanas etiqueta="Periodo" pestanas={pestanas} />);
    const semana = screen.getByRole('tab', { name: 'Semana' });
    semana.focus();
    fireEvent.keyDown(semana, { key: 'ArrowRight' });
    expect(screen.getByRole('tab', { name: 'Mes' })).toHaveFocus();
    expect(screen.getByRole('tab', { name: 'Mes' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tabpanel')).toHaveTextContent('Pedidos del mes');
    fireEvent.keyDown(screen.getByRole('tab', { name: 'Mes' }), { key: 'End' });
    expect(screen.getByRole('tab', { name: 'Año' })).toHaveFocus();
    fireEvent.keyDown(screen.getByRole('tab', { name: 'Año' }), { key: 'ArrowRight' });
    expect(screen.getByRole('tab', { name: 'Semana' })).toHaveFocus();
    fireEvent.keyDown(screen.getByRole('tab', { name: 'Semana' }), { key: 'ArrowLeft' });
    expect(screen.getByRole('tab', { name: 'Año' })).toHaveFocus();
    fireEvent.keyDown(screen.getByRole('tab', { name: 'Año' }), { key: 'Home' });
    expect(screen.getByRole('tab', { name: 'Semana' })).toHaveAttribute('aria-selected', 'true');
  });
});

describe('Paginacion', () => {
  it('marca la página actual y desactiva Anterior en la primera', () => {
    const alCambiar = vi.fn();
    render(<Paginacion pagina={1} totalPaginas={3} alCambiar={alCambiar} />);
    const nav = screen.getByRole('navigation', { name: 'Paginación' });
    // En el extremo queda inactivo pero enfocable, para que el foco no se pierda al llegar.
    const anterior = within(nav).getByRole('button', { name: 'Página anterior' });
    expect(anterior).toHaveAttribute('aria-disabled', 'true');
    fireEvent.click(anterior);
    expect(alCambiar).not.toHaveBeenCalled();
    expect(within(nav).getByRole('button', { name: 'Página 1' })).toHaveAttribute('aria-current', 'page');
    fireEvent.click(within(nav).getByRole('button', { name: 'Página siguiente' }));
    expect(alCambiar).toHaveBeenCalledWith(2);
    fireEvent.click(within(nav).getByRole('button', { name: 'Página 3' }));
    expect(alCambiar).toHaveBeenCalledWith(3);
  });

  it('con muchas páginas muestra la primera, la última y las vecinas', () => {
    render(<Paginacion pagina={10} totalPaginas={20} alCambiar={() => {}} />);
    const nombres = screen.getAllByRole('button').map((b) => b.getAttribute('aria-label'));
    expect(nombres).toEqual([
      'Página anterior', 'Página 1', 'Página 9', 'Página 10', 'Página 11', 'Página 20', 'Página siguiente',
    ]);
    expect(screen.getByRole('button', { name: 'Página siguiente' })).not.toHaveAttribute('aria-disabled');
  });

  it('no se dibuja si hay una sola página', () => {
    const { container } = render(<Paginacion pagina={1} totalPaginas={1} alCambiar={() => {}} />);
    expect(container).toBeEmptyDOMElement();
  });
});

describe('BarraFiltros', () => {
  it('es una zona de búsqueda con nombre, resumen anunciado y limpieza solo si hay filtros', () => {
    const alLimpiar = vi.fn();
    const { rerender } = render(
      <BarraFiltros titulo="Filtros de pedidos" resumen="12 pedidos" hayFiltros={false} alLimpiar={alLimpiar}>
        <label>Mes <select><option>Todos</option></select></label>
      </BarraFiltros>,
    );
    const zona = screen.getByRole('search', { name: 'Filtros de pedidos' });
    expect(within(zona).getByRole('status')).toHaveTextContent('12 pedidos');
    expect(within(zona).queryByRole('button', { name: 'Limpiar filtros' })).toBeNull();
    rerender(
      <BarraFiltros titulo="Filtros de pedidos" resumen="3 pedidos" hayFiltros alLimpiar={alLimpiar}>
        <label>Mes <select><option>Noviembre</option></select></label>
      </BarraFiltros>,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Limpiar filtros' }));
    expect(alLimpiar).toHaveBeenCalledTimes(1);
  });
});

describe('TablaAdaptable', () => {
  const columnas = [
    { clave: 'numero', encabezado: 'N.º' },
    { clave: 'cliente', encabezado: 'Cliente' },
    { clave: 'saldo', encabezado: 'Saldo', alinear: 'fin', render: (f) => `$${f.saldo}` },
  ];
  const filas = [
    { id: 1, numero: 101, cliente: 'Panadería Sol', saldo: '30.000' },
    { id: 2, numero: 102, cliente: 'Taller Ruiz', saldo: '0' },
  ];

  it('la columna principal encabeza cada fila y la clave puede calcularse', () => {
    const conPrincipal = columnas.map((c) => (c.clave === 'cliente' ? { ...c, principal: true } : c));
    render(<TablaAdaptable titulo="Pedidos" columnas={conPrincipal} filas={filas} claveFila={(f) => `p-${f.numero}`} />);
    expect(screen.getAllByRole('rowheader').map((th) => th.textContent)).toEqual(['Panadería Sol', 'Taller Ruiz']);
  });

  it('la región que anuncia la carga existe siempre, para que el lector de pantalla la oiga cambiar', () => {
    const { rerender } = render(<TablaAdaptable titulo="Pedidos" columnas={columnas} filas={filas} />);
    expect(screen.getByRole('status')).toHaveTextContent('');
    rerender(<TablaAdaptable titulo="Pedidos" columnas={columnas} filas={filas} cargando />);
    expect(screen.getByRole('status')).toHaveTextContent('Cargando pedidos');
  });

  it('es una tabla real con título, encabezados y etiquetas para el formato móvil', () => {
    render(<TablaAdaptable titulo="Pedidos" columnas={columnas} filas={filas} />);
    const tabla = screen.getByRole('table', { name: 'Pedidos' });
    expect(within(tabla).getAllByRole('columnheader').map((th) => th.textContent)).toEqual(['N.º', 'Cliente', 'Saldo']);
    expect(within(tabla).getAllByRole('columnheader')[0]).toHaveAttribute('scope', 'col');
    const celda = within(tabla).getByText('$30.000');
    expect(celda.closest('td')).toHaveAttribute('data-etiqueta', 'Saldo');
  });

  it('sin filas muestra el estado vacío en vez de una tabla vacía', () => {
    render(<TablaAdaptable titulo="Pedidos" columnas={columnas} filas={[]} vacio={<p>No hay pedidos.</p>} />);
    expect(screen.getByText('No hay pedidos.')).toBeInTheDocument();
    expect(screen.queryAllByRole('row')).toHaveLength(0);
  });

  it('mientras carga avisa al lector de pantalla y no muestra datos', () => {
    render(<TablaAdaptable titulo="Pedidos" columnas={columnas} filas={filas} cargando />);
    expect(screen.getByRole('status')).toHaveTextContent('Cargando pedidos');
    expect(screen.queryByText('Panadería Sol')).toBeNull();
  });
});
