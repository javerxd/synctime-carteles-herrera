import { useRef } from 'react';
import { RotateCcw } from 'lucide-react';
import { Aviso, Boton, Dialogo } from '../../componentes/ui';
import { TRAS_CORTE } from './trasCorte';
import { useEnvioDialogo } from './useEnvioDialogo';

/**
 * El aviso de una acción que no resultó (fase 8, con el patrón de la ficha de solicitud). Ante un 409 la ficha
 * ya se recargó detrás del diálogo: si otra persona la cambió (version_obsoleta) se dice así; si el estado ya no
 * lo permite, se muestra la explicación de la API. `entidad`: «esta cotización», «este pedido».
 * `paraCliente`: el cliente no ve «otra persona» ni «pídeselo al dueño»; se le dice qué pasó con su respuesta.
 * Un corte sin respuesta (estado 0) se explica aparte: quien llama decide si revisar antes de reintentar.
 */
export function AvisoFalla({ falla, alVerActual, entidad = 'este registro', paraCliente = false }) {
  // «No supimos si se guardó» lo explica AvisoTrasCorte, con su botón para revisar.
  if (!falla || Object.values(TRAS_CORTE).includes(falla.codigo)) return null;
  const verActual = alVerActual
    ? <Boton variante="secundario" tamano="compacto" iconoInicio={RotateCcw} onClick={alVerActual}>Ver versión actual</Boton>
    : null;
  if (falla.estado === 409 && falla.codigo !== 'abono_excede_saldo') {
    const obsoleta = falla.codigo === 'version_obsoleta';
    if (paraCliente) {
      return (
        <Aviso tono="peligro" titulo={obsoleta ? 'La cotización cambió. Te mostramos la versión actual.' : falla.message || 'Ya no es posible responder esta cotización.'}
          accion={verActual}>
          No se guardó tu respuesta. Revisa la cotización y vuelve a responder si corresponde.
        </Aviso>
      );
    }
    return (
      <Aviso tono="peligro" titulo={obsoleta ? `Otra persona modificó ${entidad}` : falla.message || 'Esta acción ya no es posible'} accion={verActual}>
        No se guardó nada. Ya cargamos la versión actual: revísala y vuelve a intentarlo si corresponde.
      </Aviso>
    );
  }
  if (falla.estado === 403) {
    return paraCliente
      ? <Aviso tono="peligro" titulo="No pudimos registrar tu respuesta.">Si el problema sigue, escríbenos o llámanos.</Aviso>
      : <Aviso tono="peligro" titulo={falla.message || 'No tienes permiso para hacer esto.'}>Pídeselo al dueño o al administrador.</Aviso>;
  }
  return <Aviso tono="peligro" titulo={falla.message || 'No pudimos hacerlo. Inténtalo de nuevo.'} />;
}

/** Confirmación simple: el foco empieza en «Cancelar», como en una acción que no se deshace. */
export function Confirmacion({
  abierto, titulo, descripcion, accion, variante = 'secundario', icono, alCerrar, alConfirmar, entidad, children,
}) {
  const envio = useEnvioDialogo(abierto);
  const refCancelar = useRef(null);
  return (
    <Dialogo abierto={abierto} alCerrar={() => !envio.enviando && alCerrar()} titulo={titulo} descripcion={descripcion}
      enfoqueInicial={refCancelar} ancho="30rem"
      pie={(
        <>
          <Boton ref={refCancelar} variante="secundario" onClick={alCerrar} disabled={envio.enviando}>Cancelar</Boton>
          <Boton variante={variante} iconoInicio={icono} onClick={() => envio.ejecutar(alConfirmar)} cargando={envio.enviando}>{accion}</Boton>
        </>
      )}>
      {children}
      <AvisoFalla falla={envio.falla} alVerActual={alCerrar} entidad={entidad} />
    </Dialogo>
  );
}
