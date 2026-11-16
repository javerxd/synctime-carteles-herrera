import { CalendarRange, History, Minus, TrendingDown, TrendingUp, TriangleAlert } from 'lucide-react';
import { Boton } from '../../componentes/ui';
import { textoComparacion } from './reglasIndicadores';
import { formatearNumero, formatearPorcentaje, formatearValor, formatearVariacion, sentidoDe, TARJETAS } from './formatoIndicadores';
import estilos from './Indicadores.module.css';

const META_A_TIEMPO = 90;

/** La variación con flecha, signo y texto (no solo color). Subir es bueno salvo en el saldo por cobrar. */
export function Variacion({ variacion, tipo, comparacion, subirEsBueno = true }) {
  if (!comparacion) return null;
  const texto = formatearVariacion(variacion, tipo);
  if (texto === null) return <p className={estilos.variacion}>Sin base para comparar con {comparacion.etiqueta}</p>;
  const sentido = sentidoDe(variacion, subirEsBueno);
  const Flecha = variacion > 0 ? TrendingUp : variacion < 0 ? TrendingDown : Minus;
  return (
    <p className={[estilos.variacion, estilos[sentido]].join(' ')}>
      <Flecha size={16} strokeWidth={2} aria-hidden="true" />
      <span><strong className="cifras">{texto}</strong> {textoComparacion(comparacion)}</span>
    </p>
  );
}

function detalleDe(t) {
  if (t.clave === 'origen') return `${formatearNumero(t.valor ?? 0)} en línea · ${formatearNumero(t.detalle.presenciales)} presenciales`
    + (t.detalle.porcentajeEnLinea === null ? '' : ` (${formatearPorcentaje(t.detalle.porcentajeEnLinea)} en línea)`);
  if (t.clave === 'concretadas') return `De ${formatearNumero(t.detalle.solicitudesEnLinea)} solicitudes en línea`;
  return null;
}

function Tarjeta({ t, comparacion }) {
  const { nombre, formula } = TARJETAS[t.clave];
  const detalle = detalleDe(t);
  return (
    <li className={estilos.tarjeta}>
      <span className={estilos.nombreTarjeta}>{nombre}</span>
      <span className={estilos.valorTarjeta}>{t.clave === 'origen' ? formatearNumero(t.valor) : formatearValor(t.valor, t.unidad)}</span>
      {t.clave === 'origen' && <span className={estilos.unidadTarjeta}>pedidos en línea</span>}
      {detalle && <span className={estilos.detalleTarjeta}>{detalle}</span>}
      {t.clave === 'entregasATiempo' && t.valor !== null && (
        <span className={estilos.meta}>
          <span className={estilos.pistaMeta} aria-hidden="true">
            <span className={estilos.rellenoMeta} style={{ width: `${Math.min(t.valor, 100)}%` }} />
            <span className={estilos.marcaMeta} style={{ left: `${META_A_TIEMPO}%` }} />
          </span>
          <span className={estilos.textoMeta}>Meta: {META_A_TIEMPO} % {t.valor >= META_A_TIEMPO ? '(cumplida)' : '(no cumplida)'}</span>
        </span>
      )}
      <Variacion variacion={t.variacion} tipo={t.tipoVariacion} comparacion={comparacion} />
      <span className={estilos.formula}>{formula}</span>
    </li>
  );
}

/**
 * Saldo por cobrar (S24), destacado como alerta. Dice siempre a qué corresponde: al período filtrado o a toda la
 * deuda vigente del taller (sin límite de fecha), y permite pasar de uno a otro con un clic para no ocultar cobros
 * antiguos.
 */
export function TarjetaSaldo({ saldo, periodo, comparacion, alCambiarAlcance }) {
  const vigente = saldo.alcance === 'vigente';
  return (
    <div className={estilos.tarjetaSaldo}>
      <div className={estilos.cabeceraSaldo}>
        <TriangleAlert size={20} strokeWidth={1.75} aria-hidden="true" className={estilos.iconoSaldo} />
        <span className={estilos.nombreTarjeta}>Saldo por cobrar</span>
      </div>
      <p className={estilos.alcance}>
        {vigente ? <History size={16} strokeWidth={1.75} aria-hidden="true" /> : <CalendarRange size={16} strokeWidth={1.75} aria-hidden="true" />}
        <span><strong>{vigente ? 'Toda la deuda vigente' : 'Saldo del período'}</strong>{' · '}{vigente ? 'sin límite de fecha, incluye cobros antiguos' : periodo.etiqueta}</span>
      </p>
      <span className={estilos.valorSaldo}>{formatearValor(saldo.valor, 'pesos')}</span>
      <span className={estilos.detalleTarjeta}>En {formatearNumero(saldo.cantidad)} {saldo.cantidad === 1 ? 'pedido no anulado' : 'pedidos no anulados'} en «no pagado» o «abonado»</span>
      {!vigente && <Variacion variacion={saldo.variacion} tipo="porcentaje" comparacion={comparacion} subirEsBueno={false} />}
      <Boton variante="secundario" tamano="compacto" iconoInicio={vigente ? CalendarRange : History}
        onClick={() => alCambiarAlcance(vigente ? 'periodo' : 'vigente')} className={estilos.botonAlcance}>
        {vigente ? 'Ver solo el período' : 'Ver toda la deuda vigente'}
      </Boton>
    </div>
  );
}

export function TarjetasResumen({ datos, alCambiarAlcance }) {
  return (
    <div className={estilos.resumen}>
      <TarjetaSaldo saldo={datos.saldo} periodo={datos.periodo} comparacion={datos.comparacion} alCambiarAlcance={alCambiarAlcance} />
      <ul className={estilos.tarjetas}>
        {datos.tarjetas.map((t) => <Tarjeta key={t.clave} t={t} comparacion={datos.comparacion} />)}
      </ul>
    </div>
  );
}
