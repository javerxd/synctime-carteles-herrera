import { useNavigate } from 'react-router-dom';
import { useSesion } from '../sesion/sesion';
import { EstructuraLateral } from './EstructuraLateral';
import { menuPara } from './navegacion';

const NOMBRE_ROL = { 'dueño': 'Dueño', administrador: 'Administrador', 'diseñador': 'Diseñador', operario: 'Operario' };

/**
 * Estructura del panel interno: menú lateral azul (fijo en escritorio, cajón en el teléfono) con «Volver a la
 * página principal» y «Cerrar sesión» anclados abajo. Sin imagen de fondo ni botones animados: el personal lo
 * usa todo el día (fase 12C y 12D).
 */
export function EstructuraPanel() {
  const { usuario, tieneRol, salir } = useSesion();
  const navegar = useNavigate();

  function cerrarSesion() {
    const destino = '/personal/ingresar';
    navegar(destino, { replace: true, state: { motivo: 'salida' } });
    // Si la API no cerró la sesión, el acceso del personal lo explica.
    salir().catch((error) => navegar(destino, { replace: true, state: { motivo: 'salida', errorSalida: error.message } }));
  }

  return (
    <EstructuraLateral
      nombreMenu="Panel del taller"
      subtitulo="Panel del taller"
      destinoMarca="/panel"
      menu={menuPara(tieneRol)}
      nombreUsuario={`${usuario?.nombre ?? ''} ${usuario?.apellido ?? ''}`.trim()}
      rolUsuario={NOMBRE_ROL[usuario?.rol] ?? usuario?.rol}
      alCerrarSesion={cerrarSesion}
    />
  );
}
