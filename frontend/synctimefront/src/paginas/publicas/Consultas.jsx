import { Mail, Phone } from 'lucide-react';
import { Boton, Enlace, IconoWhatsapp, Tarjeta } from '../../componentes/ui';
import { enlaceTelefono, enlaceWhatsapp, TALLER } from '../../configuracion/taller';
import { FormularioContacto } from './contacto/FormularioContacto';
import estilos from './Consultas.module.css';

/**
 * /consultas (fase 12C): el formulario «Escríbenos» sale de la portada y tiene su propia pantalla. El
 * formulario, sus validaciones, el consentimiento, el anti-spam, el correo y la API no cambian. Junto a
 * él quedan los canales directos para quien prefiere llamar o escribir por WhatsApp.
 */
export default function Consultas() {
  const whatsapp = enlaceWhatsapp();
  const telefono = enlaceTelefono();
  return (
    <div className={estilos.pagina}>
      <div className={estilos.contenedor}>
        <div className={estilos.encabezado}>
          <h1 id="titulo-consultas" tabIndex={-1} className={estilos.titulo}>Escríbenos</h1>
          <p className={estilos.bajada}>
            ¿Tienes una pregunta antes de pedir? Déjanos tu consulta y te respondemos a tu correo.
            Para cotizar un trabajo, usa «Solicitar cotización».
          </p>
        </div>
        <Tarjeta como="section" aria-labelledby="titulo-consultas">
          <FormularioContacto />
        </Tarjeta>
        <aside className={estilos.directo} aria-label="Contacto directo">
          <p className={estilos.directoTitulo}>¿Prefieres hablar con alguien?</p>
          <div className={estilos.canales}>
            {whatsapp && (
              <Boton como="a" href={whatsapp} target="_blank" rel="noopener noreferrer" variante="secundario" iconoInicio={IconoWhatsapp}>
                Escribir por WhatsApp<span className="solo-lectores"> (se abre en otra pestaña)</span>
              </Boton>
            )}
          </div>
          <ul className={estilos.lista}>
            {telefono && <li><Phone size={16} strokeWidth={1.75} aria-hidden="true" /><Enlace href={telefono}>{TALLER.telefono}</Enlace></li>}
            {TALLER.correo && <li><Mail size={16} strokeWidth={1.75} aria-hidden="true" /><Enlace href={`mailto:${TALLER.correo}`}>{TALLER.correo}</Enlace></li>}
          </ul>
        </aside>
      </div>
    </div>
  );
}
