import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Aviso, Boton, Campo, Enlace, Tarjeta } from '../../componentes/ui';
import { ErrorApi } from '../../api/cliente';
import { google } from '../../api/google';
import { BotonGoogle } from '../../google/BotonGoogle';
import { googleDisponible } from '../../google/configuracion';
import estilos from './Cuenta.module.css';

const ERROR_SIN_PASSWORD = 'Escribe tu contraseña actual para confirmar que eres tú.';

/** El mensaje de la contraseña actual que devuelve la API (`errores.passwordActual`), o null. */
function errorDePassword(error) {
  const lista = Object.entries(error?.errores ?? {}).find(([campo]) => campo.toLowerCase() === 'passwordactual')?.[1];
  if (!lista) return null;
  return Array.isArray(lista) ? lista[0] : String(lista);
}

function CrearContrasena({ correo }) {
  return (
    <p>
      <Enlace como={Link} to="/cuenta/recuperar" state={{ correo }}>Crear una contraseña</Enlace>
      {' '}(te enviamos un código a tu correo para crearla).
    </p>
  );
}

/** Contraseña actual de una acción delicada: el campo, su error y el foco. */
function useContrasena() {
  const [valor, setValor] = useState('');
  const [error, setError] = useState(null);
  const ref = useRef(null);
  // El foco se pide y se da después de dibujar: el campo puede seguir deshabilitado mientras termina el envío.
  const [pedidosDeFoco, setPedidosDeFoco] = useState(0);
  useEffect(() => {
    if (pedidosDeFoco > 0) ref.current?.focus();
  }, [pedidosDeFoco]);
  const exigir = () => {
    if (valor) return true;
    setError(ERROR_SIN_PASSWORD);
    setPedidosDeFoco((n) => n + 1);
    return false;
  };
  const marcarError = (texto) => {
    setError(texto);
    setValor('');
    setPedidosDeFoco((n) => n + 1);
  };
  const propiedades = {
    ref,
    etiqueta: 'Contraseña actual',
    name: 'passwordActual',
    type: 'password',
    requerido: true,
    maxLength: 200,
    autoComplete: 'current-password',
    value: valor,
    onChange: (e) => { setValor(e.target.value); setError(null); },
    error,
  };
  return { valor, setValor, error, exigir, marcarError, propiedades };
}

function Vincular({ datosCuenta, alVincular, alEmpezar }) {
  const contrasena = useContrasena();
  // Lo que se muestra al vincular lo decide esta sección; el botón de Google solo carga a Google cuando se pulsa.
  const refValor = useRef('');
  useEffect(() => {
    refValor.current = contrasena.valor;
  });

  async function alCredencial({ idToken, nonce }) {
    // El campo pudo vaciarse después de que apareció el botón de Google: no se gasta un intento en la API con la contraseña vacía.
    if (!refValor.current) {
      throw new ErrorApi({ estado: 400, codigo: 'datos_invalidos', mensaje: ERROR_SIN_PASSWORD, errores: { passwordActual: [ERROR_SIN_PASSWORD] } });
    }
    await google.vincular({ idToken, nonce, passwordActual: refValor.current });
    contrasena.setValor('');
    alVincular();
  }

  function alFallo(error) {
    const texto = errorDePassword(error);
    if (texto) {
      contrasena.marcarError(texto);
      // Null: ya se mostró junto al campo y el foco quedó en él.
      return null;
    }
    return undefined;
  }

  return (
    <div className={estilos.formulario}>
      <p className={estilos.explicacion}>
        Google no está vinculado a tu cuenta. Vincúlalo para ingresar con un clic: debe ser la cuenta de Google con el correo{' '}
        <strong>{datosCuenta.correo}</strong>.
      </p>
      <Campo {...contrasena.propiedades} ayuda="Para confirmar que eres tú." />
      <BotonGoogle etiqueta="Vincular Google" antesDeActivar={() => { alEmpezar(); return contrasena.exigir(); }} alCredencial={alCredencial} alFallo={alFallo} />
    </div>
  );
}

function Desvincular({ alDesvincular, alEmpezar }) {
  const contrasena = useContrasena();
  const [estado, setEstado] = useState({ enviando: false, error: null });
  const refError = useRef(null);
  // Un error que no es de la contraseña recibe el foco: el campo se deshabilita al enviar y el foco no debe caer al cuerpo.
  useEffect(() => {
    if (estado.error && !estado.enviando) refError.current?.focus();
  }, [estado]);

  async function desvincular(evento) {
    evento.preventDefault();
    alEmpezar();
    if (!contrasena.exigir()) return;
    setEstado({ enviando: true, error: null });
    try {
      await google.desvincular(contrasena.valor);
      contrasena.setValor('');
      setEstado({ enviando: false, error: null });
      alDesvincular();
    } catch (error) {
      const texto = errorDePassword(error);
      setEstado({ enviando: false, error: texto ? null : (error?.message || 'No pudimos desvincular Google. Inténtalo de nuevo.') });
      if (texto) contrasena.marcarError(texto);
    }
  }

  return (
    <form className={estilos.formulario} onSubmit={desvincular} noValidate>
      <p className={estilos.explicacion}>
        Tu cuenta tiene Google vinculado: puedes ingresar con «Continuar con Google». Si lo desvinculas, ingresarás solo con tu
        correo y tu contraseña.
      </p>
      {estado.error && <div ref={refError} tabIndex={-1} className={estilos.resultado}><Aviso tono="peligro" titulo={estado.error} /></div>}
      <Campo {...contrasena.propiedades} disabled={estado.enviando} ayuda="Para confirmar que eres tú." />
      <div className={estilos.pieSeccion}>
        <Boton type="submit" variante="secundario" cargando={estado.enviando} textoCargando="Desvinculando…">Desvincular Google</Boton>
      </div>
    </form>
  );
}

/**
 * Mi cuenta → «Ingreso con Google» (RFS03, RFU05): dice si la cuenta tiene Google vinculado y permite vincularlo o
 * desvincularlo, siempre con la contraseña actual. Una cuenta creada con Google no tiene contraseña: se explica y se ofrece
 * «Crear una contraseña» (la recuperación de siempre); sin contraseña no se puede desvincular (quedaría sin forma de entrar).
 * Sin ID de cliente de Google y sin Google vinculado, la sección no se muestra.
 */
export function IngresoConGoogle({ datosCuenta, alGuardar }) {
  const [exito, setExito] = useState(null);
  const refExito = useRef(null);
  // El botón enfocado se desmonta al vincular o desvincular: el foco pasa al mensaje, que así se lee.
  useEffect(() => {
    if (exito) refExito.current?.focus();
  }, [exito]);
  const vinculado = datosCuenta.googleVinculado === true;
  const tieneContrasena = datosCuenta.tieneContrasena !== false;

  if (!vinculado && !googleDisponible()) return null;

  function cambiar(googleVinculado, mensaje) {
    alGuardar((cuentaActual) => ({ ...cuentaActual, googleVinculado }));
    setExito(mensaje);
  }

  let contenido;
  if (vinculado && tieneContrasena) {
    contenido = <Desvincular alEmpezar={() => setExito(null)} alDesvincular={() => cambiar(false, 'Desvinculamos tu cuenta de Google.')} />;
  } else if (vinculado) {
    contenido = (
      <div className={estilos.formulario}>
        <p className={estilos.explicacion}>
          Tu cuenta tiene Google vinculado y no tiene contraseña: ingresas solo con Google. Por eso no se puede desvincular, te
          quedarías sin forma de entrar. Si quieres poder desvincularlo, cambiar tu correo o darte de baja, primero crea una contraseña.
        </p>
        <CrearContrasena correo={datosCuenta.correo} />
      </div>
    );
  } else if (tieneContrasena) {
    contenido = <Vincular datosCuenta={datosCuenta} alEmpezar={() => setExito(null)} alVincular={() => cambiar(true, 'Vinculamos tu cuenta de Google. Desde ahora puedes ingresar con ella.')} />;
  } else {
    contenido = (
      <div className={estilos.formulario}>
        <p className={estilos.explicacion}>Para vincular Google tu cuenta necesita una contraseña, que confirma que eres tú.</p>
        <CrearContrasena correo={datosCuenta.correo} />
      </div>
    );
  }

  return (
    <Tarjeta como="section" aria-labelledby="titulo-google" className={estilos.seccion}>
      <h2 id="titulo-google" className={estilos.subtitulo}>Ingreso con Google</h2>
      {exito && <div ref={refExito} tabIndex={-1} className={estilos.resultado}><Aviso tono="exito" titulo={exito} /></div>}
      {contenido}
    </Tarjeta>
  );
}
