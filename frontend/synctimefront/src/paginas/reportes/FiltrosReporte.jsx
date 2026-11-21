import { BarraFiltros, Campo, Chip, Opciones, TEXTOS_ESTADO_PAGO, TEXTOS_ESTADO_PEDIDO } from '../../componentes/ui';
import { nombreMes } from '../../utilidades/formato';
import { FiltroCliente, RangoPropio } from '../indicadores/FiltrosIndicadores';
import { COMPARACIONES, ESTADOS_PAGO_FILTRO, ESTADOS_PEDIDO_FILTRO, PERIODOS } from '../indicadores/reglasIndicadores';
import estilos from '../indicadores/Indicadores.module.css';

const SIN_FECHAS = { desde: '', hasta: '', anio: '', mes: '' };
const alternar = (lista, valor) => (lista.includes(valor) ? lista.filter((v) => v !== valor) : [...lista, valor]);
const listaDe = (texto) => (texto ? texto.split(',') : []);

/**
 * Filtros de los reportes (RFU24 a RFU26, RFU36): período (semana, mes o año), rango propio, año y mes, estado del pedido, estado
 * de pago («Por cobrar» incluido) y cliente; «Comparar con» solo en el gerencial y «Alcance del saldo» solo en el parametrizable
 * (lo que ese reporte no tiene, no se ofrece). Los mismos nombres y reglas del dashboard; todo vive en la dirección. Lo que se
 * elige aquí es lo que lleva el archivo exportado.
 */
export function FiltrosReporte({ reporte, filtros, cambiar, limpiar, clientes, anios, resumen }) {
  const estadosPedido = listaDe(filtros.estadoPedido);
  const porCobrar = filtros.estadoPago === 'por_cobrar';
  const estadosPago = porCobrar ? [] : listaDe(filtros.estadoPago);
  const aplicados = ['estadoPedido', 'estadoPago', 'cliente'].filter((c) => filtros[c]).length;
  const fechaPropia = Boolean(filtros.desde || filtros.anio);
  const hayFiltros = aplicados > 0 || fechaPropia || filtros.periodo !== 'mes'
    || (reporte === 'gerencial' && filtros.comparar !== 'anterior') || (reporte === 'parametrizable' && filtros.alcanceSaldo !== 'periodo');

  return (
    <BarraFiltros titulo="Filtros del reporte" resumen={resumen} hayFiltros={hayFiltros} alLimpiar={limpiar}>
      <div className={estilos.filaFiltros}>
        <Opciones leyenda="Período" nombre="periodo" enLinea className={estilos.periodos}
          opciones={Object.entries(PERIODOS).map(([valor, etiqueta]) => ({ valor, etiqueta }))}
          valor={fechaPropia ? '' : filtros.periodo} alCambiar={(valor) => cambiar({ ...SIN_FECHAS, periodo: valor })} />
        <Campo como="select" etiqueta="Año" value={filtros.anio} onChange={(e) => cambiar({ ...SIN_FECHAS, anio: e.target.value })}>
          <option value="">Según el período</option>
          {anios.map((a) => <option key={a} value={a}>{a}</option>)}
        </Campo>
        <Campo como="select" etiqueta="Mes" value={filtros.mes} disabled={!filtros.anio} ayuda={filtros.anio ? undefined : 'Elige primero un año'}
          onChange={(e) => cambiar({ desde: '', hasta: '', anio: filtros.anio, mes: e.target.value })}>
          <option value="">Todo el año</option>
          {Array.from({ length: 12 }, (_, i) => <option key={i + 1} value={i + 1}>{nombreMes(i + 1)}</option>)}
        </Campo>
        {reporte === 'gerencial' && (
          <Campo como="select" etiqueta="Comparar con" value={filtros.comparar} onChange={(e) => cambiar({ comparar: e.target.value })}>
            {Object.entries(COMPARACIONES).map(([valor, texto]) => <option key={valor} value={valor}>{texto}</option>)}
          </Campo>
        )}
        {reporte === 'parametrizable' && (
          <Campo como="select" etiqueta="Alcance del saldo" value={filtros.alcanceSaldo} onChange={(e) => cambiar({ alcanceSaldo: e.target.value })}
            ayuda="«Toda la deuda vigente» no limita por fecha.">
            <option value="periodo">Pedidos del período</option>
            <option value="vigente">Toda la deuda vigente</option>
          </Campo>
        )}
      </div>

      <RangoPropio filtros={filtros} alAplicar={cambiar} />

      <details className={estilos.masFiltros} open={aplicados > 0 || undefined}>
        <summary>Filtrar pedidos{aplicados ? ` (${aplicados} ${aplicados === 1 ? 'aplicado' : 'aplicados'})` : ''}</summary>
        <div className={estilos.contenidoFiltros}>
          <fieldset className={estilos.grupoChips}>
            <legend className={estilos.etiquetaFiltro}>Estado del pedido</legend>
            <div className={estilos.chips}>
              {ESTADOS_PEDIDO_FILTRO.map((e) => (
                <Chip key={e} seleccionado={estadosPedido.includes(e)} alCambiar={() => cambiar({ estadoPedido: alternar(estadosPedido, e).join(',') })}>
                  {TEXTOS_ESTADO_PEDIDO[e]}
                </Chip>
              ))}
            </div>
          </fieldset>
          <fieldset className={estilos.grupoChips}>
            <legend className={estilos.etiquetaFiltro}>Estado de pago</legend>
            <div className={estilos.chips}>
              <Chip seleccionado={porCobrar} alCambiar={() => cambiar({ estadoPago: porCobrar ? '' : 'por_cobrar' })}>Por cobrar</Chip>
              {ESTADOS_PAGO_FILTRO.map((e) => (
                <Chip key={e} seleccionado={estadosPago.includes(e)} alCambiar={() => cambiar({ estadoPago: alternar(estadosPago, e).join(',') })}>
                  {TEXTOS_ESTADO_PAGO[e]}
                </Chip>
              ))}
            </div>
          </fieldset>
          <div className={estilos.filaFiltros}>
            <FiltroCliente clientes={clientes} valor={filtros.cliente} alCambiar={(cliente) => cambiar({ cliente })} />
          </div>
        </div>
      </details>
    </BarraFiltros>
  );
}
