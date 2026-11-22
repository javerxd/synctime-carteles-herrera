import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { Aviso, Boton, Campo, Enlace, Tarjeta } from '../../componentes/ui';
import authService from '../../api/authService';
import { errorCorreo, LIMITES, validarCodigo, validarNuevaPassword } from './reglasCuenta';
import { useFormulario } from './useFormulario';
import estilos from './Cuenta.module.css';

const ORDEN_CODIGO = ['codigo', 'password', 'repetida'];

function validarPasoCodigo({ codigo, password, repetida }) {
  const errores = validarNuevaPassword(password, repetida);
  const errorCodigo = validarCodigo(codigo);
  return errorCodigo ? { codigo: errorCodigo, ...errores } : errores;
}

const mensajeDe = (error, porDefecto) => error?.message || porDefecto;

/**
 * /cuenta/recuperar (RNFP13): en dos pasos en la misma página, pedir un código al correo y luego
 * escribirlo con la nueva contraseña. `?tipo=personal` lo usa el acceso del personal.
 */
export default function Recuperar() {
  const [parametros] = useSearchParams();
  const tipo = parametros.get('tipo') === 'personal' ? 'personal' : 'cliente';
  const ingreso = tipo === 'personal' ? '/personal/ingresar' : '/ingresar/cliente';
  const navegar = useNavigate();

  const [paso, setPaso] = useState('correo');
  // Desde Mi cuenta («Crear una contraseña», fase 14B) el correo de la cuenta ya viene escrito.
  const ubicacion = useLocation();
  const [correo, setCorreo] = useState(() => (typeof ubicacion.state?.correo === 'string' ? ubicacion.state.correo : ''));
  const [errorDeCorreo, setErrorDeCorreo] = useState(null);
  const [mensajeEnviado, setMensajeEnviado] = useState('');
  // Cuenta los envíos: cada reenvío vuelve a dibujar el aviso, que se anuncia y recibe el foco.
  const [envios, setEnvios] = useState(0);
  const refAvisoEnvio = useRef(null);
  const refError = useRef(null);
  const [estado, setEstado] = useState({ enviando: false, error: null });
  const formulario = useFormulario({ inicial: { codigo: '', password: '', repetida: '' }, validar: validarPasoCodigo, orden: ORDEN_CODIGO });
  const refPaso = useRef(null);
  const refCorreo = useRef(null);

  // Al cambiar de paso, el foco va al título del paso (o al correo): el lector anuncia dónde quedó la persona.
  const pasoMostrado = useRef(paso);
  useEffect(() => {
    if (pasoMostrado.current === paso) return;
    pasoMostrado.current = paso;
    (paso === 'codigo' ? refPaso.current : refCorreo.current)?.focus();
  }, [paso]);

  // Un reenvío (segundo envío o más) lleva el foco al aviso nuevo.
  useEffect(() => {
    if (envios > 1) refAvisoEnvio.current?.focus();
  }, [envios]);

  // Un error general, cuando los campos ya volvieron a habilitarse, recibe el foco: no se pierde en la página.
  useEffect(() => {
    if (estado.error && !estado.enviando) refError.current?.focus();
  }, [estado]);

  async function pedirCodigo(evento) {
    evento?.preventDefault();
    const error = errorCorreo(correo);
    setErrorDeCorreo(error);
    if (error) {
      refCorreo.current?.focus();
      return;
    }
    setEstado({ enviando: true, error: null });
    try {
      const respuesta = await authService.recuperar(tipo, correo.trim());
      const mensaje = respuesta?.mensaje ?? 'Si el correo tiene una cuenta, te enviamos un código.';
      setMensajeEnviado(paso === 'codigo' ? `${mensaje} Usa el código del correo más reciente; se puede pedir uno nuevo una vez por minuto.` : mensaje);
      setEstado({ enviando: false, error: null });
      setEnvios((n) => n + 1);
      setPaso('codigo');
    } catch (err) {
      setEstado({ enviando: false, error: mensajeDe(err, 'No pudimos enviar el código. Inténtalo de nuevo en unos minutos.') });
    }
  }

  async function restablecer(evento) {
    evento.preventDefault();
    if (!formulario.comprobar()) return;
    setEstado({ enviando: true, error: null });
    try {
      await authService.restablecer({ tipo, correo: correo.trim(), codigo: formulario.datos.codigo.trim(), password: formulario.datos.password });
      navegar(ingreso, { replace: true, state: { aviso: 'Tu contraseña se cambió. Ingresa con la nueva.' } });
    } catch (err) {
      if (formulario.aplicarErroresApi(err)) {
        setEstado({ enviando: false, error: null });
        return;
      }
      setEstado({ enviando: false, error: mensajeDe(err, 'No pudimos cambiar tu contraseña. Inténtalo de nuevo.') });
    }
  }

  function cambiarCorreo() {
    formulario.reiniciar();
    setMensajeEnviado('');
    setEnvios(0);
    setEstado({ enviando: false, error: null });
    setPaso('correo');
  }

  const { propiedades, refFormulario } = formulario;
  const deshabilitado = estado.enviando;

  return (
    <div className={estilos.pagina}>
      <Tarjeta como="section" aria-labelledby="titulo-recuperar" className={estilos.tarjeta}>
        <h1 id="titulo-recuperar" className={estilos.titulo}>Recuperar tu contraseña</h1>
        <p className={estilos.bajada}>
          {tipo === 'personal' ? 'Acceso del personal del taller. ' : ''}
          {paso === 'correo'
            ? 'Paso 1 de 2: te enviaremos un código de 6 dígitos a tu correo.'
            : 'Paso 2 de 2: escribe el código que te enviamos y elige una contraseña nueva.'}
        </p>

        <div role="status" aria-live="polite">
          {paso === 'codigo' && mensajeEnviado && (
            <div ref={refAvisoEnvio} tabIndex={-1} className={estilos.resultado} key={envios}>
              <Aviso tono="exito" titulo={envios > 1 ? 'Te enviamos otro código' : 'Revisa tu correo'}>{mensajeEnviado}</Aviso>
            </div>
          )}
        </div>
        <div ref={refError} tabIndex={-1} className={estilos.resultado}>
          {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
        </div>

        {paso === 'correo' ? (
          <form className={estilos.formulario} onSubmit={pedirCodigo} noValidate>
            <Campo
              ref={refCorreo}
              etiqueta="Correo electrónico"
              name="correo"
              type="email"
              inputMode="email"
              maxLength={LIMITES.correo}
              autoComplete="email"
              value={correo}
              onChange={(e) => { setCorreo(e.target.value); if (errorDeCorreo) setErrorDeCorreo(errorCorreo(e.target.value)); }}
              onBlur={() => correo && setErrorDeCorreo(errorCorreo(correo))}
              disabled={deshabilitado}
              error={errorDeCorreo}
            />
            <Boton type="submit" anchoCompleto cargando={estado.enviando} textoCargando="Enviando…">Enviar código</Boton>
          </form>
        ) : (
          <form ref={refFormulario} className={estilos.formulario} onSubmit={restablecer} noValidate>
            <h2 ref={refPaso} tabIndex={-1} className={estilos.subtituloPaso}>Escribe el código</h2>
            {/* Para que el gestor de contraseñas asocie la clave nueva a esta cuenta. */}
            <input type="email" name="username" autoComplete="username" value={correo.trim()} readOnly hidden />
            <div className={estilos.correoElegido}>
              <span>Código enviado a <strong>{correo.trim()}</strong></span>
              <Boton variante="fantasma" tamano="compacto" onClick={cambiarCorreo} disabled={deshabilitado}>Cambiar correo</Boton>
            </div>
            <Campo etiqueta="Código de 6 dígitos" requerido inputMode="numeric" autoComplete="one-time-code" maxLength={6}
              {...propiedades('codigo')} disabled={deshabilitado} />
            <Campo etiqueta="Contraseña nueva" requerido type="password" autoComplete="new-password" maxLength={LIMITES.passwordMaxima}
              ayuda={`Entre ${LIMITES.passwordMinima} y ${LIMITES.passwordMaxima} caracteres.`} {...propiedades('password')} disabled={deshabilitado} />
            <Campo etiqueta="Repite la contraseña nueva" requerido type="password" autoComplete="new-password" maxLength={LIMITES.passwordMaxima}
              {...propiedades('repetida')} disabled={deshabilitado} />
            <div className={estilos.acciones}>
              <Boton type="submit" anchoCompleto cargando={estado.enviando} textoCargando="Cambiando…">Cambiar contraseña</Boton>
              <Boton variante="secundario" anchoCompleto onClick={() => pedirCodigo()} disabled={deshabilitado}>Enviar otro código</Boton>
            </div>
          </form>
        )}

        <p className={estilos.nota}>
          ¿Recordaste tu contraseña? <Enlace como={Link} to={ingreso}>Volver a ingresar</Enlace>.
        </p>
      </Tarjeta>
    </div>
  );
}
