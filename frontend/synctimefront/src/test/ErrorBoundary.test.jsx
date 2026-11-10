import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import ErrorBoundary from '../components/ErrorBoundary';
import AppLayout from '../components/AppLayout';
import { AuthProvider } from '../context/AuthContext';

function Bomba({ falla = true }) {
  if (falla) throw new Error('detalle-tecnico-secreto');
  return <p>contenido sano</p>;
}

describe('ErrorBoundary', () => {
  beforeEach(() => {
    // React registra en consola los errores capturados; se silencia para no ensuciar la salida.
    vi.spyOn(console, 'error').mockImplementation(() => {});
  });
  afterEach(() => vi.restoreAllMocks());

  it('muestra los hijos si no hay error', () => {
    render(<ErrorBoundary><Bomba falla={false} /></ErrorBoundary>);
    expect(screen.getByText('contenido sano')).toBeInTheDocument();
  });

  it('muestra un aviso accesible y no expone el detalle del error', () => {
    render(<ErrorBoundary><Bomba /></ErrorBoundary>);

    expect(screen.getByRole('alert')).toHaveTextContent(/no se pudo mostrar/i);
    expect(screen.queryByText(/detalle-tecnico-secreto/)).not.toBeInTheDocument();
  });

  it('se recupera al cambiar resetKey', () => {
    const { rerender } = render(<ErrorBoundary resetKey="a"><Bomba /></ErrorBoundary>);
    expect(screen.getByRole('alert')).toBeInTheDocument();

    rerender(<ErrorBoundary resetKey="b"><Bomba falla={false} /></ErrorBoundary>);
    expect(screen.getByText('contenido sano')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('"Reintentar" vuelve a dibujar los hijos', () => {
    let falla = true;
    const Intermitente = () => <Bomba falla={falla} />;
    render(<ErrorBoundary><Intermitente /></ErrorBoundary>);

    falla = false;
    fireEvent.click(screen.getByRole('button', { name: /reintentar/i }));

    expect(screen.getByText('contenido sano')).toBeInTheDocument();
  });
});

describe('AppLayout con una pantalla que falla', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    sessionStorage.setItem('synctime_token', 't');
    sessionStorage.setItem('synctime_user', JSON.stringify({ nombre: 'Ana', apellido: 'Díaz', email: 'a@b.cl', rol: 'dueño' }));
  });
  afterEach(() => {
    vi.restoreAllMocks();
    sessionStorage.clear();
  });

  it('conserva el menú y permite navegar a otra pantalla', () => {
    render(
      <MemoryRouter initialEntries={['/productos']}>
        <AuthProvider>
          <Routes>
            <Route path="/" element={<AppLayout />}>
              <Route path="productos" element={<Bomba />} />
              <Route path="clientes" element={<p>lista de clientes</p>} />
            </Route>
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('navigation')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('link', { name: 'Clientes' }));

    expect(screen.getByText('lista de clientes')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('pasar de una pantalla rota a otra también rota no tumba el menú', () => {
    render(
      <MemoryRouter initialEntries={['/productos']}>
        <AuthProvider>
          <ErrorBoundary recargar>
            <Routes>
              <Route path="/" element={<AppLayout />}>
                <Route path="productos" element={<Bomba />} />
                <Route path="solicitudes" element={<Bomba />} />
              </Route>
            </Routes>
          </ErrorBoundary>
        </AuthProvider>
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole('link', { name: 'Solicitudes' }));

    // El menú sigue ahí: el error no escaló a la barrera exterior.
    expect(screen.getByRole('link', { name: 'Clientes' })).toBeInTheDocument();
    expect(screen.getAllByRole('alert')).toHaveLength(1);
  });

  it('"Reintentar" con el fallo persistente mantiene el aviso y el menú', () => {
    render(
      <MemoryRouter initialEntries={['/productos']}>
        <AuthProvider>
          <Routes>
            <Route path="/" element={<AppLayout />}>
              <Route path="productos" element={<Bomba />} />
            </Route>
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole('button', { name: /reintentar/i }));

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Clientes' })).toBeInTheDocument();
  });
});

describe('ErrorBoundary exterior', () => {
  beforeEach(() => vi.spyOn(console, 'error').mockImplementation(() => {}));
  afterEach(() => vi.restoreAllMocks());

  it('no menciona un menú que no existe y ofrece recargar', () => {
    render(<ErrorBoundary recargar><Bomba /></ErrorBoundary>);

    expect(screen.getByRole('alert')).not.toHaveTextContent(/menú/i);
    expect(screen.getByRole('button', { name: /recargar/i })).toBeInTheDocument();
  });
});
