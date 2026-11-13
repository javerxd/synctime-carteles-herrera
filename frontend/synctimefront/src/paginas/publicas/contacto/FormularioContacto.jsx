import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Send } from 'lucide-react';
import { Aviso, Boton, Campo, Casilla, Enlace } from '../../../componentes/ui';
import { publico } from '../../../api/publico';
import { useConsulta } from '../../../utilidades/useConsulta';
import { LIMITES, ORDEN_CAMPOS, validarContacto } from './reglasContacto';
import estilos from './FormularioContacto.module.css';

const VACIO = { nombre: '', correo: '', telefono: '', asunto: '', mensaje: '', aceptaPrivacidad: false, sitioWeb: '' };

/**
 * Formulario de contacto de la portada (RFS08, E22). Valida al salir de cada campo y, desde entonces,
 * mientras se escribe; al enviar con errores, lleva el foco al primero. La casilla de privacidad viene
 * desmarcada. El campo «sitio web» es una trampa para robots: está oculto para las personas.
 */
export function FormularioContacto() {
  const documentos = useConsulta('documentos-legales', publico.documentosLegales);
  const [datos, setDatos] = useState(VACIO);
  const [tocados, setTocados] = useState({});
  const [erroresApi, setErroresApi] = useState({});
  const [estado, setEstado] = useState({ enviando: false, recibida: null, error: null });
  const refFormulario = useRef(null);
  const refResultado = useRef(null);

  // El resultado se muestra arriba del formulario: se lleva la vista y el foco ahí para que se vea (y se
  // lea) aunque el botón haya quedado al fondo de la pantalla.
  useEffect(() => {
    if (!estado.recibida && !estado.error) return;
    refResultado.current?.scrollIntoView?.({ block: 'nearest' });
    refResultado.current?.focus({ preventScroll: true });
  }, [estado.recibida, estado.error]);

  const enfocarCampo = (campo) => refFormulario.current?.querySelector(`[name="${campo}"]`)?.focus();

  const errores = { ...validarContacto(datos), ...erroresApi };
  const visibles = Object.fromEntries(Object.entries(errores).filter(([campo]) => tocados[campo]));

  function cambiar(campo, valor) {
    setDatos((d) => ({ ...d, [campo]: valor }));
    setErroresApi(({ [campo]: _descartado, ...resto }) => resto);
    if (estado.recibida) setEstado((e) => ({ ...e, recibida: null }));
  }

  const tocar = (campo) => setTocados((t) => ({ ...t, [campo]: true }));

  async function enviar(evento) {
    evento.preventDefault();
    const encontrados = validarContacto(datos);
    setTocados(Object.fromEntries(ORDEN_CAMPOS.map((c) => [c, true])));
    const primero = ORDEN_CAMPOS.find((c) => encontrados[c]);
    if (primero) {
      enfocarCampo(primero);
      return;
    }

    // El consentimiento se registra con la versión de la política que la persona tuvo a la vista: sin
    // ella no se envía nada.
    const versionPrivacidad = documentos.datos?.find((d) => d.tipo === 'privacidad')?.version;
    if (!versionPrivacidad) {
      documentos.recargar();
      setEstado({ enviando: false, recibida: null, error: 'No pudimos cargar la política de privacidad vigente. Espera unos segundos y vuelve a enviar.' });
      return;
    }
    setEstado({ enviando: true, recibida: null, error: null });
    try {
      const respuesta = await publico.enviarContacto({ ...datos, versionPrivacidad });
      setDatos(VACIO);
      setTocados({});
      setEstado({ enviando: false, recibida: respuesta?.mensaje ?? 'Recibimos tu consulta.', error: null });
    } catch (error) {
      // Los errores por campo de la API usan los mismos nombres que el formulario.
      const porCampo = Object.fromEntries(Object.entries(error?.errores ?? {}).map(([c, m]) => [c, m[0]]));
      setErroresApi(porCampo);
      setTocados((t) => ({ ...t, ...Object.fromEntries(Object.keys(porCampo).map((c) => [c, true])) }));
      const primerError = ORDEN_CAMPOS.find((c) => porCampo[c]);
      if (primerError) {
        setEstado({ enviando: false, recibida: null, error: null });
        // Tras volver a habilitar los campos.
        window.setTimeout(() => enfocarCampo(primerError), 0);
        return;
      }
      setEstado({
        enviando: false,
        recibida: null,
        error: error?.estado === 429
          ? 'Ya recibimos varias consultas desde tu conexión. Espera un rato y vuelve a intentarlo, o escríbenos por WhatsApp.'
          : error?.message || 'No pudimos enviar tu consulta. Inténtalo de nuevo en unos minutos.',
      });
    }
  }

  const propiedades = (campo) => ({
    name: campo,
    value: datos[campo],
    error: visibles[campo],
    onChange: (e) => cambiar(campo, e.target.value),
    onBlur: () => tocar(campo),
    disabled: estado.enviando,
  });

  return (
    <form ref={refFormulario} className={estilos.formulario} onSubmit={enviar} noValidate>
      <div ref={refResultado} tabIndex={-1} className={estilos.resultado}>
        <div role="status" aria-live="polite">
          {estado.recibida && <Aviso tono="exito" titulo="Consulta enviada">{estado.recibida}</Aviso>}
        </div>
        {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
      </div>

      <div className={estilos.fila}>
        <Campo etiqueta="Nombre" requerido maxLength={LIMITES.nombre} autoComplete="name" {...propiedades('nombre')} />
        <Campo etiqueta="Correo electrónico" requerido type="email" inputMode="email" maxLength={254} autoComplete="email"
          ayuda="Te respondemos aquí." {...propiedades('correo')} />
      </div>
      <div className={estilos.fila}>
        <Campo etiqueta="Teléfono" type="tel" inputMode="tel" maxLength={16} autoComplete="tel"
          ayuda="Opcional, por si prefieres que te llamemos." {...propiedades('telefono')} />
        <Campo etiqueta="Asunto" requerido maxLength={LIMITES.asunto} {...propiedades('asunto')} />
      </div>
      <Campo etiqueta="Mensaje" requerido como="textarea" rows={5} maxLength={LIMITES.mensaje}
        ayuda={`${datos.mensaje.trim().length} de ${LIMITES.mensaje} caracteres.`} {...propiedades('mensaje')} />

      {/* Trampa para robots: fuera de la vista, del orden de tabulación y del árbol accesible (inert). */}
      <div className={estilos.trampa} aria-hidden="true" inert>
        <label htmlFor="contacto-sitio">Sitio web</label>
        <input id="contacto-sitio" name="sitioWeb" tabIndex={-1} autoComplete="off" value={datos.sitioWeb}
          onChange={(e) => cambiar('sitioWeb', e.target.value)} />
      </div>

      <Casilla
        name="aceptaPrivacidad"
        etiqueta={<>Leí y acepto la <Enlace como={Link} to="/privacidad">política de privacidad</Enlace></>}
        descripcion="Usaremos tus datos solo para responder esta consulta."
        checked={datos.aceptaPrivacidad}
        onChange={(e) => { cambiar('aceptaPrivacidad', e.target.checked); tocar('aceptaPrivacidad'); }}
        error={visibles.aceptaPrivacidad}
        disabled={estado.enviando}
      />

      <div className={estilos.pie}>
        <Boton type="submit" iconoInicio={Send} cargando={estado.enviando} textoCargando="Enviando…">Enviar consulta</Boton>
        <p className={estilos.nota}>Respondemos en horario de atención, normalmente el mismo día.</p>
      </div>
    </form>
  );
}
