import { useId, useRef, useState } from 'react';
import { CalendarSearch } from 'lucide-react';
import { BarraFiltros, Boton, Campo, Chip, Opciones, TEXTOS_ESTADO_PAGO, TEXTOS_ESTADO_PEDIDO } from '../../componentes/ui';
import { esFecha } from '../../utilidades/fechas';
import { nombreMes } from '../../utilidades/formato';
import {
  COMPARACIONES, ESTADOS_PAGO_FILTRO, ESTADOS_PEDIDO_FILTRO, FILTROS_DE_DATOS, ORIGENES, PERIODOS,
} from './reglasIndicadores';
import estilos from './Indicadores.module.css';

const SIN_FECHAS = { desde: '', hasta: '', anio: '', mes: '' };
const alternar = (lista, valor) => (lista.includes(valor) ? lista.filter((v) => v !== valor) : [...lista, valor]);
const listaDe = (texto) => (texto ? texto.split(',') : []);

/**
 * Cliente buscable: un solo campo con la lista de clientes (datalist). El filtro se aplica al ELEGIR de la lista (o
 * al pulsar Enter con un nombre exacto), no en cada tecla, y el foco se queda en el campo. Con un cliente aplicado,
 * el campo muestra su nombre y el botón «Quitar» lo borra. Si el cliente de la dirección no está en el catálogo,
 * se muestra igual («Cliente N.º 9») para que siempre se pueda quitar.
 */
function FiltroCliente({ clientes, valor, alCambiar }) {
  const id = useId();
  const refCampo = useRef(null);
  const elegido = valor ? clientes.find((c) => String(c.idCliente) === valor) ?? { idCliente: valor, nombre: `Cliente N.º ${valor}` } : null;
  const [texto, setTexto] = useState(elegido?.nombre ?? '');
  const [visto, setVisto] = useState(elegido?.nombre ?? '');
  if ((elegido?.nombre ?? '') !== visto) {
    setVisto(elegido?.nombre ?? '');
    setTexto(elegido?.nombre ?? '');
  }
  const buscar = (nombre) => clientes.find((c) => c.nombre.toLowerCase() === nombre.trim().toLowerCase());
  const aplicar = (nombre) => {
    const encontrado = buscar(nombre);
    if (encontrado && String(encontrado.idCliente) !== valor) alCambiar(String(encontrado.idCliente));
  };
  return (
    <div className={estilos.filtroCliente}>
      <Campo ref={refCampo} etiqueta="Cliente" type="search" list={`${id}-clientes`} value={texto} autoComplete="off"
        ayuda="Escribe parte del nombre y elígelo de la lista."
        onChange={(e) => {
          setTexto(e.target.value);
          // Elegir una opción del datalist llega como «insertReplacementText» (o sin tipo, según el navegador).
          const tipo = e.nativeEvent?.inputType;
          if (tipo === 'insertReplacementText' || tipo === undefined) aplicar(e.target.value);
          if (!e.target.value && valor) alCambiar('');
        }}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.preventDefault();
            aplicar(texto);
          }
        }} />
      <datalist id={`${id}-clientes`}>
        {clientes.map((c) => <option key={c.idCliente} value={c.nombre} />)}
      </datalist>
      {elegido && (
        <Boton variante="fantasma" tamano="compacto" onClick={() => { alCambiar(''); refCampo.current?.focus(); }} aria-label={`Quitar el filtro por cliente: ${elegido.nombre}`}>
          Quitar
        </Boton>
      )}
    </div>
  );
}

/** Rango propio: se aplica con el botón, cuando las dos fechas son válidas y están en orden. */
function RangoPropio({ filtros, alAplicar }) {
  const [desde, setDesde] = useState(filtros.desde);
  const [hasta, setHasta] = useState(filtros.hasta);
  const [visto, setVisto] = useState(`${filtros.desde}|${filtros.hasta}`);
  if (`${filtros.desde}|${filtros.hasta}` !== visto) {
    setVisto(`${filtros.desde}|${filtros.hasta}`);
    setDesde(filtros.desde);
    setHasta(filtros.hasta);
  }
  const valido = esFecha(desde) && esFecha(hasta) && desde <= hasta;
  const error = esFecha(desde) && esFecha(hasta) && desde > hasta ? '«Hasta» debe ser igual o posterior a «Desde».' : undefined;
  const idAyuda = useId();
  return (
    <fieldset className={estilos.rango}>
      <legend className={estilos.etiquetaFiltro}>Rango propio</legend>
      <p id={idAyuda} className={estilos.ayudaRango}>Completa «Desde» y «Hasta» y pulsa «Aplicar rango».</p>
      <div className={estilos.camposRango}>
        <Campo etiqueta="Desde" type="date" value={desde} onChange={(e) => setDesde(e.target.value)} />
        <Campo etiqueta="Hasta" type="date" value={hasta} onChange={(e) => setHasta(e.target.value)} error={error} />
        <Boton variante="secundario" iconoInicio={CalendarSearch} disabled={!valido} aria-describedby={idAyuda} onClick={() => alAplicar({ ...SIN_FECHAS, desde, hasta })}
          className={estilos.botonRango}>
          Aplicar rango
        </Boton>
      </div>
    </fieldset>
  );
}

/**
 * Barra de filtros combinables del dashboard (RFS09, RFU19): período (semana, mes o año), rango propio, acceso
 * por año y mes, comparación, estado del pedido (varios), estado de pago (varios, o el acceso rápido «Por
 * cobrar»), categoría, origen y cliente. Todo vive en la dirección.
 */
export function FiltrosIndicadores({ filtros, cambiar, limpiar, catalogos, periodo, comparacion, anios }) {
  const estadosPedido = listaDe(filtros.estadoPedido);
  const porCobrar = filtros.estadoPago === 'por_cobrar';
  const estadosPago = porCobrar ? [] : listaDe(filtros.estadoPago);
  const aplicados = FILTROS_DE_DATOS.filter((c) => filtros[c]).length;
  const fechaPropia = Boolean(filtros.desde || filtros.anio);
  const hayFiltros = aplicados > 0 || fechaPropia || filtros.periodo !== 'mes' || filtros.comparar !== 'anterior' || filtros.alcanceSaldo !== 'periodo' || Boolean(filtros.cobro);
  const resumen = periodo ? `Mostrando ${periodo.etiqueta}${comparacion ? `, comparado con ${comparacion.etiqueta}` : ''}${aplicados ? ` · ${aplicados} ${aplicados === 1 ? 'filtro' : 'filtros'} de pedidos` : ''}.` : '';

  return (
    <BarraFiltros titulo="Filtros de indicadores" resumen={resumen} hayFiltros={hayFiltros} alLimpiar={limpiar}>
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
        <Campo como="select" etiqueta="Comparar con" value={filtros.comparar} onChange={(e) => cambiar({ comparar: e.target.value })}>
          {Object.entries(COMPARACIONES).map(([valor, texto]) => <option key={valor} value={valor}>{texto}</option>)}
        </Campo>
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
            <Campo como="select" etiqueta="Categoría" value={filtros.categoria} onChange={(e) => cambiar({ categoria: e.target.value })}>
              <option value="">Todas</option>
              {filtros.categoria && !catalogos.categorias.some((c) => String(c.idCategoria) === filtros.categoria) && (
                <option value={filtros.categoria}>Categoría N.º {filtros.categoria}</option>
              )}
              {catalogos.categorias.map((c) => <option key={c.idCategoria} value={c.idCategoria}>{c.nombre}</option>)}
            </Campo>
            <Campo como="select" etiqueta="Origen" value={filtros.origen} onChange={(e) => cambiar({ origen: e.target.value })}>
              <option value="">En línea y presencial</option>
              {Object.entries(ORIGENES).map(([valor, texto]) => <option key={valor} value={valor}>{texto}</option>)}
            </Campo>
            <FiltroCliente clientes={catalogos.clientes} valor={filtros.cliente} alCambiar={(cliente) => cambiar({ cliente })} />
          </div>
        </div>
      </details>
    </BarraFiltros>
  );
}
