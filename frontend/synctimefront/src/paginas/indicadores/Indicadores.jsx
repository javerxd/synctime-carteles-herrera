import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { indicadores } from '../../api/indicadores';
import { hoyChile } from '../../utilidades/fechas';
import { filtrosDesde, paraDireccion, parametrosInvalidos } from '../../utilidades/filtrosUrl';
import { useConsulta } from '../../utilidades/useConsulta';
import { BloqueIndicador, Conmutador } from './BloqueIndicador';
import { FiltrosIndicadores } from './FiltrosIndicadores';
import { conMayuscula, narrativaCobranza, narrativaDemanda, narrativaEmbudo, narrativaEvolucion, ordenarDemanda } from './formatoIndicadores';
import { GraficoCobranza, TablaCobranza } from './GraficoCobranza';
import { GraficoEvolucion, TablaEvolucion } from './GraficoEvolucion';
import { GraficoDemanda, GraficoEmbudo, TablaDemanda, TablaEmbudo } from './GraficosHorizontales';
import { DEFINICION_FILTROS, periodoComparacion, resolverPeriodo } from './reglasIndicadores';
import { TablaPorCobrar } from './TablaPorCobrar';
import { TarjetasResumen } from './TarjetasResumen';
import { useIndicadores } from './useIndicadores';
import estilos from './Indicadores.module.css';

const SIN_CATALOGOS = { categorias: [], clientes: [] };
const METRICAS = [{ valor: 'ingresos', texto: 'Ingresos' }, { valor: 'pedidos', texto: 'Pedidos' }];
const MEDIDAS = [{ valor: 'monto', texto: 'Ingresos' }, { valor: 'cantidad', texto: 'Unidades' }];
const AGRUPACIONES = [{ valor: 'categoria', texto: 'Por categoría' }, { valor: 'producto', texto: 'Por tipo de letrero' }];

/**
 * /panel/indicadores (RFS09, S10 a S14, S19, S24): dashboard de gestión con filtros combinables en la dirección,
 * siete tarjetas con su comparación, el saldo por cobrar destacado, cuatro gráficos en SVG propio (cada uno con su
 * tabla de datos) y la tabla de pedidos por cobrar. Los seis bloques cargan en paralelo y fallan por separado.
 */
export default function Indicadores() {
  const [parametros, setParametros] = useSearchParams();
  const filtros = filtrosDesde(parametros, DEFINICION_FILTROS);
  const { bloques, listos, reintentar } = useIndicadores(filtros);
  const catalogos = useConsulta('indicadores:catalogos', (senal) => indicadores.catalogos({ senal }));
  const [metrica, setMetrica] = useState('ingresos');
  const [medida, setMedida] = useState('monto');
  const anioActual = Number(hoyChile().slice(0, 4));
  const anios = [anioActual, anioActual - 1, anioActual - 2];

  // Lo que no sirve en la dirección se quita en silencio (como en los listados).
  const aQuitar = parametrosInvalidos(parametros, DEFINICION_FILTROS).join(',');
  useEffect(() => {
    if (!aQuitar) return;
    setParametros((actuales) => {
      const siguiente = new URLSearchParams(actuales);
      aQuitar.split(',').forEach((c) => siguiente.delete(c));
      return siguiente;
    }, { replace: true });
  }, [aQuitar, setParametros]);

  const cambiar = (cambios) => setParametros(paraDireccion({ ...filtros, pagina: 1, ...cambios }, DEFINICION_FILTROS), { replace: true });
  const limpiar = () => setParametros({}, { replace: true });
  const cambiarAlcance = (alcanceSaldo) => cambiar({ alcanceSaldo });
  const resumen = bloques.resumen.datos;
  // El período que muestra la barra sale de la dirección, no de la última respuesta: nunca queda desfasado.
  const periodo = resolverPeriodo(filtros, hoyChile());
  const comparacion = periodoComparacion(periodo, filtros.comparar);
  const conError = Object.values(bloques).filter((b) => b.error).length;
  const anuncio = !listos ? 'Actualizando indicadores…'
    : conError ? `Indicadores actualizados; ${conError} ${conError === 1 ? 'bloque no se pudo cargar' : 'bloques no se pudieron cargar'}.` : 'Indicadores actualizados.';

  return (
    <div className={estilos.pagina}>
      <div>
        <h1 className={estilos.titulo}>Indicadores</h1>
        <p className={estilos.bajada}>Ingresos, pedidos, cobranza y conversión del taller, con su comparación.</p>
      </div>

      <p role="status" className="solo-lectores">{anuncio}</p>

      <FiltrosIndicadores filtros={filtros} cambiar={cambiar} limpiar={limpiar} catalogos={catalogos.datos ?? SIN_CATALOGOS}
        periodo={periodo} comparacion={comparacion} anios={anios} />

      <BloqueIndicador titulo="Resumen del período" descripcion={conMayuscula(resumen?.periodo?.etiqueta) || undefined}
        estado={bloques.resumen} alReintentar={() => reintentar('resumen')} altoEsqueleto="16rem" className={estilos.bloqueResumen}>
        {(_, datos) => <TarjetasResumen datos={datos} alCambiarAlcance={cambiarAlcance} />}
      </BloqueIndicador>

      <div className={estilos.graficos}>
        <BloqueIndicador titulo="Evolución comercial" estado={bloques.evolucion} alReintentar={() => reintentar('evolucion')} conTabla
          narrativa={(d) => narrativaEvolucion(d, metrica)} className={estilos.areaEvolucion}
          vacio={(d) => !d.serie?.length || d.serie.every((p) => !p.ingresos && !p.pedidos && !p.ingresosComparacion && !p.pedidosComparacion)}
          textoVacio="Sin ingresos ni pedidos en este período"
          controles={<Conmutador etiqueta="Medir la evolución por" opciones={METRICAS} valor={metrica} alCambiar={setMetrica} />}>
          {(vista, datos) => (vista === 'tabla' ? <TablaEvolucion datos={datos} /> : <GraficoEvolucion key={`${datos.periodo?.desde}-${datos.serie.length}`} datos={datos} metrica={metrica} />)}
        </BloqueIndicador>

        <BloqueIndicador titulo="Cobranza" estado={bloques.cobranza} alReintentar={() => reintentar('cobranza')} conTabla
          narrativa={narrativaCobranza} vacio={(d) => !d.segmentos?.some((s) => s.cantidad)} textoVacio="Sin pedidos en este período"
          descripcion={bloques.cobranza.datos ? (bloques.cobranza.datos.alcance === 'vigente' ? 'Toda la deuda vigente.' : 'Pedidos del período.') + ' Elige un estado para filtrar la tabla de abajo.' : undefined}
          className={estilos.areaCobranza}>
          {(vista, datos) => (vista === 'tabla' ? <TablaCobranza datos={datos} />
            : <GraficoCobranza datos={datos} elegido={filtros.cobro} alElegir={(cobro) => cambiar({ cobro })} />)}
        </BloqueIndicador>

        <BloqueIndicador titulo="Embudo del canal web" descripcion="Solicitudes en línea del período; no depende del estado del pedido ni del pago." estado={bloques.embudo} alReintentar={() => reintentar('embudo')} conTabla
          narrativa={narrativaEmbudo} vacio={(d) => !d.pasos?.[0]?.cantidad} textoVacio="Sin solicitudes en línea en este período"
          className={estilos.areaEmbudo}>
          {(vista, datos) => (vista === 'tabla' ? <TablaEmbudo datos={datos} /> : <GraficoEmbudo datos={datos} />)}
        </BloqueIndicador>

        <BloqueIndicador titulo="Demanda por categoría" estado={bloques.demanda} alReintentar={() => reintentar('demanda')} conTabla
          narrativa={(d) => narrativaDemanda(ordenarDemanda(d.categorias, medida), medida, d.agrupar)} vacio={(d) => !d.categorias?.length}
          textoVacio={filtros.agrupar === 'producto' ? 'Sin ingresos por tipo de letrero en este período' : 'Sin ingresos por categoría en este período'} className={estilos.areaDemanda}
          controles={(
            <>
              <Conmutador etiqueta="Agrupar la demanda" opciones={AGRUPACIONES} valor={filtros.agrupar} alCambiar={(agrupar) => cambiar({ agrupar })} />
              <Conmutador etiqueta="Medir la demanda por" opciones={MEDIDAS} valor={medida} alCambiar={setMedida} />
            </>
          )}>
          {(vista, datos) => {
            const categorias = ordenarDemanda(datos.categorias, medida);
            return vista === 'tabla' ? <TablaDemanda categorias={categorias} agrupar={datos.agrupar} /> : <GraficoDemanda categorias={categorias} medida={medida} />;
          }}
        </BloqueIndicador>
      </div>

      <BloqueIndicador titulo="Pedidos por cobrar" estado={bloques.porCobrar} alReintentar={() => reintentar('porCobrar')} altoEsqueleto="12rem">
        {(_, datos) => (
          <TablaPorCobrar datos={datos} actualizando={bloques.porCobrar.actualizando} cobro={filtros.cobro}
            alQuitarCobro={() => cambiar({ cobro: '' })} alCambiarAlcance={cambiarAlcance} alCambiarPagina={(pagina) => cambiar({ pagina })} />
        )}
      </BloqueIndicador>
    </div>
  );
}
