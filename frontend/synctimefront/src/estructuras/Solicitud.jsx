import { Link } from 'react-router-dom';
import { Send } from 'lucide-react';
import { Boton } from '../componentes/ui';
import { useSesion } from '../sesion/sesion';
import { destinoSolicitud } from './navegacion';

/** «Solicitar cotización»: lleva al formulario del cliente (o, al personal, al registro presencial). */
export function BotonSolicitar({ trabajo, children = 'Solicitar cotización', iconoInicio = Send, ...resto }) {
  const { esPersonal } = useSesion();
  return (
    <Boton como={Link} to={destinoSolicitud({ esPersonal, trabajo })} iconoInicio={iconoInicio} {...resto}>
      {children}
    </Boton>
  );
}
