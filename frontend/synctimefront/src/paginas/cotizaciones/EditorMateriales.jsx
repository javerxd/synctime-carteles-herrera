import { useEffect, useRef, useState } from 'react';
import { Layers, Plus, Trash2 } from 'lucide-react';
import { Boton, Campo } from '../../componentes/ui';
import { formatearPesos } from '../../utilidades/formato';
import { useDebounce } from '../../utilidades/useDebounce';
import { calcularTotales, campoLinea, LIMITES_COTIZACION, subtotalLinea } from './reglasCotizacion';
import estilos from './Cotizaciones.module.css';

const porNombre = (a, b) => String(a.nombre).localeCompare(String(b.nombre), 'es');

/**
 * Líneas de material de la cotización: material del catálogo (sugiere su costo unitario), medida aplicada,
 * cantidad (hasta 2 decimales) y costo unitario en pesos, con el subtotal en vivo. Agregar una línea lleva el
 * foco a su material; quitarla, a «Agregar material», y ambas cosas se anuncian.
 */
export function EditorMateriales({
  editor, materiales = [], cargandoMateriales = false, deshabilitado = false, nivel = 3, titulo = 'Materiales',
}) {
  const { lineas, visibles, cambiarLinea, elegirMaterial, agregarLinea, quitarLinea, tocar } = editor;
  const Titulo = `h${nivel}`;
  const refAgregar = useRef(null);
  const refRaiz = useRef(null);
  const enfocar = useRef(null);
  const [anuncio, setAnuncio] = useState('');
  const lleno = lineas.length >= LIMITES_COTIZACION.lineas;
  const activos = materiales.filter((m) => m.activo !== false).sort(porNombre);

  useEffect(() => {
    if (enfocar.current === null) return;
    const destino = enfocar.current;
    enfocar.current = null;
    if (destino === 'agregar') refAgregar.current?.focus();
    else refRaiz.current?.querySelector(`[name="${destino}"]`)?.focus();
  }, [lineas.length]);

  function agregar() {
    if (lleno || deshabilitado) return;
    enfocar.current = campoLinea(lineas.length, 'idMaterial');
    agregarLinea();
    setAnuncio(`Agregaste el material ${lineas.length + 1}.`);
  }

  return (
    <section ref={refRaiz} aria-labelledby="titulo-materiales" className={estilos.bloqueEditor}>
      <div className={estilos.cabeceraBloque}>
        <Titulo id="titulo-materiales" className={estilos.subtituloEditor}>{titulo}</Titulo>
        <p className={estilos.nota}>Del catálogo del taller. El costo unitario se sugiere y puedes cambiarlo.</p>
      </div>

      {lineas.length === 0 && (
        <div className={estilos.sinLineas}>
          <Layers size={20} strokeWidth={1.5} aria-hidden="true" />
          <p>Sin materiales todavía. Agrega los que usa el trabajo, o cotiza solo con mano de obra y otros costos.</p>
        </div>
      )}

      {lineas.length > 0 && (
        <ol className={estilos.lineas}>
          {lineas.map((linea, i) => {
            const nombre = (campo) => campoLinea(i, campo);
            const propiedades = (campo) => ({
              name: nombre(campo),
              value: linea[campo],
              error: visibles[nombre(campo)],
              disabled: deshabilitado,
              onChange: (e) => cambiarLinea(i, campo, e.target.value),
              onBlur: () => tocar(nombre(campo)),
            });
            const material = materiales.find((m) => String(m.idMaterial) === String(linea.idMaterial));
            const unidad = material?.unidadMedida;
            const subtotal = subtotalLinea(linea);
            const inactivo = material && material.activo === false;
            return (
              <li key={linea.clave}>
                <fieldset className={estilos.linea}>
                  <legend className={estilos.leyendaLinea}>Material {i + 1}</legend>
                  <Boton variante="fantasma" tamano="compacto" iconoInicio={Trash2} className={estilos.quitarLinea} disabled={deshabilitado}
                    onClick={() => {
                      enfocar.current = 'agregar';
                      quitarLinea(i);
                      setAnuncio(`Quitaste el material ${i + 1}. Quedan ${lineas.length - 1}.`);
                    }}>
                    Quitar <span className="solo-lectores">el material {i + 1}</span>
                  </Boton>
                  <div className={estilos.camposLinea}>
                    <Campo etiqueta="Material" como="select" requerido className={estilos.campoMaterial} {...propiedades('idMaterial')}
                      disabled={deshabilitado || cargandoMateriales}
                      onChange={(e) => elegirMaterial(i, e.target.value)}>
                      <option value="">{cargandoMateriales ? 'Cargando…' : 'Elige uno'}</option>
                      {inactivo && <option value={material.idMaterial}>{material.nombre} (inactivo)</option>}
                      {activos.map((m) => <option key={m.idMaterial} value={m.idMaterial}>{m.nombre}</option>)}
                    </Campo>
                    {/* Etiquetas cortas para que los controles queden alineados; la medida es la única opcional y lo dice. */}
                    <Campo etiqueta="Medida (opcional)" autoComplete="off" maxLength={LIMITES_COTIZACION.medida} placeholder="Ej.: 120 × 60 cm"
                      {...propiedades('medidaAplicada')} />
                    <Campo etiqueta={unidad ? `Cantidad (${unidad})` : 'Cantidad'} inputMode="decimal" autoComplete="off" required
                      {...propiedades('cantidad')} />
                    <Campo etiqueta="Costo unitario ($)" inputMode="numeric" autoComplete="off" required {...propiedades('costoUnitario')} />
                  </div>
                  <div className={estilos.pieLinea}>
                    <p className={estilos.nota}>
                      {material ? <>Catálogo: <span className="cifras">{formatearPesos(material.costoUnitarioAprox)}</span>{unidad ? ` por ${unidad}` : ''}</> : 'Elige el material para ver su costo de referencia.'}
                    </p>
                    <p className={estilos.subtotalLinea}>
                      Subtotal <strong className="cifras">{subtotal === null ? '—' : formatearPesos(subtotal)}</strong>
                    </p>
                  </div>
                </fieldset>
              </li>
            );
          })}
        </ol>
      )}

      <div className={estilos.pieLineas}>
        <Boton ref={refAgregar} variante="secundario" iconoInicio={Plus} aria-disabled={lleno || deshabilitado ? 'true' : undefined} onClick={agregar}>
          Agregar material
        </Boton>
        {lleno && <p className={estilos.nota}>Llegaste al máximo de 30 materiales por cotización.</p>}
      </div>
      <p role="status" className="solo-lectores">{anuncio}</p>
    </section>
  );
}

/** Mano de obra, otros costos y observaciones (estas las ve el cliente). */
export function CostosCotizacion({ editor, deshabilitado = false, nivel = 3, titulo = 'Mano de obra y otros costos' }) {
  const Titulo = `h${nivel}`;
  const { propiedadesMonto } = editor;
  return (
    <section aria-labelledby="titulo-costos" className={estilos.bloqueEditor}>
      <Titulo id="titulo-costos" className={estilos.subtituloEditor}>{titulo}</Titulo>
      <div className={estilos.dosMontos}>
        <Campo etiqueta="Mano de obra ($)" inputMode="numeric" autoComplete="off" placeholder="0" ayuda="Las horas de trabajo del taller."
          disabled={deshabilitado} {...propiedadesMonto('costoManoObra')} />
        <Campo etiqueta="Otros costos ($)" inputMode="numeric" autoComplete="off" placeholder="0" ayuda="Traslados, diseño, permisos…"
          disabled={deshabilitado} {...propiedadesMonto('otrosCostos')} />
      </div>
      <Campo etiqueta="Observaciones para el cliente" como="textarea" rows={3} maxLength={LIMITES_COTIZACION.observaciones}
        ayuda="Opcional. Las ve el cliente en su cotización y en el correo que se la avisa: plazos, condiciones o lo que incluye." disabled={deshabilitado}
        {...propiedadesMonto('observaciones')} />
    </section>
  );
}

/**
 * El total en vivo para el pie del formulario. El lector de pantalla oye un solo mensaje con contexto
 * («Total de la cotización: $45.000») cuando se deja de escribir, no cada número.
 */
export function TotalEnVivo({ datos, etiqueta = 'Total de la cotización', extra = 0 }) {
  const { totalRedondeado } = calcularTotales(datos);
  const total = totalRedondeado + extra;
  const estable = useDebounce(total, 900);
  return (
    <div className={estilos.totalEnVivo}>
      <span className={estilos.totalEtiqueta}>{etiqueta}</span>
      <strong className={`cifras ${estilos.totalMonto}`}>{formatearPesos(total)}</strong>
      <span role="status" aria-atomic="true" className="solo-lectores">{`${etiqueta}: ${formatearPesos(estable)}`}</span>
    </div>
  );
}
