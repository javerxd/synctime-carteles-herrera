import { Component, lazy, Suspense, useState } from 'react';
import { Map as IconoMapa, Store } from 'lucide-react';
import { Boton, Enlace } from '../../../componentes/ui';
import { TALLER } from '../../../configuracion/taller';
import estilos from './MapaUbicacion.module.css';

const MapaInteractivo = lazy(() => import('./MapaInteractivo'));

// Si el mapa interactivo no carga (sin conexión, o falló su archivo), se vuelve a la imagen en lugar de
// que el error tumbe toda la portada.
class SiFallaElMapa extends Component {
  state = { fallo: false };

  static getDerivedStateFromError() {
    return { fallo: true };
  }

  render() {
    return this.state.fallo ? this.props.respaldo : this.props.children;
  }
}

const ANUNCIOS = {
  imagen: '',
  mapa: 'Mapa interactivo activado. Usa las flechas para moverlo y las teclas más y menos para acercar o alejar.',
  fachada: 'Vista de la fachada activada, desde Google Street View.',
};

/**
 * Ubicación del taller (S01). Primero muestra una imagen del mapa alojada en el propio sitio: el
 * navegador no pide nada a terceros hasta que el visitante elige el mapa interactivo (OpenStreetMap) o
 * la vista de la fachada (Google Street View).
 */
export function MapaUbicacion() {
  const [vista, setVista] = useState('imagen');
  const [intento, setIntento] = useState(0);
  const descripcion = `Mapa de ${TALLER.direccion}, ${TALLER.ciudad}, con la ubicación de ${TALLER.nombre}`;
  const imagen = (alt) => (
    <img src="/mapa/ubicacion-taller.webp" alt={alt} className={estilos.imagen} width="1200" height="600" loading="lazy" decoding="async" />
  );

  const botonMapa = (
    <Boton variante="secundario" tamano="compacto" iconoInicio={IconoMapa} onClick={() => setVista('mapa')}>
      Mapa interactivo
    </Boton>
  );
  const botonFachada = TALLER.fachada && (
    <Boton variante="secundario" tamano="compacto" iconoInicio={Store} onClick={() => setVista('fachada')}>
      Ver la fachada
    </Boton>
  );

  return (
    <figure className={estilos.mapa}>
      <div className={estilos.marco}>
        {vista === 'imagen' && (
          <>
            {imagen(descripcion)}
            <div className={estilos.activar}>{botonMapa}{botonFachada}</div>
          </>
        )}
        {vista === 'mapa' && (
          <>
            <SiFallaElMapa key={intento} respaldo={(
              <>
                {imagen(descripcion)}
                <div className={estilos.activar}>
                  <Boton variante="secundario" tamano="compacto" iconoInicio={IconoMapa} onClick={() => setIntento((i) => i + 1)}>
                    No cargó el mapa. Reintentar
                  </Boton>
                </div>
              </>
            )}>
              <Suspense fallback={imagen('')}>
                <MapaInteractivo descripcion={descripcion} />
              </Suspense>
            </SiFallaElMapa>
            {botonFachada && <div className={estilos.activar}>{botonFachada}</div>}
          </>
        )}
        {vista === 'fachada' && (
          <>
            <iframe
              src={TALLER.fachada}
              title={`Fachada de ${TALLER.nombre} en Google Street View`}
              className={estilos.interactivo}
              loading="lazy"
              referrerPolicy="strict-origin-when-cross-origin"
              allowFullScreen
            />
            <div className={estilos.activar}>{botonMapa}</div>
          </>
        )}
      </div>
      <p className="solo-lectores" role="status">{ANUNCIOS[vista]}</p>
      <figcaption className={estilos.nota}>
        {vista === 'imagen' && 'El mapa interactivo se conecta con OpenStreetMap y la vista de la fachada con Google, solo si los usas. '}
        {vista === 'mapa' && 'El mapa interactivo carga imágenes desde OpenStreetMap. '}
        {vista === 'fachada' && 'La vista de la fachada la entrega Google Street View, bajo las condiciones de Google. '}
        Datos del mapa ©{' '}
        <Enlace href="https://www.openstreetmap.org/copyright" externo>colaboradores de OpenStreetMap</Enlace>.
      </figcaption>
    </figure>
  );
}
