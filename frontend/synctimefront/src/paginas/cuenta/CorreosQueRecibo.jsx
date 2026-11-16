import { useEffect, useRef, useState } from 'react';
import { Aviso, Casilla, Tarjeta } from '../../componentes/ui';
import { cuenta as apiCuenta } from '../../api/cuenta';
import { publico } from '../../api/publico';
import { useConsulta } from '../../utilidades/useConsulta';
import estilos from './Cuenta.module.css';

const EXITOS = {
  avisosEstado: {
    true: 'Listo: te avisaremos por correo cuando cambie el estado de tus solicitudes y pedidos.',
    false: 'Listo: ya no te enviaremos avisos del estado de tus solicitudes y pedidos.',
  },
  marketing: {
    true: 'Listo: te enviaremos novedades y promociones.',
    false: 'Listo: ya no te enviaremos novedades ni promociones.',
  },
};

/**
 * Mi cuenta → «Correos que recibo» (fase 11, RFS08): los correos opcionales, cada uno con su casilla. Se guarda al
 * marcar o desmarcar (las dos preferencias juntas, PUT /mi-cuenta/preferencias) y cada cambio queda como
 * consentimiento. Los correos de cuenta y seguridad no se pueden apagar.
 */
export function CorreosQueRecibo({ datosCuenta, alGuardar }) {
  const documentos = useConsulta('documentos-legales', publico.documentosLegales);
  const [estado, setEstado] = useState({ guardando: null, exito: null, error: null });
  const refAvisos = useRef(null);
  const refMarketing = useRef(null);
  const refFoco = useRef(null);
  const actuales = { avisosEstado: datosCuenta.avisosEstado !== false, marketing: Boolean(datosCuenta.marketing) };

  // La casilla se deshabilita mientras guarda: al terminar, el foco vuelve a ella.
  useEffect(() => {
    if (!estado.guardando && refFoco.current && (estado.exito || estado.error)) {
      refFoco.current.focus();
      refFoco.current = null;
    }
  }, [estado]);

  async function cambiar(clave, valor) {
    if (estado.guardando) return;
    refFoco.current = (clave === 'marketing' ? refMarketing : refAvisos).current;
    const siguiente = { ...actuales, [clave]: valor };
    const version = documentos.datos?.find((d) => d.tipo === 'marketing')?.version;
    const aceptaMarketing = clave === 'marketing' && valor;
    if (aceptaMarketing && !version) {
      documentos.recargar();
      setEstado({ guardando: null, exito: null, error: 'No pudimos cargar el texto vigente de las novedades. Espera unos segundos y vuelve a intentarlo.' });
      return;
    }
    setEstado({ guardando: clave, exito: null, error: null });
    try {
      const respuesta = await apiCuenta.preferencias(aceptaMarketing ? { ...siguiente, versionMarketing: version } : siguiente);
      const guardadas = {
        avisosEstado: typeof respuesta?.avisosEstado === 'boolean' ? respuesta.avisosEstado : siguiente.avisosEstado,
        marketing: typeof respuesta?.marketing === 'boolean' ? respuesta.marketing : siguiente.marketing,
      };
      alGuardar((actual) => ({ ...actual, ...guardadas }));
      setEstado({ guardando: null, exito: EXITOS[clave][String(valor)], error: null });
    } catch (error) {
      setEstado({ guardando: null, exito: null, error: error?.message || 'No pudimos guardar tu preferencia. Inténtalo de nuevo.' });
    }
  }

  const ocupado = Boolean(estado.guardando);
  return (
    <Tarjeta como="section" aria-labelledby="titulo-correos-recibo" className={estilos.seccion}>
      <h2 id="titulo-correos-recibo" className={estilos.subtitulo}>Correos que recibo</h2>
      <div className={estilos.resultado}>
        <div role="status" aria-live="polite">{estado.exito && <Aviso tono="exito" titulo={estado.exito} />}</div>
        {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
      </div>
      <Casilla ref={refAvisos} etiqueta="Avisos del estado de mis solicitudes y pedidos"
        descripcion="Cuando cotizamos o respondemos tu solicitud y cuando tu pedido avanza, se entrega o se anula."
        checked={actuales.avisosEstado} onChange={(e) => cambiar('avisosEstado', e.target.checked)} disabled={ocupado} />
      <Casilla ref={refMarketing} etiqueta="Novedades y promociones"
        descripcion="Opcional. Puedes cambiarlo cuando quieras."
        checked={actuales.marketing} onChange={(e) => cambiar('marketing', e.target.checked)} disabled={ocupado} />
      <p className={estilos.explicacion}>Los correos de seguridad de tu cuenta siempre se envían.</p>
    </Tarjeta>
  );
}
