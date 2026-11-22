import { Link, useLocation } from 'react-router-dom';
import { ArrowRight, HardHat, UserRound } from 'lucide-react';
import { Tarjeta } from '../../componentes/ui';
import estilos from './SelectorIngreso.module.css';

/**
 * /ingresar (fase 12C): «¿Quién está iniciando sesión?». Dos tarjetas grandes, cliente a la izquierda y
 * personal del taller a la derecha (en el teléfono, una bajo la otra y el cliente primero). Son enlaces:
 * funcionan con teclado y lector de pantalla. El formulario de cada uno no cambia. El estado de la
 * navegación (adónde volver tras ingresar, el aviso de sesión vencida) pasa a la pantalla elegida, así las
 * guardas que mandan aquí no pierden el retorno.
 */
export default function SelectorIngreso() {
  const { state } = useLocation();
  return (
    <div className={estilos.pagina}>
      <div className={estilos.contenedor}>
        <h1 className={estilos.titulo}>¿Quién está iniciando sesión?</h1>
        <p className={estilos.bajada}>Elige cómo quieres ingresar.</p>
        <div className={estilos.opciones}>
          <Tarjeta como={Link} to="/ingresar/cliente" state={state} interactiva className={estilos.opcion}>
            <span className={estilos.icono} aria-hidden="true"><UserRound size={32} strokeWidth={1.5} /></span>
            <h2 className={estilos.nombre}>Cliente</h2>
            <p className={estilos.descripcion}>Sigue tus solicitudes, cotizaciones, pedidos y saldos.</p>
            <span className={estilos.accion}>Ingresar a mi cuenta <ArrowRight size={18} strokeWidth={1.75} aria-hidden="true" /></span>
          </Tarjeta>
          <Tarjeta como={Link} to="/personal/ingresar" state={state} interactiva className={estilos.opcion}>
            <span className={estilos.icono} aria-hidden="true"><HardHat size={32} strokeWidth={1.5} /></span>
            <h2 className={estilos.nombre}>Personal del taller</h2>
            <p className={estilos.descripcion}>Entra al panel con tu correo y tu contraseña, o con tu rostro.</p>
            <span className={estilos.accion}>Ingresar al panel <ArrowRight size={18} strokeWidth={1.75} aria-hidden="true" /></span>
          </Tarjeta>
        </div>
      </div>
    </div>
  );
}
