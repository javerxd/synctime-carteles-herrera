import { Component, lazy, Suspense, useState } from 'react';
import { Map as IconoMapa } from 'lucide-react';
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

/**
 * Ubicación del taller (S01). Primero muestra una imagen del mapa alojada en el propio sitio: el
 * navegador no pide nada a OpenStreetMap hasta que el visitante activa el mapa interactivo.
 */
export function MapaUbicacion() {
  const [interactivo, setInteractivo] = useState(false);
  const [intento, setIntento] = useState(0);
  const descripcion = `Mapa de ${TALLER.direccion}, ${TALLER.ciudad}${TALLER.ubicacionAproximada ? ' (ubicación aproximada)' : ''}`;

  return (
    <figure className={estilos.mapa}>
      <div className={estilos.marco}>
        {interactivo ? (
          <SiFallaElMapa key={intento} respaldo={(
            <>
              <img src="/mapa/ubicacion-taller.webp" alt={descripcion} className={estilos.imagen} width="1200" height="600" />
              <div className={estilos.activar}>
                <Boton variante="secundario" tamano="compacto" iconoInicio={IconoMapa} onClick={() => setIntento((i) => i + 1)}>
                  No cargó el mapa. Reintentar
                </Boton>
              </div>
            </>
          )}>
            <Suspense fallback={<img src="/mapa/ubicacion-taller.webp" alt="" className={estilos.imagen} width="1200" height="600" />}>
              <MapaInteractivo descripcion={descripcion} />
            </Suspense>
          </SiFallaElMapa>
        ) : (
          <>
            <img src="/mapa/ubicacion-taller.webp" alt={descripcion} className={estilos.imagen} width="1200" height="600" loading="lazy" decoding="async" />
            <div className={estilos.activar}>
              <Boton variante="secundario" tamano="compacto" iconoInicio={IconoMapa} onClick={() => setInteractivo(true)}>
                Usar mapa interactivo
              </Boton>
            </div>
          </>
        )}
      </div>
      <p className="solo-lectores" role="status">
        {interactivo ? 'Mapa interactivo activado. Usa las flechas para moverlo y las teclas más y menos para acercar o alejar.' : ''}
      </p>
      <figcaption className={estilos.nota}>
        {interactivo
          ? 'El mapa interactivo carga imágenes desde OpenStreetMap.'
          : 'Al usar el mapa interactivo, tu navegador se conecta con OpenStreetMap.'}{' '}
        Datos del mapa ©{' '}
        <Enlace href="https://www.openstreetmap.org/copyright" externo>colaboradores de OpenStreetMap</Enlace>.
      </figcaption>
    </figure>
  );
}
