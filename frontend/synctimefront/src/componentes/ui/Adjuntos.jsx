import { useEffect, useId, useRef, useState } from 'react';
import { CircleAlert, Download, FileText, ImagePlus, LoaderCircle, Upload, X } from 'lucide-react';
import { Boton } from './Boton';
import { Dialogo } from './Dialogo';
import { Esqueleto } from './Esqueleto';
import { unirClases } from './clases';
import {
  ACEPTA_ADJUNTOS, crearUrl, esPdf, liberarUrl, mensajeSubida, movimientoReducido, REGLAS_ADJUNTOS, useArchivoPrivado, validarArchivo,
} from './reglasAdjuntos';
import estilos from './Adjuntos.module.css';

const DURACION_SALIDA_MS = 160;

/** Miniatura cuadrada de 96 px: la imagen (local mientras sube, o privada del servidor) o la tarjeta del PDF. */
function Miniatura({ adjunto, cargarMiniatura }) {
  const remota = adjunto.tipo === 'imagen' && !adjunto.vista && adjunto.idAdjunto;
  const { url, error } = useArchivoPrivado((senal) => cargarMiniatura(adjunto, { senal }), remota ? adjunto.idAdjunto : null);
  if (adjunto.tipo === 'pdf') {
    return (
      <span className={estilos.pdf}>
        <FileText size={28} strokeWidth={1.5} aria-hidden="true" />
        <span className={estilos.nombrePdf}>{adjunto.nombre}</span>
      </span>
    );
  }
  const fuente = adjunto.vista ?? url;
  if (fuente) return <img src={fuente} alt="" className={estilos.imagen} draggable="false" />;
  if (error) {
    return (
      <span className={estilos.pdf}>
        <CircleAlert size={22} strokeWidth={1.75} aria-hidden="true" />
        <span className={estilos.nombrePdf}>Vista no disponible</span>
      </span>
    );
  }
  return <Esqueleto forma="bloque" className={estilos.cargandoMiniatura} />;
}

/** Diálogo con la imagen en grande, descargada con la sesión. */
function VisorImagen({ adjunto, cargarArchivo, alCerrar }) {
  // Mientras el diálogo sale, sigue mostrando la última imagen (no cambia el título ni se vacía).
  const [retenido, setRetenido] = useState(adjunto);
  if (adjunto && adjunto !== retenido) setRetenido(adjunto);
  const mostrado = adjunto ?? retenido;
  const { url, error } = useArchivoPrivado((senal) => cargarArchivo(mostrado, { senal }), mostrado?.idAdjunto ?? null);
  return (
    <Dialogo abierto={Boolean(adjunto)} alCerrar={alCerrar} titulo={mostrado?.nombre ?? 'Archivo adjunto'} ancho="min(60rem, 100%)">
      <div className={estilos.visor}>
        {url && <img src={url} alt={`Archivo adjunto ${mostrado?.nombre ?? ''}`} className={estilos.imagenGrande} />}
        {!url && !error && (
          <>
            <p role="status" className="solo-lectores">Cargando la imagen…</p>
            <Esqueleto forma="bloque" alto="20rem" />
          </>
        )}
        {error && <p role="alert" className={estilos.errorVisor}>No pudimos abrir la imagen. {error.message}</p>}
      </div>
    </Dialogo>
  );
}

/** Descarga un archivo privado con su nombre: Blob → enlace temporal → se libera. */
async function descargar(adjunto, cargarArchivo) {
  const url = crearUrl(await cargarArchivo(adjunto));
  if (!url) return;
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = adjunto.nombre || 'archivo.pdf';
  document.body.append(enlace);
  enlace.click();
  enlace.remove();
  setTimeout(() => liberarUrl(url), 1000);
}

/** Ver en grande (imagen) o descargar (PDF): el contenido de una miniatura que se puede abrir. */
function MiniaturaAbrible({ adjunto, cargarMiniatura, cargarArchivo, alVer, alError }) {
  const [descargando, setDescargando] = useState(false);
  if (adjunto.tipo === 'pdf') {
    return (
      <button type="button" className={estilos.abrir} aria-busy={descargando ? 'true' : undefined} aria-label={`Descargar ${adjunto.nombre}`}
        onClick={async () => {
          if (descargando) return;
          setDescargando(true);
          try {
            await descargar(adjunto, cargarArchivo);
          } catch (error) {
            alError(adjunto, `No pudimos descargarlo. ${error.message ?? ''}`.trim());
          } finally {
            setDescargando(false);
          }
        }}>
        <Miniatura adjunto={adjunto} cargarMiniatura={cargarMiniatura} />
        <span className={estilos.insigniaAccion} aria-hidden="true">
          {descargando ? <LoaderCircle size={14} className={estilos.giro} /> : <Download size={14} />}
        </span>
      </button>
    );
  }
  return (
    <button type="button" className={estilos.abrir} onClick={() => alVer(adjunto)} aria-label={`Ver ${adjunto.nombre} en grande`}>
      <Miniatura adjunto={adjunto} cargarMiniatura={cargarMiniatura} />
    </button>
  );
}

const sinError = { lista: [] };

/**
 * Lista de adjuntos solo para mirar (comprobante): miniaturas de 96 px que se abren en grande; el PDF
 * se descarga.
 */
export function ListaAdjuntos({ adjuntos = [], cargarMiniatura, cargarArchivo, etiqueta = 'Archivos adjuntos', className }) {
  const [viendo, setViendo] = useState(null);
  const [errores, setErrores] = useState(sinError);
  if (!adjuntos.length) return <p className={estilos.ninguno}>Sin archivos adjuntos.</p>;
  return (
    <div className={unirClases(estilos.lista, className)}>
      <ul className={estilos.miniaturas} aria-label={etiqueta}>
        {adjuntos.map((adjunto) => (
          <li key={adjunto.idAdjunto} className={estilos.elemento} title={adjunto.nombre}>
            <MiniaturaAbrible adjunto={adjunto} cargarMiniatura={cargarMiniatura} cargarArchivo={cargarArchivo}
              alVer={setViendo} alError={(a, mensaje) => setErrores({ lista: [{ clave: a.idAdjunto, nombre: a.nombre, mensaje }] })} />
          </li>
        ))}
      </ul>
      <ListaErrores errores={errores.lista} alDescartar={() => setErrores(sinError)} />
      <VisorImagen adjunto={viendo} cargarArchivo={cargarArchivo} alCerrar={() => setViendo(null)} />
    </div>
  );
}

function ListaErrores({ errores, alDescartar }) {
  if (!errores.length) return null;
  return (
    <ul className={estilos.errores}>
      {errores.map((e) => (
        <li key={e.clave} className={estilos.error}>
          <CircleAlert size={16} strokeWidth={2} aria-hidden="true" className={estilos.iconoError} />
          <span className={estilos.textoError}><strong>{e.nombre}</strong>: {e.mensaje}</span>
          <button type="button" className={estilos.descartar} aria-label={`Descartar el aviso de ${e.nombre}`} onClick={() => alDescartar(e.clave)}>
            <X size={16} aria-hidden="true" />
          </button>
        </li>
      ))}
    </ul>
  );
}

let siguienteClave = 1;
const nuevaClave = () => `a${siguienteClave++}`;

const desdeServidor = (adjunto) => ({
  clave: `s${adjunto.idAdjunto}`, idAdjunto: adjunto.idAdjunto, nombre: adjunto.nombre, tipo: adjunto.tipo,
  bytes: adjunto.bytes, urlMiniatura: adjunto.urlMiniatura, estado: 'listo', progreso: 1,
});

/**
 * Zona «Adjunta tu logo o diseño» (fase 7): arrastrar y soltar o elegir archivos (en el teléfono abre la
 * cámara o la galería), hasta 5 de 10 MB en JPG, PNG, WebP o PDF.
 *
 * - Cada archivo se sube al elegirlo (`subir(archivo, { alProgreso, senal })` → adjunto del servidor) y
 *   muestra su avance; «×» lo quita (`quitar(idAdjunto)`, o cancela la subida en curso).
 * - Los errores son por archivo y no borran a los demás. Todo se anuncia en una región viva.
 * - `alCambiar(listos, { subiendo })` informa los adjuntos ya subidos y si queda alguno en curso.
 * - `permitirVer`: las miniaturas se abren en grande y el PDF se descarga (ficha del personal).
 */
export function Adjuntos({
  titulo = 'Adjunta tu logo o diseño',
  descripcion = 'Opcional. JPG, PNG, WebP o PDF, de hasta 10 MB cada uno.',
  iniciales = [],
  subir,
  quitar,
  alCambiar,
  cargarMiniatura,
  cargarArchivo,
  permitirVer = false,
  maximo = REGLAS_ADJUNTOS.maximo,
  deshabilitado = false,
  error,
  className,
}) {
  const [elementos, setElementos] = useState(() => iniciales.map(desdeServidor));
  const [errores, setErrores] = useState([]);
  const [anuncio, setAnuncio] = useState('');
  const [arrastrando, setArrastrando] = useState(false);
  const [viendo, setViendo] = useState(null);
  const refEntrada = useRef(null);
  const refElegir = useRef(null);
  const refVistas = useRef(new Set());
  const refControladores = useRef(new Set());
  const refLista = useRef(null);
  const refMontado = useRef(true);
  const refAlCambiar = useRef(alCambiar);
  const idAyuda = useId();
  const idError = useId();

  useEffect(() => {
    refAlCambiar.current = alCambiar;
  });

  // Al salir de la pantalla: se liberan las vistas locales y se cancelan las subidas en curso.
  useEffect(() => {
    refMontado.current = true;
    const vistas = refVistas.current;
    const controladores = refControladores.current;
    return () => {
      refMontado.current = false;
      // Las subidas en curso se cancelan; los borradores ya subidos quedan (se recuperan al volver).
      controladores.forEach((c) => c.abort());
      controladores.clear();
      vistas.forEach(liberarUrl);
      vistas.clear();
    };
  }, []);

  const activos = elementos.filter((e) => e.estado !== 'saliendo');
  const refCuenta = useRef(activos.length);
  useEffect(() => {
    refCuenta.current = activos.length;
  });
  const lleno = activos.length >= maximo;
  const inactiva = deshabilitado || lleno;
  const listos = activos.filter((e) => e.estado === 'listo' && !e.quitando);
  // «Ocupado» mientras algo sube o se está quitando: el formulario no envía hasta que termine.
  const subiendo = activos.some((e) => e.estado === 'subiendo' || e.quitando);
  const firma = `${listos.map((e) => e.idAdjunto).join(',')}|${subiendo}`;

  useEffect(() => {
    refAlCambiar.current?.(
      listos.map(({ idAdjunto, nombre, tipo, bytes, urlMiniatura }) => ({ idAdjunto, nombre, tipo, bytes, urlMiniatura })),
      { subiendo },
    );
    // La firma resume los listos y si queda alguno subiendo: solo se informa cuando cambia.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [firma]);

  // Un anuncio idéntico al anterior no cambiaría la región viva: se alterna un carácter invisible.
  const anunciar = (texto) => setAnuncio((previo) => (previo === texto ? `${texto}\u200B` : texto));

  /** Tras retirar un archivo, el foco va al «×» vecino o, si no queda ninguno, a «Elegir archivos». */
  function enfocarVecino(clave) {
    const visibles = activos.filter((e) => e.clave !== clave);
    const indice = activos.findIndex((e) => e.clave === clave);
    const vecino = visibles[Math.min(indice, visibles.length - 1)];
    const destino = vecino && refLista.current?.querySelector(`[data-clave="${vecino.clave}"] button[aria-label^="Quitar"]`);
    (destino ?? refElegir.current)?.focus();
  }

  const actualizar = (clave, cambios) => setElementos((lista) => lista.map((e) => (e.clave === clave ? { ...e, ...cambios } : e)));

  function retirar(clave) {
    const quitarDeLista = () => setElementos((lista) => {
      const elemento = lista.find((e) => e.clave === clave);
      if (elemento?.vista) {
        liberarUrl(elemento.vista);
        refVistas.current.delete(elemento.vista);
      }
      return lista.filter((e) => e.clave !== clave);
    });
    if (movimientoReducido()) quitarDeLista();
    else {
      actualizar(clave, { estado: 'saliendo' });
      setTimeout(() => refMontado.current && quitarDeLista(), DURACION_SALIDA_MS);
    }
  }

  const agregarError = (nombre, mensaje) => setErrores((lista) => [...lista, { clave: nuevaClave(), nombre, mensaje }]);

  function agregar(archivos) {
    if (inactiva) return;
    let libres = maximo - activos.length;
    const nuevos = [];
    const rechazados = [];
    for (const archivo of Array.from(archivos ?? [])) {
      let motivo = validarArchivo(archivo);
      if (!motivo && libres <= 0) motivo = `Ya tienes ${maximo} archivos, el máximo por solicitud. Quita uno para adjuntar este.`;
      if (motivo) {
        rechazados.push({ clave: nuevaClave(), nombre: archivo.name, mensaje: motivo });
        continue;
      }
      libres -= 1;
      const pdf = esPdf(archivo);
      const vista = pdf ? null : crearUrl(archivo);
      if (vista) refVistas.current.add(vista);
      const controlador = new AbortController();
      refControladores.current.add(controlador);
      nuevos.push({
        clave: nuevaClave(), archivo, nombre: archivo.name, tipo: pdf ? 'pdf' : 'imagen', bytes: archivo.size,
        estado: 'subiendo', progreso: 0, vista, controlador,
      });
    }
    if (rechazados.length) setErrores((lista) => [...lista, ...rechazados]);
    if (nuevos.length) setElementos((lista) => [...lista, ...nuevos]);

    const partes = [];
    if (nuevos.length) partes.push(nuevos.length === 1 ? `Subiendo ${nuevos[0].nombre}.` : `Subiendo ${nuevos.length} archivos.`);
    rechazados.forEach((r) => partes.push(`${r.nombre} no se adjuntó: ${r.mensaje}`));
    if (partes.length) anunciar(partes.join(' '));

    nuevos.forEach((nuevo) => iniciarSubida(nuevo));
  }

  function iniciarSubida({ clave, archivo, nombre, controlador }) {
    Promise.resolve()
      .then(() => subir(archivo, { alProgreso: (progreso) => refMontado.current && actualizar(clave, { progreso }), senal: controlador.signal }))
      .finally(() => refControladores.current.delete(controlador))
      .then((adjunto) => {
        if (!refMontado.current) return;
        setElementos((lista) => lista.map((e) => (e.clave === clave ? {
          ...e, estado: 'listo', progreso: 1, archivo: null, controlador: null, idAdjunto: adjunto?.idAdjunto,
          tipo: adjunto?.tipo ?? e.tipo, urlMiniatura: adjunto?.urlMiniatura ?? null,
        } : e)));
        anunciar(`${nombre} adjuntado. ${refCuenta.current} de ${maximo}.`);
      }, (falla) => {
        if (!refMontado.current || falla?.name === 'AbortError') return;
        retirar(clave);
        const mensaje = mensajeSubida(falla);
        agregarError(nombre, mensaje);
        anunciar(`${nombre} no se adjuntó: ${mensaje}`);
      });
  }

  async function quitarElemento(elemento) {
    const restantes = activos.length - 1;
    if (elemento.estado === 'subiendo') {
      elemento.controlador?.abort();
      enfocarVecino(elemento.clave);
      retirar(elemento.clave);
      anunciar(`Cancelaste la subida de ${elemento.nombre}. ${restantes} de ${maximo}.`);
      return;
    }
    actualizar(elemento.clave, { quitando: true });
    try {
      await quitar(elemento.idAdjunto);
      if (!refMontado.current) return;
      // El botón que se pulsó desaparece: el foco pasa al archivo vecino o a «Elegir archivos».
      enfocarVecino(elemento.clave);
      retirar(elemento.clave);
      anunciar(`Quitaste ${elemento.nombre}. ${restantes} de ${maximo}.`);
    } catch (falla) {
      if (!refMontado.current) return;
      actualizar(elemento.clave, { quitando: false });
      const mensaje = `No pudimos quitarlo. ${falla?.message ?? ''}`.trim();
      agregarError(elemento.nombre, mensaje);
      anunciar(`${elemento.nombre}: ${mensaje}`);
    }
  }

  function alElegir(evento) {
    agregar(evento.target.files);
    // Así se puede volver a elegir el mismo archivo después de quitarlo.
    evento.target.value = '';
  }

  function alSoltar(evento) {
    evento.preventDefault();
    setArrastrando(false);
    agregar(evento.dataTransfer?.files);
  }

  const describe = [idAyuda, error && idError].filter(Boolean).join(' ');

  return (
    <fieldset className={unirClases(estilos.adjuntos, className)} aria-describedby={describe}>
      <legend className={estilos.titulo}>
        {titulo}
        <span className={estilos.contador} aria-hidden="true">{activos.length} de {maximo}</span>
      </legend>
      <p id={idAyuda} className={estilos.ayuda}>
        {descripcion} <span className="solo-lectores">Llevas {activos.length} de {maximo}.</span>
      </p>

      <div
        className={estilos.zona}
        data-arrastrando={arrastrando ? '' : undefined}
        data-inactiva={inactiva ? '' : undefined}
        onDragEnter={(e) => { e.preventDefault(); if (!inactiva) setArrastrando(true); }}
        onDragOver={(e) => { e.preventDefault(); if (e.dataTransfer) e.dataTransfer.dropEffect = inactiva ? 'none' : 'copy'; }}
        onDragLeave={(e) => { if (!e.currentTarget.contains(e.relatedTarget)) setArrastrando(false); }}
        onDrop={alSoltar}
      >
        <ImagePlus size={28} strokeWidth={1.5} aria-hidden="true" className={estilos.iconoZona} />
        <p className={estilos.textoZona}>
          {lleno ? (
            <>Llegaste al máximo de {maximo} archivos. Quita uno para adjuntar otro.</>
          ) : (
            <>
              <span className={estilos.soloPuntero}>Arrastra tus archivos aquí o </span>
              <span className={estilos.soloTactil}>Toma una foto o </span>
              elige los archivos.
            </>
          )}
        </p>
        <Boton
          ref={refElegir}
          name="adjuntos"
          variante="secundario"
          tamano="compacto"
          iconoInicio={Upload}
          aria-disabled={inactiva ? 'true' : undefined}
          aria-describedby={describe}
          onClick={() => !inactiva && refEntrada.current?.click()}
        >
          Elegir archivos
        </Boton>
        <input
          ref={refEntrada}
          type="file"
          multiple
          accept={ACEPTA_ADJUNTOS}
          className="solo-lectores"
          tabIndex={-1}
          aria-hidden="true"
          data-testid="entrada-adjuntos"
          onChange={alElegir}
        />
      </div>

      {elementos.length > 0 && (
        <ul ref={refLista} className={estilos.miniaturas} aria-label="Archivos adjuntados">
          {elementos.map((elemento) => (
            <li
              key={elemento.clave}
              data-clave={elemento.clave}
              className={unirClases(estilos.elemento, elemento.estado === 'saliendo' && estilos.saliendo)}
              title={elemento.nombre}
              aria-hidden={elemento.estado === 'saliendo' ? 'true' : undefined}
            >
              {permitirVer && elemento.estado === 'listo' ? (
                <MiniaturaAbrible adjunto={elemento} cargarMiniatura={cargarMiniatura} cargarArchivo={cargarArchivo}
                  alVer={setViendo} alError={(a, mensaje) => agregarError(a.nombre, mensaje)} />
              ) : (
                <span className={estilos.marco}>
                  <Miniatura adjunto={elemento} cargarMiniatura={cargarMiniatura} />
                  {elemento.tipo === 'imagen' && <span className="solo-lectores">{elemento.nombre}</span>}
                </span>
              )}
              {elemento.estado === 'subiendo' && (
                <span className={estilos.progreso}>
                  <progress max={100} value={Math.round((elemento.progreso ?? 0) * 100)} aria-label={`Subiendo ${elemento.nombre}`} />
                </span>
              )}
              {elemento.estado !== 'saliendo' && !deshabilitado && (
                <button
                  type="button"
                  className={estilos.quitar}
                  aria-label={`Quitar ${elemento.nombre}`}
                  aria-busy={elemento.quitando ? 'true' : undefined}
                  disabled={elemento.quitando}
                  onClick={() => quitarElemento(elemento)}
                >
                  {elemento.quitando ? <LoaderCircle size={14} className={estilos.giro} aria-hidden="true" /> : <X size={16} strokeWidth={2.25} aria-hidden="true" />}
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      <ListaErrores errores={errores} alDescartar={(clave) => setErrores((lista) => lista.filter((e) => e.clave !== clave))} />
      <p id={idError} className={estilos.errorExterno} aria-live="polite">
        {error && (
          <>
            <CircleAlert size={16} strokeWidth={2} aria-hidden="true" className={estilos.iconoError} />
            <span>{error}</span>
          </>
        )}
      </p>
      <p role="status" className="solo-lectores">{anuncio}</p>
      {permitirVer && <VisorImagen adjunto={viendo} cargarArchivo={cargarArchivo} alCerrar={() => setViendo(null)} />}
    </fieldset>
  );
}
