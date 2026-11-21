import { useLocation, useNavigate } from 'react-router-dom';
import { useSesion } from '../sesion/sesion';
import { BotonGoogle, SeparadorGoogle } from './BotonGoogle';
import { googleDisponible } from './configuracion';

export const RUTA_COMPLETAR_GOOGLE = '/cuenta/google/completar';

/**
 * El ingreso y el registro con Google de /ingresar/cliente y /cuenta/registro (RFS03, RFU05): el botón de dos clics
 * ARRIBA del formulario y, debajo, el separador «o con tu correo». Sin ID de cliente configurado no dibuja nada.
 *
 * Los tres caminos de POST /auth/google: la identidad ya vinculada abre la sesión y va a `destino`; un correo nuevo guarda
 * el pase en memoria y sigue en «Completa tu registro»; un correo que ya tiene cuenta (409 cuenta_existente) se explica.
 */
export function IngresoGoogle({ destino = '/cuenta' }) {
  const { ingresarConGoogle } = useSesion();
  const navegar = useNavigate();
  const ubicacion = useLocation();

  if (!googleDisponible()) return null;

  async function alCredencial({ idToken, nonce }) {
    const resultado = await ingresarConGoogle(idToken, nonce);
    // El pase NO viaja por la ruta ni por el estado de navegación (el navegador los guarda): queda en memoria.
    if (resultado.estado === 'registro_pendiente') navegar(RUTA_COMPLETAR_GOOGLE, { state: ubicacion.state ?? undefined });
    else navegar(destino, { replace: true });
  }

  return (
    <>
      <BotonGoogle alCredencial={alCredencial} />
      <SeparadorGoogle />
    </>
  );
}
