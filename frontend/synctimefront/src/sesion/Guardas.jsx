import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Aviso, Boton, Esqueleto } from '../componentes/ui';
import { Pagina403 } from '../paginas/errores/PaginasDeError';
import { almacenSesion, useSesion } from './sesion';

export const RUTA_INGRESO_PERSONAL = '/personal/ingresar';
export const RUTA_INGRESO_CLIENTE = '/ingresar';

/**
 * Mientras se recupera la sesión al cargar la página: nadie es expulsado antes de saber si tiene sesión.
 * Si la comprobación no pudo hacerse (sin conexión o falla del servidor), lo dice y deja reintentar.
 */
export function ComprobandoSesion() {
  const { falloComprobacion } = useSesion();
  return (
    <div style={{ padding: 'var(--espacio-8) var(--margen-pagina)', maxWidth: '40rem', margin: '0 auto' }}>
      {falloComprobacion ? (
        <Aviso
          tono="peligro"
          titulo="No pudimos comprobar tu sesión"
          accion={<Boton variante="secundario" tamano="compacto" onClick={() => almacenSesion.iniciar()}>Reintentar</Boton>}
        >
          Revisa tu conexión e inténtalo de nuevo.
        </Aviso>
      ) : (
        <>
          <p role="status" className="solo-lectores">Comprobando tu sesión…</p>
          <Esqueleto lineas={4} />
        </>
      )}
    </div>
  );
}

/**
 * Protege las rutas del panel del taller. Sin sesión del personal, lleva al ingreso y recuerda
 * adónde quería ir; con sesión pero sin el rol, muestra la página 403 en vez de esconder el motivo.
 * Los roles reales son dueño, administrador, diseñador y operario (docs/api/permisos.md).
 */
export function RequierePersonal({ roles, children }) {
  const { usuario, esPersonal, tieneRol, motivoCierre, comprobando, falloComprobacion } = useSesion();
  const ubicacion = useLocation();

  if (comprobando || falloComprobacion) return <ComprobandoSesion />;
  if (!usuario || !esPersonal) {
    return <Navigate to={RUTA_INGRESO_PERSONAL} replace state={{ desde: ubicacion, motivo: motivoCierre }} />;
  }
  if (roles && !tieneRol(roles)) return <Pagina403 />;
  return children ?? <Outlet />;
}

/** Protege las rutas del cliente (Mi cuenta). Sin sesión de cliente, lleva a su ingreso y recuerda adónde volver. */
export function RequiereCliente({ children }) {
  const { usuario, esCliente, esPersonal, motivoCierre, comprobando, falloComprobacion } = useSesion();
  const ubicacion = useLocation();

  if (comprobando || falloComprobacion) return <ComprobandoSesion />;
  // El personal tiene su propio perfil en el panel.
  if (esPersonal) return <Navigate to="/panel/perfil" replace />;
  if (!usuario || !esCliente) {
    return <Navigate to={RUTA_INGRESO_CLIENTE} replace state={{ desde: ubicacion, motivo: motivoCierre }} />;
  }
  return children ?? <Outlet />;
}
