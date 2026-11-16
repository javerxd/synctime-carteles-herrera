import { useEffect, useRef } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { CircleCheck } from 'lucide-react';
import { Boton, Tarjeta } from '../../componentes/ui';
import { formatearFechaHora } from '../../utilidades/formato';
import estilos from './Cuenta.module.css';

/**
 * Comprobante de la baja de la cuenta (fase 11), arriba de la portada: Mis datos lleva aquí al terminar, con el
 * comprobante en el estado de la navegación (no en la dirección). Recibe el foco para que se lea primero.
 */
export function ComprobanteBaja() {
  const { state, pathname } = useLocation();
  const navegar = useNavigate();
  const comprobante = state?.bajaCuenta ?? null;
  const ref = useRef(null);

  useEffect(() => {
    if (comprobante) ref.current?.focus();
  }, [comprobante]);

  if (!comprobante) return null;
  return (
    <Tarjeta como="section" aria-labelledby="titulo-comprobante-baja" className={estilos.comprobanteBaja}>
      <div className={estilos.seccion}>
        <CircleCheck size={32} strokeWidth={1.5} aria-hidden="true" color="var(--tono-exito-solido)" />
        {/* El foco va al título: el lector sigue con el número y la fecha del comprobante. */}
        <h2 ref={ref} tabIndex={-1} id="titulo-comprobante-baja" className={`${estilos.subtitulo} ${estilos.tituloComprobante}`}>Tu cuenta quedó dada de baja</h2>
        <p className={estilos.explicacion}>
          Cerramos tu sesión. Conservamos tus solicitudes, cotizaciones, pedidos y abonos; ya no recibirás correos opcionales.
        </p>
        <dl className={estilos.datosComprobante}>
          {comprobante.numero && <div><dt>Comprobante</dt><dd className="cifras">{comprobante.numero}</dd></div>}
          {comprobante.fecha && <div><dt>Fecha</dt><dd className="cifras">{formatearFechaHora(comprobante.fecha)}</dd></div>}
        </dl>
        <p className={estilos.explicacion}>Guarda este número. Si tenías el correo confirmado, también te lo enviamos por correo.</p>
        <div className={estilos.pieSeccion}>
          <Boton variante="secundario" onClick={() => {
            // El comprobante desaparece: el foco pasa al título de la página para no perderse.
            const titulo = document.querySelector('h1');
            if (titulo && !titulo.hasAttribute('tabindex')) titulo.setAttribute('tabindex', '-1');
            titulo?.focus();
            navegar(pathname, { replace: true, state: null });
          }}>
            Entendido
          </Boton>
        </div>
      </div>
    </Tarjeta>
  );
}
