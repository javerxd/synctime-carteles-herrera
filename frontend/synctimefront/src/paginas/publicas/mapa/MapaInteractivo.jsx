import { useEffect, useRef } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { TALLER } from '../../../configuracion/taller';
import estilos from './MapaUbicacion.module.css';

/**
 * Mapa de OpenStreetMap con Leaflet (BSD-2-Clause). Se carga aparte y solo cuando el visitante lo pide.
 * El marcador es un círculo dibujado (sin imágenes externas), en tinta con borde blanco como el resto
 * del sitio.
 */
export default function MapaInteractivo({ descripcion }) {
  const refContenedor = useRef(null);

  useEffect(() => {
    const { latitud, longitud } = TALLER.coordenadas;
    // En pantallas táctiles no se arrastra con un dedo: ese gesto sigue desplazando la página.
    const mapa = L.map(refContenedor.current, { scrollWheelZoom: false, dragging: !L.Browser.mobile })
      .setView([latitud, longitud], 17);
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '© colaboradores de OpenStreetMap',
    }).addTo(mapa);
    L.circleMarker([latitud, longitud], {
      radius: 11, color: '#FFFFFF', weight: 3, fillColor: '#0F172A', fillOpacity: 1,
    }).addTo(mapa).bindTooltip(TALLER.nombre, { direction: 'top', offset: [0, -10] });
    refContenedor.current.focus();
    return () => mapa.remove();
  }, []);

  return <div ref={refContenedor} className={estilos.interactivo} role="region" aria-label={descripcion} tabIndex={-1} />;
}
