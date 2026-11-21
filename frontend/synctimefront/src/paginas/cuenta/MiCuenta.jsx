import { useEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { Send } from 'lucide-react';
import { Aviso, Boton, Campo, Esqueleto, Tarjeta } from '../../componentes/ui';
import authService from '../../api/authService';
import { cuenta as apiCuenta } from '../../api/cuenta';
import { useAvisos } from '../../avisos/Avisos';
import { useSesion } from '../../sesion/sesion';
import { useConsulta } from '../../utilidades/useConsulta';
import { errorCorreo, LIMITES, telefonoLegible, validarDatos, validarNuevaPassword } from './reglasCuenta';
import { CorreosQueRecibo } from './CorreosQueRecibo';
import { IngresoConGoogle } from './IngresoConGoogle';
import { MisDatos } from './MisDatos';
import { useFormulario } from './useFormulario';
import estilos from './Cuenta.module.css';

const mensajeDe = (error, porDefecto) => error?.message || porDefecto;

/**
 * Resultado de un formulario: la región existe desde el inicio para que el lector anuncie el cambio.
 * Recibe el foco al terminar (los campos se deshabilitan al enviar y el foco no debe caer al cuerpo).
 */
function Resultado({ exito, error, refFoco }) {
  return (
    <div ref={refFoco} tabIndex={-1} className={estilos.resultado}>
      <div role="status" aria-live="polite">{exito && <Aviso tono="exito" titulo={exito} />}</div>
      {error && <Aviso tono="peligro" titulo={error} />}
    </div>
  );
}

/** Lleva el foco al resultado cuando la acción termina con un mensaje. */
function useFocoResultado({ ocupado, exito, error }) {
  const ref = useRef(null);
  useEffect(() => {
    if (!ocupado && (exito || error)) ref.current?.focus();
  }, [ocupado, exito, error]);
  return ref;
}

/** Envía un formulario de la cuenta: errores por campo junto a su campo; los demás, arriba del formulario. */
function useEnvio(formulario) {
  const [estado, setEstado] = useState({ enviando: false, exito: null, error: null });
  async function enviar(accion, { exito, porDefecto }) {
    if (!formulario.comprobar()) return;
    setEstado({ enviando: true, exito: null, error: null });
    try {
      await accion();
      setEstado({ enviando: false, exito, error: null });
    } catch (error) {
      if (formulario.aplicarErroresApi(error)) setEstado({ enviando: false, exito: null, error: null });
      else setEstado({ enviando: false, exito: null, error: mensajeDe(error, porDefecto) });
    }
  }
  const refFoco = useFocoResultado({ ocupado: estado.enviando, exito: estado.exito, error: estado.error });
  return { ...estado, enviar, refFoco };
}

const ORDEN_DATOS = ['nombre', 'apellido', 'telefono', 'rut', 'direccion'];

function FormularioDatos({ datosCuenta, alGuardar }) {
  const { actualizarUsuario } = useSesion();
  const formulario = useFormulario({
    inicial: {
      nombre: datosCuenta.nombre ?? '',
      apellido: datosCuenta.apellido ?? '',
      telefono: telefonoLegible(datosCuenta.telefono ?? ''),
      rut: datosCuenta.rut ?? '',
      direccion: datosCuenta.direccion ?? '',
    },
    validar: validarDatos,
    orden: ORDEN_DATOS,
  });
  const envio = useEnvio(formulario);
  const { datos, propiedades, refFormulario } = formulario;

  function guardar(evento) {
    evento.preventDefault();
    envio.enviar(async () => {
      const nueva = await apiCuenta.actualizar({
        nombre: datos.nombre.trim(),
        apellido: datos.apellido.trim(),
        telefono: datos.telefono.trim(),
        rut: datos.rut.trim() || null,
        direccion: datos.direccion.trim() || null,
      });
      alGuardar(nueva);
      actualizarUsuario({ nombre: nueva.nombre, apellido: nueva.apellido, telefono: nueva.telefono });
    }, { exito: 'Guardamos tus datos.', porDefecto: 'No pudimos guardar tus datos. Inténtalo de nuevo.' });
  }

  const campo = (nombre) => ({ ...propiedades(nombre), disabled: envio.enviando });

  return (
    <Tarjeta como="section" aria-labelledby="titulo-datos" className={estilos.seccion}>
      <h2 id="titulo-datos" className={estilos.subtitulo}>Tus datos</h2>
      <form ref={refFormulario} className={estilos.formulario} onSubmit={guardar} noValidate>
        <Resultado exito={envio.exito} error={envio.error} refFoco={envio.refFoco} />
        <Campo etiqueta="Nombre" requerido maxLength={LIMITES.nombre} autoComplete="given-name" {...campo('nombre')} />
        <Campo etiqueta="Apellido" requerido maxLength={LIMITES.nombre} autoComplete="family-name" {...campo('apellido')} />
        <Campo etiqueta="Teléfono" requerido type="tel" inputMode="tel" maxLength={16} autoComplete="tel-national"
          ayuda="9 dígitos, por ejemplo 9 1234 5678." {...campo('telefono')} />
        <Campo etiqueta="RUT" maxLength={12} autoComplete="off" ayuda="Opcional. Con dígito verificador, por ejemplo 12.345.678-5."
          {...campo('rut')} />
        <Campo etiqueta="Dirección" maxLength={LIMITES.direccion} autoComplete="street-address" ayuda="Opcional." {...campo('direccion')} />
        <div className={estilos.pieSeccion}>
          <Boton type="submit" cargando={envio.enviando} textoCargando="Guardando…">Guardar datos</Boton>
        </div>
      </form>
    </Tarjeta>
  );
}

const ORDEN_CORREO = ['correo', 'passwordActual'];

function validarCorreoNuevo({ correo, passwordActual }) {
  const errores = {};
  const error = errorCorreo(correo);
  if (error) errores.correo = error;
  if (!passwordActual) errores.passwordActual = 'Escribe tu contraseña actual para confirmar que eres tú.';
  return errores;
}

function FormularioCorreo({ datosCuenta, alGuardar }) {
  const { actualizarUsuario } = useSesion();
  const formulario = useFormulario({ inicial: { correo: '', passwordActual: '' }, validar: validarCorreoNuevo, orden: ORDEN_CORREO });
  const envio = useEnvio(formulario);
  const { datos, propiedades, refFormulario } = formulario;

  function guardar(evento) {
    evento.preventDefault();
    const correo = datos.correo.trim();
    envio.enviar(async () => {
      const nueva = await apiCuenta.cambiarCorreo({ correo, passwordActual: datos.passwordActual });
      alGuardar(nueva);
      actualizarUsuario({ email: nueva.correo, correoVerificado: nueva.correoVerificado });
      formulario.reiniciar();
    }, {
      exito: `Cambiamos tu correo. Te enviamos un enlace a ${correo} para confirmarlo.`,
      porDefecto: 'No pudimos cambiar tu correo. Inténtalo de nuevo.',
    });
  }

  const campo = (nombre) => ({ ...propiedades(nombre), disabled: envio.enviando });

  return (
    <Tarjeta como="section" aria-labelledby="titulo-correo" className={estilos.seccion}>
      <h2 id="titulo-correo" className={estilos.subtitulo}>Correo</h2>
      <p className={estilos.explicacion}>
        Ingresas con <strong>{datosCuenta.correo}</strong>. Si lo cambias, te enviaremos un enlace al correo nuevo para confirmarlo.
      </p>
      <form ref={refFormulario} className={estilos.formulario} onSubmit={guardar} noValidate>
        <Resultado exito={envio.exito} error={envio.error} refFoco={envio.refFoco} />
        <Campo etiqueta="Correo nuevo" requerido type="email" inputMode="email" maxLength={LIMITES.correo} autoComplete="email"
          {...campo('correo')} />
        <Campo etiqueta="Contraseña actual" requerido type="password" maxLength={200} autoComplete="current-password"
          {...campo('passwordActual')} />
        <div className={estilos.pieSeccion}>
          <Boton type="submit" variante="secundario" cargando={envio.enviando} textoCargando="Cambiando…">Cambiar correo</Boton>
        </div>
      </form>
    </Tarjeta>
  );
}

const ORDEN_PASSWORD = ['passwordActual', 'passwordNueva', 'repetida'];

function validarCambioPassword({ passwordActual, passwordNueva, repetida }) {
  const errores = validarNuevaPassword(passwordNueva, repetida, 'passwordNueva');
  return passwordActual ? errores : { passwordActual: 'Escribe tu contraseña actual.', ...errores };
}

function FormularioPassword() {
  const { actualizarToken } = useSesion();
  const formulario = useFormulario({
    inicial: { passwordActual: '', passwordNueva: '', repetida: '' }, validar: validarCambioPassword, orden: ORDEN_PASSWORD,
  });
  const envio = useEnvio(formulario);
  const { datos, propiedades, refFormulario } = formulario;

  function guardar(evento) {
    evento.preventDefault();
    envio.enviar(async () => {
      const respuesta = await apiCuenta.cambiarPassword({ passwordActual: datos.passwordActual, passwordNueva: datos.passwordNueva });
      // La contraseña ya cambió; si la respuesta no trae token, esta sesión sigue con el actual hasta renovarse.
      if (respuesta?.token) actualizarToken(respuesta.token);
      formulario.reiniciar();
    }, {
      exito: 'Cambiamos tu contraseña. Por seguridad, cerramos tu sesión en los demás equipos.',
      porDefecto: 'No pudimos cambiar tu contraseña. Inténtalo de nuevo.',
    });
  }

  const campo = (nombre) => ({ ...propiedades(nombre), disabled: envio.enviando });

  return (
    <Tarjeta como="section" aria-labelledby="titulo-password" className={estilos.seccion}>
      <h2 id="titulo-password" className={estilos.subtitulo}>Contraseña</h2>
      <form ref={refFormulario} className={estilos.formulario} onSubmit={guardar} noValidate>
        <Resultado exito={envio.exito} error={envio.error} refFoco={envio.refFoco} />
        <Campo etiqueta="Contraseña actual" requerido type="password" maxLength={200} autoComplete="current-password"
          {...campo('passwordActual')} />
        <Campo etiqueta="Contraseña nueva" requerido type="password" maxLength={LIMITES.passwordMaxima} autoComplete="new-password"
          ayuda={`Entre ${LIMITES.passwordMinima} y ${LIMITES.passwordMaxima} caracteres. Se cerrará tu sesión en los demás equipos.`}
          {...campo('passwordNueva')} />
        <Campo etiqueta="Repite la contraseña nueva" requerido type="password" maxLength={LIMITES.passwordMaxima} autoComplete="new-password"
          {...campo('repetida')} />
        <div className={estilos.pieSeccion}>
          <Boton type="submit" variante="secundario" cargando={envio.enviando} textoCargando="Cambiando…">Cambiar contraseña</Boton>
        </div>
      </form>
    </Tarjeta>
  );
}

/** Aviso persistente mientras el correo no esté confirmado, con la opción de pedir otro enlace. */
function CorreoSinConfirmar({ correo }) {
  const { anunciar } = useAvisos();
  const [estado, setEstado] = useState({ enviando: false, enviado: null, error: null });

  async function reenviar() {
    setEstado({ enviando: true, enviado: null, error: null });
    try {
      const respuesta = await authService.reenviarVerificacion();
      const mensaje = respuesta?.mensaje ?? 'Te enviamos un enlace nuevo.';
      setEstado({ enviando: false, enviado: mensaje, error: null });
      anunciar({ tono: 'exito', titulo: 'Enlace enviado', texto: mensaje });
    } catch (error) {
      setEstado({ enviando: false, enviado: null, error: mensajeDe(error, 'No pudimos enviar el enlace. Inténtalo de nuevo en unos minutos.') });
    }
  }

  return (
    <Aviso
      tono="aviso"
      titulo="Confirma tu correo"
      accion={(
        <Boton variante="secundario" tamano="compacto" iconoInicio={Send} onClick={reenviar}
          cargando={estado.enviando} textoCargando="Enviando…">
          Reenviar enlace
        </Boton>
      )}
    >
      <p>Te enviamos un enlace a <strong>{correo}</strong>. Ábrelo para confirmar que el correo es tuyo; vence en 24 horas.</p>
      {estado.enviado && <p>{estado.enviado}</p>}
      {estado.error && <p><strong>{estado.error}</strong></p>}
    </Aviso>
  );
}

/**
 * /cuenta (RNFE01): el cliente ve y corrige sus datos, cambia su correo o su contraseña, elige qué correos opcionales
 * recibe y, desde la fase 11, descarga sus datos o da de baja la cuenta. Desde la fase 12D sus solicitudes y la
 * solicitud nueva están en el menú lateral (EstructuraCuenta), no en esta pantalla.
 */
export default function MiCuenta() {
  const { usuario } = useSesion();
  const ubicacion = useLocation();
  const consulta = useConsulta('mi-cuenta', apiCuenta.obtener);
  const [actualizada, setActualizada] = useState(null);
  const datosCuenta = actualizada ?? consulta.datos;
  // Actualizador funcional: cada cambio parte de la última versión, no de una copia capturada antes.
  const guardarCuenta = (cambio) => setActualizada((previa) => (typeof cambio === 'function' ? cambio(previa ?? consulta.datos) : cambio));
  const refTitulo = useRef(null);
  const bienvenida = Boolean(ubicacion.state?.bienvenida);

  useEffect(() => {
    if (bienvenida) refTitulo.current?.focus();
  }, [bienvenida]);

  return (
    <div className={estilos.cuenta}>
      <div className={estilos.cabecera}>
        <div>
          <h1 ref={refTitulo} tabIndex={-1} className={estilos.titulo}>Mi cuenta</h1>
          <p className={estilos.bajadaCuenta}>
            {[usuario?.nombre, usuario?.apellido].filter(Boolean).join(' ')}
            {datosCuenta?.correo ? ` · ${datosCuenta.correo}` : ''}
          </p>
        </div>
      </div>

      {bienvenida && (
        <Aviso tono="exito" titulo="Tu cuenta está lista">
          Ya puedes seguir tus pedidos y cotizaciones desde aquí.
        </Aviso>
      )}

      {consulta.cargando && !actualizada && (
        <Tarjeta>
          <p role="status" className="solo-lectores">Cargando tus datos…</p>
          <Esqueleto lineas={5} />
        </Tarjeta>
      )}

      {consulta.error && !datosCuenta && (
        <Aviso tono="peligro" titulo="No pudimos cargar tus datos"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {mensajeDe(consulta.error, 'Revisa tu conexión e inténtalo de nuevo.')}
        </Aviso>
      )}

      {datosCuenta && (
        <>
          {!datosCuenta.correoVerificado && <CorreoSinConfirmar correo={datosCuenta.correo} />}
          <FormularioDatos datosCuenta={datosCuenta} alGuardar={guardarCuenta} />
          {/* Una cuenta creada con Google no tiene contraseña: cambiar el correo o la contraseña piden la actual y no podrían
              funcionar. «Ingreso con Google» explica cómo crearla (fase 14B). */}
          {datosCuenta.tieneContrasena !== false && (
            <>
              <FormularioCorreo datosCuenta={datosCuenta} alGuardar={guardarCuenta} />
              <FormularioPassword />
            </>
          )}
          <IngresoConGoogle datosCuenta={datosCuenta} alGuardar={guardarCuenta} />
          <CorreosQueRecibo datosCuenta={datosCuenta} alGuardar={guardarCuenta} />
          <MisDatos />
        </>
      )}
    </div>
  );
}
