import { useEffect, useRef, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Boton, Campo } from '../../componentes/ui';
import { areaItem, areaTotal, campoItem, formatearArea, gruposDeOpciones, LIMITES_SOLICITUD, textoMedidas } from './reglasSolicitud';
import estilos from './Solicitudes.module.css';

/**
 * Letreros repetibles de una solicitud: tipo, ancho y alto en centímetros, cantidad y un detalle opcional,
 * con el área estimada en vivo. Lo usan el cliente, el registro presencial y la edición del personal.
 * `errores` son los visibles ({ 'detalles.0.ancho': '…' }); `alCambiar(indice, campo, valor)`.
 */
export function EditorItems({ items, productos, cargandoProductos, errores, alCambiar, alTocar, alAgregar, alQuitar, deshabilitado }) {
  const refAgregar = useRef(null);
  const enfocarNuevo = useRef(null);
  const refRaiz = useRef(null);
  const [anuncio, setAnuncio] = useState('');

  // Al agregar un letrero, el foco va a su tipo; al quitarlo, a «Agregar otro letrero».
  useEffect(() => {
    if (enfocarNuevo.current === null) return;
    const destino = enfocarNuevo.current;
    enfocarNuevo.current = null;
    if (destino === 'agregar') refAgregar.current?.focus();
    else refRaiz.current?.querySelector(`[name="${destino}"]`)?.focus();
  }, [items.length]);

  const total = areaTotal(items);
  const opciones = [...(productos ?? [])].sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
  const lleno = items.length >= LIMITES_SOLICITUD.items;
  const cm = (mm) => String(mm / 10).replace('.', ',');

  return (
    <div ref={refRaiz} className={estilos.items}>
      {items.map((item, i) => {
        const nombre = (campo) => campoItem(i, campo);
        const propiedades = (campo) => ({
          name: nombre(campo),
          value: item[campo],
          error: errores[nombre(campo)],
          disabled: deshabilitado,
          onChange: (e) => alCambiar(i, campo, e.target.value),
          onBlur: () => alTocar(nombre(campo)),
        });
        const area = areaItem(item);
        const producto = (productos ?? []).find((p) => String(p.idProducto) === String(item.idProducto));
        const medidas = textoMedidas(producto);
        const fijas = producto?.medidasFijas ?? [];
        const grupos = gruposDeOpciones(producto);
        const medidaActual = fijas.findIndex((m) => cm(m.anchoMm) === item.ancho && cm(m.altoMm) === item.alto);
        function elegirProducto(e) {
          const nuevo = (productos ?? []).find((p) => String(p.idProducto) === e.target.value);
          alCambiar(i, 'idProducto', e.target.value);
          // Lo elegido era del producto anterior; una medida única se completa sola.
          alCambiar(i, 'opciones', {});
          if (nuevo?.medidasFijas?.length === 1) {
            alCambiar(i, 'ancho', cm(nuevo.medidasFijas[0].anchoMm));
            alCambiar(i, 'alto', cm(nuevo.medidasFijas[0].altoMm));
          }
        }
        return (
          <fieldset key={item.clave} className={estilos.item}>
            <legend className={estilos.leyendaItem}>Letrero {i + 1}</legend>
            {items.length > 1 && (
              <Boton variante="fantasma" tamano="compacto" iconoInicio={Trash2} className={estilos.quitarItem} disabled={deshabilitado}
                onClick={() => {
                  enfocarNuevo.current = 'agregar';
                  alQuitar(i);
                  setAnuncio(`Quitaste el letrero ${i + 1}. Quedan ${items.length - 1}.`);
                }}>
                Quitar <span className="solo-lectores">el letrero {i + 1}</span>
              </Boton>
            )}
            <Campo etiqueta="Tipo de letrero" como="select" requerido {...propiedades('idProducto')} onChange={elegirProducto}
              disabled={deshabilitado || cargandoProductos}>
              <option value="">{cargandoProductos ? 'Cargando…' : 'Elige uno'}</option>
              {opciones.map((p) => <option key={p.idProducto} value={p.idProducto}>{p.nombre}</option>)}
            </Campo>
            {fijas.length > 1 && (
              <Campo etiqueta="Medida disponible" como="select" value={medidaActual >= 0 ? String(medidaActual) : ''} disabled={deshabilitado}
                onChange={(e) => {
                  const m = fijas[Number(e.target.value)];
                  if (!m) return;
                  alCambiar(i, 'ancho', cm(m.anchoMm));
                  alCambiar(i, 'alto', cm(m.altoMm));
                }}>
                <option value="">Elige una</option>
                {fijas.map((m, k) => <option key={`${m.anchoMm}x${m.altoMm}`} value={k}>{cm(m.anchoMm)} × {cm(m.altoMm)} cm</option>)}
              </Campo>
            )}
            {medidas && <p className={estilos.ayudaMedidas}>{medidas}</p>}
            <div className={estilos.medidas}>
              <Campo etiqueta="Ancho (cm)" inputMode="decimal" autoComplete="off" {...propiedades('ancho')} />
              <Campo etiqueta="Alto (cm)" inputMode="decimal" autoComplete="off" {...propiedades('alto')} />
              <Campo etiqueta="Cantidad" inputMode="numeric" requerido autoComplete="off" {...propiedades('cantidad')} />
            </div>
            {grupos.map(({ grupo, valores }, g) => (
              <Campo key={grupo} etiqueta={grupo} como="select" disabled={deshabilitado} value={item.opciones?.[grupo] ?? ''}
                // El primer grupo lleva el nombre del campo «opciones»: el error de la API se muestra y se enfoca ahí.
                {...(g === 0 ? { name: nombre('opciones') } : {})}
                error={g === 0 ? errores[nombre('opciones')] : undefined}
                onChange={(e) => alCambiar(i, 'opciones', { ...item.opciones, [grupo]: e.target.value })}>
                <option value="">Sin elegir</option>
                {valores.map((v) => <option key={v} value={v}>{v}</option>)}
              </Campo>
            ))}
            <Campo etiqueta="Detalle de este letrero" como="textarea" rows={2} maxLength={LIMITES_SOLICITUD.descripcionItem}
              ayuda="Opcional. Textos, colores o materiales de este letrero." {...propiedades('descripcion')} />
            <p className={estilos.area}>
              Área estimada: <strong className="cifras">{area === null ? 'escribe ancho y alto' : formatearArea(area)}</strong>
            </p>
          </fieldset>
        );
      })}

      <div className={estilos.pieItems}>
        <Boton ref={refAgregar} variante="secundario" iconoInicio={Plus} aria-disabled={lleno || deshabilitado ? 'true' : undefined}
          onClick={() => {
            if (lleno || deshabilitado) return;
            enfocarNuevo.current = campoItem(items.length, 'idProducto');
            alAgregar();
            setAnuncio(`Agregaste el letrero ${items.length + 1}.`);
          }}>
          Agregar otro letrero
        </Boton>
        <p className={estilos.total}>
          {lleno ? 'Llegaste al máximo de 10 letreros por solicitud. ' : ''}
          Área total estimada: <strong className="cifras">{formatearArea(total)}</strong>
        </p>
      </div>
      <p role="status" className="solo-lectores">{anuncio}</p>
    </div>
  );
}
