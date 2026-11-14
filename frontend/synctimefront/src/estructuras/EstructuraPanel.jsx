import { useEffect, useRef, useState } from 'react';
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { LogOut, Menu, X } from 'lucide-react';
import { Boton, Enlace, Insignia } from '../componentes/ui';
import { useSesion } from '../sesion/sesion';
import ErrorBoundary from '../componentes/ErrorBoundary';
import { Marca } from './Marca';
import { menuPara } from './navegacion';
import estilos from './EstructuraPanel.module.css';

const NOMBRE_ROL = { 'dueño': 'Dueño', administrador: 'Administrador', 'diseñador': 'Diseñador', operario: 'Operario' };

function MenuLateral({ alElegir }) {
  const { tieneRol } = useSesion();
  return (
    <nav aria-label="Panel del taller" className={estilos.menu}>
      {menuPara(tieneRol).map((grupo) => (
        <div key={grupo.grupo} className={estilos.grupo}>
          <h2 className={estilos.tituloGrupo}>{grupo.grupo}</h2>
          <ul className={estilos.lista}>
            {grupo.entradas.map(({ ruta, etiqueta, icono: Icono, exacta, fase }) => (
              <li key={ruta}>
                <NavLink to={ruta} end={exacta} onClick={alElegir}
                  className={({ isActive }) => (isActive ? `${estilos.entrada} ${estilos.activa}` : estilos.entrada)}>
                  <Icono size={18} strokeWidth={1.75} aria-hidden="true" />
                  <span className={estilos.etiqueta}>{etiqueta}</span>
                  {fase && <span className={estilos.fase}>Fase {fase}</span>}
                </NavLink>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </nav>
  );
}

function PieLateral() {
  const { usuario, salir } = useSesion();
  const navegar = useNavigate();
  return (
    <div className={estilos.pieLateral}>
      <div className={estilos.usuario}>
        <p className={estilos.nombreUsuario}>{usuario?.nombre} {usuario?.apellido}</p>
        <Insignia tono="neutro">{NOMBRE_ROL[usuario?.rol] ?? usuario?.rol}</Insignia>
      </div>
      <Boton variante="secundario" tamano="compacto" anchoCompleto iconoInicio={LogOut}
        onClick={() => {
          const destino = '/personal/ingresar';
          navegar(destino, { replace: true, state: { motivo: 'salida' } });
          // Si la API no cerró la sesión, el acceso del personal lo explica.
          salir().catch((error) => navegar(destino, { replace: true, state: { motivo: 'salida', errorSalida: error.message } }));
        }}>
        Salir
      </Boton>
      <ul className={estilos.legales}>
        <li><Enlace como={Link} to="/privacidad">Privacidad</Enlace></li>
        <li><Enlace como={Link} to="/terminos">Términos</Enlace></li>
      </ul>
    </div>
  );
}

/** Estructura del panel interno: menú lateral fijo en escritorio y cajón deslizable en el teléfono. */
export function EstructuraPanel() {
  const refCajon = useRef(null);
  const refBotonMenu = useRef(null);
  const [cajonAbierto, setCajonAbierto] = useState(false);
  const ubicacion = useLocation();

  const [rutaAnterior, setRutaAnterior] = useState(ubicacion.key);
  if (rutaAnterior !== ubicacion.key) {
    setRutaAnterior(ubicacion.key);
    setCajonAbierto(false);
  }

  useEffect(() => {
    const cajon = refCajon.current;
    if (!cajon) return;
    if (cajonAbierto && !cajon.open) cajon.showModal();
    if (!cajonAbierto && cajon.open) {
      cajon.close();
      refBotonMenu.current?.focus();
    }
  }, [cajonAbierto]);

  return (
    <div className={estilos.panel}>
      <a href="#contenido" className="enlace-salto">Saltar al contenido</a>

      <aside className={estilos.lateral}>
        <div className={estilos.cabeceraLateral}><Marca destino="/panel" subtitulo="Panel del taller" /></div>
        <MenuLateral />
        <PieLateral />
      </aside>

      <header className={estilos.barraMovil}>
        <Marca destino="/panel" subtitulo="Panel del taller" />
        <button ref={refBotonMenu} type="button" className={estilos.botonMenu} aria-haspopup="dialog"
          aria-expanded={cajonAbierto} onClick={() => setCajonAbierto(true)}>
          <Menu size={22} aria-hidden="true" />
          <span>Menú</span>
        </button>
      </header>

      <dialog ref={refCajon} className={estilos.cajon} aria-label="Menú del panel"
        onCancel={(e) => { e.preventDefault(); setCajonAbierto(false); }}
        onClick={(e) => e.target === e.currentTarget && setCajonAbierto(false)}>
        <div className={estilos.cajonContenido}>
          <div className={estilos.cabeceraCajon}>
            <Marca destino="/panel" subtitulo="Panel del taller" />
            <Boton variante="fantasma" tamano="compacto" aria-label="Cerrar menú" onClick={() => setCajonAbierto(false)}>
              <X size={20} aria-hidden="true" />
            </Boton>
          </div>
          {cajonAbierto && <MenuLateral alElegir={() => setCajonAbierto(false)} />}
          <PieLateral />
        </div>
      </dialog>

      <main id="contenido" tabIndex={-1} className={estilos.contenido}>
        <ErrorBoundary resetKey={ubicacion.pathname}>
          <Outlet />
        </ErrorBoundary>
      </main>
    </div>
  );
}
