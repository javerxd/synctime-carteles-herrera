import { Link } from 'react-router-dom';
import { Aviso, Esqueleto, EstadoCartel, Insignia, Tarjeta } from '../../componentes/ui';
import { panel } from '../../api/panel';
import { publico } from '../../api/publico';
import { menuPara, GESTION } from '../../estructuras/navegacion';
import { useSesion } from '../../sesion/sesion';
import { formatearPesos } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { esPorCobrar } from './reglasPedidos';
import estilos from './Panel.module.css';

const DESCRIPCIONES = {
  '/panel/solicitudes': 'Bandeja de solicitudes por cotizar.',
  '/panel/cotizaciones': 'Cotizaciones enviadas y por aprobar.',
  '/panel/pedidos': 'Estados, cobranza y fichas de los pedidos.',
  '/panel/calendario': 'Entregas por día, pendientes y entregadas.',
  '/panel/clientes': 'Búsqueda por RUT o teléfono.',
  '/panel/local': 'Cartel abierto o cerrado, horario y código QR.',
  '/panel/indicadores': 'Pedidos, ingresos, cobranza y conversión.',
  '/panel/reportes': 'Reportes operativo, gerencial y parametrizable.',
  '/panel/mantenedores': 'Catálogo, materiales y categorías.',
  '/panel/perfil': 'Tus datos y el acceso con rostro.',
};

function indicadoresDelTaller(pedidos) {
  const hoy = new Date();
  const enSieteDias = new Date(hoy.getTime() + 7 * 86400000);
  const abiertos = pedidos.filter((p) => !['entregado', 'anulado'].includes(p.estadoPedido));
  return {
    enProceso: pedidos.filter((p) => p.estadoPedido === 'en_proceso').length,
    proximas: abiertos.filter((p) => new Date(p.fechaEntregaEstimada) <= enSieteDias && new Date(p.fechaEntregaEstimada) >= hoy).length,
    atrasadas: abiertos.filter((p) => new Date(p.fechaEntregaEstimada) < hoy).length,
    porCobrar: pedidos.filter(esPorCobrar).reduce((s, p) => s + Number(p.saldoPendiente ?? 0), 0),
  };
}

/** Inicio del panel: resumen del día y el menú general filtrado por rol. */
export default function InicioPanel() {
  const { usuario, tieneRol } = useSesion();
  const pedidos = useConsulta('pedidos', panel.pedidos);
  const cartel = useConsulta('cartel', publico.cartel);
  const esGestion = tieneRol(GESTION);
  const datos = pedidos.datos ? indicadoresDelTaller(pedidos.datos) : null;

  const tarjetas = [
    { nombre: 'Pedidos en proceso', valor: datos?.enProceso },
    { nombre: 'Entregas en los próximos 7 días', valor: datos?.proximas },
    { nombre: 'Entregas atrasadas', valor: datos?.atrasadas, aviso: datos?.atrasadas > 0 },
    ...(esGestion ? [{ nombre: 'Saldo por cobrar', valor: datos && formatearPesos(datos.porCobrar) }] : []),
  ];

  return (
    <div className={estilos.pagina}>
      <div>
        <h1 className={estilos.titulo}>Hola, {usuario?.nombre}</h1>
        <p className={estilos.bajada}>Resumen del taller con los datos de la base de demostración.</p>
      </div>

      <section aria-labelledby="titulo-resumen">
        <h2 id="titulo-resumen" className={estilos.subtitulo}>Hoy en el taller</h2>
        {pedidos.error && <Aviso tono="peligro" titulo="No pudimos cargar el resumen">{pedidos.error.message}</Aviso>}
        <ul className={estilos.indicadores}>
          {tarjetas.map((t) => (
            <li key={t.nombre}>
              <Tarjeta className={estilos.indicador}>
                <span className={estilos.indicadorNombre}>{t.nombre}</span>
                {pedidos.cargando ? <Esqueleto alto="2rem" ancho="5rem" forma="bloque" /> : <span className={estilos.indicadorValor}>{t.valor ?? '—'}</span>}
                {t.aviso && <Insignia tono="aviso">Revisar en Pedidos</Insignia>}
              </Tarjeta>
            </li>
          ))}
          <li>
            <Tarjeta className={estilos.indicador}>
              <span className={estilos.indicadorNombre}>Cartel del local</span>
              {cartel.cargando && <Esqueleto alto="2rem" forma="bloque" />}
              {cartel.datos && <EstadoCartel abierto={cartel.datos.abiertoEnLocal} />}
              <Link to="/panel/local" className={estilos.textoSeccion}>Cambiar el estado del local</Link>
            </Tarjeta>
          </li>
        </ul>
      </section>

      <section aria-labelledby="titulo-menu">
        <h2 id="titulo-menu" className={estilos.subtitulo}>Menú general</h2>
        {menuPara(tieneRol).map((grupo) => (
          <div key={grupo.grupo} className={estilos.pagina}>
            <h3 className={estilos.indicadorNombre}>{grupo.grupo}</h3>
            <ul className={estilos.secciones}>
              {grupo.entradas.filter((e) => e.ruta !== '/panel').map(({ ruta, etiqueta, icono: Icono, fase }) => (
                <li key={ruta}>
                  <Tarjeta interactiva className={estilos.seccionMenu}>
                    <Icono size={22} strokeWidth={1.5} aria-hidden="true" className={estilos.iconoSeccion} />
                    <Link to={ruta}>{etiqueta}</Link>
                    <p className={estilos.textoSeccion}>{DESCRIPCIONES[ruta]}</p>
                    {fase && <Insignia tono="neutro">Llega en la fase {fase}</Insignia>}
                  </Tarjeta>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </section>
    </div>
  );
}
