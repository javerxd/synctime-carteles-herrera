import { useRef } from 'react';
import { Link } from 'react-router-dom';
import { CheckCheck, Coins, Plus } from 'lucide-react';
import {
  Aviso, BarraFiltros, Boton, Campo, Chip, EstadoPago, EstadoPedido, EstadoVacio, Paginacion, TablaAdaptable, Tarjeta,
  TEXTOS_ESTADO_PAGO, TEXTOS_ESTADO_PEDIDO,
} from '../../componentes/ui';
import { pedidos } from '../../api/pedidos';
import { hoyChile } from '../../utilidades/fechas';
import { formatearFecha, formatearPesos, nombreMes } from '../../utilidades/formato';
import { useListadoEnDireccion } from '../../utilidades/useListadoEnDireccion';
import { FiltroCliente } from '../compartidos/FiltroCliente';
import { telefonoVisible } from '../solicitudes/reglasSolicitud';
import {
  aniosFiltro, FILTROS_PEDIDOS, nombreDe, numeroPedido, parametrosPedidos, TEXTOS_ORDEN_PEDIDOS,
} from './reglasPedido';
import estilosPanel from '../panel/Panel.module.css';
import estilosSolicitud from '../solicitudes/Solicitudes.module.css';
import estilos from './Pedidos.module.css';

const cantidad = (n, uno, varios) => `${n} ${n === 1 ? uno : varios}`;

/**
 * /panel/pedidos (RFS06, RFU36, E27): el estado del pedido y el de pago van por separado. Los filtros (estado del
 * pedido, de pago, cobranza, año, mes, cliente y búsqueda), el orden y la página quedan en la dirección y se
 * aplican en la API. «Por cobrar» (S24) muestra el total por cobrar de todo el filtro, no solo de la página.
 */
export default function ListaPedidos() {
  const refTitulo = useRef(null);
  const hoy = hoyChile();
  const {
    filtros, consulta, cambiar, limpiar, texto, escribir, hayFiltros, errorVisible, actualizando,
  } = useListadoEnDireccion(FILTROS_PEDIDOS, { clave: 'pedidos', consultar: (f, senal) => pedidos.listar(parametrosPedidos(f), { senal }) });
  const datos = consulta.datos;
  const filas = datos?.elementos ?? [];
  const total = datos?.total ?? 0;
  const resumen = datos?.resumen;
  const cobranza = filtros.cobranza === 'true';

  const columnas = [
    {
      clave: 'numero', encabezado: 'N.º', principal: true,
      render: (p) => (
        <Link to={`/panel/pedidos/${p.idPedido}`} className={estilosSolicitud.enlaceFila} aria-label={`Pedido ${numeroPedido(p)}`}>
          <span className="cifras">{numeroPedido(p)}</span>
        </Link>
      ),
    },
    {
      clave: 'cliente', encabezado: 'Cliente',
      render: (p) => (
        <span className={estilosSolicitud.celdaLetrero}>
          <span>{nombreDe(p.cliente)}</span>
          {p.cliente?.telefono && <span className={estilosSolicitud.detalleLetrero}>{telefonoVisible(p.cliente.telefono)}</span>}
        </span>
      ),
    },
    { clave: 'fecha', encabezado: 'Fecha', render: (p) => <span className={`cifras ${estilos.sinCorte}`}>{formatearFecha(p.fechaPedido)}</span> },
    { clave: 'entrega', encabezado: 'Entrega estimada', render: (p) => <span className={`cifras ${estilos.sinCorte}`}>{formatearFecha(p.fechaEntregaEstimada)}</span> },
    {
      clave: 'estadoPedido', encabezado: 'Pedido',
      render: (p) => (
        <span className={estilos.estadosCelda}>
          <EstadoPedido estado={p.estadoPedido} />
          {p.cicloCerrado && <span className={estilos.cicloCerrado}><CheckCheck size={14} aria-hidden="true" />Ciclo cerrado</span>}
        </span>
      ),
    },
    { clave: 'estadoPago', encabezado: 'Pago', render: (p) => <EstadoPago estado={p.estadoPago} /> },
    { clave: 'final', encabezado: 'Monto final', alinear: 'fin', render: (p) => <span className="cifras">{formatearPesos(p.montoTotalFinal)}</span> },
    { clave: 'abonado', encabezado: 'Abonado', alinear: 'fin', render: (p) => <span className="cifras">{formatearPesos(p.montoAbonado)}</span> },
    {
      clave: 'saldo', encabezado: 'Saldo', alinear: 'fin',
      render: (p) => <strong className="cifras">{formatearPesos(p.estadoPedido === 'anulado' ? 0 : p.saldoPendiente)}</strong>,
    },
  ];

  const textoResumen = datos
    ? `${cantidad(total, 'pedido', 'pedidos')}${resumen ? ` · ${formatearPesos(resumen.totalPorCobrar)} por cobrar` : ''}`
    : '';

  return (
    <div className={estilosPanel.pagina}>
      <div className={estilosPanel.cabecera}>
        <div>
          <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>{cobranza ? 'Pedidos por cobrar' : 'Pedidos'}</h1>
          <p className={estilosPanel.bajada}>Cada pedido tiene su estado de fabricación y su estado de pago, por separado.</p>
        </div>
        <Boton como={Link} to="/panel/pedidos/directo" iconoInicio={Plus}>Pedido directo en el taller</Boton>
      </div>

      <BarraFiltros titulo="Filtros de pedidos" hayFiltros={hayFiltros} alLimpiar={limpiar} resumen={textoResumen}>
        <Campo etiqueta="Buscar" type="search" ayuda="N.º, nombre, teléfono o RUT" value={texto} onChange={(e) => escribir(e.target.value)} />
        <Campo etiqueta="Estado del pedido" como="select" value={filtros.estadoPedido} onChange={(e) => cambiar({ estadoPedido: e.target.value })}>
          <option value="">Todos</option>
          {Object.entries(TEXTOS_ESTADO_PEDIDO).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
        </Campo>
        <Campo etiqueta="Estado de pago" como="select" value={filtros.estadoPago} onChange={(e) => cambiar({ estadoPago: e.target.value })}>
          <option value="">Todos</option>
          {Object.entries(TEXTOS_ESTADO_PAGO).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
        </Campo>
        <Campo etiqueta="Año" como="select" value={filtros.anio} onChange={(e) => cambiar({ anio: e.target.value, mes: e.target.value ? filtros.mes : '' })}>
          <option value="">Todos</option>
          {aniosFiltro(hoy, filtros.anio).map((anio) => <option key={anio} value={anio}>{anio}</option>)}
        </Campo>
        {/* El mes filtra junto con el año: elegir un mes sin año toma el año en curso. */}
        <Campo etiqueta="Mes" como="select" value={filtros.mes}
          onChange={(e) => cambiar({ mes: e.target.value, anio: e.target.value && !filtros.anio ? hoy.slice(0, 4) : filtros.anio })}>
          <option value="">Todos</option>
          {Array.from({ length: 12 }, (_, i) => <option key={i + 1} value={i + 1}>{nombreMes(i + 1)}</option>)}
        </Campo>
        <Campo etiqueta="Orden" como="select" value={filtros.orden} onChange={(e) => cambiar({ orden: e.target.value })}>
          {Object.entries(TEXTOS_ORDEN_PEDIDOS).map(([valor, nombre]) => <option key={valor} value={valor}>{nombre}</option>)}
        </Campo>
        <div className={estilosPanel.chips}>
          <Chip seleccionado={cobranza} alCambiar={(valor) => cambiar({ cobranza: valor ? 'true' : '' })}>Por cobrar</Chip>
          {filtros.idCliente && <FiltroCliente idCliente={filtros.idCliente} alQuitar={() => cambiar({ idCliente: '' })} />}
        </div>
      </BarraFiltros>

      {cobranza && resumen && (
        <Tarjeta como="section" aria-labelledby="titulo-por-cobrar" className={estilos.porCobrar}>
          <Coins size={24} strokeWidth={1.5} aria-hidden="true" className={estilos.iconoPorCobrar} />
          <div>
            <h2 id="titulo-por-cobrar" className={estilos.etiquetaPorCobrar}>Total por cobrar</h2>
            <p className={estilos.montoPorCobrar}><span className="cifras">{formatearPesos(resumen.totalPorCobrar)}</span></p>
            <p className={estilosSolicitud.nota}>
              {cantidad(resumen.cantidadPorCobrar ?? total, 'pedido no anulado', 'pedidos no anulados')} con pago «No pagado» o «Abonado»
              {hayFiltrosAparteDeCobranza(filtros) ? ', con los filtros elegidos' : ''}.
            </p>
          </div>
        </Tarjeta>
      )}

      {errorVisible ? (
        <Aviso tono="peligro" titulo="No pudimos cargar los pedidos"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {errorVisible.message}
        </Aviso>
      ) : (
        <TablaAdaptable titulo={cobranza ? 'Pedidos por cobrar' : 'Pedidos'} columnas={columnas} filas={filas} claveFila="idPedido" cargando={consulta.cargando}
          actualizando={actualizando}
          vacio={(
            <EstadoVacio
              titulo={cobranza && !hayFiltrosAparteDeCobranza(filtros) ? 'No hay pedidos por cobrar' : hayFiltros ? 'Ningún pedido coincide con estos filtros' : 'Aún no hay pedidos'}
              descripcion={hayFiltros
                ? 'Prueba con otro estado, otro mes o quita la búsqueda.'
                : 'Los pedidos aparecen aquí al crearse desde una cotización aprobada o directo en el taller.'}
              accion={hayFiltros ? <Boton variante="secundario" onClick={limpiar}>Limpiar filtros</Boton> : null}
            />
          )} />
      )}
      <Paginacion pagina={filtros.pagina} totalPaginas={consulta.cargando ? 0 : datos?.totalPaginas ?? 0} etiqueta="Paginación de pedidos"
        alCambiar={(numero) => { cambiar({ pagina: numero }); refTitulo.current?.focus(); }} />
    </div>
  );
}

function hayFiltrosAparteDeCobranza(filtros) {
  return ['estadoPedido', 'estadoPago', 'anio', 'mes', 'idCliente', 'q'].some((c) => filtros[c]);
}
