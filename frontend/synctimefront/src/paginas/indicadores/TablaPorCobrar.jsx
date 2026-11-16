import { Link } from 'react-router-dom';
import { CalendarRange, History, PackageCheck } from 'lucide-react';
import { Boton, Chip, EstadoPago, EstadoPedido, EstadoVacio, Insignia, Paginacion, TablaAdaptable, TEXTOS_ESTADO_PAGO } from '../../componentes/ui';
import { formatearFecha, formatearPesos } from '../../utilidades/formato';
import { formatearNumero } from './formatoIndicadores';
import estilos from './Indicadores.module.css';

const COLUMNAS = [
  { clave: 'cliente', encabezado: 'Cliente', principal: true, render: (p) => p.cliente?.nombre ?? 'Cliente sin datos' },
  { clave: 'pedido', encabezado: 'Pedido', render: (p) => <Link to={`/panel/pedidos/${p.idPedido}`} className="cifras">#{p.idPedido}</Link> },
  { clave: 'fecha', encabezado: 'Fecha', render: (p) => <span className={`cifras ${estilos.sinCorte}`}>{formatearFecha(p.fechaPedido)}</span> },
  {
    clave: 'estado',
    encabezado: 'Estado',
    render: (p) => (
      <span className={estilos.estadosFila}>
        <EstadoPedido estado={p.estadoPedido} />
        <EstadoPago estado={p.estadoPago} />
        {p.listoSinPagar && <Insignia tono="peligro" icono={PackageCheck}>Listo y sin pagar</Insignia>}
      </span>
    ),
  },
  { clave: 'monto', encabezado: 'Monto final', alinear: 'fin', render: (p) => <span className="cifras">{formatearPesos(p.montoTotalFinal)}</span> },
  { clave: 'abonado', encabezado: 'Abonado', alinear: 'fin', render: (p) => <span className="cifras">{formatearPesos(p.montoAbonado)}</span> },
  { clave: 'saldo', encabezado: 'Saldo', alinear: 'fin', render: (p) => <strong className="cifras">{formatearPesos(p.saldoPendiente)}</strong> },
];

/**
 * «Pedidos por cobrar» bajo los gráficos: el cliente, los estados (los terminados y entregados sin ningún pago,
 * destacados), el monto final, lo abonado y el saldo, con el total al pie. Dice si es el saldo del período o toda
 * la deuda vigente, y lo cambia con un clic. Un segmento elegido en el anillo de cobranza se ve como un chip.
 */
export function TablaPorCobrar({ datos, actualizando, cobro, alQuitarCobro, alCambiarAlcance, alCambiarPagina }) {
  const vigente = datos.alcance === 'vigente';
  return (
    <>
      <div className={estilos.barraTabla}>
        <p className={estilos.alcance}>
          {vigente ? <History size={16} strokeWidth={1.75} aria-hidden="true" /> : <CalendarRange size={16} strokeWidth={1.75} aria-hidden="true" />}
          <span>{vigente ? 'Toda la deuda vigente, sin límite de fecha' : `Saldo de los pedidos de ${datos.periodo.etiqueta}`}</span>
        </p>
        <div className={estilos.accionesTabla}>
          {cobro && (
            <Chip quitable alCambiar={alQuitarCobro} aria-label={`Quitar el filtro por estado de pago: ${TEXTOS_ESTADO_PAGO[cobro]}`}>
              Estado de pago: {TEXTOS_ESTADO_PAGO[cobro]}
            </Chip>
          )}
          <Boton variante="fantasma" tamano="compacto" iconoInicio={vigente ? CalendarRange : History} onClick={() => alCambiarAlcance(vigente ? 'periodo' : 'vigente')}>
            {vigente ? 'Ver solo el período' : 'Ver toda la deuda vigente'}
          </Boton>
        </div>
      </div>
      <p role="status" className="solo-lectores">
        {actualizando ? '' : `${formatearNumero(datos.total)} ${datos.total === 1 ? 'pedido' : 'pedidos'} por cobrar${cobro ? ` en «${TEXTOS_ESTADO_PAGO[cobro]}»` : ''}, ${formatearPesos(datos.totalSaldo)} en total.`}
      </p>
      <TablaAdaptable
        titulo="Pedidos por cobrar"
        columnas={COLUMNAS}
        filas={datos.elementos}
        claveFila="idPedido"
        actualizando={actualizando}
        vacio={<EstadoVacio titulo="No hay pedidos por cobrar con estos filtros" nivelTitulo={3}
          descripcion={vigente ? undefined : 'Puede haber deuda de otros meses: revisa «Ver toda la deuda vigente».'} />}
      />
      {datos.total > 0 && (
        <p className={estilos.totalTabla}>
          <span>Total por cobrar ({formatearNumero(datos.total)} {datos.total === 1 ? 'pedido' : 'pedidos'})</span>
          <strong className="cifras">{formatearPesos(datos.totalSaldo)}</strong>
        </p>
      )}
      <Paginacion pagina={datos.pagina} totalPaginas={datos.totalPaginas} alCambiar={alCambiarPagina} etiqueta="Páginas de pedidos por cobrar" />
    </>
  );
}
