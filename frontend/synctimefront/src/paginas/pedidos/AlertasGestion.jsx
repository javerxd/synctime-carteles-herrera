import { Link } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';
import { Aviso, Boton, Esqueleto, EstadoEntrega, Tarjeta } from '../../componentes/ui';
import { pedidos } from '../../api/pedidos';
import { formatearFecha, formatearPesos } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { estadoEntrega } from './reglasCalendario';
import { MODALIDADES_CORTAS, nombreDe, numeroPedido } from './reglasPedido';
import estilos from './Pedidos.module.css';

function ListaAlerta({ id, titulo, pedidos: lista, vacio, enlace, textoEnlace }) {
  return (
    <section aria-labelledby={id} className={estilos.bloqueAlerta}>
      <div className={estilos.cabeceraAlerta}>
        <h3 id={id} className={estilos.tituloAlerta}>{titulo} <span className={estilos.cuentaAlerta}>({lista.length})</span></h3>
        <Link to={enlace} className={estilos.verMas}>{textoEnlace}<ArrowRight size={16} aria-hidden="true" /></Link>
      </div>
      {lista.length === 0 ? <p className={estilos.nota}>{vacio}</p> : (
        <ul className={estilos.listaAlerta}>
          {lista.map((p) => (
            <li key={p.idPedido}>
              <Link to={`/panel/pedidos/${p.idPedido}`} className={estilos.enlaceAlerta}>
                <span className="cifras">{numeroPedido(p)}</span> · {nombreDe(p.cliente)}
              </Link>
              <span className={estilos.metaAlerta}>
                <span className="cifras">{formatearFecha(p.fecha)}</span>
                {MODALIDADES_CORTAS[p.modalidadEntrega] ? ` · ${MODALIDADES_CORTAS[p.modalidadEntrega]}` : ''}
              </span>
              <EstadoEntrega estado={estadoEntrega(p)} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/**
 * Alertas para gestión en el inicio del panel (dueño y administrador): las entregas de hoy a 3 días, las
 * atrasadas y el total por cobrar, cada una con su enlace.
 */
export function AlertasGestion() {
  const consulta = useConsulta('alertas-pedidos', pedidos.alertas);
  const datos = consulta.datos;
  return (
    <Tarjeta como="section" aria-labelledby="titulo-alertas" className={estilos.alertas}>
      <h2 id="titulo-alertas" className={estilos.tituloAlertas}>Entregas y cobranza</h2>
      {consulta.cargando && (
        <>
          <p role="status" className="solo-lectores">Cargando las alertas…</p>
          <Esqueleto lineas={4} />
        </>
      )}
      {consulta.error && (
        <Aviso tono="peligro" titulo="No pudimos cargar las alertas"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {consulta.error.message}
        </Aviso>
      )}
      {datos && (
        <div className={estilos.rejillaAlertas}>
          <ListaAlerta id="titulo-atrasadas" titulo="Atrasadas" pedidos={datos.atrasadas ?? []} vacio="No hay entregas atrasadas."
            enlace="/panel/calendario" textoEnlace="Ver calendario" />
          <ListaAlerta id="titulo-proximas" titulo="Próximos 3 días" pedidos={datos.proximas ?? []} vacio="No hay entregas de hoy a 3 días."
            enlace="/panel/calendario" textoEnlace="Ver calendario" />
          <section aria-labelledby="titulo-cobrar" className={estilos.bloqueAlerta}>
            <div className={estilos.cabeceraAlerta}>
              <h3 id="titulo-cobrar" className={estilos.tituloAlerta}>Por cobrar</h3>
              <Link to="/panel/pedidos?cobranza=true" className={estilos.verMas}>Ver cobranza<ArrowRight size={16} aria-hidden="true" /></Link>
            </div>
            <p className={estilos.montoAlerta}><span className="cifras">{formatearPesos(datos.totalPorCobrar)}</span></p>
            <p className={estilos.nota}>
              En <span className="cifras">{datos.cantidadPorCobrar ?? 0}</span> {datos.cantidadPorCobrar === 1 ? 'pedido no anulado' : 'pedidos no anulados'} sin pagar del todo.
            </p>
          </section>
        </div>
      )}
    </Tarjeta>
  );
}
