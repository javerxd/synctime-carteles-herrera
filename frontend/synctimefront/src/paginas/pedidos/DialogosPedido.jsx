import { useRef, useState } from 'react';
import { Ban, HandCoins, Truck, Undo2 } from 'lucide-react';
import { Aviso, Boton, Campo, Dialogo } from '../../componentes/ui';
import { formatearFecha, formatearPesos } from '../../utilidades/formato';
import { useFormulario } from '../cuenta/useFormulario';
import { AvisoFalla } from '../compartidos/Dialogos';
import { AvisoTrasCorte } from '../compartidos/AvisoTrasCorte';
import { bloqueaReintento, errorTrasCorte } from '../compartidos/trasCorte';
import { useEnvioDialogo } from '../compartidos/useEnvioDialogo';
import { fallaDeClave, useClaveIdempotencia } from '../compartidos/useClaveIdempotencia';
import { CamposAbono } from './CamposPedido';
import {
  ABONO_VACIO, abonoParaApi, errorFechaEntrega, errorMotivo, LIMITES_PEDIDO, MEDIOS_PAGO, numeroPedido, ORDEN_ABONO, validarAbono,
} from './reglasPedido';
import estilosPanel from '../panel/Panel.module.css';

/** Motivo obligatorio con su largo, para los diálogos de anular. */
function useMotivo(abierto, limites) {
  const [motivo, setMotivo] = useState('');
  const [intento, setIntento] = useState(false);
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    if (abierto) {
      setMotivo('');
      setIntento(false);
    }
  }
  const error = errorMotivo(motivo, limites);
  return { motivo, setMotivo, intento, setIntento, error, visible: intento ? error ?? undefined : undefined };
}

/**
 * Registrar un abono (E25). El tope es el saldo pendiente: un abono mayor se explica antes de enviarlo y, si
 * otro abono llegó antes, la API responde 409 abono_excede_saldo con el saldo vigente, que se muestra en el
 * monto sin perder lo escrito; una factura distinta de la del pedido, en el número de factura.
 * - Sin saldo (el pedido se pagó o se anuló mientras tanto) no se puede enviar.
 * - Si la respuesta se pierde, `alRegistrar` ya revisó la ficha: si no se pudo confirmar, el envío queda
 *   bloqueado hasta «Revisar de nuevo» (`alRevisar(abono)` → 'registrado' | 'no_registrado' | 'sin_confirmar').
 */
export function DialogoAbono({ pedido, hoy, abierto, alCerrar, alRegistrar, alRevisar }) {
  const saldo = Number(pedido?.saldoPendiente ?? 0);
  const sinSaldo = pedido?.estadoPedido === 'anulado' || saldo <= 0;
  const form = useFormulario({ inicial: ABONO_VACIO, validar: (a) => validarAbono(a, { hoy, saldo }), orden: ORDEN_ABONO });
  const { refFormulario, reiniciar } = form;
  const envio = useEnvioDialogo(abierto);
  // Fase 11: una clave por cada vez que se abre el diálogo; la misma en cada reintento mientras sigue abierto.
  const idempotencia = useClaveIdempotencia(abierto);
  const refMonto = useRef(null);
  const ultimo = useRef(null);
  const [revisando, setRevisando] = useState(false);
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    if (abierto) reiniciar(ABONO_VACIO);
  }
  const bloqueado = sinSaldo || bloqueaReintento(envio.falla) || revisando;

  async function enviar(evento) {
    evento.preventDefault();
    if (envio.enviando || bloqueado || !form.comprobar()) return;
    ultimo.current = abonoParaApi(form.datos);
    const falla = await envio.ejecutar(async () => {
      try {
        await alRegistrar(ultimo.current, idempotencia.clave);
      } catch (error) {
        throw fallaDeClave(error, idempotencia.renovar);
      }
    });
    // El error se explica junto a su campo cuando corresponde (el saldo vigente, en el monto; la factura, en
    // la suya); si no, arriba.
    let enCampo = false;
    if (falla?.codigo === 'abono_excede_saldo') enCampo = form.aplicarErroresApi({ errores: { monto: [falla.message] } });
    else if (falla?.codigo === 'factura_distinta' || (falla?.estado === 409 && /factura/i.test(falla.message ?? ''))) {
      enCampo = form.aplicarErroresApi({ errores: { numeroFactura: [falla.message] } });
    } else if (falla?.estado === 400) enCampo = form.aplicarErroresApi(falla);
    if (enCampo) envio.olvidarFalla();
  }

  async function revisar() {
    setRevisando(true);
    await envio.ejecutar(async () => {
      const resultado = await alRevisar(ultimo.current);
      if (resultado !== 'registrado') throw errorTrasCorte(resultado);
    });
    setRevisando(false);
  }

  return (
    <Dialogo abierto={abierto} alCerrar={() => !envio.enviando && alCerrar()} titulo={`Registrar abono del pedido ${numeroPedido(pedido)}`}
      descripcion={`Saldo pendiente: ${formatearPesos(saldo)}. Queda registrado con tu nombre, la fecha y el medio de pago.`}
      enfoqueInicial={refMonto} ancho="36rem" cerrarAlPulsarFondo={false}
      pie={(
        <>
          <Boton variante="secundario" onClick={alCerrar} disabled={envio.enviando}>{sinSaldo ? 'Cerrar' : 'Cancelar'}</Boton>
          <Boton type="submit" form="formulario-abono" iconoInicio={HandCoins} cargando={envio.enviando && !revisando} textoCargando="Registrando…"
            aria-disabled={bloqueado ? 'true' : undefined}>
            Registrar abono
          </Boton>
        </>
      )}>
      <form id="formulario-abono" ref={refFormulario} className={estilosPanel.formulario} onSubmit={enviar} noValidate>
        {sinSaldo && (
          <Aviso tono="info" titulo={pedido?.estadoPedido === 'anulado' ? 'El pedido está anulado: no recibe abonos.' : 'El pedido ya no tiene saldo pendiente.'} />
        )}
        <AvisoFalla falla={envio.falla} alVerActual={alCerrar} entidad="este pedido" />
        <AvisoTrasCorte falla={envio.falla} alRevisar={revisar} revisando={revisando} />
        <CamposAbono form={form} hoy={hoy} deshabilitado={envio.enviando || sinSaldo} conFactura prefijo="dialogo-" refMonto={refMonto} />
      </form>
    </Dialogo>
  );
}

/** Anular un abono mal ingresado (decisión 2): con motivo; queda tachado en el historial y deja de sumar. */
export function DialogoAnularAbono({ pago, abierto, alCerrar, alAnular }) {
  const m = useMotivo(abierto, { maximo: LIMITES_PEDIDO.motivoAbono, que: 'por qué se anula' });
  const envio = useEnvioDialogo(abierto);
  const refMotivo = useRef(null);

  async function enviar(evento) {
    evento.preventDefault();
    m.setIntento(true);
    if (m.error) {
      refMotivo.current?.focus();
      return;
    }
    if (await envio.ejecutar(() => alAnular(m.motivo.trim()))) refMotivo.current?.focus();
  }

  const descripcion = pago
    ? `${formatearPesos(pago.monto)} del ${formatearFecha(pago.fechaPago)} (${MEDIOS_PAGO[pago.medioPago] ?? pago.medioPago}). No se borra: queda tachado y deja de sumar. Después puedes registrar el correcto.`
    : undefined;
  return (
    <Dialogo abierto={abierto} alCerrar={() => !envio.enviando && alCerrar()} titulo="Anular abono" descripcion={descripcion}
      enfoqueInicial={refMotivo} ancho="32rem"
      pie={(
        <>
          <Boton variante="secundario" onClick={alCerrar} disabled={envio.enviando}>Cancelar</Boton>
          <Boton type="submit" form="formulario-anular-abono" variante="peligro" iconoInicio={Undo2} cargando={envio.enviando} textoCargando="Anulando…">Anular abono</Boton>
        </>
      )}>
      <form id="formulario-anular-abono" className={estilosPanel.formulario} onSubmit={enviar} noValidate>
        <AvisoFalla falla={envio.falla} entidad="este pedido" />
        <Campo ref={refMotivo} etiqueta="Motivo" como="textarea" requerido rows={3} name="motivo" maxLength={LIMITES_PEDIDO.motivoAbono}
          ayuda="Entre 10 y 300 caracteres. Por ejemplo: «Se ingresó dos veces»." value={m.motivo}
          onChange={(e) => m.setMotivo(e.target.value)} error={m.visible} disabled={envio.enviando} />
      </form>
    </Dialogo>
  );
}

/** Anular el pedido: motivo obligatorio. Con abonos vigentes, solo gestión, y lo abonado queda a favor del cliente. */
export function DialogoAnularPedido({ pedido, abierto, alCerrar, alAnular }) {
  const m = useMotivo(abierto, { maximo: LIMITES_PEDIDO.motivoAnulacion, que: 'el motivo de la anulación' });
  const envio = useEnvioDialogo(abierto);
  const refMotivo = useRef(null);
  const abonado = Number(pedido?.montoAbonado ?? 0);

  async function enviar(evento) {
    evento.preventDefault();
    m.setIntento(true);
    if (m.error) {
      refMotivo.current?.focus();
      return;
    }
    if (await envio.ejecutar(() => alAnular(m.motivo.trim()))) refMotivo.current?.focus();
  }

  return (
    <Dialogo abierto={abierto} alCerrar={() => !envio.enviando && alCerrar()} titulo={`Anular el pedido ${numeroPedido(pedido)}`}
      descripcion={abonado > 0
        ? `Tiene abonos por ${formatearPesos(abonado)}: quedarán como «a devolver o a favor del cliente». Un pedido anulado no cambia más.`
        : 'Un pedido anulado no cambia más ni recibe abonos. El motivo queda en el historial.'}
      enfoqueInicial={refMotivo} ancho="32rem"
      pie={(
        <>
          <Boton variante="secundario" onClick={alCerrar} disabled={envio.enviando}>Cancelar</Boton>
          <Boton type="submit" form="formulario-anular-pedido" variante="peligro" iconoInicio={Ban} cargando={envio.enviando} textoCargando="Anulando…">Anular pedido</Boton>
        </>
      )}>
      <form id="formulario-anular-pedido" className={estilosPanel.formulario} onSubmit={enviar} noValidate>
        <AvisoFalla falla={envio.falla} alVerActual={alCerrar} entidad="este pedido" />
        <Campo ref={refMotivo} etiqueta="Motivo de la anulación" como="textarea" requerido rows={3} name="motivo" maxLength={LIMITES_PEDIDO.motivoAnulacion}
          ayuda="Entre 10 y 500 caracteres. Por ejemplo: «El cliente desistió del trabajo»." value={m.motivo}
          onChange={(e) => m.setMotivo(e.target.value)} error={m.visible} disabled={envio.enviando} />
      </form>
    </Dialogo>
  );
}

/** Registrar la entrega: la fecha real (hoy, por defecto; no futura ni anterior al pedido) y una nota opcional. */
export function DialogoEntrega({ pedido, hoy, abierto, alCerrar, alEntregar }) {
  const [fecha, setFecha] = useState(hoy);
  const [nota, setNota] = useState('');
  const [intento, setIntento] = useState(false);
  const envio = useEnvioDialogo(abierto);
  const refFecha = useRef(null);
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    if (abierto) {
      setFecha(hoy);
      setNota('');
      setIntento(false);
    }
  }
  const fechaPedido = String(pedido?.fechaPedido ?? '').slice(0, 10);
  const errorFecha = errorFechaEntrega(fecha, { hoy, fechaPedido });
  const errorNota = nota.trim().length > LIMITES_PEDIDO.nota ? 'Usa hasta 500 caracteres.' : null;

  async function enviar(evento) {
    evento.preventDefault();
    setIntento(true);
    if (errorFecha || errorNota) {
      evento.currentTarget.querySelector(errorFecha ? 'input' : 'textarea')?.focus();
      return;
    }
    await envio.ejecutar(() => alEntregar({ fechaEntregaReal: fecha, ...(nota.trim() ? { nota: nota.trim() } : {}) }));
  }

  const saldo = Number(pedido?.saldoPendiente ?? 0);
  return (
    <Dialogo abierto={abierto} alCerrar={() => !envio.enviando && alCerrar()} titulo={`Registrar la entrega del pedido ${numeroPedido(pedido)}`}
      descripcion={saldo > 0
        ? `Se puede entregar sin pagar: el saldo de ${formatearPesos(saldo)} sigue por cobrar y el ciclo se cierra al completar el pago.`
        : 'Está pagado: al entregarlo, el ciclo del pedido queda cerrado.'}
      enfoqueInicial={refFecha} ancho="32rem"
      pie={(
        <>
          <Boton variante="secundario" onClick={alCerrar} disabled={envio.enviando}>Cancelar</Boton>
          <Boton type="submit" form="formulario-entrega" iconoInicio={Truck} cargando={envio.enviando} textoCargando="Registrando…">Registrar entrega</Boton>
        </>
      )}>
      <form id="formulario-entrega" className={estilosPanel.formulario} onSubmit={enviar} noValidate>
        <AvisoFalla falla={envio.falla} alVerActual={alCerrar} entidad="este pedido" />
        <Campo ref={refFecha} etiqueta="Fecha de entrega" type="date" requerido name="fechaEntregaReal" value={fecha} max={hoy} min={fechaPedido || undefined}
          onChange={(e) => setFecha(e.target.value)} error={intento ? errorFecha ?? undefined : undefined} disabled={envio.enviando} />
        <Campo etiqueta="Nota" como="textarea" rows={2} name="nota" maxLength={LIMITES_PEDIDO.nota} ayuda="Opcional. Por ejemplo, quién lo recibió."
          value={nota} onChange={(e) => setNota(e.target.value)} error={intento ? errorNota ?? undefined : undefined} disabled={envio.enviando} />
      </form>
    </Dialogo>
  );
}
