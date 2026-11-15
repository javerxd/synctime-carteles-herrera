import { useEffect, useRef } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { CalendarDays, ChevronLeft, ChevronRight } from 'lucide-react';
import { Aviso, Boton, Esqueleto, EstadoEntrega, EstadoVacio, ESTADOS_ENTREGA } from '../../componentes/ui';
import { pedidos } from '../../api/pedidos';
import { unirClases } from '../../componentes/ui/clases';
import { DIAS_SEMANA, fechaLarga, nombreMesAnio } from '../../utilidades/fechas';
import { useHoyChile } from '../../utilidades/useHoyChile';
import { useConsulta } from '../../utilidades/useConsulta';
import {
  agruparPorDia, construirMes, contarMes, estadoEntrega, mesDeFecha, mesDesdeTexto, moverMes, rangoCuadricula, textoMes,
} from './reglasCalendario';
import { MODALIDADES, MODALIDADES_CORTAS, nombreDe, numeroPedido } from './reglasPedido';
import estilosPanel from '../panel/Panel.module.css';
import estilos from './Pedidos.module.css';

const ESTILO_ENTREGA = { pendiente: estilos.entregaPendiente, entregado: estilos.entregaEntregado, atrasado: estilos.entregaAtrasado };

/** Un pedido del día: enlace a su ficha con el estado de la entrega (color, ícono y texto), el cliente y la modalidad. */
function PedidoDelDia({ pedido }) {
  const estado = estadoEntrega(pedido);
  const { icono: Icono, texto } = ESTADOS_ENTREGA[estado];
  return (
    <li>
      <Link to={`/panel/pedidos/${pedido.idPedido}`} className={unirClases(estilos.entrega, ESTILO_ENTREGA[estado])}>
        <span className={estilos.entregaEstado}><Icono size={14} strokeWidth={2} aria-hidden="true" />{texto}</span>
        <span className={estilos.entregaCliente}><span className="cifras">{numeroPedido(pedido)}</span> · {nombreDe(pedido.cliente)}</span>
        <span className={estilos.entregaModalidad}>
          <span aria-hidden="true">{MODALIDADES_CORTAS[pedido.modalidadEntrega] ?? ''}</span>
          <span className="solo-lectores">{MODALIDADES[pedido.modalidadEntrega] ?? ''}</span>
        </span>
      </Link>
    </li>
  );
}

/**
 * /panel/calendario (S08): las entregas del mes. Desde 768 px es una cuadrícula de lunes a domingo; en el
 * teléfono, una lista con solo los días que tienen entregas. Los pendientes se ubican en su fecha estimada y
 * los entregados en la real; pendiente, entregado y atrasado se distinguen con color, ícono y texto. El mes
 * queda en la dirección (?mes=2026-11) y cada pedido abre su ficha.
 */
export default function CalendarioEntregas() {
  const [parametros, setParametros] = useSearchParams();
  const hoy = useHoyChile();
  const mesHoy = mesDeFecha(hoy);
  const elegido = mesDesdeTexto(parametros.get('mes')) ?? mesHoy;
  const rango = rangoCuadricula(elegido);
  // Al cambiar de mes se sigue viendo la cuadrícula (atenuada) hasta que llegan las entregas del nuevo.
  const consulta = useConsulta(`calendario:${rango.desde}:${rango.hasta}`, (senal) => pedidos.calendario(rango, { senal }), { conservar: true });
  const actualizando = consulta.recargando;
  const refTitulo = useRef(null);
  const semanas = construirMes(elegido, hoy);
  const porDia = agruparPorDia(consulta.datos ?? []);
  const cuenta = contarMes(consulta.datos ?? [], elegido);
  const totalMes = cuenta.pendiente + cuenta.entregado + cuenta.atrasado;
  const esMesActual = elegido.anio === mesHoy.anio && elegido.mes === mesHoy.mes;

  // Un mes mal escrito en la dirección se quita en silencio.
  const malo = parametros.has('mes') && !mesDesdeTexto(parametros.get('mes'));
  useEffect(() => {
    if (malo) setParametros({}, { replace: true });
  }, [malo, setParametros]);

  function ir(mes) {
    const esHoy = mes.anio === mesHoy.anio && mes.mes === mesHoy.mes;
    setParametros(esHoy ? {} : { mes: textoMes(mes) }, { replace: true });
  }

  const titulo = nombreMesAnio(elegido.anio, elegido.mes);
  const tituloCapital = titulo.charAt(0).toUpperCase() + titulo.slice(1);

  return (
    <div className={estilosPanel.pagina}>
      <div>
        <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>Calendario de entregas</h1>
        <p className={estilosPanel.bajada}>Los pendientes en su fecha estimada y los entregados en la fecha real. Toca un pedido para abrir su ficha.</p>
      </div>

      <div className={estilos.barraCalendario}>
        <h2 className={estilos.mesCalendario}>{tituloCapital}</h2>
        <div className={estilos.navegacionMes} role="group" aria-label="Cambiar de mes">
          <Boton variante="secundario" iconoInicio={ChevronLeft} onClick={() => ir(moverMes(elegido, -1))}
            aria-label={`Mes anterior: ${nombreMesAnio(moverMes(elegido, -1).anio, moverMes(elegido, -1).mes)}`}>
            <span className={estilos.textoBotonMes}>Anterior</span>
          </Boton>
          <Boton variante="secundario" onClick={() => !esMesActual && ir(mesHoy)} aria-disabled={esMesActual ? 'true' : undefined}>Hoy</Boton>
          <Boton variante="secundario" iconoFin={ChevronRight} onClick={() => ir(moverMes(elegido, 1))}
            aria-label={`Mes siguiente: ${nombreMesAnio(moverMes(elegido, 1).anio, moverMes(elegido, 1).mes)}`}>
            <span className={estilos.textoBotonMes}>Siguiente</span>
          </Boton>
        </div>
      </div>

      <ul className={estilos.leyenda} aria-label="Qué significa cada estado">
        {Object.entries(cuenta).map(([estado, n]) => (
          <li key={estado}>
            <EstadoEntrega estado={estado} />
            <span className={estilos.cuentaLeyenda}>
              <span className="cifras">{consulta.datos && !actualizando ? n : '—'}</span>
              <span className="solo-lectores"> en {titulo}</span>
            </span>
          </li>
        ))}
      </ul>

      {consulta.error && (
        <Aviso tono="peligro" titulo="No pudimos cargar las entregas"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {consulta.error.message}
        </Aviso>
      )}
      {/* Una sola región que existe desde el inicio y cambia su texto: «Cargando…» y luego el mes con su cuenta. */}
      <p role="status" className="solo-lectores">
        {consulta.cargando || actualizando
          ? `Cargando las entregas de ${titulo}…`
          : consulta.datos ? `${tituloCapital}: ${totalMes} ${totalMes === 1 ? 'entrega' : 'entregas'}.` : ''}
      </p>
      {consulta.cargando && <Esqueleto forma="bloque" alto="24rem" />}

      {consulta.datos && (
        <>
          {totalMes === 0 && !actualizando && (
            <EstadoVacio icono={CalendarDays} titulo={`No hay entregas en ${titulo}`}
              descripcion="Cuando un pedido tenga su fecha estimada en este mes, aparecerá aquí." className={estilos.vacioMes} />
          )}
          <div className={unirClases(estilos.calendario, totalMes === 0 && !actualizando && estilos.calendarioVacio, actualizando && estilos.actualizando)}
            aria-busy={actualizando ? 'true' : undefined}>
            <div className={estilos.diasSemana} aria-hidden="true">
              {DIAS_SEMANA.map((d) => <span key={d}>{d.slice(0, 3)}</span>)}
            </div>
            <ol className={estilos.cuadricula} aria-label={`Entregas de ${titulo}`}>
              {semanas.flat().map((dia) => {
                const lista = porDia[dia.fecha] ?? [];
                return (
                  <li key={dia.fecha}
                    className={unirClases(estilos.dia, !dia.enMes && estilos.fueraDeMes, dia.esHoy && estilos.hoy, dia.finDeSemana && estilos.finDeSemana, !lista.length && estilos.diaSinEntregas)}
                    aria-current={dia.esHoy ? 'date' : undefined}>
                    <h3 className={estilos.numeroDia}>
                      <span aria-hidden="true" className={estilos.diaCorto}>{dia.dia}</span>
                      <span className={estilos.diaLargo}>{fechaLarga(dia.fecha)}</span>
                      {dia.esHoy && <span className={estilos.marcaHoy}>Hoy</span>}
                      {lista.length > 0 && <span className="solo-lectores">, {lista.length} {lista.length === 1 ? 'entrega' : 'entregas'}</span>}
                    </h3>
                    {lista.length > 0 && <ul className={estilos.entregas}>{lista.map((p) => <PedidoDelDia key={p.idPedido} pedido={p} />)}</ul>}
                  </li>
                );
              })}
            </ol>
          </div>
        </>
      )}
    </div>
  );
}
