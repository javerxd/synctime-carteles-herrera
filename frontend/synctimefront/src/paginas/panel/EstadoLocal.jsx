import { TALLER } from '../../configuracion/taller';
import { useRef, useState } from 'react';
import { AvisoConflicto } from '../compartidos/AvisoConflicto';
import { useConflictoDeVersion } from '../compartidos/useConflictoDeVersion';
import { Download, Printer, RefreshCw } from 'lucide-react';
import { Aviso, Boton, Campo, Esqueleto, EstadoCartel, Tarjeta } from '../../componentes/ui';
import { panel } from '../../api/panel';
import { useAvisos } from '../../avisos/Avisos';
import { GESTION } from '../../estructuras/navegacion';
import { useSesion } from '../../sesion/sesion';
import { formatearFechaHora } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import { DisponibilidadEquipo } from '../local/DisponibilidadEquipo';
import { EditorHorario, HorarioLectura } from '../local/HorarioSemanal';
import { MiDisponibilidad } from '../local/MiDisponibilidad';
import { LIMITES_CARTEL } from '../local/reglasLocal';
import { diaDeHoy } from '../publicas/horario';
import { CodigoQr, descargarPng, descargarSvg } from './CodigoQr';
import estilos from './Panel.module.css';

const ID_QR = 'qr-portal';

/**
 * Estado del local (RFS02, fase 9): el cartel electrónico (lo da vuelta todo el personal), el horario semanal
 * (lo edita gestión), la disponibilidad propia de cada persona (RFU20) y, para gestión, la de todo el equipo.
 */
export default function EstadoLocal() {
  const { tieneRol } = useSesion();
  const esGestion = tieneRol(GESTION);
  // Fase 11: el cartel y el horario se leen con su versión (If-Match al guardar).
  const cartel = useConsulta('cartel-version', (senal) => panel.cartel({ senal }));
  const horarios = useConsulta('horario-version', (senal) => panel.horario({ senal }));
  const dias = horarios.datos?.dias;
  const urlPortal = TALLER.urlPortal;
  const hoy = diaDeHoy();

  return (
    <div className={estilos.pagina}>
      <div className={`${estilos.cabecera} ${estilos.noImprimir}`}>
        <div>
          <h1 className={estilos.titulo}>Estado del local</h1>
          <p className={estilos.bajada}>Lo que ven los clientes en la portada y al escanear el código de la puerta.</p>
        </div>
      </div>

      <div className={estilos.dosPaneles}>
        <Tarjeta como="section" aria-labelledby="titulo-cartel" className={`${estilos.cartel} ${estilos.noImprimir}`}>
          <h2 id="titulo-cartel" className={estilos.subtitulo}>Cartel electrónico</h2>
          {cartel.cargando && <Esqueleto forma="bloque" alto="3rem" />}
          {cartel.error && <Aviso tono="peligro" titulo="No pudimos cargar el cartel">{cartel.error.message}</Aviso>}
          {cartel.datos && (
            <>
              <EstadoCartel abierto={cartel.datos.abiertoEnLocal} grande />
              <p>{cartel.datos.mensajeEstado}</p>
              {cartel.datos.tiempoEstimadoRegreso && <p className={estilos.bajada}>Regreso estimado: {cartel.datos.tiempoEstimadoRegreso}</p>}
              <p className={estilos.indicadorFormula}>Actualizado el {formatearFechaHora(cartel.datos.fechaUltimaModificacion)}</p>
              <DarVuelta actual={cartel.datos} releer={panel.cartel} alGuardar={cartel.recargar} />
            </>
          )}
        </Tarjeta>

        <Tarjeta como="section" aria-labelledby="titulo-qr" className={`${estilos.qr} zona-impresion`}>
          <h2 id="titulo-qr" className={estilos.subtitulo}>Código QR del portal</h2>
          <div className={estilos.qrImagen}>
            <CodigoQr texto={urlPortal} titulo={`Código QR que abre ${urlPortal}`} idSvg={ID_QR} />
          </div>
          <p className={estilos.bajada}>Escanéalo para ver si estamos abiertos, el horario y nuestros trabajos. Lleva a <span className="cifras">{urlPortal}</span>.</p>
          <div className={`${estilos.acciones} ${estilos.noImprimir}`}>
            <Boton variante="secundario" tamano="compacto" iconoInicio={Download} onClick={() => descargarPng(ID_QR, 'qr-letreria-los-herrera')}>Descargar PNG</Boton>
            <Boton variante="secundario" tamano="compacto" iconoInicio={Download} onClick={() => descargarSvg(ID_QR, 'qr-letreria-los-herrera')}>Descargar SVG</Boton>
            <Boton variante="secundario" tamano="compacto" iconoInicio={Printer} onClick={() => window.print()}>Imprimir</Boton>
          </div>
        </Tarjeta>
      </div>

      <Tarjeta como="section" aria-labelledby="titulo-horario" className={estilos.noImprimir}>
        <h2 id="titulo-horario" className={estilos.subtitulo}>Horario semanal</h2>
        {horarios.cargando && <Esqueleto lineas={7} />}
        {horarios.error && <Aviso tono="peligro" titulo="No pudimos cargar el horario">{horarios.error.message}</Aviso>}
        {dias && (esGestion
          ? (
            <EditorHorario key={horarios.datos.rowVersion ?? JSON.stringify(dias)} dias={dias} rowVersion={horarios.datos.rowVersion}
              releer={panel.horario} alGuardar={horarios.recargar} />
          )
          : (
            <>
              <HorarioLectura dias={dias} hoy={hoy} />
              <p className={estilos.indicadorFormula}>Lo cambian el dueño o el administrador.</p>
            </>
          ))}
      </Tarjeta>

      <Tarjeta como="section" aria-labelledby="titulo-disponibilidad" className={estilos.noImprimir}>
        <h2 id="titulo-disponibilidad" className={estilos.subtitulo}>Mi disponibilidad</h2>
        <p className={`${estilos.bajada} ${estilos.bajadaSeccion}`}>
          Las franjas en que atiendes cada semana. La portada muestra las del equipo juntas, sin nombres.
        </p>
        {dias && <MiDisponibilidad horario={dias} />}
        {horarios.cargando && <Esqueleto lineas={4} />}
      </Tarjeta>

      {esGestion && (
        <Tarjeta como="section" aria-labelledby="titulo-equipo" className={estilos.noImprimir}>
          <h2 id="titulo-equipo" className={estilos.subtitulo}>Disponibilidad del equipo</h2>
          <DisponibilidadEquipo />
        </Tarjeta>
      )}
    </div>
  );
}

/**
 * Dar vuelta el cartel (todo el personal). Fase 11: con la versión vista (If-Match); si otra persona lo cambió,
 * lo escrito se queda y se ofrece guardar sobre la versión actual o descartar.
 */
function DarVuelta({ actual, releer, alGuardar }) {
  const { anunciar } = useAvisos();
  const version = useConflictoDeVersion({ releer, versionInicial: actual.rowVersion });
  const [abierto, setAbierto] = useState(actual.abiertoEnLocal ? 'abierto' : 'cerrado');
  const [mensaje, setMensaje] = useState(actual.mensajeEstado ?? '');
  const [regreso, setRegreso] = useState(actual.tiempoEstimadoRegreso ?? '');
  const [guardando, setGuardando] = useState(false);
  const [errorMensaje, setErrorMensaje] = useState(null);
  const [errorGuardado, setErrorGuardado] = useState(null);
  const refMensaje = useRef(null);

  function alCambiarEstado(evento) {
    const valor = evento.target.value;
    setAbierto(valor);
    setMensaje(valor === 'abierto' ? 'Local abierto y atendiendo' : 'Salimos a una instalación en terreno');
  }

  async function alEnviar(evento, sobreActual = false) {
    evento?.preventDefault();
    if (guardando) return;
    if (!mensaje.trim()) {
      setErrorMensaje('Escribe el mensaje que verán los clientes.');
      refMensaje.current?.focus();
      return;
    }
    setGuardando(true);
    setErrorMensaje(null);
    setErrorGuardado(null);
    try {
      await panel.actualizarCartel({
        abiertoEnLocal: abierto === 'abierto',
        mensajeEstado: mensaje.trim(),
        tiempoEstimadoRegreso: abierto === 'cerrado' && regreso.trim() ? regreso.trim() : null,
      }, sobreActual ? version.version : actual.rowVersion);
      version.olvidar();
      anunciar({ tono: 'exito', titulo: abierto === 'abierto' ? 'El cartel dice Abierto' : 'El cartel dice Cerrado' });
      alGuardar();
    } catch (e) {
      if (e?.codigo === 'version_obsoleta') version.detectar();
      else setErrorGuardado(e?.message || 'No pudimos guardar el cartel.');
    } finally {
      setGuardando(false);
    }
  }

  function descartar() {
    const vigente = version.conflicto?.actual;
    version.olvidar();
    if (vigente) {
      setAbierto(vigente.abiertoEnLocal ? 'abierto' : 'cerrado');
      setMensaje(vigente.mensajeEstado ?? '');
      setRegreso(vigente.tiempoEstimadoRegreso ?? '');
    }
    alGuardar();
  }

  const vigente = version.conflicto?.actual;

  return (
    <form className={estilos.formulario} onSubmit={alEnviar} noValidate>
      <Campo etiqueta="Estado" como="select" value={abierto} onChange={alCambiarEstado}>
        <option value="abierto">Abierto / Disponible</option>
        <option value="cerrado">Cerrado / Ausente</option>
      </Campo>
      <Campo ref={refMensaje} name="mensaje" etiqueta="Mensaje para los clientes" maxLength={LIMITES_CARTEL.mensaje} value={mensaje} onChange={(e) => setMensaje(e.target.value)} error={errorMensaje} />
      {abierto === 'cerrado' && (
        <Campo etiqueta="Regreso estimado" ayuda="Por ejemplo: 16:30, o mañana a las 9:00" maxLength={LIMITES_CARTEL.regreso}
          value={regreso} onChange={(e) => setRegreso(e.target.value)} />
      )}
      <AvisoConflicto conflicto={version.conflicto} entidad="el cartel" guardando={guardando} refAccion={version.refAccion}
        resumen={vigente ? `dice «${vigente.abiertoEnLocal ? 'Abierto' : 'Cerrado'}»: ${vigente.mensajeEstado ?? ''}` : null}
        alGuardarSobre={() => alEnviar(null, true)} alReintentar={version.detectar} alDescartar={descartar} />
      {errorGuardado && <Aviso tono="peligro" titulo="No pudimos dar vuelta el cartel">{errorGuardado}</Aviso>}
      <Boton type="submit" iconoInicio={RefreshCw} cargando={guardando} textoCargando="Guardando…">Dar vuelta el cartel</Boton>
    </form>
  );
}
