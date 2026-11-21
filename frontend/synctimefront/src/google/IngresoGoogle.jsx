import { Link, useLocation, useNavigate } from 'react-router-dom';
import { ErrorApi } from '../api/cliente';
import { Enlace } from '../componentes/ui';
import { useSesion } from '../sesion/sesion';
import { paseGoogle } from './pase';
import { BotonGoogle, SeparadorGoogle } from './BotonGoogle';
import { googleDisponible } from './configuracion';

export const RUTA_COMPLETAR_GOOGLE = '/cuenta/google/completar';

/**
 * El ingreso y el registro con Google de /ingresar/cliente y /cuenta/registro (RFS03, RFU05): el botón de dos clics
 * ARRIBA del formulario y, debajo, el separador «o con tu correo». Sin ID de cliente configurado no dibuja nada.
 *
 * Los caminos de POST /auth/google: la identidad ya vinculada abre la sesión (la de cliente va a `destino`; la de personal, fase 14C,
 * a `destinoPersonal`); un correo nuevo guarda el pase en memoria y sigue en «Completa tu registro»; un correo que ya tiene cuenta
 * (409 cuenta_existente) o es de un empleado sin vincular (409 cuenta_personal_existente) se explica.
 * `soloPersonal` (el acceso del personal): un correo nuevo NO lleva a crear una cuenta de cliente desde ahí, se avisa.
 */
export function IngresoGoogle({ destino = '/cuenta', destinoPersonal = '/panel', soloPersonal = false, alEmpezar }) {
  const { ingresarConGoogle } = useSesion();
  const navegar = useNavigate();
  const ubicacion = useLocation();

  if (!googleDisponible()) return null;

  async function alCredencial({ idToken, nonce }) {
    const resultado = await ingresarConGoogle(idToken, nonce);
    // El pase NO viaja por la ruta ni por el estado de navegación (el navegador los guarda): queda en memoria.
    if (resultado.estado === 'registro_pendiente') {
      if (soloPersonal) {
        paseGoogle.limpiar();
        throw new ErrorApi({ estado: 0, codigo: 'sin_cuenta_personal', mensaje: 'Este Google no está vinculado a una cuenta del personal.' });
      }
      navegar(RUTA_COMPLETAR_GOOGLE, { state: ubicacion.state ?? undefined });
    } else if (resultado.tipo === 'empleado') navegar(destinoPersonal, { replace: true });
    else navegar(destino, { replace: true });
  }

  // Los avisos de «ya tienes una cuenta» mandan a Mi cuenta o a Mi perfil, pero la contraseña de un cliente no sirve en el acceso del
  // personal ni al revés: si la persona está en el acceso equivocado, el aviso dice dónde ingresar. undefined = el mensaje de siempre.
  function alFallo(error) {
    if (error?.codigo === 'cuenta_personal_existente' && !soloPersonal) {
      return {
        tono: 'aviso',
        titulo: 'Este correo es de una cuenta del personal.',
        texto: <>Ingresa desde el <Enlace como={Link} to="/personal/ingresar">acceso del personal</Enlace> con tu contraseña y vincula Google desde Mi perfil.</>,
      };
    }
    if (error?.codigo === 'cuenta_existente' && soloPersonal) {
      return {
        tono: 'aviso',
        titulo: 'Ya tienes una cuenta de cliente con este correo.',
        texto: <>Ingresa desde la <Enlace como={Link} to="/ingresar/cliente">página de clientes</Enlace> con tu contraseña y vincula Google desde Mi cuenta.</>,
      };
    }
    if (error?.codigo === 'sin_cuenta_personal') {
      return {
        tono: 'aviso',
        titulo: 'Este Google no está vinculado a una cuenta del personal.',
        texto: <>Ingresa con tu contraseña y vincula Google desde Mi perfil. Si eres cliente, ingresa desde la <Enlace como={Link} to="/ingresar/cliente">página de clientes</Enlace>.</>,
      };
    }
    return undefined;
  }

  return (
    <>
      <BotonGoogle alCredencial={alCredencial} alFallo={alFallo} antesDeActivar={() => { alEmpezar?.(); return true; }} />
      <SeparadorGoogle />
    </>
  );
}
