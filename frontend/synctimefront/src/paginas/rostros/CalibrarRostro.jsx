import { useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, ScanFace, Trash2 } from 'lucide-react';
import { Aviso, Boton, Campo, Dialogo, Enlace, EstadoVacio, Esqueleto, Opciones, TablaAdaptable, Tarjeta } from '../../componentes/ui';
import authService from '../../api/authService';
import { personal } from '../../api/mantenedores';
import { rostros } from '../../api/rostros';
import { useAvisos } from '../../avisos/Avisos';
import FlujoFacial from '../../rostro/FlujoFacial';
import { pasosDelDesafio } from '../../rostro/desafio';
import { mensajeDeError } from '../../rostro/motorFacial';
import { useDevolverFoco } from '../../rostro/useDevolverFoco';
import { useConsulta } from '../../utilidades/useConsulta';
import { ETIQUETAS_CALIBRACION, decimal, fechaHora, filasDe } from './reglasRostros';
import panel from '../panel/Panel.module.css';
import estilos from './Rostros.module.css';

const OPCIONES_ETIQUETA = [
  { valor: 'propio', etiqueta: ETIQUETAS_CALIBRACION.propio, descripcion: 'El titular del rostro frente a la cámara.' },
  { valor: 'otra_persona', etiqueta: ETIQUETAS_CALIBRACION.otra_persona, descripcion: 'Alguien distinto, para medir cuánto se parece.' },
];

const peor = (m) => Math.max(...[m.distanciaFrente, m.distanciaPrimerGiro, m.distanciaSegundoGiro].filter(Number.isFinite));

function Apagada() {
  return (
    <EstadoVacio
      titulo="La calibración está apagada"
      descripcion={(
        <>
          Se enciende en la configuración del servidor con <code className={estilos.codigo}>RostroFacial:Calibracion:Activa</code> en
          true, y se vuelve a apagar al terminar. Mientras está apagada no se guarda ninguna medición.
        </>
      )}
    />
  );
}

function Serie({ titulo, serie }) {
  return (
    <div>
      <dt>{titulo}</dt>
      <dd>
        {serie?.n ? `${serie.n} ${serie.n === 1 ? 'intento' : 'intentos'} · mín. ${decimal(serie.min)} · máx. ${decimal(serie.max)} · prom. ${decimal(serie.promedio)}` : 'Sin intentos'}
      </dd>
    </div>
  );
}

/**
 * Calibrar el rostro (decisión 3 de la fase 10, solo gestión). Existe solo si el servidor tiene la calibración
 * encendida (GET responde `activa`; apagada, 404). Cada intento guiado guarda SOLO distancias y giros, sin imágenes
 * ni descriptores. La pantalla sugiere un umbral, pero no cambia la configuración: se copia a mano.
 */
export default function CalibrarRostro() {
  const { anunciar } = useAvisos();
  const consulta = useConsulta('calibracion', (senal) => rostros.calibracion.obtener({ senal }));
  const apagada = consulta.error?.estado === 404;
  const datos = consulta.datos?.activa ? consulta.datos : null;
  const empleados = useConsulta(datos ? 'rostros' : null, (senal) => (datos ? rostros.listar({ senal }) : null));

  const [idEmpleado, setIdEmpleado] = useState('');
  const [etiqueta, setEtiqueta] = useState('');
  const [errores, setErrores] = useState({});
  const [midiendo, setMidiendo] = useState(false);
  const [resultado, setResultado] = useState(null);
  const [confirmando, setConfirmando] = useState(false);
  const [borrando, setBorrando] = useState(false);
  const refDesafio = useRef(null);
  const refCancelar = useRef(null);
  const botonIntento = useDevolverFoco(midiendo);

  const conRostro = filasDe(empleados.datos).filter((f) => f.estado === 'activo');
  const elegido = conRostro.find((f) => String(f.idEmpleado) === idEmpleado);
  // Lo elegido se congela al empezar: una recarga de la lista durante el intento no lo cambia.
  const refIntento = useRef(null);

  function alIniciar(evento) {
    evento.preventDefault();
    const encontrados = {};
    if (!elegido) encontrados.empleado = 'Elige el empleado cuyo rostro se compara.';
    if (!etiqueta) encontrados.etiqueta = 'Elige si eres tú u otra persona.';
    setErrores(encontrados);
    setResultado(null);
    if (Object.keys(encontrados).length === 0) {
      refIntento.current = { empleado: elegido, etiqueta };
      setMidiendo(true);
    }
  }

  // El desafío va atado al email del empleado; la fila de S16 no lo trae, así que se lee de su ficha.
  async function prepararPasos(senal) {
    refDesafio.current = null;
    const { empleado } = refIntento.current;
    const email = empleado.email ?? (await personal.obtener(empleado.idEmpleado))?.email;
    if (!email) throw new Error('No encontramos el correo de ese empleado. Revisa su ficha en Personal.');
    const respuesta = await authService.desafioFacial(email, senal);
    const pasos = pasosDelDesafio(respuesta);
    if (!senal?.aborted) refDesafio.current = respuesta.desafio;
    return pasos;
  }

  async function alTerminar(capturas) {
    try {
      const { empleado, etiqueta: quien } = refIntento.current;
      const respuesta = await rostros.calibracion.medir({
        idEmpleado: empleado.idEmpleado, etiqueta: quien, desafio: refDesafio.current, capturas,
      });
      const habriaEntrado = respuesta?.habriaEntrado ?? respuesta?.medicion?.habriaEntrado;
      // El Aviso de la pantalla ya se anuncia: no se repite en la región de avisos.
      setResultado({ tono: 'exito', titulo: 'Medición guardada', habriaEntrado });
      consulta.recargar();
    } catch (error) {
      setResultado({ tono: 'peligro', titulo: error?.estado === 0 ? mensajeDeError(error) : error?.message || 'No pudimos guardar la medición.' });
    } finally {
      refDesafio.current = null;
      setMidiendo(false);
    }
  }

  function alFallar(error) {
    refDesafio.current = null;
    setMidiendo(false);
    // Un error propio de esta pantalla (sin correo) trae su texto; los demás, el de la cámara o la red.
    const propio = !error?.codigo && error?.estado === undefined && error?.message;
    setResultado({ tono: 'peligro', titulo: propio || mensajeDeError(error) });
  }

  async function alBorrar() {
    setBorrando(true);
    try {
      await rostros.calibracion.borrar();
      anunciar({ tono: 'exito', titulo: 'Mediciones borradas' });
      consulta.recargar();
    } catch (error) {
      setResultado({ tono: 'peligro', titulo: error?.message || 'No pudimos borrar las mediciones.' });
    } finally {
      setBorrando(false);
      setConfirmando(false);
    }
  }

  const columnas = [
    { clave: 'fecha', encabezado: 'Fecha', principal: true, render: (m) => fechaHora(m.fecha) },
    { clave: 'empleado', encabezado: 'Rostro de', render: (m) => m.empleado ?? m.nombre
      ?? filasDe(empleados.datos).find((f) => f.idEmpleado === m.idEmpleado)?.nombre ?? `#${m.idEmpleado}` },
    { clave: 'etiqueta', encabezado: 'Quién', render: (m) => ETIQUETAS_CALIBRACION[m.etiqueta] ?? m.etiqueta },
    { clave: 'frente', encabezado: 'Frente', alinear: 'fin', render: (m) => decimal(m.distanciaFrente) },
    { clave: 'primero', encabezado: 'Primer giro', alinear: 'fin', render: (m) => decimal(m.distanciaPrimerGiro) },
    { clave: 'segundo', encabezado: 'Segundo giro', alinear: 'fin', render: (m) => decimal(m.distanciaSegundoGiro) },
    { clave: 'peor', encabezado: 'Peor distancia', alinear: 'fin', render: (m) => decimal(peor(m)) },
    { clave: 'entre', encabezado: 'Entre capturas', alinear: 'fin', render: (m) => decimal(m.distanciaMaxEntreCapturas) },
    {
      clave: 'giros', encabezado: 'Giros (g)', alinear: 'fin',
      render: (m) => `${decimal(m.giroFrente)} / ${decimal(m.giroPrimero)} / ${decimal(m.giroSegundo)}`,
    },
  ];

  const resumen = datos?.resumen;

  return (
    <div className={panel.pagina}>
      <div className={panel.cabecera}>
        <div>
          <h1 className={panel.titulo}>Calibrar el rostro</h1>
          <p className={panel.bajada}>
            Intentos marcados como «Soy yo» u «Otra persona» para elegir el umbral con datos. Solo se guardan distancias y giros.
          </p>
        </div>
        <Enlace como={Link} to="/panel/rostros"><ArrowLeft size={16} aria-hidden="true" /> Rostros registrados</Enlace>
      </div>

      {consulta.cargando && <><p role="status" className="solo-lectores">Consultando…</p><Esqueleto lineas={4} /></>}
      {apagada && <Apagada />}
      {consulta.error && !apagada && (
        <Aviso tono="peligro" titulo={consulta.error.message || 'No pudimos consultar la calibración.'}
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>} />
      )}
      {consulta.datos && !datos && <Apagada />}

      {datos && (
        <>
          <Tarjeta como="section" aria-labelledby="titulo-intento" className={panel.ficha}>
            <h2 id="titulo-intento" className={panel.subtitulo}>Hacer un intento</h2>
            {resultado && (
              <Aviso tono={resultado.tono} titulo={resultado.titulo}>
                {resultado.habriaEntrado === true && `Con el umbral vigente (${decimal(datos.umbralVigente)}), este intento habría entrado.`}
                {resultado.habriaEntrado === false && `Con el umbral vigente (${decimal(datos.umbralVigente)}), este intento no habría entrado.`}
              </Aviso>
            )}
            <form className={panel.formulario} onSubmit={alIniciar} noValidate>
              <Campo como="select" etiqueta="Empleado" value={idEmpleado} onChange={(e) => setIdEmpleado(e.target.value)}
                error={errores.empleado} disabled={midiendo}
                ayuda={empleados.cargando ? 'Cargando los rostros registrados…' : 'Solo aparecen los rostros activos con las tres muestras.'}>
                <option value="">Elige un empleado</option>
                {conRostro.map((f) => <option key={f.idEmpleado} value={String(f.idEmpleado)}>{f.nombre} ({f.rol})</option>)}
              </Campo>
              <Opciones leyenda="¿Quién está frente a la cámara?" nombre="etiqueta-calibracion" opciones={OPCIONES_ETIQUETA}
                valor={etiqueta} alCambiar={setEtiqueta} error={errores.etiqueta} deshabilitado={midiendo} enLinea />
              {!midiendo && (
                <div><Boton ref={botonIntento} type="submit" iconoInicio={ScanFace}>Hacer un intento</Boton></div>
              )}
            </form>
            {midiendo && (
              <FlujoFacial prepararPasos={prepararPasos} rangos={datos.rangos} alTerminar={alTerminar} alError={alFallar}
                alCancelar={() => setMidiendo(false)} />
            )}
          </Tarjeta>

          <Tarjeta como="section" aria-labelledby="titulo-resumen" className={panel.ficha}>
            <h2 id="titulo-resumen" className={panel.subtitulo}>Resumen</h2>
            <dl className={estilos.resumen}>
              <div><dt>Umbral vigente</dt><dd>{decimal(datos.umbralVigente)}</dd></div>
              <div><dt>Umbral sugerido</dt><dd>{decimal(resumen?.umbralSugerido)}</dd></div>
              <div><dt>Margen</dt><dd>{decimal(resumen?.margen)}</dd></div>
              <Serie titulo="Soy yo (peor distancia por intento)" serie={resumen?.propio} />
              <Serie titulo="Otra persona (peor distancia por intento)" serie={resumen?.otra} />
            </dl>
            {Number.isFinite(resumen?.margen) && resumen.margen < 0 && (
              <Aviso tono="aviso" titulo="Las dos series se cruzan">
                Ningún umbral las separa del todo: el sugerido es el que comete menos errores. Conviene hacer más intentos con buena luz.
              </Aviso>
            )}
            <p className={panel.bajada}>
              Esta pantalla no cambia la configuración. Para usar el umbral sugerido, cópialo a mano
              en <code className={estilos.codigo}>RostroFacial:UmbralDistancia</code> en el servidor y reinícialo. Los rangos de giro
              vigentes son: frente hasta {decimal(datos.rangos?.frenteMaximo)}, giros entre {decimal(datos.rangos?.giroMinimo)} y {decimal(datos.rangos?.giroMaximo)}.
            </p>
          </Tarjeta>

          <section aria-labelledby="titulo-mediciones" className={panel.ficha}>
            <div className={panel.cabecera}>
              <h2 id="titulo-mediciones" className={panel.subtitulo}>Mediciones</h2>
              <Boton variante="peligro" iconoInicio={Trash2} onClick={() => setConfirmando(true)} disabled={!datos.mediciones?.length}>
                Borrar mediciones
              </Boton>
            </div>
            <TablaAdaptable titulo="Mediciones" columnas={columnas} filas={datos.mediciones ?? []} claveFila="idMedicion"
              actualizando={consulta.recargando} />
          </section>
        </>
      )}

      <Dialogo
        abierto={confirmando}
        alCerrar={() => setConfirmando(false)}
        enfoqueInicial={refCancelar}
        titulo="Borrar las mediciones"
        descripcion="Se borrarán todas las mediciones de la calibración. No cambia el umbral configurado."
        pie={(
          <>
            <Boton ref={refCancelar} variante="secundario" onClick={() => setConfirmando(false)}>Cancelar</Boton>
            <Boton variante="peligro" onClick={alBorrar} cargando={borrando} textoCargando="Borrando…">Borrar</Boton>
          </>
        )}
      />
    </div>
  );
}
