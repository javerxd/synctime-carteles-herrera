import { useMemo, useState } from 'react';
import { Aviso, Campo, EstadoPago, EstadoPedido, Pestanas, TablaAdaptable, TEXTOS_ESTADO_PAGO, TEXTOS_ESTADO_PEDIDO } from '../../componentes/ui';
import { panel } from '../../api/panel';
import { formatearFecha, formatearPesos } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { filtrarPedidos, nombreCliente, resumirPedidos } from './reglasPedidos';
import estilos from './Panel.module.css';

/** Reportes (RFS10): la estructura de los tres reportes con su vista previa; exportar llega en la fase 13. */
export default function Reportes() {
  const consulta = useConsulta('pedidos', panel.pedidos);
  const [filtros, setFiltros] = useState({ desde: '', hasta: '', estadoPedido: '', estadoPago: '' });
  const pedidos = useMemo(() => {
    const base = filtrarPedidos(consulta.datos ?? [], filtros);
    return base.filter((p) => (!filtros.desde || p.fechaPedido.slice(0, 10) >= filtros.desde)
      && (!filtros.hasta || p.fechaPedido.slice(0, 10) <= filtros.hasta));
  }, [consulta.datos, filtros]);
  const resumen = resumirPedidos(pedidos);
  const cambiar = (campo) => (e) => setFiltros((f) => ({ ...f, [campo]: e.target.value }));

  const filtrosParametrizables = (
    <div className={estilos.tresColumnas}>
      <Campo etiqueta="Desde" type="date" value={filtros.desde} onChange={cambiar('desde')} />
      <Campo etiqueta="Hasta" type="date" value={filtros.hasta} onChange={cambiar('hasta')} />
      <Campo etiqueta="Estado del pedido" como="select" value={filtros.estadoPedido} onChange={cambiar('estadoPedido')}>
        <option value="">Todos</option>
        {Object.entries(TEXTOS_ESTADO_PEDIDO).map(([v, t]) => <option key={v} value={v}>{t}</option>)}
      </Campo>
      <Campo etiqueta="Estado de pago" como="select" value={filtros.estadoPago} onChange={cambiar('estadoPago')}>
        <option value="">Todos</option>
        {Object.entries(TEXTOS_ESTADO_PAGO).map(([v, t]) => <option key={v} value={v}>{t}</option>)}
      </Campo>
    </div>
  );

  const tabla = (titulo, filas) => (
    <TablaAdaptable
      titulo={titulo}
      cargando={consulta.cargando}
      filas={filas}
      claveFila="idPedido"
      columnas={[
        { clave: 'cliente', encabezado: 'Cliente', principal: true, render: nombreCliente },
        { clave: 'idPedido', encabezado: 'N.º' },
        { clave: 'fecha', encabezado: 'Fecha', render: (p) => formatearFecha(p.fechaPedido) },
        { clave: 'entrega', encabezado: 'Entrega estimada', render: (p) => formatearFecha(p.fechaEntregaEstimada) },
        { clave: 'estadoPedido', encabezado: 'Pedido', render: (p) => <EstadoPedido estado={p.estadoPedido} /> },
        { clave: 'estadoPago', encabezado: 'Pago', render: (p) => <EstadoPago estado={p.estadoPago} /> },
        { clave: 'saldo', encabezado: 'Saldo', alinear: 'fin', render: (p) => formatearPesos(p.saldoPendiente) },
      ]}
    />
  );

  return (
    <div className={estilos.pagina}>
      <div>
        <h1 className={estilos.titulo}>Reportes</h1>
        <p className={estilos.bajada}>Vista previa con los pedidos de la base de demostración: {resumen.cantidad} pedidos, {formatearPesos(resumen.porCobrar)} por cobrar.</p>
      </div>
      <Aviso tono="info" titulo="La exportación a Excel, PDF y CSV y la impresión llegan en la fase 13" />
      {consulta.error && <Aviso tono="peligro" titulo="No pudimos cargar los pedidos">{consulta.error.message}</Aviso>}
      <Pestanas
        etiqueta="Tipo de reporte"
        pestanas={[
          { id: 'operativo', etiqueta: 'Operativo', contenido: tabla('Reporte operativo de pedidos', filtrarPedidos(consulta.datos ?? [], {})) },
          {
            id: 'gerencial', etiqueta: 'Gerencial',
            contenido: <Aviso tono="info" titulo="Ingresos, costos y totales por período">Usa los mismos cálculos de Indicadores; se completa en la fase 13.</Aviso>,
          },
          { id: 'parametrizable', etiqueta: 'Parametrizable', contenido: <div className={estilos.ficha}>{filtrosParametrizables}{tabla('Reporte parametrizable de pedidos', pedidos)}</div> },
        ]}
      />
    </div>
  );
}
