import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Eye, MessageSquareReply, Package, PackagePlus, Pencil, Trash2 } from 'lucide-react';
import { Boton } from '../../componentes/ui';
import { cotizaciones } from '../../api/cotizaciones';
import { useAvisos } from '../../avisos/Avisos';
import { GESTION } from '../../estructuras/navegacion';
import { useSesion } from '../../sesion/sesion';
import { Confirmacion } from '../compartidos/Dialogos';
import { DialogoRespuesta } from './DialogoRespuesta';
import { numeroCotizacion } from './reglasCotizacion';
import estilos from './Cotizaciones.module.css';

/**
 * Lo que se puede hacer con una cotización según su estado (ficha de la cotización y sección de la solicitud):
 * - pendiente: registrar la respuesta del cliente (la acción principal) y editarla;
 * - aprobada sin pedido: crear el pedido (la acción principal); con pedido, ir a él; si se eliminó, decirlo;
 * - gestión: eliminarla (borrado lógico) si está pendiente o rechazada y sin pedido.
 * `alCambiar` recarga quien la muestra; ante un 409 se recarga detrás del diálogo.
 */
export function AccionesCotizacion({ cotizacion, alCambiar, conVer = false, anchoCompleto = false, alEliminar }) {
  const { tieneRol } = useSesion();
  const { anunciar } = useAvisos();
  const navegar = useNavigate();
  const [dialogo, setDialogo] = useState(null);
  const numero = numeroCotizacion(cotizacion);
  const id = cotizacion.idCotizacion;
  const puedeEliminar = tieneRol(GESTION) && ['pendiente', 'rechazada'].includes(cotizacion.estado) && !cotizacion.idPedido;

  async function conRecarga(accion) {
    try {
      await accion();
    } catch (falla) {
      if (falla?.estado === 409) alCambiar?.();
      throw falla;
    }
  }

  async function responder(respuesta) {
    await conRecarga(() => cotizaciones.responder(id, respuesta, cotizacion.rowVersion));
    setDialogo(null);
    anunciar(respuesta.decision === 'aprobada'
      ? { tono: 'exito', titulo: `Cotización ${numero} aprobada`, texto: 'La solicitud quedó «Aceptada». Ya puedes crear el pedido.' }
      : { tono: 'exito', titulo: `Cotización ${numero} rechazada`, texto: 'La solicitud volvió a «Solicitada» para cotizarla de nuevo.' });
    alCambiar?.();
  }

  async function eliminar() {
    await conRecarga(() => cotizaciones.eliminar(id, cotizacion.rowVersion));
    setDialogo(null);
    anunciar({ tono: 'exito', titulo: `Cotización ${numero} eliminada` });
    if (alEliminar) alEliminar();
    else navegar('/panel/cotizaciones', { replace: true });
  }

  const comun = { anchoCompleto };
  return (
    <div className={estilos.acciones} style={anchoCompleto ? { display: 'grid' } : undefined}>
      {cotizacion.estado === 'pendiente' && (
        <>
          <Boton iconoInicio={MessageSquareReply} onClick={() => setDialogo('respuesta')} {...comun}>Registrar respuesta del cliente</Boton>
          <Boton como={Link} to={`/panel/cotizaciones/${id}/editar`} variante="secundario" iconoInicio={Pencil} {...comun}>Editar cotización</Boton>
        </>
      )}
      {cotizacion.estado === 'aprobada' && !cotizacion.idPedido && (
        <Boton como={Link} to={`/panel/cotizaciones/${id}/pedido`} iconoInicio={PackagePlus} {...comun}>Crear pedido</Boton>
      )}
      {cotizacion.idPedido && !cotizacion.pedidoEliminado && (
        <Boton como={Link} to={`/panel/pedidos/${cotizacion.idPedido}`} variante="secundario" iconoInicio={Package} {...comun}>Ver el pedido</Boton>
      )}
      {/* Un pedido eliminado (borrado lógico) no se reemplaza: la cotización ya dio su pedido. */}
      {cotizacion.idPedido && cotizacion.pedidoEliminado && (
        <p className={estilos.nota}>El pedido <span className="cifras">#{cotizacion.idPedido}</span> fue eliminado.</p>
      )}
      {conVer && (
        <Boton como={Link} to={`/panel/cotizaciones/${id}`} variante="fantasma" iconoInicio={Eye} {...comun}>
          Ver cotización <span className="solo-lectores">{numero}</span>
        </Boton>
      )}
      {puedeEliminar && (
        <Boton variante="fantasma" iconoInicio={Trash2} onClick={() => setDialogo('eliminar')} {...comun}>Eliminar cotización</Boton>
      )}

      <DialogoRespuesta cotizacion={cotizacion} abierto={dialogo === 'respuesta'} alCerrar={() => setDialogo(null)} alResponder={responder} />
      <Confirmacion abierto={dialogo === 'eliminar'} titulo={`¿Eliminar la cotización ${numero}?`} entidad="esta cotización"
        descripcion={cotizacion.estado === 'pendiente'
          ? 'Deja de verse en el panel y la solicitud vuelve a «Solicitada». Queda guardada en la base.'
          : 'Deja de verse en el panel. Queda guardada en la base.'}
        accion="Eliminar cotización" variante="peligro" icono={Trash2} alCerrar={() => setDialogo(null)} alConfirmar={eliminar} />
    </div>
  );
}
