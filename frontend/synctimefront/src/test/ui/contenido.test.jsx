import { render, screen, within } from '@testing-library/react';
import {
  Aviso, Esqueleto, EstadoCartel, EstadoPago, EstadoPedido, EstadoVacio, Insignia, Tarjeta,
} from '../../componentes/ui';

describe('insignias de estado (la información no depende solo del color)', () => {
  it.each([
    ['aceptado', 'Aceptado'],
    ['en_proceso', 'En proceso'],
    ['terminado', 'Terminado'],
    ['entregado', 'Entregado'],
    ['anulado', 'Anulado'],
  ])('estado del pedido %s se lee como «%s» y lleva icono', (estado, texto) => {
    const { container } = render(<EstadoPedido estado={estado} />);
    expect(screen.getByText(texto)).toBeInTheDocument();
    expect(container.querySelector('svg[aria-hidden="true"]')).not.toBeNull();
  });

  it.each([
    ['no_pagado', 'No pagado'],
    ['abonado', 'Abonado'],
    ['pagado_total', 'Pagado total'],
  ])('estado de pago %s se lee como «%s» y lleva icono', (estado, texto) => {
    const { container } = render(<EstadoPago estado={estado} />);
    expect(screen.getByText(texto)).toBeInTheDocument();
    expect(container.querySelector('svg[aria-hidden="true"]')).not.toBeNull();
  });

  it('cada estado tiene su propio icono, no solo otro color', () => {
    const iconos = ['aceptado', 'en_proceso', 'terminado', 'entregado', 'anulado'].map((estado) => {
      const { container, unmount } = render(<EstadoPedido estado={estado} />);
      const forma = container.querySelector('svg').innerHTML;
      unmount();
      return forma;
    });
    expect(new Set(iconos).size).toBe(iconos.length);
  });

  it('el cartel dice con palabras si el local está abierto o cerrado', () => {
    const { rerender } = render(<EstadoCartel abierto />);
    expect(screen.getByText('Abierto / Disponible')).toBeInTheDocument();
    rerender(<EstadoCartel abierto={false} />);
    expect(screen.getByText('Cerrado / Ausente')).toBeInTheDocument();
  });

  it('un estado desconocido se muestra tal cual, en tono neutro, sin inventar uno', () => {
    render(<EstadoPedido estado="cancelado" />);
    expect(screen.getByText('cancelado')).toBeInTheDocument();
    expect(screen.queryByText('Anulado')).toBeNull();
  });

  it('la insignia genérica muestra su texto', () => {
    render(<Insignia tono="info">Datos de ejemplo</Insignia>);
    expect(screen.getByText('Datos de ejemplo')).toBeInTheDocument();
  });
});

describe('Aviso', () => {
  it('un error interrumpe al lector de pantalla y una confirmación no', () => {
    const { rerender } = render(<Aviso tono="peligro" titulo="No se pudo guardar">Revisa tu conexión.</Aviso>);
    expect(screen.getByRole('alert')).toHaveTextContent('No se pudo guardar');
    rerender(<Aviso tono="exito" titulo="Solicitud enviada">Te responderemos pronto.</Aviso>);
    expect(screen.getByRole('status')).toHaveTextContent('Solicitud enviada');
  });
});

describe('EstadoVacio', () => {
  it('explica qué falta y ofrece el siguiente paso', () => {
    render(
      <EstadoVacio titulo="No hay pedidos con estos filtros" descripcion="Prueba con otro mes." nivelTitulo={3}
        accion={<button type="button">Limpiar filtros</button>} />,
    );
    expect(screen.getByRole('heading', { level: 3, name: 'No hay pedidos con estos filtros' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Limpiar filtros' })).toBeInTheDocument();
  });
});

describe('Esqueleto y Tarjeta', () => {
  it('el esqueleto es decorativo y no lo lee el lector de pantalla', () => {
    const { container } = render(<Esqueleto lineas={3} />);
    expect(container.firstChild).toHaveAttribute('aria-hidden', 'true');
  });

  it('la tarjeta usa el elemento que se le indica', () => {
    render(<Tarjeta como="article" aria-label="Letrero luminoso"><p>Acrílico</p></Tarjeta>);
    const tarjeta = screen.getByRole('article', { name: 'Letrero luminoso' });
    expect(within(tarjeta).getByText('Acrílico')).toBeInTheDocument();
  });
});
