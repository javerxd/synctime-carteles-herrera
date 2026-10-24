import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import SolicitudesPage from '../pages/SolicitudesPage';
import { AuthProvider } from '../context/AuthContext';
import { ToastProvider } from '../context/ToastContext';
import solicitudService from '../api/solicitudService';
import clienteService from '../api/clienteService';
import productoService from '../api/productoService';

vi.mock('../api/solicitudService', () => ({
  default: {
    getAll: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
  },
}));

vi.mock('../api/clienteService', () => ({
  default: {
    getAll: vi.fn(),
  },
}));

vi.mock('../api/productoService', () => ({
  default: {
    getAll: vi.fn(),
  },
}));

const mockClientes = [
  { idCliente: 1, nombre: 'Empresa ABC', rut: '76.123.456-7' },
  { idCliente: 2, nombre: 'Comercial XYZ', rut: '77.890.123-4' },
];

const mockProductos = [
  { idProducto: 1, nombre: 'Letrero Acrílico 3D', precioBase: 120000 },
  { idProducto: 2, nombre: 'Caja de Luz LED', precioBase: 250000 },
];

const mockSolicitudes = [
  {
    idSolicitud: 101,
    idCliente: 1,
    idProducto: 1,
    cantidad: 2,
    telefonoContacto: '+56912345678',
    materialSolicitado: 'Acrílico 5mm',
    estado: 'Nueva',
    fechaSolicitud: '2026-10-15T10:00:00Z',
    notasWhatsapp: 'Entregar urgente',
  },
];

function renderSolicitudes(user = { id: 1, email: 'dueño@test.cl', rol: 'dueno' }) {
  sessionStorage.setItem('synctime_token', 'test-token');
  sessionStorage.setItem('synctime_user', JSON.stringify(user));

  return render(
    <AuthProvider>
      <ToastProvider>
        <SolicitudesPage />
      </ToastProvider>
    </AuthProvider>
  );
}

describe('SolicitudesPage CRUD Component', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    solicitudService.getAll.mockResolvedValue(mockSolicitudes);
    clienteService.getAll.mockResolvedValue(mockClientes);
    productoService.getAll.mockResolvedValue(mockProductos);
  });

  it('loads and displays list of solicitudes from API', async () => {
    renderSolicitudes();

    expect(screen.getByText(/cargando solicitudes/i)).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText('#101')).toBeInTheDocument();
      expect(screen.getByText('Empresa ABC')).toBeInTheDocument();
      expect(screen.getByText('Letrero Acrílico 3D')).toBeInTheDocument();
      expect(screen.getByText('+56912345678')).toBeInTheDocument();
    });
  });

  it('filters solicitudes by text search input', async () => {
    renderSolicitudes();

    await waitFor(() => {
      expect(screen.getByText('#101')).toBeInTheDocument();
    });

    const searchInput = screen.getByLabelText(/buscar solicitudes/i);
    fireEvent.change(searchInput, { target: { value: 'Inexistente' } });

    await waitFor(() => {
      expect(screen.getByText(/no se encontraron solicitudes/i)).toBeInTheDocument();
    });
  });

  it('opens creation modal when clicking Nueva Solicitud and submits form', async () => {
    solicitudService.create.mockResolvedValueOnce({ idSolicitud: 102 });

    renderSolicitudes();

    await waitFor(() => {
      expect(screen.getByText('#101')).toBeInTheDocument();
    });

    // Click "Nueva solicitud" button
    const createBtn = screen.getByRole('button', { name: /nueva solicitud/i });
    fireEvent.click(createBtn);

    // Modal should be open
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /nueva solicitud/i })).toBeInTheDocument();

    // Fill form
    fireEvent.change(screen.getByLabelText(/^cliente/i), { target: { value: '2' } });
    fireEvent.change(screen.getByLabelText(/^producto/i), { target: { value: '2' } });
    fireEvent.change(screen.getByLabelText(/^cantidad/i), { target: { value: '3' } });
    fireEvent.change(screen.getByLabelText(/teléfono de contacto/i), {
      target: { value: '+56987654321' },
    });

    // Submit
    const submitBtn = screen.getByRole('button', { name: /crear solicitud/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(solicitudService.create).toHaveBeenCalledWith(
        expect.objectContaining({
          idCliente: 2,
          idProducto: 2,
          cantidad: 3,
          telefonoContacto: '+56987654321',
        })
      );
    });
  });

  it('prompts confirmation modal and removes solicitud', async () => {
    solicitudService.remove.mockResolvedValueOnce();

    renderSolicitudes({ id: 1, email: 'dueño@test.cl', rol: 'dueno' });

    await waitFor(() => {
      expect(screen.getByText('#101')).toBeInTheDocument();
    });

    // Click delete icon button
    const deleteBtn = screen.getByTitle(/eliminar solicitud/i);
    fireEvent.click(deleteBtn);

    // Confirm modal opens
    expect(screen.getByText(/¿está seguro de que desea eliminar la solicitud #101\?/i)).toBeInTheDocument();

    // Confirm delete
    const confirmBtn = screen.getByRole('button', { name: /^eliminar$/i });
    fireEvent.click(confirmBtn);

    await waitFor(() => {
      expect(solicitudService.remove).toHaveBeenCalledWith(101);
    });
  });
});
