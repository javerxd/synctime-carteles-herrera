import { DoorClosed, DoorOpen } from 'lucide-react';
import { Aviso, Boton, Esqueleto, EstadoCartel } from '../../../componentes/ui';
import { unirClases } from '../../../componentes/ui/clases';
import { formatearFechaHora } from '../../../utilidades/formato';
import { textoEstado } from '../horario';
import { useEstadoLocal } from '../useEstadoLocal';
import { CARTEL_ABIERTO, CARTEL_CERRADO, estadoCartel } from './estadoCartel';
import estilos from './CartelLocal.module.css';

/**
 * Cartel grande de la portada (fase 12C): la imagen del cartel según el estado real del local y, a su lado,
 * los textos como texto real sobre una base sólida. La imagen es decorativa (alt vacío): el lector de
 * pantalla lee el estado, y el contraste no depende de la foto. Al abrir o cerrar el local las dos imágenes
 * se cruzan con un fundido; la región `aria-live="polite"` anuncia el cambio. Entra deslizándose una sola
 * vez al cargar la página (el contenedor no se vuelve a montar al actualizarse el estado).
 */
export function CartelGrande({ estado }) {
  const { datos, cargando, error, recargar } = estado;
  const cartel = estadoCartel(datos);
  return (
    <div className={estilos.grande}>
      <div className={estilos.letrero} aria-hidden="true">
        {[CARTEL_CERRADO, CARTEL_ABIERTO].map((imagen) => (
          <img
            key={imagen}
            src={imagen}
            alt=""
            width="960"
            height="720"
            decoding="async"
            data-activo={cartel?.imagen === imagen ? 'true' : 'false'}
            className={estilos.imagen}
          />
        ))}
      </div>
      <div className={estilos.textos} aria-live="polite" aria-atomic="true">
        {cargando && (
          <>
            <span className="solo-lectores">Cargando el estado del local…</span>
            <Esqueleto forma="bloque" alto="3rem" />
          </>
        )}
        {error && !datos && (
          <Aviso tono="aviso" titulo="No pudimos cargar el estado del local"
            accion={<Boton variante="secundario" tamano="compacto" onClick={recargar}>Reintentar</Boton>} />
        )}
        {datos && (
          <>
            <EstadoCartel abierto={datos.abiertoAhora} grande />
            <p className={estilos.mensaje}>{textoEstado(datos)}</p>
            {datos.motivo === 'ausente' && datos.cartel?.tiempoEstimadoRegreso && (
              <p className={estilos.dato}>Regreso estimado: <strong>{datos.cartel.tiempoEstimadoRegreso}</strong></p>
            )}
            {datos.cartel && (
              <p className={estilos.actualizado}>Cartel actualizado el {formatearFechaHora(datos.cartel.fechaUltimaModificacion)}</p>
            )}
          </>
        )}
      </div>
    </div>
  );
}

/**
 * Cartel pequeño de la esquina derecha de la barra: icono y estado. En pantallas angostas queda solo
 * «Abierto» o «Cerrado» (el complemento «/ Disponible», «/ Ausente» se oculta con CSS, no se duplica).
 */
export function CartelMini({ className }) {
  const { datos } = useEstadoLocal();
  const cartel = estadoCartel(datos);
  if (!cartel) return null;
  const Icono = cartel.abierto ? DoorOpen : DoorClosed;
  return (
    <p
      className={unirClases(estilos.mini, cartel.abierto ? estilos.miniAbierto : estilos.miniCerrado, className)}
      data-estado={cartel.abierto ? 'abierto' : 'cerrado'}
    >
      <Icono size={16} strokeWidth={2} aria-hidden="true" />
      <span>
        {cartel.etiqueta}
        <span className={estilos.complemento}> / {cartel.abierto ? 'Disponible' : 'Ausente'}</span>
      </span>
    </p>
  );
}
