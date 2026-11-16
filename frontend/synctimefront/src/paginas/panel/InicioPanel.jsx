import { Link } from 'react-router-dom';
import { Esqueleto, EstadoCartel, Insignia, Tarjeta } from '../../componentes/ui';
import { cotizaciones } from '../../api/cotizaciones';
import { pedidos } from '../../api/pedidos';
import { publico } from '../../api/publico';
import { menuPara, GESTION } from '../../estructuras/navegacion';
import { useSesion } from '../../sesion/sesion';
import { useConsulta } from '../../utilidades/useConsulta';
import { AvisoCorreosNoEnviados } from '../correos/AvisoCorreosNoEnviados';
import { MetricasInicio } from '../indicadores/MetricasInicio';
import { AlertasGestion } from '../pedidos/AlertasGestion';
import { SolicitudesPorAtender } from '../solicitudes/panel/SolicitudesPorAtender';
import estilos from './Panel.module.css';

const DESCRIPCIONES = {
  '/panel/solicitudes': 'Bandeja de solicitudes, con filtros y comprobante.',
  '/panel/cotizaciones': 'Cotizaciones enviadas y por aprobar.',
  '/panel/pedidos': 'Estados, cobranza y fichas de los pedidos.',
  '/panel/calendario': 'Entregas por día, pendientes y entregadas.',
  '/panel/clientes': 'Listado y ficha, con búsqueda por RUT o teléfono.',
  '/panel/local': 'Cartel abierto o cerrado, horario y código QR.',
  '/panel/indicadores': 'Dashboard: ingresos, pedidos, cobranza y conversión.',
  '/panel/reportes': 'Reportes operativo, gerencial y parametrizable.',
  '/panel/mantenedores': 'Catálogo, materiales y categorías.',
  '/panel/rostros': 'Quién entra con su rostro y la calibración.',
  '/panel/correos-no-enviados': 'Correos que no salieron, para reenviarlos.',
  '/panel/auditoria': 'Quién cambió qué y cuándo, y los consentimientos.',
  '/panel/perfil': 'Tus datos y el acceso con rostro.',
};

/** Cuántos hay de algo: la API lo cuenta (total del listado), sin traer las filas. */
const contar = (listar, filtros) => () => listar({ ...filtros, pagina: 1, tamano: 1 }).then((r) => r?.total ?? 0);

const INDICADORES = [
  { clave: 'en-proceso', nombre: 'Pedidos en proceso', ruta: '/panel/pedidos?estadoPedido=en_proceso', consulta: contar(pedidos.listar, { estadoPedido: 'en_proceso' }) },
  { clave: 'terminados', nombre: 'Terminados por entregar', ruta: '/panel/pedidos?estadoPedido=terminado', consulta: contar(pedidos.listar, { estadoPedido: 'terminado' }) },
  { clave: 'por-responder', nombre: 'Cotizaciones por responder', ruta: '/panel/cotizaciones?estado=pendiente', consulta: contar(cotizaciones.listar, { estado: 'pendiente' }) },
];

function Indicador({ nombre, ruta, consulta: consultar, clave }) {
  const consulta = useConsulta(`inicio:${clave}`, consultar);
  return (
    <Tarjeta interactiva className={estilos.indicador}>
      <Link to={ruta} className={estilos.enlaceIndicador}>{nombre}</Link>
      {consulta.cargando && <Esqueleto alto="2rem" ancho="4rem" forma="bloque" />}
      {consulta.error && <span className={estilos.indicadorFormula}>No pudimos contarlos</span>}
      {consulta.datos !== undefined && <span className={estilos.indicadorValor}>{consulta.datos}</span>}
    </Tarjeta>
  );
}

/**
 * Inicio del panel: las solicitudes por atender, las alertas de entrega y cobranza (gestión), el resumen del día
 * contado en la API y el menú general filtrado por rol.
 */
export default function InicioPanel() {
  const { usuario, tieneRol } = useSesion();
  const cartel = useConsulta('cartel', publico.cartel);
  const esGestion = tieneRol(GESTION);

  return (
    <div className={estilos.pagina}>
      <div>
        <h1 className={estilos.titulo}>Hola, {usuario?.nombre}</h1>
        <p className={estilos.bajada}>Resumen del taller con los datos de la base de demostración.</p>
      </div>

      {esGestion && <AvisoCorreosNoEnviados />}

      <SolicitudesPorAtender />

      {esGestion && <MetricasInicio />}

      {esGestion && <AlertasGestion />}

      <section aria-labelledby="titulo-resumen">
        <h2 id="titulo-resumen" className={estilos.subtitulo}>Hoy en el taller</h2>
        <ul className={estilos.indicadores}>
          {INDICADORES.map((i) => <li key={i.clave}><Indicador {...i} /></li>)}
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
