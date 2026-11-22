import { forwardRef } from 'react';
import { Clock, MapPin, Phone } from 'lucide-react';
import { Aviso, Boton, Esqueleto, EstadoCartel, IconoWhatsapp } from '../../componentes/ui';
import { publico } from '../../api/publico';
import { enlaceTelefono, enlaceWhatsapp, TALLER } from '../../configuracion/taller';
import { useConsulta } from '../../utilidades/useConsulta';
import { franja, textoEstado } from '../publicas/horario';
import { MENSAJE_CONTACTO_DIRECTO } from './reglasSolicitud';
import estilos from './Solicitudes.module.css';

/**
 * Aviso de contacto directo (RFS04, S25): la regla contra solicitudes falsas frenó el envío. Es el mismo
 * para todos los motivos y no revela datos de nadie. Suma el estado del local ahora, el horario de hoy,
 * la dirección, el teléfono y el WhatsApp. Lo escrito en el formulario se conserva.
 */
export const ContactoDirecto = forwardRef(function ContactoDirecto(_props, ref) {
  const estado = useConsulta('estado-local', publico.estadoLocal);
  const whatsapp = enlaceWhatsapp('Hola, tengo una solicitud en revisión y quiero agregar algo o pedir otra cosa.');
  const telefono = enlaceTelefono();
  return (
    <div ref={ref} tabIndex={-1} className={estilos.foco}>
      <Aviso tono="aviso" titulo="Para esta solicitud es preferible contactarse directamente con el local">
        <p>{MENSAJE_CONTACTO_DIRECTO}</p>
        <div className={estilos.contacto}>
          {estado.cargando && <Esqueleto forma="bloque" alto="1.75rem" ancho="12rem" />}
          {estado.datos && (
            <p className={estilos.estadoLocal}>
              <EstadoCartel abierto={estado.datos.abiertoAhora} />
              <span>{textoEstado(estado.datos)}</span>
            </p>
          )}
          <ul className={estilos.datosContacto}>
            {estado.datos && (
              <li><Clock size={16} strokeWidth={1.75} aria-hidden="true" />Hoy: {franja(estado.datos.hoy)} (hora de Chile)</li>
            )}
            <li><MapPin size={16} strokeWidth={1.75} aria-hidden="true" />{TALLER.direccion}, {TALLER.ciudad}</li>
          </ul>
          <div className={estilos.accionesContacto}>
            {whatsapp && (
              <Boton como="a" href={whatsapp} target="_blank" rel="noopener noreferrer" variante="secundario" tamano="compacto" iconoInicio={IconoWhatsapp}>
                Escribir por WhatsApp<span className="solo-lectores"> (se abre en otra pestaña)</span>
              </Boton>
            )}
            {telefono && (
              <Boton como="a" href={telefono} variante="secundario" tamano="compacto" iconoInicio={Phone}>
                Llamar al {TALLER.telefono}
              </Boton>
            )}
          </div>
        </div>
      </Aviso>
    </div>
  );
});
