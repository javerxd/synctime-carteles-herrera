import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { reportes } from '../../api/reportes';
import { Aviso, Boton, EstadoVacio, Esqueleto, Pestanas } from '../../componentes/ui';
import { useSesion } from '../../sesion/sesion';
import { hoyChile } from '../../utilidades/fechas';
import { filtrosDesde, paraDireccion, parametrosInvalidos } from '../../utilidades/filtrosUrl';
import { useConsulta } from '../../utilidades/useConsulta';
import { resolverPeriodo } from '../indicadores/reglasIndicadores';
import { BotonesExportar } from './BotonesExportar';
import { FiltrosReporte } from './FiltrosReporte';
import { GraficoReporte } from './GraficoReporte';
import {
  DEFINICION_REPORTES, emisionChile, nombreDelReporte, parametrosReporte, pestanasPara, REPORTES, reporteDeRol,
} from './reglasReportes';
import { TablaReporte } from './TablaReporte';
import { useExportacion } from './useExportacion';
import estilos from './Reportes.module.css';

const SIN_CLIENTES = [];

/** El documento de un reporte: sus gráficos (el gerencial), sus tablas con totales y sus notas. */
function DocumentoVista({ documento }) {
  const vacio = documento.tablas.every((t) => t.filas.length === 0);
  if (vacio) {
    return (
      <EstadoVacio titulo="No hay datos para estos filtros" nivelTitulo={2}
        descripcion="Prueba con otro período o quita algún filtro: el archivo que exportes llevará lo mismo que ves aquí." />
    );
  }
  return (
    <>
      {documento.graficos.map((g) => (
        <section key={g.clave} aria-labelledby={`grafico-${g.clave}`} className={estilos.seccion}>
          <h2 id={`grafico-${g.clave}`} className={estilos.tituloSeccion}>{g.titulo}</h2>
          <GraficoReporte grafico={g} />
        </section>
      ))}
      {documento.tablas.map((t) => <TablaReporte key={t.clave} tabla={t} />)}
      {documento.notas.length > 0 && (
        <ul role="list" className={estilos.notas} aria-label="Notas del reporte">
          {documento.notas.map((n, i) => <li key={i}>{n}</li>)}
        </ul>
      )}
    </>
  );
}

/** Lo que se imprime arriba de las tablas (RFU24): título, fecha de emisión y filtros aplicados. En pantalla no se ve. */
function HojaDeImpresion({ documento }) {
  return (
    <section aria-label="Datos para imprimir" className={estilos.portadaImpresion}>
      <h2 className={estilos.tituloSeccion}>{documento.titulo}</h2>
      <p>Emitido el {emisionChile(documento.emitidoEn)} (hora de Chile) · Letreros Herrera</p>
      <ul role="list" className={estilos.filtrosAplicados} aria-label="Filtros aplicados">
        {documento.filtros.map((f) => <li key={f.clave}><strong>{f.etiqueta}:</strong> {f.valor}</li>)}
      </ul>
    </section>
  );
}

function PanelReporte({ consulta }) {
  const { datos: documento, error, cargando, recargar } = consulta;
  return (
    <div className={estilos.panelReporte}>
      {cargando && (
        <div aria-hidden="true" className={estilos.seccion}>
          <Esqueleto alto="2rem" forma="bloque" />
          <Esqueleto alto="12rem" forma="bloque" />
        </div>
      )}
      {error && (
        <Aviso tono="peligro" titulo="No pudimos cargar el reporte"
          accion={error.estado === 400 ? undefined : <Boton variante="secundario" tamano="compacto" onClick={recargar}>Reintentar</Boton>}>
          {error.message}
        </Aviso>
      )}
      {documento && !error && (
        <>
          <HojaDeImpresion documento={documento} />
          <DocumentoVista documento={documento} />
        </>
      )}
    </div>
  );
}

/**
 * /panel/reportes (RFS10, RFU24 a RFU26, RFU36, S18, S24): los tres reportes que calcula el servidor, con los filtros en la
 * dirección, la misma tabla que luego se exporta, los tres botones de exportación (Excel, CSV y PDF, en segundo plano) y la
 * impresión. El operativo lo ve todo el personal; el gerencial y el parametrizable, solo dueño y administrador (quien no
 * puede verlos ni siquiera ve la pestaña; la API lo exige también).
 */
export default function Reportes() {
  const { usuario } = useSesion();
  const rol = usuario?.rol;
  const [parametros, setParametros] = useSearchParams();
  const filtros = filtrosDesde(parametros, DEFINICION_REPORTES);
  const reporte = reporteDeRol(filtros.reporte, rol);
  const visibles = pestanasPara(rol);

  // Lo que no sirve en la dirección (o un reporte que el rol no ve) se quita en silencio, como en los listados.
  const aQuitar = [...parametrosInvalidos(parametros, DEFINICION_REPORTES), ...(parametros.get('reporte') && parametros.get('reporte') !== reporte ? ['reporte'] : [])];
  const claveQuitar = [...new Set(aQuitar)].join(',');
  useEffect(() => {
    if (!claveQuitar) return;
    setParametros((actuales) => {
      const siguiente = new URLSearchParams(actuales);
      claveQuitar.split(',').forEach((c) => siguiente.delete(c));
      return siguiente;
    }, { replace: true });
  }, [claveQuitar, setParametros]);

  // El título de la ventana es también el encabezado del papel y el nombre sugerido del PDF al imprimir: sin esto quedaba el de la
  // página anterior («Auditoría: bitácora…»).
  useEffect(() => {
    document.title = `${nombreDelReporte(reporte)} · SyncTime`;
  }, [reporte]);

  const cambiar = (cambios) => setParametros(paraDireccion({ ...filtros, reporte, ...cambios }, DEFINICION_REPORTES), { replace: true });
  const limpiar = () => setParametros(reporte === 'operativo' ? {} : { reporte }, { replace: true });

  const clientes = useConsulta('reportes:clientes', (senal) => reportes.clientes({ senal }));
  const consulta = useConsulta(`reporte:${JSON.stringify([reporte, parametrosReporte(reporte, filtros)])}`, (senal) => reportes.leer(reporte, filtros, { senal }));
  const exportacion = useExportacion({ reporte, filtros });

  const anioActual = Number(hoyChile().slice(0, 4));
  const periodo = resolverPeriodo(filtros, hoyChile());
  const resumen = `Mostrando ${periodo.etiqueta}.`;

  return (
    <div className={estilos.pagina}>
      <div data-no-imprimir>
        <h1 className={estilos.titulo}>Reportes</h1>
        <p className={estilos.bajada}>Consulta, imprime y exporta a Excel, CSV o PDF los reportes del taller.</p>
      </div>

      <div data-no-imprimir>
        <FiltrosReporte reporte={reporte} filtros={filtros} cambiar={cambiar} limpiar={limpiar} clientes={clientes.datos ?? SIN_CLIENTES}
          anios={[anioActual, anioActual - 1, anioActual - 2]} resumen={resumen} />
      </div>

      {/* El aviso de carga vive aquí, siempre montado: al cambiar de pestaña no nace con su texto (los lectores no lo anunciarían). */}
      <p role="status" className="solo-lectores">{consulta.cargando ? 'Cargando el reporte…' : consulta.datos ? 'Reporte actualizado.' : ''}</p>

      <BotonesExportar reporte={reporte} estado={exportacion.estado} alPedir={exportacion.pedir} alReintentar={exportacion.reintentar}
        alDescargar={exportacion.descargarDeNuevo} alCerrar={exportacion.cerrar} />

      <Pestanas
        etiqueta="Tipo de reporte"
        activa={reporte}
        alCambiar={(id) => cambiar({ reporte: id })}
        pestanas={visibles.map((id) => ({
          id,
          etiqueta: REPORTES[id].etiqueta,
          contenido: id === reporte ? <PanelReporte consulta={consulta} /> : null,
        }))}
      />
    </div>
  );
}
