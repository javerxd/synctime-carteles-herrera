import { useEffect, useRef, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Aviso, Boton, Campo, Casilla, Enlace, Tarjeta } from '../../componentes/ui';
import { publico } from '../../api/publico';
import { useSesion } from '../../sesion/sesion';
import { useConsulta } from '../../utilidades/useConsulta';
import { LIMITES, ORDEN_REGISTRO, validarRegistro } from './reglasCuenta';
import { useFormulario } from './useFormulario';
import estilos from './Cuenta.module.css';

const VACIO = {
  nombre: '', apellido: '', telefono: '', correo: '', rut: '', direccion: '', password: '',
  aceptaTerminosYPrivacidad: false, aceptaMarketing: false,
};

const versionDe = (documentos, tipo) => documentos?.find((d) => d.tipo === tipo)?.version ?? null;

/**
 * /cuenta/registro (RFU04, E23): crea la cuenta del cliente. Las dos casillas vienen desmarcadas y
 * separadas: aceptar los términos y la privacidad es obligatorio; recibir novedades, opcional
 * (RNFP21: sin patrones engañosos). El consentimiento se envía con la versión vigente de cada documento.
 */
export default function Registro() {
  const { registrarCliente, esCliente, comprobando } = useSesion();
  const navegar = useNavigate();
  const ubicacion = useLocation();
  const documentos = useConsulta('documentos-legales', publico.documentosLegales);
  const formulario = useFormulario({ inicial: VACIO, validar: validarRegistro, orden: ORDEN_REGISTRO });
  const { datos, visibles, refFormulario, cambiar, tocar, propiedades } = formulario;
  const [estado, setEstado] = useState({ enviando: false, error: null });
  const refResultado = useRef(null);

  useEffect(() => {
    if (!estado.error) return;
    refResultado.current?.scrollIntoView?.({ block: 'nearest' });
    refResultado.current?.focus({ preventScroll: true });
  }, [estado.error]);

  if (esCliente && !comprobando && !estado.enviando) return <Navigate to="/cuenta" replace />;

  async function enviar(evento) {
    evento.preventDefault();
    if (!formulario.comprobar()) return;

    const versionTerminos = versionDe(documentos.datos, 'terminos');
    const versionPrivacidad = versionDe(documentos.datos, 'privacidad');
    if (!versionTerminos || !versionPrivacidad) {
      documentos.recargar();
      setEstado({ enviando: false, error: 'No pudimos cargar los términos y la política de privacidad vigentes. Espera unos segundos y vuelve a enviar.' });
      return;
    }

    const versionMarketing = versionDe(documentos.datos, 'marketing');
    if (datos.aceptaMarketing && !versionMarketing) {
      documentos.recargar();
      setEstado({ enviando: false, error: 'No pudimos cargar el texto vigente de las novedades y promociones. Espera unos segundos y vuelve a enviar.' });
      return;
    }

    setEstado({ enviando: true, error: null });
    try {
      await registrarCliente({
        nombre: datos.nombre.trim(),
        apellido: datos.apellido.trim(),
        telefono: datos.telefono.trim(),
        correo: datos.correo.trim(),
        rut: datos.rut.trim() || null,
        direccion: datos.direccion.trim() || null,
        password: datos.password,
        aceptaTerminosYPrivacidad: datos.aceptaTerminosYPrivacidad,
        aceptaMarketing: datos.aceptaMarketing,
        versionTerminos,
        versionPrivacidad,
        versionMarketing: datos.aceptaMarketing ? versionMarketing : null,
      });
      navegar('/cuenta', { replace: true, state: { bienvenida: true } });
    } catch (error) {
      if (formulario.aplicarErroresApi(error)) {
        setEstado({ enviando: false, error: null });
        return;
      }
      // 409: el correo, el teléfono o el RUT ya tienen cuenta, o los documentos cambiaron mientras tanto.
      if (error?.estado === 409) documentos.recargar();
      setEstado({ enviando: false, error: error?.message || 'No pudimos crear tu cuenta. Inténtalo de nuevo en unos minutos.' });
    }
  }

  const deshabilitado = estado.enviando;
  const texto = (campo) => ({ ...propiedades(campo), disabled: deshabilitado });

  return (
    <div className={estilos.pagina}>
      <Tarjeta como="section" aria-labelledby="titulo-registro" className={`${estilos.tarjeta} ${estilos.tarjetaAncha}`}>
        <h1 id="titulo-registro" className={estilos.titulo}>Crear cuenta</h1>
        <p className={estilos.bajada}>
          Con tu cuenta sigues tus pedidos y cotizaciones desde el celular. Los campos sin la marca «Obligatorio» son opcionales.
        </p>

        <form ref={refFormulario} className={estilos.formulario} onSubmit={enviar} noValidate>
          <div ref={refResultado} tabIndex={-1} className={estilos.resultado}>
            {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
          </div>

          <Campo etiqueta="Nombre" requerido maxLength={LIMITES.nombre} autoComplete="given-name" {...texto('nombre')} />
          <Campo etiqueta="Apellido" requerido maxLength={LIMITES.nombre} autoComplete="family-name" {...texto('apellido')} />
          <Campo etiqueta="Teléfono" requerido type="tel" inputMode="tel" maxLength={16} autoComplete="tel-national"
            ayuda="9 dígitos, por ejemplo 9 1234 5678." {...texto('telefono')} />
          <Campo etiqueta="Correo electrónico" requerido type="email" inputMode="email" maxLength={LIMITES.correo} autoComplete="email"
            ayuda="Con él ingresas y recuperas tu contraseña. Te enviaremos un enlace para confirmarlo." {...texto('correo')} />
          <Campo etiqueta="RUT" maxLength={12} autoComplete="off" ayuda="Opcional. Con dígito verificador, por ejemplo 12.345.678-5."
            {...texto('rut')} />
          <Campo etiqueta="Dirección" maxLength={LIMITES.direccion} autoComplete="street-address" ayuda="Opcional."
            {...texto('direccion')} />
          <Campo etiqueta="Contraseña" requerido type="password" maxLength={LIMITES.passwordMaxima} autoComplete="new-password"
            ayuda={`Entre ${LIMITES.passwordMinima} y ${LIMITES.passwordMaxima} caracteres.`} {...texto('password')} />

          <fieldset className={estilos.consentimientos} disabled={deshabilitado}>
            <legend>Tus autorizaciones</legend>
            <Casilla
              name="aceptaTerminosYPrivacidad"
              etiqueta="Leí y acepto los términos y condiciones y la política de privacidad"
              descripcion={(
                <>
                  Obligatorio para crear la cuenta. Léelos aquí: <Enlace como={Link} to="/terminos" externo>términos y
                  condiciones</Enlace> y <Enlace como={Link} to="/privacidad" externo>política de privacidad</Enlace>.
                </>
              )}
              checked={datos.aceptaTerminosYPrivacidad}
              onChange={(e) => { cambiar('aceptaTerminosYPrivacidad', e.target.checked); tocar('aceptaTerminosYPrivacidad'); }}
              error={visibles.aceptaTerminosYPrivacidad}
            />
            <Casilla
              name="aceptaMarketing"
              etiqueta="Quiero recibir novedades y promociones"
              descripcion="Opcional. Puedes cambiarlo cuando quieras desde Mi cuenta."
              checked={datos.aceptaMarketing}
              onChange={(e) => cambiar('aceptaMarketing', e.target.checked)}
            />
          </fieldset>

          <Boton type="submit" anchoCompleto cargando={estado.enviando} textoCargando="Creando tu cuenta…">Crear cuenta</Boton>
        </form>

        <p className={estilos.nota}>
          ¿Ya tienes cuenta? <Enlace como={Link} to="/ingresar" state={ubicacion.state}>Ingresa aquí</Enlace>.
        </p>
      </Tarjeta>
    </div>
  );
}
