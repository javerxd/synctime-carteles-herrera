import { useEffect, useRef, useState } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { LayoutDashboard, Mail, Menu, MessageCircle, Phone, UserRound, X } from 'lucide-react';
import { Boton, Enlace } from '../componentes/ui';
import { unirClases } from '../componentes/ui/clases';
import { enlaceTelefono, enlaceWhatsapp, TALLER } from '../configuracion/taller';
import { abrirPreferenciasCookies } from '../utilidades/cookies';
import { BannerCookies } from './BannerCookies';
import ErrorBoundary from '../componentes/ErrorBoundary';
import { useSesion } from '../sesion/sesion';
import { Marca } from './Marca';
import { BotonSolicitar, ProveedorSolicitud } from './Solicitud';
import estilos from './EstructuraPublica.module.css';

const ANIO_ACTUAL = new Date().getFullYear();

/** «Ingresar», o el acceso a la cuenta (cliente) o al panel (personal) cuando ya hay sesión. */
function AccesoCuenta() {
  const { esCliente, esPersonal } = useSesion();
  if (esCliente) return <Boton como={Link} to="/cuenta" variante="fantasma" tamano="compacto" iconoInicio={UserRound}>Mi cuenta</Boton>;
  if (esPersonal) return <Boton como={Link} to="/panel" variante="fantasma" tamano="compacto" iconoInicio={LayoutDashboard}>Ir al panel</Boton>;
  return <Boton como={Link} to="/ingresar" variante="fantasma" tamano="compacto">Ingresar</Boton>;
}

const SECCIONES = [
  ['/#vitrina', 'Trabajos'],
  ['/#horario', 'Horario'],
  ['/#ubicacion', 'Cómo llegar'],
  ['/#contacto', 'Contacto'],
];

/** Estructura del portal público: barra flotante de cristal, contenido y pie con enlaces legales. */
export function EstructuraPublica() {
  const [menuAbierto, setMenuAbierto] = useState(false);
  const refBotonMenu = useRef(null);
  const ubicacion = useLocation();

  // Al cambiar de página o de sección, el menú del teléfono se cierra.
  const [rutaAnterior, setRutaAnterior] = useState(ubicacion.key);
  if (rutaAnterior !== ubicacion.key) {
    setRutaAnterior(ubicacion.key);
    setMenuAbierto(false);
  }

  // Esc cierra el menú y devuelve el foco a su botón.
  useEffect(() => {
    if (!menuAbierto) return undefined;
    function alTeclear(evento) {
      if (evento.key === 'Escape') {
        setMenuAbierto(false);
        refBotonMenu.current?.focus();
      }
    }
    document.addEventListener('keydown', alTeclear);
    return () => document.removeEventListener('keydown', alTeclear);
  }, [menuAbierto]);

  return (
    <ProveedorSolicitud>
      <a href="#contenido" className="enlace-salto">Saltar al contenido</a>
      {/* Al principio del documento: quien navega con teclado o lector lo encuentra antes que el resto. */}
      <BannerCookies />
      <header className={estilos.barra}>
        <div className={estilos.interior}>
          <Marca />
          <button
            ref={refBotonMenu}
            type="button"
            className={estilos.botonMenu}
            aria-expanded={menuAbierto}
            aria-controls="menu-publico"
            onClick={() => setMenuAbierto((abierto) => !abierto)}
          >
            {menuAbierto ? <X size={22} aria-hidden="true" /> : <Menu size={22} aria-hidden="true" />}
            <span>Menú</span>
          </button>
          <nav id="menu-publico" aria-label="Principal" className={unirClases(estilos.navegacion, menuAbierto && estilos.abierta)}>
            <ul className={estilos.enlaces}>
              {SECCIONES.map(([destino, nombre]) => (
                <li key={destino}>
                  <Link to={destino} onClick={() => setMenuAbierto(false)}>{nombre}</Link>
                </li>
              ))}
            </ul>
            <div className={estilos.acciones}>
              <AccesoCuenta />
              <BotonSolicitar tamano="compacto" variante="secundario" />
            </div>
          </nav>
        </div>
      </header>

      <main id="contenido" tabIndex={-1} className={estilos.principal}>
        <ErrorBoundary resetKey={ubicacion.pathname}>
          <Outlet />
        </ErrorBoundary>
      </main>

      <footer className={estilos.pie}>
        <div className={estilos.pieInterior}>
          <div className={estilos.pieMarca}>
            <Marca />
            <p className={estilos.pieTexto}>Letreros, rotulación de vehículos y gráfica, diseñados y fabricados en el taller.</p>
          </div>
          <div>
            <h2 className={estilos.pieTitulo}>Visítanos</h2>
            <address className={estilos.pieTexto}>
              {TALLER.direccion}<br />{TALLER.ciudad}
            </address>
            <Enlace href={TALLER.mapa} externo className={estilos.pieEnlace}>Abrir en Google Maps</Enlace>
          </div>
          <div>
            <h2 className={estilos.pieTitulo}>Contacto</h2>
            <ul className={estilos.pieLista}>
              {enlaceTelefono() && (
                <li><Enlace href={enlaceTelefono()} className={estilos.pieContacto}><Phone size={16} aria-hidden="true" />{TALLER.telefono}</Enlace></li>
              )}
              {TALLER.correo && (
                <li><Enlace href={`mailto:${TALLER.correo}`} className={estilos.pieContacto}><Mail size={16} aria-hidden="true" />{TALLER.correo}</Enlace></li>
              )}
              {enlaceWhatsapp() && (
                <li>
                  <Enlace href={enlaceWhatsapp()} externo className={estilos.pieContacto}>
                    <MessageCircle size={16} aria-hidden="true" />Escribir por WhatsApp
                  </Enlace>
                </li>
              )}
            </ul>
          </div>
          <nav aria-label="Información legal">
            <h2 className={estilos.pieTitulo}>Información</h2>
            <ul className={estilos.pieLista}>
              <li><Enlace como={Link} to="/privacidad">Política de privacidad</Enlace></li>
              <li><Enlace como={Link} to="/terminos">Términos y condiciones</Enlace></li>
              <li><button type="button" className={estilos.pieBoton} onClick={abrirPreferenciasCookies}>Preferencias de cookies</button></li>
              <li><Enlace como={Link} to="/personal/ingresar">Acceso del personal</Enlace></li>
            </ul>
          </nav>
        </div>
        <p className={estilos.derechos}>© {ANIO_ACTUAL} {TALLER.nombre} · SyncTime</p>
      </footer>

      {enlaceWhatsapp() && (
        <a href={enlaceWhatsapp()} target="_blank" rel="noopener noreferrer" className={estilos.whatsapp}>
          <MessageCircle size={24} strokeWidth={2} aria-hidden="true" />
          <span className="solo-lectores">Escribir por WhatsApp (se abre en otra pestaña)</span>
        </a>
      )}
    </ProveedorSolicitud>
  );
}
