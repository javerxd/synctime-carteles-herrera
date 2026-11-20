import { useEffect, useMemo, useRef, useState } from 'react';
import { Search } from 'lucide-react';
import { Aviso, Boton, Campo, Esqueleto, EstadoVacio, Insignia, Tarjeta } from '../../../componentes/ui';
import { publico } from '../../../api/publico';
import { BotonSolicitar } from '../../../estructuras/Solicitud';
import { formatearPesos } from '../../../utilidades/formato';
import { useConsulta } from '../../../utilidades/useConsulta';
import estilos from '../Portada.module.css';
import { filtrarTrabajos } from './reglasVitrina';

const POR_PAGINA = 9;

function medidas(trabajo) {
  if (!trabajo.anchoMm || !trabajo.altoMm) return null;
  const cm = (mm) => new Intl.NumberFormat('es-CL', { maximumFractionDigits: 1 }).format(mm / 10);
  return `${cm(trabajo.anchoMm)} × ${cm(trabajo.altoMm)} cm`;
}

/**
 * Vitrina (RFS01, RNFP32). La API entrega los trabajos en páginas de hasta 100: se piden todas de una vez
 * y se filtran en el navegador (fase 12C), así la búsqueda responde al escribir, sin una consulta por
 * letra. La búsqueda es por «contiene» (los títulos que empiezan con lo escrito, primero), sin distinguir
 * mayúsculas ni tildes, y se combina con la categoría. Las fotos se sirven en WebP, con carga diferida.
 */
export function Vitrina() {
  const catalogo = useConsulta('catalogo-completo', publico.catalogoCompleto);
  const categorias = useConsulta('categorias', publico.categorias);
  const [texto, setTexto] = useState('');
  const [categoria, setCategoria] = useState(null);
  const [visibles, setVisibles] = useState(POR_PAGINA);
  const refBusqueda = useRef(null);
  const refLista = useRef(null);
  const enfocarDesde = useRef(null);

  const todos = useMemo(() => catalogo.datos ?? [], [catalogo.datos]);
  const filtrados = useMemo(() => filtrarTrabajos(todos, { texto, categoria }), [todos, texto, categoria]);
  const mostrados = filtrados.slice(0, visibles);
  const hayMas = filtrados.length > visibles;
  const hayFiltro = texto.trim() !== '' || categoria !== null;

  const opciones = useMemo(
    () => (categorias.datos ?? []).filter((c) => c.activo !== false).sort((a, b) => a.nombre.localeCompare(b.nombre, 'es')),
    [categorias.datos],
  );

  // Al pedir más, el foco pasa al primer trabajo nuevo: quien usa teclado o lector sigue leyendo desde ahí.
  useEffect(() => {
    const desde = enfocarDesde.current;
    if (desde === null || mostrados.length <= desde) return;
    enfocarDesde.current = null;
    refLista.current?.querySelectorAll('article')[desde]?.focus();
  }, [mostrados.length]);

  function verMas() {
    enfocarDesde.current = mostrados.length;
    setVisibles((v) => v + POR_PAGINA);
  }

  function limpiar() {
    setTexto('');
    setCategoria(null);
    setVisibles(POR_PAGINA);
    refBusqueda.current?.focus();
  }

  const total = filtrados.length;
  const cargado = catalogo.datos !== undefined;

  return (
    <section id="vitrina" className={estilos.seccionAlterna} aria-labelledby="titulo-vitrina">
      <div className={estilos.contenedorSimple}>
        <div className={estilos.encabezado}>
          <h2 id="titulo-vitrina">Trabajos realizados</h2>
          <p className={estilos.descripcion}>Precios de referencia: cada trabajo se cotiza según medidas, materiales e instalación.</p>
        </div>

        {todos.length > 0 && (
          <div className={estilos.busqueda} role="search" aria-label="Trabajos">
            <Campo
              ref={refBusqueda}
              etiqueta="Buscar trabajos"
              type="search"
              name="busqueda"
              autoComplete="off"
              maxLength={100}
              placeholder="Por ejemplo: letrero, vinilo, panadería"
              value={texto}
              onChange={(e) => {
                setTexto(e.target.value);
                setVisibles(POR_PAGINA);
              }}
            />
            {opciones.length > 0 && (
              <Campo
                como="select"
                etiqueta="Categoría"
                name="categoria"
                value={categoria === null ? '' : String(categoria)}
                onChange={(e) => {
                  setCategoria(e.target.value === '' ? null : Number(e.target.value));
                  setVisibles(POR_PAGINA);
                }}
              >
                <option value="">Todas</option>
                {opciones.map((c) => <option key={c.idCategoria} value={c.idCategoria}>{c.nombre}</option>)}
              </Campo>
            )}
          </div>
        )}
        {/* Lo dice en voz baja cada vez que cambia la lista: «12 trabajos». */}
        <p className={estilos.contadorFiltros} role="status">
          {cargado ? `${total} ${total === 1 ? 'trabajo' : 'trabajos'}` : catalogo.cargando ? 'Cargando trabajos…' : ''}
        </p>

        {catalogo.cargando && (
          <div className={estilos.rejilla} aria-hidden="true">
            {[1, 2, 3].map((i) => <Esqueleto key={i} forma="bloque" alto="20rem" />)}
          </div>
        )}
        {catalogo.error && (
          <Aviso tono="aviso" titulo="No pudimos cargar la vitrina"
            accion={<Boton variante="secundario" tamano="compacto" onClick={catalogo.recargar}>Reintentar</Boton>} />
        )}
        {cargado && todos.length === 0 && (
          <EstadoVacio nivelTitulo={3} titulo="Aún no hay trabajos publicados"
            descripcion="Cuéntanos qué necesitas y te enviamos una cotización." accion={<BotonSolicitar />} />
        )}
        {cargado && todos.length > 0 && total === 0 && (
          <EstadoVacio nivelTitulo={3} icono={Search} titulo="No hay trabajos con esa búsqueda"
            descripcion="Prueba con otra palabra o con otra categoría."
            accion={hayFiltro ? <Boton variante="secundario" onClick={limpiar}>Limpiar</Boton> : undefined} />
        )}
        {mostrados.length > 0 && (
          <ul ref={refLista} className={estilos.rejilla}>
            {mostrados.map((trabajo) => (
              <li key={trabajo.idCatalogo}>
                <TarjetaTrabajo trabajo={trabajo} />
              </li>
            ))}
          </ul>
        )}
        {hayMas && (
          <div className={estilos.masTrabajos}>
            <Boton variante="secundario" onClick={verMas}>Ver más trabajos</Boton>
            <p className={estilos.conteo}>Mostrando {mostrados.length} de {total}</p>
          </div>
        )}
      </div>
    </section>
  );
}

function TarjetaTrabajo({ trabajo }) {
  const { imagen } = trabajo;
  return (
    <Tarjeta como="article" interactiva sinRelleno tabIndex={-1} className={estilos.trabajo} aria-labelledby={`trabajo-${trabajo.idCatalogo}`}>
      <div className={estilos.marcoFoto}>
        {imagen ? (
          <img
            src={imagen.urlMiniatura}
            srcSet={`${imagen.urlMiniatura} 640w, ${imagen.urlGrande} 1600w`}
            sizes="(min-width: 64rem) 22rem, (min-width: 40rem) 45vw, 92vw"
            alt="" /* el título del trabajo, justo debajo, ya la nombra: así el lector no lo repite */
            loading="lazy"
            decoding="async"
            width={imagen.ancho}
            height={imagen.alto}
            className={estilos.foto}
          />
        ) : (
          <div className={estilos.sinFoto}><span>Foto del trabajo pendiente</span></div>
        )}
        {imagen?.esEjemplo && <span className={estilos.marcaEjemplo}>Imagen de ejemplo</span>}
      </div>
      <div className={estilos.cuerpoTrabajo}>
        <Insignia tono="neutro">{trabajo.categoria.nombre}</Insignia>
        <h3 id={`trabajo-${trabajo.idCatalogo}`} className={estilos.tituloTrabajo}>{trabajo.titulo}</h3>
        {trabajo.descripcion && <p className={estilos.textoTrabajo}>{trabajo.descripcion}</p>}
        <p className={estilos.ficha}>{[trabajo.tipoLetrero, medidas(trabajo)].filter(Boolean).join(' · ')}</p>
        {trabajo.precioReferencial > 0 && (
          <p className={estilos.precio}><span>Desde</span> <strong className="cifras">{formatearPesos(trabajo.precioReferencial)}</strong></p>
        )}
        <BotonSolicitar trabajo={trabajo} variante="secundario" tamano="compacto" iconoInicio={null} className={estilos.accionTrabajo}>
          Cotizar uno similar <span className="solo-lectores">a {trabajo.titulo}</span>
        </BotonSolicitar>
      </div>
    </Tarjeta>
  );
}
