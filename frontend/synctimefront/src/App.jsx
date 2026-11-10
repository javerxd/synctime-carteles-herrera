import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { ToastProvider } from './context/ToastContext';
import ProtectedRoute from './components/ProtectedRoute';
import AppLayout from './components/AppLayout';
import ErrorBoundary from './components/ErrorBoundary';

import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/DashboardPage';
import SolicitudesPage from './pages/SolicitudesPage';
import ClientesPage from './pages/ClientesPage';
import ProductosPage from './pages/ProductosPage';
import PerfilPage from './pages/PerfilPage';
import ForbiddenPage from './pages/ForbiddenPage';
import NotFoundPage from './pages/NotFoundPage';

/**
 * App Component
 * —————————————
 * Configuración central del enrutador y los proveedores de contexto:
 * - AuthProvider: Gestión global de autenticación, JWT y decodificación de roles
 * - ToastProvider: Notificaciones accesibles vía aria-live
 * - Rutas públicas: /login, /403, 404
 * - Rutas protegidas: /dashboard, /solicitudes, /clientes, /productos (envueltas en ProtectedRoute y AppLayout)
 */
export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <ToastProvider>
          {/* Última red de seguridad: las pantallas internas tienen su propio límite en AppLayout */}
          <ErrorBoundary recargar>
            <Routes>
              {/* Rutas Públicas */}
              <Route path="/login" element={<LoginPage />} />
              <Route path="/403" element={<ForbiddenPage />} />

              {/* Rutas Protegidas bajo AppLayout */}
              <Route
                path="/"
                element={
                  <ProtectedRoute>
                    <AppLayout />
                  </ProtectedRoute>
                }
              >
                <Route index element={<Navigate to="/dashboard" replace />} />
                <Route path="dashboard" element={<DashboardPage />} />
                <Route path="solicitudes" element={<SolicitudesPage />} />
                <Route path="clientes" element={<ClientesPage />} />
                <Route path="productos" element={<ProductosPage />} />
                <Route path="perfil" element={<PerfilPage />} />
              </Route>

              {/* Ruta no encontrada */}
              <Route path="*" element={<NotFoundPage />} />
            </Routes>
          </ErrorBoundary>
        </ToastProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}
