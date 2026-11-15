import { lazy, Suspense } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { ProveedorAvisos } from './avisos/Avisos';
import ErrorBoundary from './componentes/ErrorBoundary';
import { Esqueleto } from './componentes/ui';
import { EstructuraPanel } from './estructuras/EstructuraPanel';
import { EstructuraPublica } from './estructuras/EstructuraPublica';
import { GESTION } from './estructuras/navegacion';
import { Pagina404 } from './paginas/errores/PaginasDeError';
import { RequiereCliente, RequierePersonal } from './sesion/Guardas';

// Cada pantalla se descarga al visitarla: la portada no carga el panel ni el reconocimiento facial.
const Portada = lazy(() => import('./paginas/publicas/Portada'));
const IngresoPersonal = lazy(() => import('./paginas/publicas/IngresoPersonal'));
const DocumentoLegal = lazy(() => import('./paginas/publicas/legales/DocumentoLegal'));
const IngresoCliente = lazy(() => import('./paginas/cuenta/IngresoCliente'));
const Registro = lazy(() => import('./paginas/cuenta/Registro'));
const Recuperar = lazy(() => import('./paginas/cuenta/Recuperar'));
const Verificar = lazy(() => import('./paginas/cuenta/Verificar'));
const MiCuenta = lazy(() => import('./paginas/cuenta/MiCuenta'));
const NuevaSolicitud = lazy(() => import('./paginas/solicitudes/NuevaSolicitud'));
const MisSolicitudes = lazy(() => import('./paginas/solicitudes/MisSolicitudes'));
const MiSolicitud = lazy(() => import('./paginas/solicitudes/MisSolicitudes').then((m) => ({ default: m.MiSolicitud })));
const BandejaSolicitudes = lazy(() => import('./paginas/solicitudes/panel/BandejaSolicitudes'));
const FichaSolicitud = lazy(() => import('./paginas/solicitudes/panel/FichaSolicitud'));
const RegistroPresencial = lazy(() => import('./paginas/solicitudes/panel/RegistroPresencial'));
const Clientes = lazy(() => import('./paginas/solicitudes/panel/Clientes'));
const FichaCliente = lazy(() => import('./paginas/solicitudes/panel/Clientes').then((m) => ({ default: m.FichaCliente })));
const InicioPanel = lazy(() => import('./paginas/panel/InicioPanel'));
const ListaPedidos = lazy(() => import('./paginas/pedidos/ListaPedidos'));
const FichaPedido = lazy(() => import('./paginas/pedidos/FichaPedido'));
const PedidoDirecto = lazy(() => import('./paginas/pedidos/PedidoDirecto'));
const CrearPedido = lazy(() => import('./paginas/pedidos/CrearPedido'));
const CalendarioEntregas = lazy(() => import('./paginas/pedidos/CalendarioEntregas'));
const ListaCotizaciones = lazy(() => import('./paginas/cotizaciones/ListaCotizaciones'));
const FichaCotizacion = lazy(() => import('./paginas/cotizaciones/FichaCotizacion'));
const EditorCotizacion = lazy(() => import('./paginas/cotizaciones/EditorCotizacion'));
const EstadoLocal = lazy(() => import('./paginas/panel/EstadoLocal'));
const Indicadores = lazy(() => import('./paginas/panel/Indicadores'));
const Reportes = lazy(() => import('./paginas/panel/Reportes'));
const Perfil = lazy(() => import('./paginas/panel/Perfil'));
const ConfirmarDestino = lazy(() => import('./paginas/avisosTaller/ConfirmarDestino'));
const Mantenedores = lazy(() => import('./paginas/mantenedores/Mantenedores'));
const InicioMantenedores = lazy(() => import('./paginas/mantenedores/Mantenedores').then((m) => ({ default: m.InicioMantenedores })));
const ListaMantenedor = lazy(() => import('./paginas/mantenedores/ListaMantenedor'));
const EditorMantenedor = lazy(() => import('./paginas/mantenedores/EditorMantenedor'));
const RostrosRegistrados = lazy(() => import('./paginas/rostros/RostrosRegistrados'));
const CalibrarRostro = lazy(() => import('./paginas/rostros/CalibrarRostro'));

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
              <Route path="ingresar" element={enCarga(<IngresoCliente />)} />
              <Route path="cuenta" element={<RequiereCliente>{enCarga(<MiCuenta />)}</RequiereCliente>} />
              <Route path="cuenta/solicitud" element={<RequiereCliente>{enCarga(<NuevaSolicitud />)}</RequiereCliente>} />
              <Route path="cuenta/solicitudes" element={<RequiereCliente>{enCarga(<MisSolicitudes />)}</RequiereCliente>} />
              <Route path="cuenta/solicitudes/:id" element={<RequiereCliente>{enCarga(<MiSolicitud />)}</RequiereCliente>} />
              <Route path="cuenta/registro" element={enCarga(<Registro />)} />
              <Route path="cuenta/recuperar" element={enCarga(<Recuperar />)} />
              <Route path="cuenta/verificar" element={enCarga(<Verificar />)} />
              <Route path="personal/ingresar" element={enCarga(<IngresoPersonal />)} />
              <Route path="privacidad" element={enCarga(<DocumentoLegal tipo="privacidad" />)} />
              <Route path="terminos" element={enCarga(<DocumentoLegal tipo="terminos" />)} />
              {/* Enlace del correo que confirma el destino de los avisos del taller (fase 9): público, sin sesión. */}
              <Route path="avisos/confirmar" element={enCarga(<ConfirmarDestino />)} />
              {/* Direcciones antiguas del panel */}
              <Route path="login" element={<Navigate to="/personal/ingresar" replace />} />
              <Route path="dashboard" element={<Navigate to="/panel" replace />} />
              <Route path="*" element={<Pagina404 />} />
            </Route>

            <Route path="panel" element={<RequierePersonal><EstructuraPanel /></RequierePersonal>}>
              <Route index element={enCarga(<InicioPanel />)} />
              <Route path="pedidos" element={enCarga(<ListaPedidos />)} />
              <Route path="pedidos/directo" element={enCarga(<PedidoDirecto />)} />
              <Route path="pedidos/:id" element={enCarga(<FichaPedido />)} />
              <Route path="local" element={enCarga(<EstadoLocal />)} />
              <Route path="perfil" element={enCarga(<Perfil />)} />
              <Route path="solicitudes" element={enCarga(<BandejaSolicitudes />)} />
              <Route path="solicitudes/nueva" element={enCarga(<RegistroPresencial />)} />
              <Route path="solicitudes/:id" element={enCarga(<FichaSolicitud />)} />
              <Route path="solicitudes/:id/cotizar" element={enCarga(<EditorCotizacion modo="cotizar" />)} />
              <Route path="cotizaciones" element={enCarga(<ListaCotizaciones />)} />
              <Route path="cotizaciones/:id" element={enCarga(<FichaCotizacion />)} />
              <Route path="cotizaciones/:id/editar" element={enCarga(<EditorCotizacion modo="editar" />)} />
              <Route path="cotizaciones/:id/pedido" element={enCarga(<CrearPedido />)} />
              <Route path="calendario" element={enCarga(<CalendarioEntregas />)} />
              <Route path="clientes" element={enCarga(<Clientes />)} />
              <Route path="clientes/:id" element={enCarga(<FichaCliente />)} />
              <Route element={<RequierePersonal roles={GESTION} />}>
                <Route path="indicadores" element={enCarga(<Indicadores />)} />
                <Route path="reportes" element={enCarga(<Reportes />)} />
                <Route path="mantenedores" element={enCarga(<Mantenedores />)}>
                  <Route index element={enCarga(<InicioMantenedores />)} />
                  <Route path=":tipo" element={enCarga(<ListaMantenedor />)} />
                </Route>
                <Route path="mantenedores/:tipo/nuevo" element={enCarga(<EditorMantenedor />)} />
                <Route path="mantenedores/:tipo/:id" element={enCarga(<EditorMantenedor />)} />
                <Route path="rostros" element={enCarga(<RostrosRegistrados />)} />
                <Route path="rostros/calibrar" element={enCarga(<CalibrarRostro />)} />
              </Route>
              <Route path="*" element={<Pagina404 />} />
            </Route>
          </Routes>
        </ErrorBoundary>
      </ProveedorAvisos>
    </BrowserRouter>
  );
}
