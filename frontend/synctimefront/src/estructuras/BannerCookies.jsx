import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Boton, Casilla, Enlace } from '../componentes/ui';
import { publico } from '../api/publico';
import {
  alPedirPreferencias, guardarPreferencia, identificadorVisitante, leerPreferencia,
} from '../utilidades/cookies';
import estilos from './BannerCookies.module.css';

const VARIABLE_ALTO = '--altura-banner-cookies';

/**
 * Aviso de cookies (RFS12, RNFP21). No bloquea la página ni roba el foco en la primera visita. Las dos
 * acciones tienen el mismo tamaño y estilo, y la casilla de analítica viene desmarcada: aceptar no es
 * más fácil que rechazar. Hoy no hay analítica que cargar; la elección queda guardada para cuando la haya.
 *
 * Mientras se ve, publica su alto en --altura-banner-cookies: la página reserva ese espacio abajo, para
 * que no tape el final del contenido, el elemento enfocado (WCAG 2.4.11) ni el botón de WhatsApp.
 */
export function BannerCookies() {
  const [visible, setVisible] = useState(() => leerPreferencia() === null);
  const [analitica, setAnalitica] = useState(() => leerPreferencia()?.analitica === true);
  const [pedido, setPedido] = useState(false);
  const refBanner = useRef(null);
  const refTitulo = useRef(null);
  const volverA = useRef(null);

  useEffect(() => alPedirPreferencias(() => {
    volverA.current = document.activeElement;
    setAnalitica(leerPreferencia()?.analitica === true);
    setPedido(true);
    setVisible(true);
  }), []);

  // Abierto desde «Preferencias de cookies»: el foco va al aviso para que se lea en el acto.
  useEffect(() => {
    if (visible && pedido) refTitulo.current?.focus();
  }, [visible, pedido]);

  useLayoutEffect(() => {
    const raiz = document.documentElement;
    const banner = refBanner.current;
    if (!visible || !banner) {
      raiz.style.removeProperty(VARIABLE_ALTO);
      return undefined;
    }
    const medir = () => raiz.style.setProperty(VARIABLE_ALTO, `${banner.offsetHeight}px`);
    medir();
    const observador = typeof ResizeObserver === 'function' ? new ResizeObserver(medir) : null;
    observador?.observe(banner);
    return () => {
      observador?.disconnect();
      raiz.style.removeProperty(VARIABLE_ALTO);
    };
  }, [visible]);

  if (!visible) return null;

  async function decidir(acepta) {
    const antes = leerPreferencia()?.analitica === true;
    guardarPreferencia(acepta);
    setVisible(false);
    setPedido(false);
    // El foco vuelve a donde estaba (el botón del pie) o, en la primera visita, al contenido.
    const destino = volverA.current?.isConnected ? volverA.current : document.getElementById('contenido');
    volverA.current = null;
    destino?.focus?.({ preventScroll: true });

    // La constancia (versión, fecha y hora) queda en la API: la aceptación y, si la había, su retiro.
    // Quien nunca aceptó no deja datos. Si falla, la elección igual rige en este navegador.
    try {
      if (!acepta) {
        if (antes) await publico.retirarCookies(identificadorVisitante());
        return;
      }
      const documentos = await publico.documentosLegales();
      const version = documentos.find((d) => d.tipo === 'cookies')?.version;
      if (version) await publico.aceptarCookies(identificadorVisitante(), version);
    } catch {
      // Sin conexión: se registra la próxima vez que el visitante cambie su preferencia.
    }
  }

  return (
    <section ref={refBanner} className={estilos.banner} aria-labelledby="titulo-cookies">
      <div className={estilos.interior}>
        <div className={estilos.texto}>
          <h2 id="titulo-cookies" ref={refTitulo} tabIndex={-1} className={estilos.titulo}>Cookies y privacidad</h2>
          <p>
            Solo usamos lo necesario para que el sitio funcione. Hoy no medimos visitas ni usamos rastreadores;
            si algún día lo hacemos, será solo con tu permiso.{' '}
            <Enlace como={Link} to="/privacidad#cookies">Más información</Enlace>
          </p>
          <Casilla
            etiqueta="Permitir analítica anónima de visitas"
            descripcion="Opcional. Hoy no se activa nada: guardamos tu elección para cuando midamos visitas."
            checked={analitica}
            onChange={(e) => setAnalitica(e.target.checked)}
          />
        </div>
        <div className={estilos.acciones}>
          <Boton variante="secundario" onClick={() => decidir(false)}>Rechazar opcionales</Boton>
          <Boton variante="secundario" onClick={() => decidir(analitica)}>Guardar mi elección</Boton>
        </div>
      </div>
    </section>
  );
}
