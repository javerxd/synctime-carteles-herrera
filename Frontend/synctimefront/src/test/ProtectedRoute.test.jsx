import { render, screen } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import ProtectedRoute from '../components/ProtectedRoute';
import { AuthProvider } from '../context/AuthContext';

describe('ProtectedRoute Component', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('redirects unauthenticated users to /login', () => {
    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<div>Página de Login</div>} />
            <Route
              path="/dashboard"
              element={
                <ProtectedRoute>
                  <div>Panel Secreto</div>
                </ProtectedRoute>
              }
            />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    );

    expect(screen.queryByText('Panel Secreto')).not.toBeInTheDocument();
    expect(screen.getByText('Página de Login')).toBeInTheDocument();
  });

  it('renders child component when authenticated', () => {
    sessionStorage.setItem('synctime_token', 'valid-token');
    sessionStorage.setItem(
      'synctime_user',
      JSON.stringify({ id: 1, email: 'dueño@letrerosherrera.cl', rol: 'dueno' })
    );

    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<div>Página de Login</div>} />
            <Route
              path="/dashboard"
              element={
                <ProtectedRoute>
                  <div>Panel Secreto</div>
                </ProtectedRoute>
              }
            />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    );

    expect(screen.getByText('Panel Secreto')).toBeInTheDocument();
    expect(screen.queryByText('Página de Login')).not.toBeInTheDocument();
  });
});
