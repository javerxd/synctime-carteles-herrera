import { createContext, useContext, useState, useCallback, useMemo } from 'react';
import PropTypes from 'prop-types';
import authService from '../api/authService';

const AuthContext = createContext(null);

/**
 * AuthProvider
 * —————————————————
 * Gestiona el ciclo de vida de la sesión JWT:
 * - Almacena token en sessionStorage (aislado por pestaña, se destruye al cerrar navegador).
 * - Expone login, logout y datos del usuario autenticado.
 * - Rehidrata la sesión al montar si existe un token previo.
 */
export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => sessionStorage.getItem('synctime_token'));
  const [user, setUser] = useState(() => {
    const savedUser = sessionStorage.getItem('synctime_user');
    if (savedUser) {
      try {
        return JSON.parse(savedUser);
      } catch {
        sessionStorage.removeItem('synctime_user');
      }
    }
    return null;
  });
  const [loading] = useState(false);

  const login = useCallback(async (email, password) => {
    const data = await authService.login(email, password);
    sessionStorage.setItem('synctime_token', data.token);
    sessionStorage.setItem('synctime_user', JSON.stringify(data.usuario));
    setToken(data.token);
    setUser(data.usuario);
    return data.usuario;
  }, []);

  const logout = useCallback(() => {
    sessionStorage.removeItem('synctime_token');
    sessionStorage.removeItem('synctime_user');
    setToken(null);
    setUser(null);
  }, []);

  const isAuthenticated = !!token && !!user;

  /**
   * hasRole — verifica si el usuario tiene uno de los roles indicados.
   * @param {string|string[]} roles
   */
  const hasRole = useCallback((roles) => {
    if (!user?.rol) return false;
    const allowed = Array.isArray(roles) ? roles : [roles];
    return allowed.some((r) => r.toLowerCase() === user.rol.toLowerCase());
  }, [user]);

  const value = useMemo(() => ({
    user,
    token,
    loading,
    isAuthenticated,
    login,
    logout,
    hasRole,
  }), [user, token, loading, isAuthenticated, login, logout, hasRole]);

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}

AuthProvider.propTypes = {
  children: PropTypes.node.isRequired,
};

/**
 * useAuth — hook para acceder al contexto de autenticación.
 */
export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth debe usarse dentro de un AuthProvider.');
  }
  return context;
}
