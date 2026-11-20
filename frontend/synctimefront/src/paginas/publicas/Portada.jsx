import { useEffect, useRef } from 'react';
import { Link, Navigate, useLocation } from 'react-router-dom';
import { ArrowRight, Clock, Mail, MapPin, MessageSquareText, Phone } from 'lucide-react';
import {
  Aviso, Boton, Enlace, Esqueleto, IconoWhatsapp, Insignia, Tarjeta,
} from '../../componentes/ui';
import { publico } from '../../api/publico';
import { enlaceTelefono, enlaceWhatsapp, TALLER } from '../../configuracion/taller';
import { BotonSolicitar } from '../../estructuras/Solicitud';
import { useConsulta } from '../../utilidades/useConsulta';
import { ComprobanteBaja } from '../cuenta/ComprobanteBaja';
import { CartelGrande } from './cartel/CartelLocal';
import { franja, hora } from './horario';
import { MapaUbicacion } from './mapa/MapaUbicacion';
import { useEstadoLocal } from './useEstadoLocal';
import { Vitrina } from './vitrina/Vitrina';
import estilos from './Portada.module.css';

/**
 * Portada pública, estructura «Mostrador» (elegida por el usuario el 13-11-2026): la propuesta de
 * valor y la acción principal a la izquierda; a la derecha, la ficha del mostrador con el estado del
 * local (horario en hora de Chile y cartel), el horario de hoy y cómo contactarnos. Debajo, la vitrina,
 * el horario semanal con el mapa y los canales de contacto. El formulario «Escríbenos» vive en /consultas
 * (fase 12C); el estado del local lo comparte la estructura pública con el cartel pequeño de la barra.
 */
export default function Portada() {
  const estado = useEstadoLocal();
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

  // El enlace antiguo /#contacto (correos, marcadores) lleva al formulario, que ahora es /consultas.
  if (hash === '#contacto') return <Navigate to="/consultas" replace />;

  return (
    <>
      <ComprobanteBaja />
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
      <CartelGrande estado={estado} />
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
          <Boton como="a" href={whatsapp} target="_blank" rel="noopener noreferrer" variante="secundario" tamano="compacto" iconoInicio={IconoWhatsapp}>
            Escribir por WhatsApp<span className="solo-lectores"> (se abre en otra pestaña)</span>
          </Boton>
        )}
      </div>
    </Tarjeta>
  );
}

function HorarioYUbicacion({ estado }) {
  const datos = estado.datos;
  const hoy = datos?.hoy?.diaSemana;
  // Franjas de atención del equipo (RFU20): la unión de todo el personal, sin nombres. Si no cargan, el horario igual se ve.
  const atencion = useConsulta('atencion', publico.atencion);
  const franjasDe = (dia) => (atencion.datos ?? []).find((a) => Number(a.diaSemana) === dia)?.franjas ?? [];
  const hayFranjas = (atencion.datos ?? []).some((a) => a.franjas?.length);
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
                      <td className="cifras">
                        {franja(dia)}
                        {dia.abierto && franjasDe(dia.diaSemana).length > 0 && (
                          <span className={estilos.atencion}>
                            Atención: {franjasDe(dia.diaSemana).map((f) => `${hora(f.desde)} a ${hora(f.hasta)}`).join(' · ')}
                          </span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {hayFranjas && (
                <p className={estilos.descripcion}>«Atención» son las franjas en que el equipo está para atenderte en el local.</p>
              )}
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
          <div className={estilos.accionesUbicacion}>
            <Boton como="a" href={TALLER.mapa} target="_blank" rel="noopener noreferrer" variante="secundario" iconoInicio={MapPin}>
              Abrir en Google Maps<span className="solo-lectores"> (se abre en otra pestaña)</span>
            </Boton>
          </div>
        </div>
      </div>
    </section>
  );
}

/** Canales directos; el formulario por escrito está en /consultas (fase 12C). */
function Contacto() {
  const whatsapp = enlaceWhatsapp();
  const telefono = enlaceTelefono();
  return (
    <section data-fondo="alterno" className={estilos.seccionAlterna} aria-labelledby="titulo-contacto">
      <div className={estilos.contenedorContacto}>
        <div className={estilos.encabezado}>
          <h2 id="titulo-contacto">¿Tienes una consulta?</h2>
          <p className={estilos.descripcion}>
            Llámanos o escríbenos por WhatsApp. Si prefieres dejarla por escrito, te respondemos a tu correo.
            Para cotizar un trabajo, usa «Solicitar cotización».
          </p>
        </div>
        <div className={estilos.canales}>
          <Boton como={Link} to="/consultas" variante="secundario" iconoInicio={MessageSquareText}>Escribir una consulta</Boton>
          {whatsapp && (
            <Boton como="a" href={whatsapp} target="_blank" rel="noopener noreferrer" variante="secundario" iconoInicio={IconoWhatsapp}>
              Escribir por WhatsApp<span className="solo-lectores"> (se abre en otra pestaña)</span>
            </Boton>
          )}
        </div>
        <ul className={estilos.canalesLista}>
          {telefono && (
            <li><Phone size={16} strokeWidth={1.75} aria-hidden="true" /><Enlace href={telefono}>{TALLER.telefono}</Enlace></li>
          )}
          {TALLER.correo && (
            <li><Mail size={16} strokeWidth={1.75} aria-hidden="true" /><Enlace href={`mailto:${TALLER.correo}`}>{TALLER.correo}</Enlace></li>
          )}
        </ul>
      </div>
    </section>
  );
}
