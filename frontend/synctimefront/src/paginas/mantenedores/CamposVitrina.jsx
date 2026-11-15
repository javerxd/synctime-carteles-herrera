import { useEffect, useId, useRef, useState } from 'react';
import { ImagePlus, Trash2 } from 'lucide-react';
import { Boton, Campo, Casilla } from '../../componentes/ui';
import { todasLasPaginas, vitrina } from '../../api/mantenedores';
import { useConsulta } from '../../utilidades/useConsulta';
import { LIMITES, logoVigente } from './reglasMantenedores';
import estilos from './Mantenedores.module.css';

const TIPOS_IMAGEN = 'image/jpeg,image/png,image/webp';

/**
 * Formulario de una publicación de la vitrina (E12): tipo de letrero, título, descripción, imagen (con el
 * procesamiento de la fase 4), medidas, precio, si lleva el logo de un cliente y si se muestra. Con logo y sin la
 * autorización registrada, «Mostrar en la vitrina» queda bloqueado y dice por qué.
 */
export function CamposVitrina({ campo, form, cambiar, registro, deshabilitado, errores, alSubiendo }) {
  const productos = useConsulta('productos-activos', () => todasLasPaginas('/productos', { activo: true }));
  const opciones = [...(productos.datos ?? [])];
  if (registro?.idProducto && !opciones.some((p) => String(p.idProducto) === String(registro.idProducto))) {
    opciones.push({ idProducto: registro.idProducto, nombre: `${registro.tipoLetrero ?? 'Tipo de letrero'} (inactivo)` });
  }
  const autorizado = logoVigente(form, registro);
  const bloqueado = form.incluyeLogoCliente && !autorizado;
  const idMotivo = useId();
  const propsProducto = campo('idProducto');

  function marcarLogo(evento) {
    const marcado = evento.target.checked;
    cambiar('incluyeLogoCliente', marcado);
    // Sin autorización no se puede mostrar: se desmarca y la explicación queda a la vista.
    if (marcado && !logoVigente({ ...form, incluyeLogoCliente: true }, registro)) cambiar('visible', false);
  }

  return (
    <>
      <div className={estilos.dosColumnas}>
        <Campo etiqueta="Tipo de letrero" como="select" requerido {...propsProducto} disabled={propsProducto.disabled || productos.cargando}>
          <option value="">{productos.cargando ? 'Cargando…' : 'Elige un tipo'}</option>
          {opciones.map((p) => <option key={p.idProducto} value={p.idProducto}>{p.nombre}</option>)}
        </Campo>
        <Campo etiqueta="Título" requerido maxLength={LIMITES.titulo} autoComplete="off" {...campo('titulo')}
          ayuda="Por ejemplo: Letrero luminoso para panadería." />
      </div>
      <Campo etiqueta="Descripción" como="textarea" rows={3} maxLength={LIMITES.descripcionPublicacion} {...campo('descripcion')}
        ayuda="Materiales y terminaciones. Opcional." />

      <SubidaImagen imagen={form.imagen} error={errores.imagen} deshabilitado={deshabilitado} alSubiendo={alSubiendo}
        alCambiar={(imagen) => cambiar('imagen', imagen)} />

      <fieldset className={estilos.grupo}>
        <legend>Medidas y precio de referencia</legend>
        <div className={estilos.tresColumnas}>
          <Campo etiqueta="Ancho (cm)" inputMode="decimal" {...campo('ancho')} />
          <Campo etiqueta="Alto (cm)" inputMode="decimal" {...campo('alto')} />
          <Campo etiqueta="Precio referencial" inputMode="numeric" ayuda="En pesos." {...campo('precioReferencial')} />
        </div>
      </fieldset>

      <fieldset className={estilos.grupo}>
        <legend>{registro ? 'Logo del cliente' : 'Logo y visibilidad'}</legend>
        <Casilla etiqueta="Incluye el logo de un cliente" checked={form.incluyeLogoCliente} onChange={marcarLogo} disabled={deshabilitado}
          descripcion="Para mostrarla hace falta la autorización del cliente, registrada en esta ficha (RNFO17)." />
        {/* Al crear se elige aquí si se muestra; en una ficha existente, en «Autorización y visibilidad». */}
        {!registro && (
          <Casilla etiqueta="Mostrar en la vitrina" checked={form.visible && !bloqueado} disabled={deshabilitado || bloqueado}
            onChange={(e) => cambiar('visible', e.target.checked)} aria-describedby={bloqueado ? idMotivo : undefined}
            error={errores.visible} />
        )}
        {registro && errores.visible && <p role="alert" className={estilos.errorLista}>{errores.visible}</p>}
        {bloqueado && (!registro || registro.visible || (registro.logoAutorizado && registro.incluyeLogoCliente)) && (
          <p id={idMotivo} className={estilos.motivo}>
            {!registro
              ? 'Guárdala oculta y luego registra la autorización del cliente en esta ficha.'
              : registro.logoAutorizado && registro.incluyeLogoCliente
                ? 'Cambiaste la imagen o el logo: la autorización anterior ya no vale. Al guardar quedará oculta hasta que la registres de nuevo.'
                : 'Al guardar quedará oculta hasta que registres la autorización del cliente.'}
          </p>
        )}
      </fieldset>
    </>
  );
}

/**
 * Subida de la foto del trabajo, con avance. La API la procesa (tamaños y formato) y devuelve sus direcciones.
 * Mientras sube, el formulario no se guarda (`alSubiendo`); al salir de la página, la subida se corta.
 */
function SubidaImagen({ imagen, error, deshabilitado, alCambiar, alSubiendo }) {
  const [estado, setEstado] = useState({ subiendo: false, avance: 0, error: null, anuncio: '' });
  const refArchivo = useRef(null);
  const refControl = useRef(null);
  const refAlSubiendo = useRef(alSubiendo);
  const idTitulo = useId();
  const idError = useId();

  useEffect(() => {
    refAlSubiendo.current = alSubiendo;
  });
  useEffect(() => () => refControl.current?.abort(), []);

  async function elegir(evento) {
    const archivo = evento.target.files?.[0];
    evento.target.value = '';
    if (!archivo) return;
    if (!TIPOS_IMAGEN.split(',').includes(archivo.type)) {
      setEstado({ subiendo: false, avance: 0, error: 'Elige una foto JPG, PNG o WebP.', anuncio: '' });
      return;
    }
    refControl.current?.abort();
    const control = new AbortController();
    refControl.current = control;
    setEstado({ subiendo: true, avance: 0, error: null, anuncio: 'Subiendo la imagen…' });
    refAlSubiendo.current?.(true);
    try {
      const subida = await vitrina.subirImagen(archivo, {
        senal: control.signal,
        alProgreso: (avance) => setEstado((e) => ({ ...e, avance })),
      });
      alCambiar({ idImagen: subida?.idImagen ?? null, clave: subida?.clave, urlMiniatura: subida?.urlMiniatura, urlGrande: subida?.urlGrande });
      setEstado({ subiendo: false, avance: 1, error: null, anuncio: 'Imagen subida' });
    } catch (falla) {
      if (falla?.name === 'AbortError') return;
      setEstado({ subiendo: false, avance: 0, error: falla?.message || 'No pudimos subir la imagen.', anuncio: '' });
    } finally {
      if (!control.signal.aborted) refAlSubiendo.current?.(false);
    }
  }

  const mensaje = estado.error || error;
  return (
    <div className={estilos.imagen} role="group" aria-labelledby={idTitulo} aria-describedby={mensaje ? idError : undefined}>
      <span id={idTitulo} className={estilos.etiquetaGrupo}>Imagen del trabajo</span>
      <div className={estilos.filaImagen}>
        {imagen?.urlMiniatura
          ? <img src={imagen.urlMiniatura} alt="Vista previa de la imagen elegida" width="96" height="96" className={estilos.vistaPrevia} />
          : <span className={estilos.vistaPreviaVacia} aria-hidden="true"><ImagePlus size={24} strokeWidth={1.5} /></span>}
        <div className={estilos.accionesImagen}>
          <input ref={refArchivo} type="file" accept={TIPOS_IMAGEN} onChange={elegir} className="solo-lectores" tabIndex={-1}
            aria-hidden="true" disabled={deshabilitado || estado.subiendo} />
          <Boton variante="secundario" tamano="compacto" iconoInicio={ImagePlus} onClick={() => refArchivo.current?.click()}
            disabled={deshabilitado} cargando={estado.subiendo}
            textoCargando={`Subiendo… ${Math.round(estado.avance * 100)} %`}>
            {imagen ? 'Cambiar imagen' : 'Subir imagen'}
          </Boton>
          {imagen && !estado.subiendo && (
            <Boton variante="fantasma" tamano="compacto" iconoInicio={Trash2} onClick={() => alCambiar(null)} disabled={deshabilitado}>
              Quitar imagen
            </Boton>
          )}
          <p className={estilos.ayudaImagen}>JPG, PNG o WebP. La ajustamos al tamaño de la vitrina.</p>
        </div>
      </div>
      <p id={idError} className={estilos.errorImagen} aria-live="polite">{mensaje}</p>
      {/* Región que existe desde el inicio: anuncia «Subiendo la imagen…» y luego «Imagen subida». */}
      <p role="status" className="solo-lectores">{estado.anuncio}</p>
    </div>
  );
}
