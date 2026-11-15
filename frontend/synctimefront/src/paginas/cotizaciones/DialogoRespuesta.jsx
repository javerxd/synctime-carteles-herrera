import { useRef, useState } from 'react';
import { FileCheck, FileX, MessageCircle, Phone, Store } from 'lucide-react';
import { Boton, Campo, Dialogo, Opciones } from '../../componentes/ui';
import { AvisoFalla } from '../compartidos/Dialogos';
import { useEnvioDialogo } from '../compartidos/useEnvioDialogo';
import { LIMITES_COTIZACION, numeroCotizacion } from './reglasCotizacion';
import estilosPanel from '../panel/Panel.module.css';

const DECISIONES = [
  { valor: 'aprobada', etiqueta: 'La aprobó', descripcion: 'La solicitud pasa a «Aceptada» y se puede crear el pedido.', icono: FileCheck },
  { valor: 'rechazada', etiqueta: 'La rechazó', descripcion: 'La solicitud vuelve a «Solicitada» para cotizar de nuevo.', icono: FileX },
];
const VIAS = [
  { valor: 'telefono', etiqueta: 'Teléfono', icono: Phone },
  { valor: 'whatsapp', etiqueta: 'WhatsApp', icono: MessageCircle },
  { valor: 'presencial', etiqueta: 'En el local', icono: Store },
];

/**
 * Registrar la respuesta que el cliente dio por teléfono, WhatsApp o en el local (RFU08, E07): decisión, vía y
 * un motivo opcional si la rechazó. Queda registrado quién la marcó y por qué vía. `alResponder` lanza el error
 * de la API para explicarlo aquí (un 409 recarga la cotización detrás del diálogo).
 */
export function DialogoRespuesta({ cotizacion, abierto, alCerrar, alResponder }) {
  const [decision, setDecision] = useState('');
  const [via, setVia] = useState('');
  const [motivo, setMotivo] = useState('');
  const [intento, setIntento] = useState(false);
  const envio = useEnvioDialogo(abierto);
  const refPrimera = useRef(null);
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    if (abierto) {
      setDecision('');
      setVia('');
      setMotivo('');
      setIntento(false);
    }
  }

  const errores = {
    decision: decision ? null : 'Elige si el cliente la aprobó o la rechazó.',
    via: via ? null : 'Elige por dónde respondió el cliente.',
    motivo: motivo.trim().length > LIMITES_COTIZACION.motivo ? 'Usa hasta 500 caracteres.' : null,
  };
  const primero = Object.entries(errores).find(([, e]) => e)?.[0];

  async function enviar(evento) {
    evento.preventDefault();
    setIntento(true);
    if (primero) {
      evento.currentTarget.querySelector(primero === 'motivo' ? 'textarea' : `input[name="${primero}"]`)?.focus();
      return;
    }
    await envio.ejecutar(() => alResponder({
      decision, via, ...(decision === 'rechazada' && motivo.trim() ? { motivo: motivo.trim() } : {}),
    }));
  }

  return (
    <Dialogo
      abierto={abierto}
      alCerrar={() => !envio.enviando && alCerrar()}
      titulo={`Respuesta del cliente a la cotización ${numeroCotizacion(cotizacion)}`}
      descripcion="Para cuando el cliente responde fuera de la web. Queda registrado con tu nombre y la vía."
      enfoqueInicial={refPrimera}
      ancho="34rem"
      pie={(
        <>
          <Boton variante="secundario" onClick={alCerrar} disabled={envio.enviando}>Cancelar</Boton>
          <Boton type="submit" form="formulario-respuesta" cargando={envio.enviando} textoCargando="Registrando…">Registrar respuesta</Boton>
        </>
      )}
    >
      <form id="formulario-respuesta" className={estilosPanel.formulario} onSubmit={enviar} noValidate>
        <AvisoFalla falla={envio.falla} alVerActual={alCerrar} entidad="esta cotización" />
        <Opciones leyenda="¿Qué respondió?" nombre="decision" opciones={DECISIONES} valor={decision} requerido refPrimera={refPrimera}
          alCambiar={setDecision} error={intento ? errores.decision ?? undefined : undefined} deshabilitado={envio.enviando} />
        <Opciones leyenda="¿Por dónde?" nombre="via" opciones={VIAS} valor={via} requerido enLinea
          alCambiar={setVia} error={intento ? errores.via ?? undefined : undefined} deshabilitado={envio.enviando} />
        {decision === 'rechazada' && (
          <Campo etiqueta="Motivo del rechazo" como="textarea" rows={3} name="motivo" maxLength={LIMITES_COTIZACION.motivo}
            ayuda="Opcional. Por ejemplo: «Le pareció caro» o «Lo hará más adelante». Queda en el historial de la solicitud."
            value={motivo} onChange={(e) => setMotivo(e.target.value)} error={intento ? errores.motivo ?? undefined : undefined}
            disabled={envio.enviando} />
        )}
      </form>
    </Dialogo>
  );
}
