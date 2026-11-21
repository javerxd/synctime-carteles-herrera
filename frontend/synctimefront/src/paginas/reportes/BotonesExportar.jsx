import { useEffect, useId, useRef } from 'react';
import { LoaderCircle, Printer } from 'lucide-react';
import { Aviso, Boton } from '../../componentes/ui';
import { unirClases } from '../../componentes/ui/clases';
import { IconoArchivo } from './IconosExportar';
import { anuncioExportacion, estaOcupada, FORMATOS, nombreDelReporte, textoDelCsv } from './reglasReportes';
import estilos from './Reportes.module.css';

/**
 * Los tres botones de exportación (RFU26): Excel en verde, CSV en azul y PDF en rojo, con su texto completo («Exportar a
 * Excel»), y «Imprimir». Mientras se prepara un archivo, el pulsado dice «Preparando…» y los TRES quedan inactivos
 * (un trabajo por persona), pero siguen enfocables (aria-disabled): el foco no se pierde. No bloquean la pantalla: los
 * filtros y las pestañas siguen funcionando. Un fallo es una alerta con su motivo (y «Reintentar» si reintentar sirve).
 */
export function BotonesExportar({ reporte, estado, alPedir, alReintentar, alDescargar, alCerrar }) {
  const id = useId();
  const ocupada = estaOcupada(estado);
  const anuncio = anuncioExportacion(estado);
  const botones = useRef({});
  const etiquetaDe = (formato) => FORMATOS.find((f) => f.id === formato)?.etiqueta;

  // El foco no se pierde: si el botón que se pulsó desaparece (Reintentar, Cerrar, Cerrar aviso, o el aviso de «listo» que pasa a un
  // fallo) y el foco cae al documento, vuelve al botón de ese formato (enfocable aunque esté inactivo).
  // Solo cuando la fase CAMBIA: al abrir la pantalla no se le roba el foco a nadie.
  const faseAnterior = useRef(null);
  useEffect(() => {
    const cambio = faseAnterior.current !== null && faseAnterior.current !== estado.fase;
    faseAnterior.current = estado.fase;
    if (!cambio || (document.activeElement && document.activeElement !== document.body)) return;
    (botones.current[estado.formato] ?? botones.current.xlsx)?.focus();
  }, [estado.fase, estado.formato]);

  return (
    <section aria-labelledby={`${id}-titulo`} className={estilos.exportar} data-no-imprimir>
      <div className={estilos.filaExportar}>
        <h2 id={`${id}-titulo`} className={estilos.rotuloExportar}>Exportar</h2>
        <div className={estilos.botonesExportar}>
          {FORMATOS.map(({ id: formato, etiqueta }) => {
            const pulsado = ocupada && estado.formato === formato;
            return (
              <button key={formato} ref={(nodo) => { botones.current[formato] = nodo; }} type="button" aria-disabled={ocupada ? 'true' : undefined} aria-busy={pulsado ? 'true' : undefined}
                className={unirClases(estilos.botonExportar, estilos[`exportar-${formato}`])}
                onClick={() => !ocupada && alPedir(formato)}>
                {pulsado ? <LoaderCircle size={22} strokeWidth={2} aria-hidden="true" className={estilos.giro} /> : <IconoArchivo formato={formato} />}
                <span>{pulsado ? `Preparando ${etiqueta}…` : `Exportar a ${etiqueta}`}</span>
              </button>
            );
          })}
          <Boton variante="secundario" iconoInicio={Printer} onClick={() => window.print()}>Imprimir</Boton>
        </div>
      </div>

      {/* La región de estado existe siempre y solo cambia su texto: así el lector de pantalla la anuncia. */}
      <p role="status" className={anuncio ? estilos.anuncio : 'solo-lectores'}>{anuncio}</p>
      {estado.fase === 'lista' && (
        <div className={estilos.listo}>
          <span>{estado.nombreArchivo}</span>
          <Boton variante="fantasma" tamano="compacto" onClick={alDescargar}>Descargar de nuevo</Boton>
          <Boton variante="fantasma" tamano="compacto" onClick={alCerrar}>Cerrar aviso</Boton>
        </div>
      )}
      {estado.fase === 'fallida' && (
        <Aviso tono="peligro" titulo="No pudimos preparar tu archivo"
          accion={(
            <>
              {estado.reintentable && <Boton variante="secundario" tamano="compacto" onClick={alReintentar}>Reintentar</Boton>}
              <Boton variante="fantasma" tamano="compacto" onClick={alCerrar}>Cerrar</Boton>
            </>
          )}>
          {estado.reporte && <span className={estilos.deQue}>{nombreDelReporte(estado.reporte)} en {etiquetaDe(estado.formato)}. </span>}
          {estado.mensaje}
        </Aviso>
      )}

      <p className={estilos.leyenda}>El archivo lleva los filtros de pantalla. {textoDelCsv(reporte)}</p>
    </section>
  );
}
