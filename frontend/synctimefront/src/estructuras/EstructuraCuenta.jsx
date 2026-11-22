import { useNavigate } from 'react-router-dom';
import { useAvisos } from '../avisos/Avisos';
import { useSesion } from '../sesion/sesion';
import { EstructuraLateral } from './EstructuraLateral';
import { MENU_CUENTA } from './navegacion';

/**
 * Estructura de la cuenta del cliente (/cuenta/*): el mismo menú lateral azul que el panel del taller, con
 * «Mis solicitudes», «Solicitar cotización» y «Mi cuenta». A diferencia del panel, esta zona lleva el fondo de
 * baja opacidad y los botones animados de la 12C. Registro, recuperación y verificación no la usan.
 */
export function EstructuraCuenta() {
  const { usuario, salir } = useSesion();
  const navegar = useNavigate();
  const { anunciar } = useAvisos();

  function cerrarSesion() {
    // Primero se sale de la página protegida y luego se cierra la sesión: así no se pasa por el ingreso.
    navegar('/', { replace: true });
    salir().catch((error) => anunciar({ tono: 'peligro', titulo: error.message }));
  }

  return (
    <EstructuraLateral
      nombreMenu="Mi cuenta"
      subtitulo="Mi cuenta"
      destinoMarca="/cuenta"
      menu={MENU_CUENTA}
      nombreUsuario={`${usuario?.nombre ?? ''} ${usuario?.apellido ?? ''}`.trim()}
      rolUsuario="Cliente"
      alCerrarSesion={cerrarSesion}
      zonaAnimada
    />
  );
}
