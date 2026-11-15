import { Store, Truck, Wrench } from 'lucide-react';
import { Campo, Casilla, Opciones } from '../../componentes/ui';
import { sumarAnios, sumarDias } from '../../utilidades/fechas';
import { LIMITES_PEDIDO, MEDIOS_PAGO, MODALIDADES } from './reglasPedido';
import estilosPanel from '../panel/Panel.module.css';

const ICONOS_MODALIDAD = { retiro_local: Store, entrega_domicilio: Truck, instalacion_terreno: Wrench };
const OPCIONES_MODALIDAD = Object.entries(MODALIDADES).map(([valor, etiqueta]) => ({ valor, etiqueta, icono: ICONOS_MODALIDAD[valor] }));

const nombreEmpleado = (e) => [e.nombre, e.apellido].filter(Boolean).join(' ');

/**
 * Los datos del pedido (E08): responsable, fecha estimada, modalidad, instalación, dirección, costo de
 * instalación y factura. `form` es un useFormulario. Con `soloFactura` (pedido entregado) solo se edita la
 * factura; con `conMontoBase` (edición) también el monto del trabajo. La dirección aparece si no es retiro, y
 * el costo de instalación si se marca que la requiere. Con `montosSoloLectura` (quien no es de gestión, al
 * editar) los montos se ven pero no se cambian.
 */
export function CamposPedido({
  form, empleados, hoy, deshabilitado = false, soloFactura = false, conMontoBase = false, montosSoloLectura = false, fechaVigente = null,
}) {
  const { datos, cambiar, tocar, visibles, propiedades } = form;
  const campo = (nombre) => ({ ...propiedades(nombre), disabled: deshabilitado });
  const ayudaMonto = (ayuda) => (montosSoloLectura ? 'Solo el dueño o el administrador cambian los montos.' : ayuda);
  const monto = (nombre) => ({ ...campo(nombre), readOnly: montosSoloLectura || undefined });
  if (soloFactura) {
    return (
      <Campo etiqueta="Número de factura" autoComplete="off" maxLength={LIMITES_PEDIDO.factura}
        ayuda="Un pedido entregado solo cambia su factura." {...campo('numeroFactura')} />
    );
  }
  const lista = (empleados?.datos ?? []).filter((e) => e.activo !== false || String(e.idEmpleado) === datos.idEmpleadoResponsable);
  const minimo = fechaVigente && fechaVigente < hoy ? fechaVigente : hoy;

  function elegirModalidad(valor) {
    cambiar('modalidadEntrega', valor);
    tocar('modalidadEntrega');
    // La instalación en terreno siempre la requiere: se marca sola.
    if (valor === 'instalacion_terreno') cambiar('requiereInstalacion', true);
  }

  return (
    <div className={estilosPanel.formulario}>
      <div className={estilosPanel.dosColumnas}>
        <Campo etiqueta="Responsable" como="select" requerido {...campo('idEmpleadoResponsable')} disabled={deshabilitado || empleados?.cargando}>
          <option value="">{empleados?.cargando ? 'Cargando…' : 'Elige a alguien'}</option>
          {lista.map((e) => <option key={e.idEmpleado} value={e.idEmpleado}>{nombreEmpleado(e)}{e.rol ? ` (${e.rol})` : ''}</option>)}
        </Campo>
        <Campo etiqueta="Entrega estimada" type="date" requerido min={minimo} max={sumarAnios(hoy, 2) ?? undefined}
          ayuda="Desde hoy y hasta 2 años." {...campo('fechaEntregaEstimada')} />
      </div>
      {empleados?.error && <p className={estilosPanel.textoSeccion} role="alert">No pudimos cargar el personal: {empleados.error.message}</p>}
      <Opciones leyenda="Cómo se entrega" nombre="modalidadEntrega" opciones={OPCIONES_MODALIDAD} valor={datos.modalidadEntrega} requerido enLinea
        alCambiar={elegirModalidad} error={visibles.modalidadEntrega} deshabilitado={deshabilitado} />
      <Casilla name="requiereInstalacion" etiqueta="Requiere instalación" checked={datos.requiereInstalacion} disabled={deshabilitado}
        descripcion={datos.modalidadEntrega === 'instalacion_terreno' ? 'La instalación en terreno siempre la requiere.' : 'Por ejemplo, si se despacha y además se instala.'}
        error={visibles.requiereInstalacion}
        onChange={(e) => { cambiar('requiereInstalacion', e.target.checked); tocar('requiereInstalacion'); }} />
      {datos.modalidadEntrega && datos.modalidadEntrega !== 'retiro_local' && (
        <Campo etiqueta="Dirección de entrega o instalación" requerido autoComplete="off" maxLength={LIMITES_PEDIDO.direccion} {...campo('direccionInstalacion')} />
      )}
      <div className={estilosPanel.dosColumnas}>
        {datos.requiereInstalacion && (
          <Campo etiqueta="Costo de instalación ($)" inputMode="numeric" autoComplete="off" placeholder="0"
            ayuda={ayudaMonto('Se suma al monto del trabajo.')} {...monto('costoInstalacionAdicional')} />
        )}
        {conMontoBase && (
          <Campo etiqueta="Monto del trabajo ($)" inputMode="numeric" autoComplete="off" requerido={!montosSoloLectura}
            ayuda={ayudaMonto('El de la cotización. Cámbialo solo si se acordó otro.')} {...monto('montoBaseTrabajo')} />
        )}
        <Campo etiqueta="Número de factura" autoComplete="off" maxLength={LIMITES_PEDIDO.factura} ayuda="Opcional." {...campo('numeroFactura')} />
      </div>
    </div>
  );
}

const OPCIONES_MEDIO = Object.entries(MEDIOS_PAGO);

/**
 * Un abono (E25): monto, fecha de pago (hoy, si se deja en blanco), medio y una observación. `conFactura` suma
 * el número de factura, que actualiza la del pedido.
 */
export function CamposAbono({ form, hoy, deshabilitado = false, conFactura = false, opcional = false, prefijo = '', refMonto }) {
  const { propiedades } = form;
  const campo = (nombre) => ({ ...propiedades(nombre), disabled: deshabilitado });
  return (
    <div className={estilosPanel.formulario}>
      <div className={estilosPanel.tresColumnas}>
        <Campo ref={refMonto} id={`${prefijo}monto`} etiqueta="Monto ($)" inputMode="numeric" autoComplete="off" requerido={!opcional} {...campo('monto')} />
        <Campo id={`${prefijo}fecha`} etiqueta="Fecha de pago" type="date" max={hoy} min={sumarDias(hoy, -365) ?? undefined}
          ayuda="En blanco, hoy." {...campo('fechaPago')} />
        <Campo id={`${prefijo}medio`} etiqueta="Medio de pago" como="select" requerido={!opcional} {...campo('medioPago')}>
          <option value="">Elige uno</option>
          {OPCIONES_MEDIO.map(([valor, texto]) => <option key={valor} value={valor}>{texto}</option>)}
        </Campo>
      </div>
      <Campo id={`${prefijo}observacion`} etiqueta="Observación" como="textarea" rows={2} maxLength={LIMITES_PEDIDO.observacionAbono}
        ayuda="Opcional. Por ejemplo, el número de la transferencia." {...campo('observacion')} />
      {conFactura && (
        <Campo id={`${prefijo}factura`} etiqueta="Número de factura" autoComplete="off" maxLength={LIMITES_PEDIDO.factura}
          ayuda="Opcional. Si lo escribes, queda como la factura del pedido." {...campo('numeroFactura')} />
      )}
    </div>
  );
}
