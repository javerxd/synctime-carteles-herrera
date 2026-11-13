import { useEffect, useMemo, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { ArrowRight, Clock, Mail, MapPin, MessageCircle, Phone } from 'lucide-react';
import {
  Aviso, Boton, Chip, Enlace, Esqueleto, EstadoCartel, EstadoVacio, Insignia, Tarjeta,
} from '../../componentes/ui';
import { publico } from '../../api/publico';
import { enlaceTelefono, enlaceWhatsapp, TALLER } from '../../configuracion/taller';
import { BotonSolicitar, useSolicitud } from '../../estructuras/Solicitud';
import { formatearFechaHora, formatearPesos } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { FormularioContacto } from './contacto/FormularioContacto';
import { franja, hora, textoEstado } from './horario';
import { MapaUbicacion } from './mapa/MapaUbicacion';
import estilos from './Portada.module.css';

const POR_PAGINA = 9;
// El estado «abierto ahora» cambia con la hora: una pestaña abierta todo el día se actualiza sola.
const REFRESCO_ESTADO_MS = 60_000;

/**
 * Portada pública, estructura «Mostrador» (elegida por el usuario el 13-11-2026): la propuesta de
 * valor y la acción principal a la izquierda; a la derecha, la ficha del mostrador con el estado del
 * local (horario en hora de Chile y cartel), el horario de hoy y cómo contactarnos. Debajo, la vitrina,
 * el horario semanal con el mapa y el formulario de contacto.
 */
export default function Portada() {
  const estado = useConsulta('estado-local', publico.estadoLocal);
  const { recargar: recargarEstado } = estado;
  const { hash, key } = useLocation();
  const ajustePendiente = useRef(null);

  // Con un ancla (/#vitrina, /#horario) se baja a la sección, también al repetir el mismo enlace (cada
  // navegación trae otra key). Cuando llega el estado del local, la altura de arriba cambia: se ajusta
  // una sola vez, para no devolver al ancla a quien ya siguió bajando.
  useEffect(() => {
    if (!hash) return;
    document.getElementById(hash.slice(1))?.scrollIntoView();
    ajustePendiente.current = key;
  }, [hash, key]);

  useEffect(() => {
    if (!hash || !estado.datos || ajustePendiente.current !== key) return;
    ajustePendiente.current = null;
    document.getElementById(hash.slice(1))?.scrollIntoView();
  }, [estado.datos, hash, key]);

  useEffect(() => {
    const refrescar = () => {
      if (!document.hidden) recargarEstado();
    };
    const cada = window.setInterval(refrescar, REFRESCO_ESTADO_MS);
    document.addEventListener('visibilitychange', refrescar);
    return () => {
      window.clearInterval(cada);
      document.removeEventListener('visibilitychange', refrescar);
    };
  }, [recargarEstado]);

  return (
    <>
      <section className={estilos.portada} aria-labelledby="titulo-portada">
        <div className={estilos.contenedor}>
          <div className={estilos.propuesta}>
            <h1 id="titulo-portada" className={estilos.titulo}>Letreros que se ven de lejos.</h1>
            <p className={estilos.bajada}>
              Diseñamos, fabricamos e instalamos letreros luminosos, letras corpóreas, rotulación de vehículos y
              gráfica para tu negocio en Los Ángeles.
            </p>
            <div className={estilos.acciones}>
              <BotonSolicitar />
              <Boton como="a" href="#vitrina" variante="secundario" iconoFin={ArrowRight}>Ver trabajos</Boton>
            </div>
          </div>
          <Mostrador estado={estado} />
        </div>
      </section>

      <Vitrina />
      <HorarioYUbicacion estado={estado} />
      <Contacto />
    </>
  );
}

function Mostrador({ estado }) {
  const datos = estado.datos;
  const whatsapp = enlaceWhatsapp();
  const telefono = enlaceTelefono();
  return (
    <Tarjeta como="aside" aria-labelledby="titulo-mostrador" className={estilos.mostrador}>
      <h2 id="titulo-mostrador" className={estilos.tituloMostrador}>En el taller ahora</h2>
      {estado.cargando && <Esqueleto forma="bloque" alto="3rem" />}
      {estado.error && (
        <Aviso tono="aviso" titulo="No pudimos cargar el estado del local"
          accion={<Boton variante="secundario" tamano="compacto" onClick={estado.recargar}>Reintentar</Boton>} />
      )}
      {datos && (
        <div className={estilos.cartel}>
          <EstadoCartel abierto={datos.abiertoAhora} grande />
          <p className={estilos.mensaje}>{textoEstado(datos)}</p>
          {datos.motivo === 'ausente' && datos.cartel?.tiempoEstimadoRegreso && (
            <p className={estilos.dato}>Regreso estimado: <strong>{datos.cartel.tiempoEstimadoRegreso}</strong></p>
          )}
          {datos.cartel && (
            <p className={estilos.actualizado}>Cartel actualizado el {formatearFechaHora(datos.cartel.fechaUltimaModificacion)}</p>
          )}
        </div>
      )}
      <ul className={estilos.datos}>
        <li>
          <Clock size={18} strokeWidth={1.75} aria-hidden="true" />
          <span>Hoy: {estado.cargando ? '…' : franja(datos?.hoy)}</span>
        </li>
        <li>
          <MapPin size={18} strokeWidth={1.75} aria-hidden="true" />
          <span>{TALLER.direccion}, {TALLER.ciudad}</span>
        </li>
        {telefono && (
          <li>
            <Phone size={18} strokeWidth={1.75} aria-hidden="true" />
            <Enlace href={telefono}>{TALLER.telefono}</Enlace>
          </li>
        )}
        {TALLER.correo && (
          <li>
            <Mail size={18} strokeWidth={1.75} aria-hidden="true" />
            <Enlace href={`mailto:${TALLER.correo}`}>{TALLER.correo}</Enlace>
          </li>
        )}
      </ul>
      <div className={estilos.accionesMostrador}>
        <Boton como="a" href="#ubicacion" variante="secundario" tamano="compacto" iconoInicio={MapPin}>Cómo llegar</Boton>
        {whatsapp && (
          <Boton como="a" href={whatsapp} target="_blank" rel="noopener noreferrer" variante="secundario" tamano="compacto" iconoInicio={MessageCircle}>
            Escribir por WhatsApp<span className="solo-lectores"> (se abre en otra pestaña)</span>
          </Boton>
        )}
      </div>
    </Tarjeta>
  );
}

function medidas(trabajo) {
  if (!trabajo.anchoMm || !trabajo.altoMm) return null;
  const cm = (mm) => new Intl.NumberFormat('es-CL', { maximumFractionDigits: 1 }).format(mm / 10);
  return `${cm(trabajo.anchoMm)} × ${cm(trabajo.altoMm)} cm`;
}

/**
 * Vitrina (RFS01, RNFP32): la API filtra por categoría y pagina; «Ver más trabajos» suma la página
 * siguiente. Las fotos se sirven en WebP, en dos tamaños, con carga diferida.
 */
function Vitrina() {
  const [categoria, setCategoria] = useState(null);
  const [pagina, setPagina] = useState(1);
  const categorias = useConsulta('categorias', publico.categorias);
  const { abrir } = useSolicitud();
  const claveActual = categoria ?? 'todas';
  const consulta = useConsulta(`catalogo:${claveActual}:${pagina}`,
    () => publico.catalogo({ categoria: categoria ?? undefined, pagina, tamano: POR_PAGINA }));

  // Las páginas ya vistas de la categoría actual se conservan al pedir la siguiente, con su total: así,
  // mientras llega la página nueva, el botón «Ver más trabajos» sigue ahí (cargando) y no pierde el foco.
  const [acumulado, setAcumulado] = useState({ clave: null, pagina: 0, trabajos: [], total: 0, totalPaginas: 0 });
  if (consulta.datos && acumulado.clave !== `${claveActual}:${pagina}`) {
    const previos = acumulado.clave?.startsWith(`${claveActual}:`) && pagina > 1 ? acumulado.trabajos : [];
    const nuevos = consulta.datos.elementos.filter((t) => !previos.some((p) => p.idCatalogo === t.idCatalogo));
    setAcumulado({
      clave: `${claveActual}:${pagina}`, pagina, trabajos: [...previos, ...nuevos],
      total: consulta.datos.total, totalPaginas: consulta.datos.totalPaginas,
    });
  }
  const vigente = acumulado.clave?.startsWith(`${claveActual}:`) ?? false;
  const trabajos = vigente ? acumulado.trabajos : [];
  const total = vigente ? acumulado.total : 0;
  const hayMas = vigente && acumulado.pagina < acumulado.totalPaginas;
  const cargandoMas = vigente && acumulado.pagina < pagina && !consulta.error;

  // Al llegar la página pedida, el foco pasa al primer trabajo nuevo: quien usa teclado o lector sigue
  // leyendo desde ahí, sin volver a recorrer los anteriores.
  const refLista = useRef(null);
  const enfocarDesde = useRef(null);
  useEffect(() => {
    const desde = enfocarDesde.current;
    if (desde === null || trabajos.length <= desde) return;
    enfocarDesde.current = null;
    refLista.current?.querySelectorAll('article')[desde]?.focus();
  }, [trabajos.length]);

  function verMas() {
    if (cargandoMas) return;
    enfocarDesde.current = trabajos.length;
    setPagina(acumulado.pagina + 1);
  }

  const opciones = useMemo(
    () => (categorias.datos ?? []).filter((c) => c.activo !== false).sort((a, b) => a.nombre.localeCompare(b.nombre, 'es')),
    [categorias.datos],
  );

  function elegir(id) {
    setCategoria(id);
    setPagina(1);
  }

  const nombreCategoria = opciones.find((c) => c.idCategoria === categoria)?.nombre;

  return (
    <section id="vitrina" className={estilos.seccionAlterna} aria-labelledby="titulo-vitrina">
      <div className={estilos.contenedorSimple}>
        <div className={estilos.encabezado}>
          <h2 id="titulo-vitrina">Trabajos realizados</h2>
          <p className={estilos.descripcion}>Precios de referencia: cada trabajo se cotiza según medidas, materiales e instalación.</p>
        </div>

        {opciones.length > 1 && (
          <div className={estilos.filtros} role="group" aria-label="Filtrar trabajos por categoría">
            <Chip seleccionado={categoria === null} alCambiar={() => elegir(null)}>Todos</Chip>
            {opciones.map((c) => (
              <Chip key={c.idCategoria} seleccionado={categoria === c.idCategoria}
                alCambiar={() => elegir(categoria === c.idCategoria ? null : c.idCategoria)}>
                {c.nombre}
              </Chip>
            ))}
          </div>
        )}
        <p className="solo-lectores" role="status">
          {vigente
            ? `Mostrando ${trabajos.length} de ${total} ${total === 1 ? 'trabajo' : 'trabajos'}${nombreCategoria ? ` en ${nombreCategoria}` : ''}`
            : ''}
        </p>

        {consulta.cargando && trabajos.length === 0 && (
          <div className={estilos.rejilla} aria-hidden="true">
            {[1, 2, 3].map((i) => <Esqueleto key={i} forma="bloque" alto="20rem" />)}
          </div>
        )}
        {consulta.error && (
          <Aviso tono="aviso" titulo="No pudimos cargar la vitrina"
            accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>} />
        )}
        {vigente && total === 0 && (
          <EstadoVacio nivelTitulo={3}
            titulo={nombreCategoria ? `Aún no publicamos trabajos de ${nombreCategoria}` : 'Aún no hay trabajos publicados'}
            descripcion="Cuéntanos qué necesitas y te enviamos una cotización." accion={<BotonSolicitar />} />
        )}
        {trabajos.length > 0 && (
          <ul ref={refLista} className={estilos.rejilla}>
            {trabajos.map((trabajo) => (
              <li key={trabajo.idCatalogo}>
                <TarjetaTrabajo trabajo={trabajo} alCotizar={() => abrir(trabajo)} />
              </li>
            ))}
          </ul>
        )}
        {hayMas && (
          <div className={estilos.masTrabajos}>
            <Boton variante="secundario" cargando={cargandoMas} textoCargando="Cargando…" onClick={verMas}>
              Ver más trabajos
            </Boton>
            <p className={estilos.conteo}>Mostrando {trabajos.length} de {total}</p>
          </div>
        )}
      </div>
    </section>
  );
}

function TarjetaTrabajo({ trabajo, alCotizar }) {
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
        <Boton variante="secundario" tamano="compacto" className={estilos.accionTrabajo} onClick={alCotizar}>
          Cotizar uno similar
        </Boton>
      </div>
    </Tarjeta>
  );
}

function HorarioYUbicacion({ estado }) {
  const datos = estado.datos;
  const hoy = datos?.hoy?.diaSemana;
  return (
    <section className={estilos.seccion} aria-label="Horario y ubicación">
      <div className={estilos.contenedorDoble}>
        <div id="horario">
          <h2 className={estilos.tituloBloque}>Horario de atención</h2>
          {estado.cargando && <Esqueleto lineas={7} />}
          {estado.error && <Aviso tono="aviso" titulo="No pudimos cargar el horario" />}
          {datos && (
            <>
              <table className={estilos.horario}>
                <caption className="solo-lectores">Horario semanal de atención</caption>
                <tbody>
                  {datos.semana.map((dia) => (
                    <tr key={dia.diaSemana} className={dia.diaSemana === hoy ? estilos.hoy : undefined}>
                      <th scope="row">
                        {dia.nombreDia}
                        {dia.diaSemana === hoy && <Insignia tono="neutro" className={estilos.marcaHoy}>Hoy</Insignia>}
                      </th>
                      <td className="cifras">{franja(dia)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <p className={estilos.descripcion}>Hora de Chile. Ahora son las <span className="cifras">{hora(datos.horaChile?.slice(11, 16))}</span>.</p>
            </>
          )}
        </div>
        <div id="ubicacion">
          <h2 className={estilos.tituloBloque}>Cómo llegar</h2>
          <address className={estilos.direccion}>
            <strong>{TALLER.nombre}</strong><br />
            {TALLER.direccion}<br />
            {TALLER.codigoPostal} {TALLER.ciudad}
          </address>
          <MapaUbicacion />
          <Enlace href={TALLER.mapa} externo>Abrir la ubicación en OpenStreetMap</Enlace>
        </div>
      </div>
    </section>
  );
}

function Contacto() {
  return (
    <section id="contacto" className={estilos.seccionAlterna} data-fondo="alterno" aria-labelledby="titulo-contacto">
      <div className={estilos.contenedorContacto}>
        <div className={estilos.encabezado}>
          <h2 id="titulo-contacto">Escríbenos</h2>
          <p className={estilos.descripcion}>
            ¿Tienes una pregunta antes de pedir? Déjanos tu consulta y te respondemos a tu correo.
            Para cotizar un trabajo, usa «Solicitar cotización».
          </p>
        </div>
        <Tarjeta>
          <FormularioContacto />
        </Tarjeta>
      </div>
    </section>
  );
}
