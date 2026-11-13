import { useMemo, useRef, useState } from 'react';
import { Eye, Plus } from 'lucide-react';
import {
  Aviso, BarraFiltros, Boton, Campo, Chip, Dialogo, EstadoPago, EstadoPedido, EstadoVacio, Paginacion, TablaAdaptable,
  TEXTOS_ESTADO_PAGO, TEXTOS_ESTADO_PEDIDO,
} from '../../componentes/ui';
import { panel } from '../../api/panel';
import { formatearFecha, formatearPesos, nombreMes } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { useDebounce } from '../../utilidades/useDebounce';
import { aniosDisponibles, filtrarPedidos, nombreCliente, resumirPedidos } from './reglasPedidos';
import { PedidoDirecto } from './PedidoDirecto';
import estilos from './Panel.module.css';

const POR_PAGINA = 10;
const SIN_FILTROS = { estadoPedido: '', estadoPago: '', anio: '', mes: '', cliente: '', cobranza: false };
const ORIGEN = { cotizacion: 'Desde cotización', directo: 'Presencial directo' };
const MODALIDAD = { retiro_local: 'Retiro en el local', entrega_domicilio: 'Entrega a domicilio', instalacion_terreno: 'Instalación en terreno' };

/** Pedidos (RFS06): estado del pedido y de pago por separado, filtros y cobranza. */
export default function Pedidos() {
  const consulta = useConsulta('pedidos', panel.pedidos);
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const [pagina, setPagina] = useState(1);
  const [ficha, setFicha] = useState(null);
  const [fichaAbierta, setFichaAbierta] = useState(false);
  const [directoAbierto, setDirectoAbierto] = useState(false);
  const refTitulo = useRef(null);
  const clienteBuscado = useDebounce(filtros.cliente);

  const pedidos = useMemo(() => consulta.datos ?? [], [consulta.datos]);
  const filtrados = useMemo(() => filtrarPedidos(pedidos, { ...filtros, cliente: clienteBuscado }), [pedidos, filtros, clienteBuscado]);
  const resumen = resumirPedidos(filtrados);
  const totalPaginas = Math.max(1, Math.ceil(filtrados.length / POR_PAGINA));
  const paginaActual = Math.min(pagina, totalPaginas);
  const visibles = filtrados.slice((paginaActual - 1) * POR_PAGINA, paginaActual * POR_PAGINA);
  const hayFiltros = Object.entries(filtros).some(([clave, valor]) => valor !== SIN_FILTROS[clave]);

  const cambiar = (campo) => (evento) => {
    setFiltros((f) => ({ ...f, [campo]: evento.target.value }));
    setPagina(1);
  };

  function irAPagina(numero) {
    setPagina(numero);
    refTitulo.current?.focus();
  }

  const columnas = [
    { clave: 'cliente', encabezado: 'Cliente', principal: true, render: nombreCliente },
    { clave: 'idPedido', encabezado: 'N.º', render: (p) => <span className="cifras">{p.idPedido}</span> },
    { clave: 'fechaPedido', encabezado: 'Fecha', render: (p) => <span className="cifras">{formatearFecha(p.fechaPedido)}</span> },
    { clave: 'estadoPedido', encabezado: 'Pedido', render: (p) => <EstadoPedido estado={p.estadoPedido} /> },
    { clave: 'estadoPago', encabezado: 'Pago', render: (p) => <EstadoPago estado={p.estadoPago} /> },
    { clave: 'montoTotalFinal', encabezado: 'Monto final', alinear: 'fin', render: (p) => formatearPesos(p.montoTotalFinal) },
    { clave: 'montoAbonado', encabezado: 'Abonado', alinear: 'fin', render: (p) => formatearPesos(p.montoAbonado) },
    { clave: 'saldoPendiente', encabezado: 'Saldo', alinear: 'fin', render: (p) => <strong>{formatearPesos(p.saldoPendiente)}</strong> },
    {
      clave: 'acciones', encabezado: 'Acciones', alinear: 'fin',
      render: (p) => (
        <Boton variante="fantasma" tamano="compacto" iconoInicio={Eye} onClick={() => { setFicha(p); setFichaAbierta(true); }}>
          Ver ficha<span className="solo-lectores"> del pedido {p.idPedido}</span>
        </Boton>
      ),
    },
  ];

  return (
    <div className={estilos.pagina}>
      <div className={estilos.cabecera}>
        <div>
          <h1 ref={refTitulo} tabIndex={-1} className={estilos.titulo}>Pedidos</h1>
          <p className={estilos.bajada}>Cada pedido tiene su estado de fabricación y su estado de pago, por separado.</p>
        </div>
        <Boton iconoInicio={Plus} onClick={() => setDirectoAbierto(true)}>Pedido presencial directo</Boton>
      </div>

      <BarraFiltros
        titulo="Filtros de pedidos"
        hayFiltros={hayFiltros}
        alLimpiar={() => { setFiltros(SIN_FILTROS); setPagina(1); }}
        resumen={consulta.datos ? `${resumen.cantidad} ${resumen.cantidad === 1 ? 'pedido' : 'pedidos'} · ${formatearPesos(resumen.porCobrar)} por cobrar` : ''}
      >
        <Campo etiqueta="Estado del pedido" como="select" value={filtros.estadoPedido} onChange={cambiar('estadoPedido')}>
          <option value="">Todos</option>
          {Object.entries(TEXTOS_ESTADO_PEDIDO).map(([valor, texto]) => <option key={valor} value={valor}>{texto}</option>)}
        </Campo>
        <Campo etiqueta="Estado de pago" como="select" value={filtros.estadoPago} onChange={cambiar('estadoPago')}>
          <option value="">Todos</option>
          {Object.entries(TEXTOS_ESTADO_PAGO).map(([valor, texto]) => <option key={valor} value={valor}>{texto}</option>)}
        </Campo>
        <Campo etiqueta="Año" como="select" value={filtros.anio} onChange={cambiar('anio')}>
          <option value="">Todos</option>
          {aniosDisponibles(pedidos).map((anio) => <option key={anio} value={anio}>{anio}</option>)}
        </Campo>
        <Campo etiqueta="Mes" como="select" value={filtros.mes} onChange={cambiar('mes')}>
          <option value="">Todos</option>
          {Array.from({ length: 12 }, (_, i) => <option key={i + 1} value={i + 1}>{nombreMes(i + 1)}</option>)}
        </Campo>
        <Campo etiqueta="Cliente" type="search" ayuda="Nombre, RUT o teléfono" placeholder="Buscar cliente" value={filtros.cliente} onChange={cambiar('cliente')} />
        <div className={estilos.chips}>
          <Chip seleccionado={filtros.cobranza} alCambiar={(valor) => { setFiltros((f) => ({ ...f, cobranza: valor })); setPagina(1); }}>
            Por cobrar
          </Chip>
        </div>
      </BarraFiltros>

      {consulta.error ? (
        <Aviso tono="peligro" titulo="No pudimos cargar los pedidos" accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {consulta.error.message}
        </Aviso>
      ) : (
        <TablaAdaptable
          titulo="Pedidos"
          columnas={columnas}
          filas={visibles}
          claveFila="idPedido"
          cargando={consulta.cargando}
          vacio={(
            <EstadoVacio
              nivelTitulo={2}
              titulo={pedidos.length ? 'Ningún pedido coincide con estos filtros' : 'Aún no hay pedidos'}
              descripcion={pedidos.length ? 'Prueba con otro estado, otro mes o quita la búsqueda por cliente.' : 'Los pedidos aparecen aquí al aprobarse una cotización o al registrar uno presencial.'}
              accion={hayFiltros ? <Boton variante="secundario" onClick={() => { setFiltros(SIN_FILTROS); setPagina(1); }}>Limpiar filtros</Boton> : null}
            />
          )}
        />
      )}
      <Paginacion pagina={paginaActual} totalPaginas={consulta.cargando ? 0 : totalPaginas} alCambiar={irAPagina} etiqueta="Paginación de pedidos" />

      <FichaPedido pedido={ficha} abierta={fichaAbierta} alCerrar={() => setFichaAbierta(false)} />
      <PedidoDirecto abierto={directoAbierto} alCerrar={() => setDirectoAbierto(false)} />
    </div>
  );
}

function Dato({ termino, children }) {
  return (
    <div className={estilos.dato}>
      <dt>{termino}</dt>
      <dd>{children}</dd>
    </div>
  );
}

/** Ficha del pedido (S07), con los datos que ya entrega el listado. */
function FichaPedido({ pedido, abierta, alCerrar }) {
  const cliente = pedido?.solicitud?.cliente;
  return (
    <Dialogo abierto={abierta && Boolean(pedido)} alCerrar={alCerrar} titulo={pedido ? `Pedido N.º ${pedido.idPedido}` : 'Pedido'}
      descripcion={pedido ? `${nombreCliente(pedido)} · ${formatearFecha(pedido.fechaPedido)}` : undefined} ancho="38rem">
      {pedido && (
        <div className={estilos.ficha}>
          <div className={estilos.estados}>
            <EstadoPedido estado={pedido.estadoPedido} />
            <EstadoPago estado={pedido.estadoPago} />
          </div>
          <dl className={estilos.datos}>
            <Dato termino="Monto final"><strong>{formatearPesos(pedido.montoTotalFinal)}</strong></Dato>
            <Dato termino="Abonado">{formatearPesos(pedido.montoAbonado)}</Dato>
            <Dato termino="Saldo pendiente"><strong>{formatearPesos(pedido.saldoPendiente)}</strong></Dato>
            <Dato termino="Trabajo">{formatearPesos(pedido.montoBaseTrabajo)}</Dato>
            <Dato termino="Instalación">{formatearPesos(pedido.costoInstalacionAdicional)}</Dato>
            <Dato termino="Factura">{pedido.numeroFactura || 'Sin factura'}</Dato>
            <Dato termino="Origen">{ORIGEN[pedido.origen] ?? pedido.origen}</Dato>
            <Dato termino="Modalidad">{MODALIDAD[pedido.modalidadEntrega] ?? pedido.modalidadEntrega}</Dato>
            <Dato termino="Entrega estimada">{formatearFecha(pedido.fechaEntregaEstimada)}</Dato>
            <Dato termino="Entregado el">{pedido.fechaEntregaReal ? formatearFecha(pedido.fechaEntregaReal) : 'Aún no se entrega'}</Dato>
            <Dato termino="Responsable">
              {[pedido.empleadoResponsable?.nombre, pedido.empleadoResponsable?.apellido].filter(Boolean).join(' ') || '—'}
            </Dato>
            <Dato termino="Contacto">{cliente?.telefono || '—'}</Dato>
          </dl>
          {pedido.estadoPedido === 'entregado' && pedido.estadoPago === 'pagado_total' && (
            <Aviso tono="exito" titulo="Ciclo cerrado: entregado y pagado" />
          )}
          <Aviso tono="info" titulo="El registro de abonos y el cambio de estado llegan en la fase 8">
            El historial de abonos ya se guarda en la base con fecha y medio de pago.
          </Aviso>
        </div>
      )}
    </Dialogo>
  );
}
