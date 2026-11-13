import { Link } from 'react-router-dom';
import { Hammer, UserRound, Wrench } from 'lucide-react';
import { Boton, EstadoVacio, Tarjeta } from '../componentes/ui';
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

/** /ingresar: el cliente y el personal entran por puertas distintas. */
export function ElegirIngreso() {
  return (
    <div className={estilos.publica}>
      <h1 className={estilos.titulo}>Ingresar</h1>
      <div className={estilos.opciones}>
        <Tarjeta como="section" aria-labelledby="ingreso-cliente" className={estilos.opcion}>
          <UserRound size={28} strokeWidth={1.5} aria-hidden="true" />
          <h2 id="ingreso-cliente" className={estilos.subtitulo}>Soy cliente</h2>
          <p className={estilos.texto}>
            Para seguir tus pedidos, aprobar cotizaciones y ver tu saldo. La cuenta de clientes se habilita en la fase 6.
          </p>
        </Tarjeta>
        <Tarjeta como="section" aria-labelledby="ingreso-personal" className={estilos.opcion}>
          <Wrench size={28} strokeWidth={1.5} aria-hidden="true" />
          <h2 id="ingreso-personal" className={estilos.subtitulo}>Soy del taller</h2>
          <p className={estilos.texto}>Acceso al panel de solicitudes, pedidos, cobranza e indicadores.</p>
          <Boton como={Link} to="/personal/ingresar">Acceso del personal</Boton>
        </Tarjeta>
      </div>
    </div>
  );
}
