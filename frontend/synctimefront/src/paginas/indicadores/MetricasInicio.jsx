import { Link } from 'react-router-dom';
import { ArrowRight, RotateCcw } from 'lucide-react';
import { Aviso, Boton, Esqueleto, Insignia } from '../../componentes/ui';
import { ES_DEMO, indicadores } from '../../api/indicadores';
import { useConsulta } from '../../utilidades/useConsulta';
import { conMayuscula, formatearValor, TARJETAS } from './formatoIndicadores';
import { Variacion } from './TarjetasResumen';
import estilos from './Indicadores.module.css';

const FILTROS = { periodo: 'mes', comparar: 'anterior', alcanceSaldo: 'vigente' };
const CLAVES = ['ingresos', 'pedidos', 'conversion'];

/**
 * Inicio del dueño y del administrador (fase 12A): el saldo por cobrar de toda la deuda vigente y los ingresos,
 * los pedidos y la conversión del mes, con un enlace al dashboard.
 */
export function MetricasInicio() {
  const consulta = useConsulta('inicio:indicadores', (senal) => indicadores.resumen(FILTROS, { senal }));
  const datos = consulta.datos;

  return (
    <section aria-labelledby="titulo-metricas" className={estilos.bloque}>
      <header className={estilos.cabeceraBloque}>
        <div className={estilos.textosBloque}>
          <h2 id="titulo-metricas" className={estilos.tituloBloque}>Indicadores del mes</h2>
          {datos?.periodo && <p className={estilos.descripcionBloque}>{conMayuscula(datos.periodo.etiqueta)}{datos.comparacion ? `, comparado con ${datos.comparacion.etiqueta}` : ''}.</p>}
        </div>
        <div className={estilos.controlesBloque}>
          {ES_DEMO && <Insignia tono="info">Datos de demostración</Insignia>}
          <Boton como={Link} to="/panel/indicadores" variante="secundario" tamano="compacto" iconoFin={ArrowRight}>Ver el dashboard</Boton>
        </div>
      </header>
      {consulta.error && (
        <Aviso tono="peligro" titulo="No pudimos cargar los indicadores"
          accion={<Boton variante="secundario" tamano="compacto" iconoInicio={RotateCcw} onClick={consulta.recargar}>Reintentar</Boton>} />
      )}
      {consulta.cargando && <Esqueleto forma="bloque" alto="7rem" />}
      {datos && (
        <ul className={estilos.metricasInicio}>
          <li className={estilos.tarjetaSaldo}>
            <span className={estilos.nombreTarjeta}>Saldo por cobrar</span>
            <span className={estilos.valorTarjeta}>{formatearValor(datos.saldo.valor, 'pesos')}</span>
            <span className={estilos.detalleTarjeta}>Toda la deuda vigente, sin límite de fecha</span>
          </li>
          {CLAVES.map((clave) => datos.tarjetas?.find((x) => x.clave === clave)).filter(Boolean).map((t) => {
            const { clave } = t;
            return (
              <li key={clave} className={estilos.tarjeta}>
                <span className={estilos.nombreTarjeta}>{TARJETAS[clave].nombre}</span>
                <span className={estilos.valorTarjeta}>{formatearValor(t.valor, t.unidad)}</span>
                <Variacion variacion={t.variacion} tipo={t.tipoVariacion} comparacion={datos.comparacion} />
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
