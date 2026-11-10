import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ProductosPage from '../pages/ProductosPage';
import productoService from '../api/productoService';

vi.mock('../api/productoService', () => ({ default: { getAll: vi.fn() } }));

// Forma real de GET /api/v1/productos: la categoria viene como objeto y el precio como precioBaseRef.
const letreros = { idCategoria: 1, nombre: 'Letreros', descripcion: 'Letreros comerciales', activo: true };
const impresion = { idCategoria: 2, nombre: 'Impresión', descripcion: 'Impresión digital', activo: true };
const productos = [
  { idProducto: 1, idCategoria: 1, nombre: 'Cartel metálico impreso', descripcion: 'Estructura metálica', precioBaseRef: 45000, activo: true, categoria: letreros },
  { idProducto: 2, idCategoria: 2, nombre: 'Lienzo publicitario', descripcion: 'Lienzo PVC', precioBaseRef: null, activo: true, categoria: impresion },
];

describe('ProductosPage con el contrato real de la API', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    productoService.getAll.mockResolvedValue(productos);
  });

  it('muestra el nombre de la categoria y no se cae al recibir un objeto', async () => {
    render(<ProductosPage />);

    expect(await screen.findByText('Letreros')).toBeInTheDocument();
    expect(screen.getByText('Impresión')).toBeInTheDocument();
  });

  it('muestra el precio de precioBaseRef y "A cotizar" si no tiene', async () => {
    render(<ProductosPage />);

    expect(await screen.findByText(/\$45[.\s]?000/)).toBeInTheDocument();
    expect(screen.getByText('A cotizar')).toBeInTheDocument();
  });

  it('busca por el nombre de la categoria', async () => {
    render(<ProductosPage />);
    await screen.findByText('Cartel metálico impreso');

    fireEvent.change(screen.getByLabelText('Buscar productos'), { target: { value: 'impresión' } });

    await waitFor(() => expect(screen.queryByText('Cartel metálico impreso')).not.toBeInTheDocument());
    expect(screen.getByText('Lienzo publicitario')).toBeInTheDocument();
  });

  it('usa "General" si el producto llega sin categoria', async () => {
    productoService.getAll.mockResolvedValue([{ ...productos[0], categoria: null }]);
    render(<ProductosPage />);

    expect(await screen.findByText('General')).toBeInTheDocument();
  });
});
