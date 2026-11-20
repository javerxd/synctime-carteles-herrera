import { DoorClosed, DoorOpen, Pause, Play } from 'lucide-react';
import { Aviso, Boton } from '../../../componentes/ui';
import { unirClases } from '../../../componentes/ui/clases';
import { formatearFechaHora } from '../../../utilidades/formato';
import { textoEstado } from '../horario';
import { useEstadoLocal } from '../useEstadoLocal';
import { estadoCartel } from './estadoCartel';
import { useMovimientoCartel } from './useMovimientoCartel';
import estilos from './CartelLocal.module.css';

/** Letrero colgado: cuerda, clavo, tablero con esquinas recortadas, ojales y doble filete (como el cartel del taller). */
function Letrero() {
  return (
    <svg className={estilos.dibujo} viewBox="0 0 400 350" aria-hidden="true" focusable="false">
      <path className={estilos.cuerda} d="M200 14 L62 124 M200 14 L338 124" />
      <path className={estilos.cuerdaHebra} d="M200 14 L62 124 M200 14 L338 124" />
      <circle className={estilos.clavo} cx="200" cy="14" r="8" />
      <path className={estilos.tablero} d="M42 90H358A22 22 0 0 0 380 112V316A22 22 0 0 0 358 338H42A22 22 0 0 0 20 316V112A22 22 0 0 0 42 90Z" />
      <path className={estilos.filete} d="M46 102H354A16 16 0 0 0 370 118V310A16 16 0 0 0 354 326H46A16 16 0 0 0 30 310V118A16 16 0 0 0 46 102Z" />
      <path className={estilos.fileteFino} d="M50 112H350A12 12 0 0 0 360 122V306A12 12 0 0 0 350 316H50A12 12 0 0 0 40 306V122A12 12 0 0 0 50 112Z" />
      <circle className={estilos.ojal} cx="62" cy="124" r="8" />
      <circle className={estilos.ojal} cx="338" cy="124" r="8" />
    </svg>
  );
}

/**
 * Cartel grande de la portada (fase 12C): un letrero hecho de cuerda, clavo y tablero (verde si el local está
 * abierto, rojo si está cerrado) con los textos reales dentro: «En el taller ahora.», «Abierto» o «Cerrado»,
 * la frase del estado y cuándo se actualizó. Los dibujos son decorativos; el lector de pantalla lee el
 * texto, y el contraste de las letras no depende de ninguna imagen. Entra deslizándose de izquierda a
 * derecha una sola vez al cargar y luego se balancea sin parar, como un cartel colgado. Al abrir o cerrar el
 * local el tablero cambia de color con un fundido y la región `aria-live="polite"` lo anuncia.
 * Con `prefers-reduced-motion` arranca quieto (y sin entrada); el botón de abajo lo mueve o lo pausa.
 */
export function CartelGrande({ estado }) {
  const { datos, cargando, error, recargar } = estado;
  const cartel = estadoCartel(datos);
  const { mover, alternar } = useMovimientoCartel();

  if (error && !datos) {
    return (
      <div className={estilos.grande}>
        <h2 id="titulo-mostrador" className={estilos.tituloAviso}>En el taller ahora</h2>
        <Aviso tono="aviso" titulo="No pudimos cargar el estado del local"
          accion={<Boton variante="secundario" tamano="compacto" onClick={recargar}>Reintentar</Boton>} />
      </div>
    );
  }

  return (
    <div className={estilos.grande} data-movimiento={mover ? 'si' : 'no'}>
      <div className={estilos.colgante} data-estado={cartel ? (cartel.abierto ? 'abierto' : 'cerrado') : 'cargando'}>
        <Letrero />
        <div className={estilos.contenido}>
          <h2 id="titulo-mostrador" className={estilos.titulo}>En el taller ahora.</h2>
          <div className={estilos.textos} aria-live="polite" aria-atomic="true">
            {cargando && <span className="solo-lectores">Cargando el estado del local…</span>}
            {datos && (
              <>
                <p className={estilos.estado}>{cartel.etiqueta}</p>
                <p className={estilos.cinta}><span>{textoEstado(datos)}</span></p>
                {datos.motivo === 'ausente' && datos.cartel?.tiempoEstimadoRegreso && (
                  <p className={estilos.regreso}>Regreso estimado: <strong>{datos.cartel.tiempoEstimadoRegreso}</strong></p>
                )}
                {datos.cartel && (
                  <p className={estilos.actualizado}>Cartel actualizado el {formatearFechaHora(datos.cartel.fechaUltimaModificacion)}</p>
                )}
              </>
            )}
          </div>
        </div>
      </div>
      <button type="button" className={estilos.movimiento} onClick={alternar}>
        {mover ? <Pause size={14} aria-hidden="true" /> : <Play size={14} aria-hidden="true" />}
        {mover ? 'Pausar el balanceo' : 'Balancear el cartel'}
      </button>
    </div>
  );
}

/** Cartel pequeño de la esquina derecha de la barra: icono y «Abierto» o «Cerrado». */
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
      <span>{cartel.etiqueta}</span>
    </p>
  );
}
