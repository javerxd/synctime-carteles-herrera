import { Link } from 'react-router-dom';
import { Hammer } from 'lucide-react';
import { Boton, EstadoVacio } from '../componentes/ui';
import estilos from './Pendientes.module.css';

/** Una sección cuya lógica llega en una fase posterior: lo dice sin fingir datos. */
export function SeccionPendiente({ titulo, fase, descripcion, publica = false }) {
  return (
    <div className={publica ? estilos.publica : undefined}>
      <h1 className={estilos.titulo}>{titulo}</h1>
      <EstadoVacio
        icono={Hammer}
        titulo={`Esta sección se construye en la fase ${fase}`}
        descripcion={descripcion}
        accion={publica
          ? <Boton como={Link} to="/" variante="secundario">Volver a la portada</Boton>
          : <Boton como={Link} to="/panel" variante="secundario">Volver al inicio del panel</Boton>}
      />
    </div>
  );
}
