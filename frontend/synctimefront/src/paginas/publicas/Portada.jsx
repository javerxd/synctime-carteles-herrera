import { useMemo, useState } from 'react';
import { ArrowRight, Clock, MapPin, MessageCircle } from 'lucide-react';
import {
  Aviso, Boton, Chip, Enlace, Esqueleto, EstadoCartel, EstadoVacio, Insignia, Tarjeta,
} from '../../componentes/ui';
import { publico } from '../../api/publico';
import { enlaceWhatsapp, TALLER } from '../../configuracion/taller';
import { BotonSolicitar, useSolicitud } from '../../estructuras/Solicitud';
import { formatearFechaHora, formatearPesos } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { diaDeHoy, franja } from './horario';
import estilos from './Portada.module.css';

/**
 * Portada pública, estructura «Mostrador» (elegida por el usuario el 13-11-2026): la propuesta de
 * valor y la acción principal a la izquierda; a la derecha, la ficha del mostrador con el cartel
 * electrónico, el horario de hoy y cómo llegar. Debajo, la vitrina, el horario semanal y la ubicación.
 */
export default function Portada() {
  const cartel = useConsulta('cartel', publico.cartel);
  const horarios = useConsulta('horarios', publico.horarios);
  const catalogo = useConsulta('catalogo', publico.catalogo);

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
          <Mostrador cartel={cartel} horarios={horarios} />
        </div>
      </section>

      <Vitrina consulta={catalogo} />
      <HorarioYUbicacion horarios={horarios} />
    </>
  );
}

function Mostrador({ cartel, horarios }) {
  const hoy = horarios.datos?.find((d) => d.diaSemana === diaDeHoy());
  const whatsapp = enlaceWhatsapp();
  return (
    <Tarjeta como="aside" aria-labelledby="titulo-mostrador" className={estilos.mostrador}>
      <h2 id="titulo-mostrador" className={estilos.tituloMostrador}>En el taller ahora</h2>
      {cartel.cargando && <Esqueleto forma="bloque" alto="3rem" />}
      {cartel.error && <Aviso tono="aviso" titulo="No pudimos cargar el estado del local">Inténtalo de nuevo en unos minutos.</Aviso>}
      {cartel.datos && (
        <div className={estilos.cartel}>
          <EstadoCartel abierto={cartel.datos.abiertoEnLocal} grande />
          <p className={estilos.mensaje}>{cartel.datos.mensajeEstado}</p>
          {!cartel.datos.abiertoEnLocal && cartel.datos.tiempoEstimadoRegreso && (
            <p className={estilos.dato}>Regreso estimado: <strong>{cartel.datos.tiempoEstimadoRegreso}</strong></p>
          )}
          <p className={estilos.actualizado}>Actualizado el {formatearFechaHora(cartel.datos.fechaUltimaModificacion)}</p>
        </div>
      )}
      <ul className={estilos.datos}>
        <li>
          <Clock size={18} strokeWidth={1.75} aria-hidden="true" />
          <span>Hoy: {horarios.cargando ? '…' : franja(hoy)}</span>
        </li>
        <li>
          <MapPin size={18} strokeWidth={1.75} aria-hidden="true" />
          <span>{TALLER.direccion}, {TALLER.ciudad}</span>
        </li>
      </ul>
      <div className={estilos.accionesMostrador}>
        <Boton como="a" href={TALLER.mapa} target="_blank" rel="noopener noreferrer" variante="secundario" tamano="compacto" iconoInicio={MapPin}>
          Cómo llegar<span className="solo-lectores"> (se abre en otra pestaña)</span>
        </Boton>
        {whatsapp && (
          <Boton como="a" href={whatsapp} target="_blank" rel="noopener noreferrer" variante="secundario" tamano="compacto" iconoInicio={MessageCircle}>
            WhatsApp<span className="solo-lectores"> (se abre en otra pestaña)</span>
          </Boton>
        )}
      </div>
    </Tarjeta>
  );
}

// La semilla demo apunta a fotos de ejemplo que llegan en la fase 5 (subida de imágenes en WebP).
// Mientras tanto no se piden, para no provocar errores 404: la tarjeta muestra «Foto pendiente».
const fotoDisponible = (url) => Boolean(url) && !url.startsWith('/imagenes/demo/');

function medidas(trabajo) {
  if (!trabajo.anchoMm || !trabajo.altoMm) return null;
  const cm = (mm) => new Intl.NumberFormat('es-CL', { maximumFractionDigits: 1 }).format(mm / 10);
  return `${cm(trabajo.anchoMm)} × ${cm(trabajo.altoMm)} cm`;
}

function Vitrina({ consulta }) {
  const [categoria, setCategoria] = useState(null);
  const { abrir } = useSolicitud();
  const trabajos = useMemo(() => consulta.datos ?? [], [consulta.datos]);

  const categorias = useMemo(() => {
    const porId = new Map();
    for (const t of trabajos) {
      const c = t.producto?.categoria;
      if (c) porId.set(c.idCategoria, c.nombre);
    }
    return [...porId].sort((a, b) => a[1].localeCompare(b[1], 'es'));
  }, [trabajos]);

  const visibles = categoria === null ? trabajos : trabajos.filter((t) => t.producto?.categoria?.idCategoria === categoria);

  return (
    <section id="vitrina" className={estilos.seccionAlterna} aria-labelledby="titulo-vitrina">
      <div className={estilos.contenedorSimple}>
        <div className={estilos.encabezado}>
          <h2 id="titulo-vitrina">Trabajos realizados</h2>
          <p className={estilos.descripcion}>Precios de referencia: cada trabajo se cotiza según medidas, materiales e instalación.</p>
        </div>

        {categorias.length > 1 && (
          <div className={estilos.filtros} role="group" aria-label="Filtrar trabajos por categoría">
            <Chip seleccionado={categoria === null} alCambiar={() => setCategoria(null)}>Todos</Chip>
            {categorias.map(([id, nombre]) => (
              <Chip key={id} seleccionado={categoria === id} alCambiar={() => setCategoria(categoria === id ? null : id)}>{nombre}</Chip>
            ))}
          </div>
        )}
        <p className="solo-lectores" role="status">
          {consulta.datos ? `${visibles.length} ${visibles.length === 1 ? 'trabajo' : 'trabajos'}` : ''}
        </p>

        {consulta.cargando && (
          <div className={estilos.rejilla} aria-hidden="true">
            {[1, 2, 3].map((i) => <Esqueleto key={i} forma="bloque" alto="20rem" />)}
          </div>
        )}
        {consulta.error && <Aviso tono="aviso" titulo="No pudimos cargar la vitrina">Inténtalo de nuevo en unos minutos.</Aviso>}
        {consulta.datos && visibles.length === 0 && (
          <EstadoVacio nivelTitulo={3} titulo="Aún no hay trabajos publicados"
            descripcion="Mientras tanto, cuéntanos qué necesitas y te enviamos una cotización." accion={<BotonSolicitar />} />
        )}
        {visibles.length > 0 && (
          <ul className={estilos.rejilla}>
            {visibles.map((trabajo) => (
              <li key={trabajo.idCatalogo}>
                <Tarjeta como="article" interactiva sinRelleno className={estilos.trabajo} aria-labelledby={`trabajo-${trabajo.idCatalogo}`}>
                  {fotoDisponible(trabajo.imagenUrl) ? (
                    <img src={trabajo.imagenUrl} alt={trabajo.titulo} loading="lazy" decoding="async" width="640" height="480" className={estilos.foto} />
                  ) : (
                    <div className={estilos.sinFoto}><span>Foto del trabajo pendiente</span></div>
                  )}
                  <div className={estilos.cuerpoTrabajo}>
                    {trabajo.producto?.categoria && <Insignia tono="neutro">{trabajo.producto.categoria.nombre}</Insignia>}
                    <h3 id={`trabajo-${trabajo.idCatalogo}`} className={estilos.tituloTrabajo}>{trabajo.titulo}</h3>
                    {trabajo.descripcion && <p className={estilos.textoTrabajo}>{trabajo.descripcion}</p>}
                    <p className={estilos.ficha}>
                      {[trabajo.producto?.nombre, medidas(trabajo)].filter(Boolean).join(' · ')}
                    </p>
                    {trabajo.precioReferencial > 0 && (
                      <p className={estilos.precio}><span>Desde</span> <strong className="cifras">{formatearPesos(trabajo.precioReferencial)}</strong></p>
                    )}
                    <Boton variante="secundario" tamano="compacto" className={estilos.accionTrabajo} onClick={() => abrir(trabajo)}>
                      Cotizar uno similar
                    </Boton>
                  </div>
                </Tarjeta>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}

function HorarioYUbicacion({ horarios }) {
  const hoy = diaDeHoy();
  return (
    <section className={estilos.seccion} aria-label="Horario y ubicación">
      <div className={estilos.contenedorDoble}>
        <div id="horario">
          <h2 className={estilos.tituloBloque}>Horario de atención</h2>
          {horarios.cargando && <Esqueleto lineas={7} />}
          {horarios.error && <Aviso tono="aviso" titulo="No pudimos cargar el horario" />}
          {horarios.datos && (
            <table className={estilos.horario}>
              <caption className="solo-lectores">Horario semanal de atención</caption>
              <tbody>
                {horarios.datos.map((dia) => (
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
          )}
        </div>
        <div id="ubicacion">
          <h2 className={estilos.tituloBloque}>Cómo llegar</h2>
          <address className={estilos.direccion}>
            <strong>{TALLER.nombre}</strong><br />
            {TALLER.direccion}<br />
            {TALLER.codigoPostal} {TALLER.ciudad}
          </address>
          <p className={estilos.descripcion}>
            El mapa interactivo se agrega en la fase 5; se activará solo si lo pides, para no enviar tus datos a terceros.
          </p>
          <Enlace href={TALLER.mapa} externo>Abrir la ubicación en OpenStreetMap</Enlace>
        </div>
      </div>
    </section>
  );
}
