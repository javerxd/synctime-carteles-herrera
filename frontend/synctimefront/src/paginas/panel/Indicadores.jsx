import { Aviso, Esqueleto, EstadoVacio, Tarjeta, TEXTOS_ESTADO_PAGO, TEXTOS_ESTADO_PEDIDO } from '../../componentes/ui';
import { panel } from '../../api/panel';
import { formatearPesos, nombreMes } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { esPorCobrar } from './reglasPedidos';
import { GraficoBarras } from './GraficoBarras';
import estilos from './Panel.module.css';

const mesDe = (fecha) => { const f = new Date(fecha); return `${f.getFullYear()}-${f.getMonth() + 1}`; };
const etiquetaMes = (clave) => { const [, m] = clave.split('-'); return nombreMes(Number(m)).slice(0, 3); };

/** Los últimos seis meses, del más antiguo al más reciente. */
function ultimosMeses(cantidad = 6) {
  const hoy = new Date();
  return Array.from({ length: cantidad }, (_, i) => {
    const f = new Date(hoy.getFullYear(), hoy.getMonth() - (cantidad - 1 - i), 1);
    return `${f.getFullYear()}-${f.getMonth() + 1}`;
  });
}

function calcular(pedidos, conversion) {
  const meses = ultimosMeses();
  const esteMes = meses.at(-1);
  const validos = pedidos.filter((p) => p.estadoPedido !== 'anulado');
  const delMes = validos.filter((p) => mesDe(p.fechaPedido) === esteMes);
  const entregadosDelMes = pedidos.filter((p) => p.estadoPedido === 'entregado' && p.fechaEntregaReal && mesDe(p.fechaEntregaReal) === esteMes);
  const entregados = pedidos.filter((p) => p.estadoPedido === 'entregado' && p.fechaEntregaReal);
  const aTiempo = entregados.filter((p) => new Date(p.fechaEntregaReal) <= new Date(p.fechaEntregaEstimada)).length;
  const ingresosMes = entregadosDelMes.reduce((s, p) => s + Number(p.montoTotalFinal), 0);
  const ultimaConversion = conversion?.[0];

  return {
    indicadores: [
      { nombre: 'Pedidos del mes', valor: String(delMes.length), formula: 'Pedidos creados este mes, sin anulados (S10)' },
      { nombre: 'Ingresos del mes', valor: formatearPesos(ingresosMes), formula: 'Monto final de los pedidos entregados este mes (S11)' },
      { nombre: 'Ticket promedio', valor: formatearPesos(validos.length ? validos.reduce((s, p) => s + Number(p.montoTotalFinal), 0) / validos.length : 0), formula: 'Monto final promedio de los pedidos no anulados' },
      { nombre: 'Saldo por cobrar', valor: formatearPesos(pedidos.filter(esPorCobrar).reduce((s, p) => s + Number(p.saldoPendiente), 0)), formula: 'Saldo de los pedidos no anulados sin pagar del todo' },
      { nombre: 'Conversión en línea', valor: ultimaConversion ? `${Number(ultimaConversion.porcentajeConversion).toLocaleString('es-CL', { maximumFractionDigits: 1 })} %` : 'Sin datos', formula: 'Solicitudes en línea con pedido no anulado sobre las solicitudes en línea, último mes con datos (S14)' },
      { nombre: 'Entregas a tiempo', valor: entregados.length ? `${Math.round((aTiempo / entregados.length) * 100)} %` : 'Sin datos', formula: 'Entregados en o antes de la fecha estimada, sobre el total entregado' },
    ],
    porMes: meses.map((m) => ({ etiqueta: etiquetaMes(m), valor: validos.filter((p) => mesDe(p.fechaPedido) === m).length })),
    ingresosPorMes: meses.map((m) => ({
      etiqueta: etiquetaMes(m),
      valor: pedidos.filter((p) => p.estadoPedido === 'entregado' && p.fechaEntregaReal && mesDe(p.fechaEntregaReal) === m).reduce((s, p) => s + Number(p.montoTotalFinal), 0),
    })),
    porEstado: Object.entries(TEXTOS_ESTADO_PEDIDO).map(([clave, texto]) => ({ etiqueta: texto, valor: pedidos.filter((p) => p.estadoPedido === clave).length })),
    porPago: Object.entries(TEXTOS_ESTADO_PAGO).map(([clave, texto]) => ({ etiqueta: texto, valor: validos.filter((p) => p.estadoPago === clave).length })),
  };
}

/** Indicadores (RFS09): seis indicadores con su fórmula y cuatro gráficos con su tabla de datos. */
export default function Indicadores() {
  const pedidos = useConsulta('pedidos', panel.pedidos);
  const conversion = useConsulta('conversion', panel.conversionEnLinea);
  const datos = pedidos.datos ? calcular(pedidos.datos, conversion.datos) : null;

  return (
    <div className={estilos.pagina}>
      <div>
        <h1 className={estilos.titulo}>Indicadores</h1>
        <p className={estilos.bajada}>Calculados con los pedidos de la base de demostración.</p>
      </div>
      <Aviso tono="info" titulo="Cálculo provisional en el navegador">
        La fase 12 los lleva al servidor, con filtros por fechas, categoría, estado del pedido, estado de pago y origen.
      </Aviso>

      {pedidos.error && <Aviso tono="peligro" titulo="No pudimos cargar los pedidos">{pedidos.error.message}</Aviso>}
      {pedidos.cargando && <Esqueleto forma="bloque" alto="12rem" />}
      {datos && pedidos.datos.length === 0 && <EstadoVacio titulo="Aún no hay pedidos para calcular indicadores" />}
      {datos && pedidos.datos.length > 0 && (
        <>
          <ul className={estilos.indicadores}>
            {datos.indicadores.map((i) => (
              <li key={i.nombre}>
                <Tarjeta className={estilos.indicador}>
                  <span className={estilos.indicadorNombre}>{i.nombre}</span>
                  <span className={estilos.indicadorValor}>{i.valor}</span>
                  <span className={estilos.indicadorFormula}>{i.formula}</span>
                </Tarjeta>
              </li>
            ))}
          </ul>
          <div className={estilos.graficos}>
            <GraficoBarras titulo="Pedidos por mes" descripcion="Pedidos creados en los últimos seis meses, sin contar los anulados." datos={datos.porMes} nombreValor="Pedidos" />
            <GraficoBarras titulo="Ingresos por mes" descripcion="Monto final de los pedidos entregados cada mes." datos={datos.ingresosPorMes} formato={formatearPesos} nombreValor="Ingresos" />
            <GraficoBarras horizontal titulo="Pedidos por estado" descripcion="Todos los pedidos, según su estado de fabricación." datos={datos.porEstado} nombreValor="Pedidos" />
            <GraficoBarras horizontal titulo="Pedidos por estado de pago" descripcion="Pedidos no anulados, según cuánto se ha pagado." datos={datos.porPago} nombreValor="Pedidos" />
          </div>
        </>
      )}
    </div>
  );
}
