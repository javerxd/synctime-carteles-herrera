import { useEffect, useId, useRef, useState } from 'react';
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom';
import { House, LogOut, Menu, X } from 'lucide-react';
import { Boton, Enlace, Insignia } from '../componentes/ui';
import { unirClases } from '../componentes/ui/clases';
import ErrorBoundary from '../componentes/ErrorBoundary';
import { Marca } from './Marca';
import estilos from './EstructuraLateral.module.css';

/** Menú por grupos: [{ grupo, entradas: [{ ruta, etiqueta, icono, exacta }] }]. */
function MenuLateral({ nombre, menu, alElegir }) {
  const base = useId();
  return (
    <nav aria-label={nombre} className={estilos.menu}>
      {menu.map((grupo, g) => (
        <div key={grupo.grupo} className={estilos.grupo}>
          <p id={`${base}-${g}`} className={estilos.tituloGrupo}>{grupo.grupo}</p>
          <ul className={estilos.lista} aria-labelledby={`${base}-${g}`}>
            {grupo.entradas.map(({ ruta, etiqueta, icono: Icono, exacta }) => (
              <li key={ruta}>
                <NavLink to={ruta} end={exacta} onClick={alElegir}
                  className={({ isActive }) => (isActive ? `${estilos.entrada} ${estilos.activa}` : estilos.entrada)}>
                  <Icono size={18} strokeWidth={1.75} aria-hidden="true" />
                  <span className={estilos.etiqueta}>{etiqueta}</span>
                </NavLink>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </nav>
  );
}

/**
 * Pie anclado abajo y siempre visible: quién es, «Volver a la página principal» (borde y letras blancas),
 * «Cerrar sesión» (rojo con letras blancas) y los enlaces legales. El menú es lo único que se desplaza.
 */
function PieLateral({ nombreUsuario, rolUsuario, alCerrarSesion, alElegir }) {
  return (
    <div className={estilos.pieLateral}>
      <div className={estilos.usuario}>
        <p className={estilos.nombreUsuario}>{nombreUsuario}</p>
        <Insignia tono="neutro">{rolUsuario}</Insignia>
      </div>
      <Boton como={Link} to="/" variante="fantasma" tamano="compacto" anchoCompleto iconoInicio={House}
        className={estilos.volver} onClick={alElegir}>
        Volver a la página principal
      </Boton>
      <Boton variante="peligro" tamano="compacto" anchoCompleto iconoInicio={LogOut} className={estilos.salir} onClick={alCerrarSesion}>
        Cerrar sesión
      </Boton>
      <ul className={estilos.legales}>
        <li><Enlace como={Link} to="/privacidad">Privacidad</Enlace></li>
        <li><Enlace como={Link} to="/terminos">Términos</Enlace></li>
      </ul>
    </div>
  );
}

/**
 * Estructura con menú lateral azul: fijo en escritorio y cajón deslizable en el teléfono (diálogo nativo:
 * atrapa el foco y Esc lo cierra). La usan el panel del taller y la cuenta del cliente.
 *
 * - `menu`: los grupos que ve esta persona (ya filtrados por rol).
 * - `zonaAnimada`: la zona lleva el fondo de baja opacidad y los botones animados de la 12C (cuenta del
 *   cliente). El panel del taller no: lo usa el personal todo el día.
 */
export function EstructuraLateral({
  nombreMenu, subtitulo, destinoMarca, menu, nombreUsuario, rolUsuario, alCerrarSesion, zonaAnimada = false,
}) {
  const refCajon = useRef(null);
  const refBotonMenu = useRef(null);
  const [cajonAbierto, setCajonAbierto] = useState(false);
  const ubicacion = useLocation();

  const [rutaAnterior, setRutaAnterior] = useState(ubicacion.key);
  if (rutaAnterior !== ubicacion.key) {
    setRutaAnterior(ubicacion.key);
    setCajonAbierto(false);
  }

  // Si el cajón se cerró al elegir un enlace, el foco va al contenido de la página nueva; si se cerró a mano (Esc o
  // la equis), vuelve al botón «Menú».
  const refElegido = useRef(false);

  useEffect(() => {
    const cajon = refCajon.current;
    if (!cajon) return;
    if (cajonAbierto && !cajon.open) cajon.showModal();
    if (!cajonAbierto && cajon.open) {
      cajon.close();
      if (refElegido.current) document.getElementById('contenido')?.focus();
      else refBotonMenu.current?.focus();
      refElegido.current = false;
    }
  }, [cajonAbierto]);

  // Si la ventana se ensancha con el cajón abierto, el menú fijo toma su lugar: el cajón modal se cierra.
  useEffect(() => {
    const consulta = window.matchMedia?.('(min-width: 64rem)');
    if (!consulta) return undefined;
    const alCambiar = (e) => e.matches && setCajonAbierto(false);
    consulta.addEventListener?.('change', alCambiar);
    return () => consulta.removeEventListener?.('change', alCambiar);
  }, []);

  const elegir = () => {
    refElegido.current = true;
    setCajonAbierto(false);
  };

  return (
    <div className={unirClases(estilos.panel, zonaAnimada && estilos.zona)}>
      <a href="#contenido" className="enlace-salto">Saltar al contenido</a>

      <div className={estilos.lateral}>
        <div className={estilos.cabeceraLateral}><Marca invertida destino={destinoMarca} subtitulo={subtitulo} /></div>
        <MenuLateral nombre={nombreMenu} menu={menu} />
        <PieLateral nombreUsuario={nombreUsuario} rolUsuario={rolUsuario} alCerrarSesion={alCerrarSesion} />
      </div>

      <header className={estilos.barraMovil}>
        <Marca destino={destinoMarca} subtitulo={subtitulo} />
        <button ref={refBotonMenu} type="button" className={estilos.botonMenu} aria-haspopup="dialog"
          aria-expanded={cajonAbierto} onClick={() => setCajonAbierto(true)}>
          <Menu size={22} aria-hidden="true" />
          <span>Menú</span>
        </button>
      </header>

      <dialog ref={refCajon} className={estilos.cajon} aria-label={`Menú: ${nombreMenu}`}
        onCancel={(e) => { e.preventDefault(); setCajonAbierto(false); }}
        onClick={(e) => e.target === e.currentTarget && setCajonAbierto(false)}>
        <div className={estilos.cajonContenido}>
          <div className={estilos.cabeceraCajon}>
            <Marca invertida destino={destinoMarca} subtitulo={subtitulo} />
            <button type="button" className={estilos.cerrarCajon} aria-label="Cerrar menú" onClick={() => setCajonAbierto(false)}>
              <X size={20} aria-hidden="true" />
            </button>
          </div>
          {cajonAbierto && <MenuLateral nombre={nombreMenu} menu={menu} alElegir={elegir} />}
          <PieLateral nombreUsuario={nombreUsuario} rolUsuario={rolUsuario} alCerrarSesion={alCerrarSesion} alElegir={elegir} />
        </div>
      </dialog>

      <main id="contenido" tabIndex={-1} className={estilos.contenido} data-animar={zonaAnimada ? '' : undefined}>
        <ErrorBoundary resetKey={ubicacion.pathname}>
          <Outlet />
        </ErrorBoundary>
      </main>
    </div>
  );
}
