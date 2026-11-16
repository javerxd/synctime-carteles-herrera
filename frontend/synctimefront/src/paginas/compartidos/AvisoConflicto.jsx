import { RotateCcw, Save } from 'lucide-react';
import { Aviso, Boton } from '../../componentes/ui';
import estilos from './Compartidos.module.css';

/**
 * El aviso de una versión vieja al guardar, sin perder lo escrito. Mientras se relee la versión actual lo dice;
 * luego ofrece «Guardar sobre la versión actual» (si todavía se puede) y «Descartar mis cambios y ver la versión
 * actual». `resumen` cuenta en una línea cómo quedó la versión actual; `noSePuede`, por qué ya no se puede guardar.
 */
export function AvisoConflicto({
  conflicto, entidad, resumen, noSePuede, guardando = false, alGuardarSobre, alDescartar, alReintentar, refAccion,
}) {
  if (!conflicto) return null;
  const listo = conflicto.estado === 'listo';
  return (
    <Aviso tono="peligro" titulo={`Otra persona modificó ${entidad} mientras lo editabas`}>
      <div className={estilos.conflicto}>
        <p>Tus cambios siguen aquí y no se guardaron.</p>
        {conflicto.estado === 'buscando' && <p>Buscando la versión actual…</p>}
        {conflicto.estado === 'error' && <p>No pudimos traer la versión actual. Revisa tu conexión y vuelve a intentarlo.</p>}
        {listo && resumen && <p>Versión actual: {resumen}</p>}
        {listo && noSePuede && <p><strong>{noSePuede}</strong></p>}
        <div className={estilos.accionesConflicto}>
          {listo && !noSePuede && (
            <Boton ref={refAccion} type="button" tamano="compacto" iconoInicio={Save} cargando={guardando} textoCargando="Guardando…" onClick={alGuardarSobre}>
              Guardar sobre la versión actual
            </Boton>
          )}
          {conflicto.estado === 'error' && (
            <Boton ref={refAccion} type="button" variante="secundario" tamano="compacto" iconoInicio={RotateCcw} onClick={alReintentar}>
              Buscar la versión actual
            </Boton>
          )}
          <Boton ref={listo && noSePuede ? refAccion : undefined} type="button" variante="secundario" tamano="compacto" iconoInicio={RotateCcw}
            onClick={alDescartar} disabled={guardando}>
            Descartar mis cambios y ver la versión actual
          </Boton>
        </div>
      </div>
    </Aviso>
  );
}
