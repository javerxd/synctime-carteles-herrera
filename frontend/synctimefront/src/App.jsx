import { lazy, Suspense } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { ProveedorAvisos } from './avisos/Avisos';
import ErrorBoundary from './componentes/ErrorBoundary';
import { Esqueleto } from './componentes/ui';
import { EstructuraPanel } from './estructuras/EstructuraPanel';
import { EstructuraPublica } from './estructuras/EstructuraPublica';
import { GESTION } from './estructuras/navegacion';
import { ElegirIngreso, SeccionPendiente } from './paginas/Pendientes';
import { Pagina404 } from './paginas/errores/PaginasDeError';
import { RequierePersonal } from './sesion/Guardas';

// Cada pantalla se descarga al visitarla: la portada no carga el panel ni el reconocimiento facial.
const Portada = lazy(() => import('./paginas/publicas/Portada'));
const IngresoPersonal = lazy(() => import('./paginas/publicas/IngresoPersonal'));
const InicioPanel = lazy(() => import('./paginas/panel/InicioPanel'));
const Pedidos = lazy(() => import('./paginas/panel/Pedidos'));
const EstadoLocal = lazy(() => import('./paginas/panel/EstadoLocal'));
const Indicadores = lazy(() => import('./paginas/panel/Indicadores'));
const Reportes = lazy(() => import('./paginas/panel/Reportes'));
const Perfil = lazy(() => import('./paginas/panel/Perfil'));

function Cargando() {
  return (
    <div style={{ padding: 'var(--espacio-8) var(--margen-pagina)' }}>
      <p role="status" className="solo-lectores">Cargando…</p>
      <Esqueleto lineas={4} />
    </div>
  );
}

const enCarga = (elemento) => <Suspense fallback={<Cargando />}>{elemento}</Suspense>;

export default function App() {
  return (
    <BrowserRouter>
      <ProveedorAvisos>
        <ErrorBoundary recargar>
          <Routes>
            <Route element={<EstructuraPublica />}>
              <Route index element={enCarga(<Portada />)} />
              <Route path="ingresar" element={<ElegirIngreso />} />
              <Route path="personal/ingresar" element={enCarga(<IngresoPersonal />)} />
              <Route path="privacidad" element={<SeccionPendiente publica titulo="Política de privacidad" fase={5} descripcion="El borrador de la política, con su versión y la revisión legal pendiente, se publica en la fase 5." />} />
              <Route path="terminos" element={<SeccionPendiente publica titulo="Términos y condiciones" fase={5} descripcion="El borrador de los términos se publica en la fase 5." />} />
              {/* Direcciones antiguas del panel */}
              <Route path="login" element={<Navigate to="/personal/ingresar" replace />} />
              <Route path="dashboard" element={<Navigate to="/panel" replace />} />
              <Route path="*" element={<Pagina404 />} />
            </Route>

            <Route path="panel" element={<RequierePersonal><EstructuraPanel /></RequierePersonal>}>
              <Route index element={enCarga(<InicioPanel />)} />
              <Route path="pedidos" element={enCarga(<Pedidos />)} />
              <Route path="local" element={enCarga(<EstadoLocal />)} />
              <Route path="perfil" element={enCarga(<Perfil />)} />
              <Route path="solicitudes" element={<SeccionPendiente titulo="Solicitudes" fase={7} descripcion="La bandeja de solicitudes en línea y presenciales, con filtros, búsqueda y su comprobante." />} />
              <Route path="cotizaciones" element={<SeccionPendiente titulo="Cotizaciones" fase={8} descripcion="El editor de cotizaciones y su aprobación por el cliente o por el personal." />} />
              <Route path="calendario" element={<SeccionPendiente titulo="Calendario de entregas" fase={8} descripcion="Las entregas por día, pendientes y entregadas, con alertas de atraso." />} />
              <Route path="clientes" element={<SeccionPendiente titulo="Clientes" fase={7} descripcion="El listado y la ficha de clientes, con búsqueda por RUT o teléfono." />} />
              <Route element={<RequierePersonal roles={GESTION} />}>
                <Route path="indicadores" element={enCarga(<Indicadores />)} />
                <Route path="reportes" element={enCarga(<Reportes />)} />
                <Route path="mantenedores" element={<SeccionPendiente titulo="Catálogo y mantenedores" fase={9} descripcion="Catálogo, categorías, materiales y el horario de atención." />} />
              </Route>
              <Route path="*" element={<Pagina404 />} />
            </Route>
          </Routes>
        </ErrorBoundary>
      </ProveedorAvisos>
    </BrowserRouter>
  );
}
