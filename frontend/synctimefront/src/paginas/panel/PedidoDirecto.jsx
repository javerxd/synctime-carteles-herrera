import { useState } from 'react';
import { Save } from 'lucide-react';
import { Aviso, Boton, Campo, Casilla, Dialogo } from '../../componentes/ui';
import { publico } from '../../api/publico';
import { useConsulta } from '../../utilidades/useConsulta';
import { formatearPesos } from '../../utilidades/formato';
import { validarRut } from '../../utilidades/rut';
import estilos from './Panel.module.css';

const VACIO = {
  cliente: '', idProducto: '', ancho: '', alto: '', cantidad: '1', glosa: '', materiales: '', manoObra: '',
  instalacion: '', requiereInstalacion: false, fechaEntrega: '', abono: '', medioPago: 'efectivo',
};

const entero = (valor) => (valor === '' ? 0 : Number(valor));
const esMonto = (valor) => valor === '' || (Number.isInteger(Number(valor)) && Number(valor) >= 0);

function validar(d) {
  const e = {};
  const busqueda = d.cliente.trim();
  if (!busqueda) e.cliente = 'Escribe el RUT o el teléfono del cliente.';
  else if (/[kK\d]$/.test(busqueda) && busqueda.includes('-') && !validarRut(busqueda)) e.cliente = 'El RUT no es válido: revisa el dígito verificador.';
  if (!d.idProducto) e.idProducto = 'Elige el tipo de letrero.';
  if (!Number.isInteger(Number(d.cantidad)) || Number(d.cantidad) < 1) e.cantidad = 'La cantidad debe ser 1 o más.';
  if (d.glosa.trim().length < 5) e.glosa = 'Describe el trabajo en al menos 5 letras.';
  for (const campo of ['materiales', 'manoObra', 'instalacion', 'abono']) {
    if (!esMonto(d[campo])) e[campo] = 'Escribe un monto en pesos, sin puntos ni decimales.';
  }
  if (!d.fechaEntrega) e.fechaEntrega = 'Indica la fecha estimada de entrega.';
  const total = entero(d.materiales) + entero(d.manoObra) + entero(d.instalacion);
  if (!e.abono && entero(d.abono) > total) e.abono = `El abono no puede superar el monto final (${formatearPesos(total)}).`;
  return e;
}

/**
 * Pedido presencial directo (RFU35, E26): el cliente acuerda el trabajo en el mostrador. En esta fase
 * el formulario se completa y valida; la fase 8 lo guarda creando, en una sola transacción, la
 * solicitud presencial, la cotización aprobada, el pedido y el abono.
 */
export function PedidoDirecto({ abierto, alCerrar }) {
  return (
    <Dialogo abierto={abierto} alCerrar={alCerrar} titulo="Pedido presencial directo"
      descripcion="Para un trabajo acordado en el taller, sin solicitud previa." ancho="40rem" cerrarAlPulsarFondo={false}>
      {abierto && <Formulario alCerrar={alCerrar} />}
    </Dialogo>
  );
}

function Formulario({ alCerrar }) {
  const productos = useConsulta('productos', publico.productos);
  const [d, setD] = useState(VACIO);
  const [errores, setErrores] = useState({});
  const [listo, setListo] = useState(false);
  const cambiar = (campo) => (ev) => setD((x) => ({ ...x, [campo]: ev.target.type === 'checkbox' ? ev.target.checked : ev.target.value }));
  const total = entero(d.materiales) + entero(d.manoObra) + entero(d.instalacion);

  function alEnviar(evento) {
    evento.preventDefault();
    const encontrados = validar(d);
    setErrores(encontrados);
    const primero = Object.keys(encontrados)[0];
    if (primero) {
      evento.currentTarget.querySelector(`[name="${primero}"]`)?.focus();
      return;
    }
    setListo(true);
  }

  if (listo) {
    return (
      <div className={estilos.ficha}>
        <Aviso tono="info" titulo="Pedido validado (demostración)">
          Monto final {formatearPesos(total)}, abono {formatearPesos(entero(d.abono))}, saldo {formatearPesos(total - entero(d.abono))}.
          El guardado real llega en la fase 8.
        </Aviso>
        <div className={estilos.pieFormulario}>
          <Boton variante="secundario" onClick={() => setListo(false)}>Volver al formulario</Boton>
          <Boton onClick={alCerrar}>Cerrar</Boton>
        </div>
      </div>
    );
  }

  return (
    <form className={estilos.formulario} onSubmit={alEnviar} noValidate>
      <Campo name="cliente" etiqueta="Cliente" requerido ayuda="Busca por RUT o teléfono; si no existe, se crea al guardar."
        value={d.cliente} onChange={cambiar('cliente')} error={errores.cliente} />
      <fieldset className={estilos.grupoFormulario}>
        <legend>El trabajo</legend>
        <Campo name="idProducto" etiqueta="Tipo de letrero" como="select" requerido value={d.idProducto}
          onChange={cambiar('idProducto')} error={errores.idProducto} disabled={productos.cargando}>
          <option value="">{productos.cargando ? 'Cargando…' : 'Elige uno'}</option>
          {(productos.datos ?? []).map((p) => <option key={p.idProducto} value={p.idProducto}>{p.nombre}</option>)}
        </Campo>
        <div className={estilos.tresColumnas}>
          <Campo name="ancho" etiqueta="Ancho (cm)" inputMode="decimal" value={d.ancho} onChange={cambiar('ancho')} />
          <Campo name="alto" etiqueta="Alto (cm)" inputMode="decimal" value={d.alto} onChange={cambiar('alto')} />
          <Campo name="cantidad" etiqueta="Cantidad" inputMode="numeric" requerido value={d.cantidad} onChange={cambiar('cantidad')} error={errores.cantidad} />
        </div>
        <Campo name="glosa" etiqueta="Glosa de especificaciones" como="textarea" requerido value={d.glosa} onChange={cambiar('glosa')} error={errores.glosa} />
      </fieldset>
      <fieldset className={estilos.grupoFormulario}>
        <legend>Costos y entrega</legend>
        <div className={estilos.tresColumnas}>
          <Campo name="materiales" etiqueta="Materiales ($)" inputMode="numeric" value={d.materiales} onChange={cambiar('materiales')} error={errores.materiales} />
          <Campo name="manoObra" etiqueta="Mano de obra ($)" inputMode="numeric" value={d.manoObra} onChange={cambiar('manoObra')} error={errores.manoObra} />
          <Campo name="instalacion" etiqueta="Instalación ($)" inputMode="numeric" value={d.instalacion} onChange={cambiar('instalacion')} error={errores.instalacion} />
        </div>
        <Casilla name="requiereInstalacion" etiqueta="Requiere instalación en terreno" checked={d.requiereInstalacion} onChange={cambiar('requiereInstalacion')} />
        <Campo name="fechaEntrega" etiqueta="Entrega estimada" type="date" requerido value={d.fechaEntrega} onChange={cambiar('fechaEntrega')} error={errores.fechaEntrega} />
        <p className={estilos.totalFormulario}>Monto final: <strong className="cifras">{formatearPesos(total)}</strong></p>
      </fieldset>
      <fieldset className={estilos.grupoFormulario}>
        <legend>Abono (opcional)</legend>
        <div className={estilos.dosColumnas}>
          <Campo name="abono" etiqueta="Monto abonado ($)" inputMode="numeric" value={d.abono} onChange={cambiar('abono')} error={errores.abono} />
          <Campo name="medioPago" etiqueta="Medio de pago" como="select" value={d.medioPago} onChange={cambiar('medioPago')}>
            <option value="efectivo">Efectivo</option>
            <option value="transferencia">Transferencia</option>
            <option value="tarjeta_debito">Tarjeta de débito</option>
            <option value="tarjeta_credito">Tarjeta de crédito</option>
            <option value="cheque">Cheque</option>
            <option value="otro">Otro</option>
          </Campo>
        </div>
      </fieldset>
      <div className={estilos.pieFormulario}>
        <Boton variante="secundario" onClick={alCerrar}>Cancelar</Boton>
        <Boton type="submit" iconoInicio={Save}>Validar pedido</Boton>
      </div>
    </form>
  );
}
