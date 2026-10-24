import { render, screen } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';
import RoleGuard from '../components/RoleGuard';
import { AuthProvider } from '../context/AuthContext';

function renderWithRole(role, rolesAllowed, fallback = null) {
  sessionStorage.setItem('synctime_token', 'test-token');
  sessionStorage.setItem(
    'synctime_user',
    JSON.stringify({ id: 1, email: 'user@test.cl', rol: role })
  );

  return render(
    <AuthProvider>
      <RoleGuard roles={rolesAllowed} fallback={fallback}>
        <div data-testid="protected-content">Contenido Autorizado</div>
      </RoleGuard>
    </AuthProvider>
  );
}

describe('RoleGuard Component', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('renders content when user role matches single allowed role', () => {
    renderWithRole('dueno', 'dueno');
    expect(screen.getByTestId('protected-content')).toBeInTheDocument();
  });

  it('renders content when user role is in array of allowed roles', () => {
    renderWithRole('disenador', ['dueno', 'disenador']);
    expect(screen.getByTestId('protected-content')).toBeInTheDocument();
  });

  it('does not render content when user role is not allowed and no fallback given', () => {
    renderWithRole('operario', 'dueno');
    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
  });

  it('renders fallback when user role is not allowed', () => {
    renderWithRole('operario', 'dueno', <div>No Autorizado</div>);
    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
    expect(screen.getByText('No Autorizado')).toBeInTheDocument();
  });
});
