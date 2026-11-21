import { useEffect, useRef, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Aviso, Boton, Campo, Enlace, Tarjeta } from '../../componentes/ui';
import { publico } from '../../api/publico';
import { paseGoogle } from '../../google/pase';
import { useSesion } from '../../sesion/sesion';
import { useConsulta } from '../../utilidades/useConsulta';
import { Consentimientos } from './Consentimientos';
import { LIMITES, ORDEN_COMPLETAR_GOOGLE, validarCompletarGoogle } from './reglasCuenta';
import { useFormulario } from './useFormulario';
import estilos from './Cuenta.module.css';

const versionDe = (documentos, tipo) => documentos?.find((d) => d.tipo === tipo)?.version ?? null;

/** Adónde vuelve quien llega sin pase (recarga, pase vencido o ya usado): al ingreso, con el motivo. */
const AVISO_VENCIDO = {
  aviso: 'Tu registro con Google venció. Pulsa «Continuar con Google» para empezar de nuevo.',
  avisoTono: 'aviso',
};

/**
 * /cuenta/google/completar (RFS03, RFS12, RFU06): «Completa tu registro». Google ya verificó el correo y entregó el nombre;
 * aquí se pide lo que no entrega (apellido si falta, teléfono) y las autorizaciones, con las casillas SIN marcar y los textos
 * y versiones de siempre. La cuenta nace con Google y sin contraseña. El pase vive solo en memoria (paseGoogle): si se recarga
 * la página o vence, vuelve a /ingresar/cliente con un aviso.
 */
export default function CompletarGoogle() {
  const { completarRegistroGoogle, esCliente, comprobando } = useSesion();
  const navegar = useNavigate();
  const ubicacion = useLocation();
  // Lo que Google entregó, tomado una vez: correo y nombre se muestran; el pase se vuelve a leer al enviar.
  const [deGoogle] = useState(() => paseGoogle.obtener());
  const documentos = useConsulta('documentos-legales', publico.documentosLegales);
  const formulario = useFormulario({
    inicial: {
      nombre: deGoogle?.nombre ?? '', apellido: deGoogle?.apellido ?? '', telefono: '', rut: '', direccion: '',
      aceptaTerminosYPrivacidad: false, aceptaMarketing: false,
    },
    validar: validarCompletarGoogle,
    orden: ORDEN_COMPLETAR_GOOGLE,
  });
  const { datos, refFormulario, propiedades } = formulario;
  const [estado, setEstado] = useState({ enviando: false, error: null });
  const refResultado = useRef(null);

  useEffect(() => {
    if (!estado.error) return;
    refResultado.current?.scrollIntoView?.({ block: 'nearest' });
    refResultado.current?.focus({ preventScroll: true });
  }, [estado.error]);

  if (esCliente && !comprobando && !estado.enviando) return <Navigate to="/cuenta" replace />;
  if (!deGoogle) return <Navigate to="/ingresar/cliente" replace state={AVISO_VENCIDO} />;

  function volverAlIngreso() {
    paseGoogle.limpiar();
    navegar('/ingresar/cliente', { replace: true, state: AVISO_VENCIDO });
  }

  async function enviar(evento) {
    evento.preventDefault();
    if (!formulario.comprobar()) return;

    const vigente = paseGoogle.obtener();
    if (!vigente) {
      volverAlIngreso();
      return;
    }
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
      // El pase va solo en el cuerpo de la petición: nunca en la URL ni en el almacenamiento del navegador.
      await completarRegistroGoogle({
        pase: vigente.pase,
        nombre: datos.nombre.trim(),
        apellido: datos.apellido.trim(),
        telefono: datos.telefono.trim(),
        rut: datos.rut.trim() || null,
        direccion: datos.direccion.trim() || null,
        aceptaTerminosYPrivacidad: datos.aceptaTerminosYPrivacidad,
        aceptaMarketing: datos.aceptaMarketing,
        versionTerminos,
        versionPrivacidad,
        versionMarketing: datos.aceptaMarketing ? versionMarketing : null,
      });
      navegar('/cuenta', { replace: true, state: { bienvenida: true } });
    } catch (error) {
      // El pase venció o ya se usó: no se puede reintentar con él.
      if (error?.codigo === 'google_pase_invalido') {
        volverAlIngreso();
        return;
      }
      // 400 por campo: el pase sigue sirviendo para corregir y volver a enviar.
      if (formulario.aplicarErroresApi(error)) {
        setEstado({ enviando: false, error: null });
        return;
      }
      // 409: el teléfono o el RUT ya están en el taller, o los textos legales cambiaron mientras tanto.
      if (error?.estado === 409) documentos.recargar();
      setEstado({ enviando: false, error: error?.message || 'No pudimos crear tu cuenta. Inténtalo de nuevo en unos minutos.' });
    }
  }

  const deshabilitado = estado.enviando;
  const texto = (campo) => ({ ...propiedades(campo), disabled: deshabilitado });

  return (
    <div className={estilos.pagina}>
      <Tarjeta como="section" aria-labelledby="titulo-completar" className={`${estilos.tarjeta} ${estilos.tarjetaAncha}`}>
        <h1 id="titulo-completar" className={estilos.titulo}>Completa tu registro</h1>
        <p className={estilos.bajada}>
          Google confirmó tu correo. Solo falta lo que Google no nos entrega. Los campos sin la marca «Obligatorio» son opcionales. Tienes 10 minutos para terminar este paso; si se vencen, empiezas de nuevo con Google.
        </p>

        <dl className={estilos.datosGoogle}>
          <div><dt>Correo de Google</dt><dd>{deGoogle.correo}</dd></div>
          {deGoogle.nombre && <div><dt>Nombre en Google</dt><dd>{deGoogle.nombre}</dd></div>}
        </dl>

        <form ref={refFormulario} className={estilos.formulario} onSubmit={enviar} noValidate>
          <div ref={refResultado} tabIndex={-1} className={estilos.resultado}>
            {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
          </div>

          <Campo etiqueta="Nombre" requerido maxLength={LIMITES.nombre} autoComplete="given-name" {...texto('nombre')} />
          <Campo etiqueta="Apellido" requerido maxLength={LIMITES.nombre} autoComplete="family-name" {...texto('apellido')} />
          <Campo etiqueta="Teléfono" requerido type="tel" inputMode="tel" maxLength={16} autoComplete="tel-national"
            ayuda="9 dígitos, por ejemplo 9 1234 5678." {...texto('telefono')} />
          <Campo etiqueta="RUT" maxLength={12} autoComplete="off" ayuda="Opcional. Con dígito verificador, por ejemplo 12.345.678-5."
            {...texto('rut')} />
          <Campo etiqueta="Dirección" maxLength={LIMITES.direccion} autoComplete="street-address" ayuda="Opcional."
            {...texto('direccion')} />

          <Consentimientos formulario={formulario} deshabilitado={deshabilitado} />

          <Boton type="submit" anchoCompleto cargando={estado.enviando} textoCargando="Creando tu cuenta…">Crear mi cuenta</Boton>
        </form>

        <p className={estilos.nota}>
          ¿Prefieres ingresar de otra forma?{' '}
          <Enlace como={Link} to="/ingresar/cliente" state={ubicacion.state} onClick={() => paseGoogle.limpiar()}>Volver al ingreso</Enlace>.
        </p>
      </Tarjeta>
    </div>
  );
}
