import { useEffect, useId, useRef, useState } from 'react';
import {
  BellRing, CircleCheck, Clock3, MailPlus, RefreshCw, Replace, Send, Trash2, Undo2,
} from 'lucide-react';
import { Aviso, Boton, Campo, Dialogo, Esqueleto, Insignia } from '../../componentes/ui';
import { avisosTaller } from '../../api/avisosTaller';
import { useAvisos } from '../../avisos/Avisos';
import { formatearFechaHora } from '../../utilidades/formato';
import { useConsulta } from '../../utilidades/useConsulta';
import {
  ETIQUETA_PASSWORD, esPasswordIncorrecta, mensajeFallaGestion, PASSWORD_VACIA, textoPasswordIncorrecta,
} from '../compartidos/reautenticacion';
import {
  enlaceVencido, enmascararCorreo, errorCorreoAviso, MOTIVO_NO_QUITAR, ordenarDestinos, puedeQuitar, reemplazoPendiente, TEXTOS_ACCION, textoEstadoPrueba,
} from './reglasAvisosTaller';
import { PreferenciaDestino } from './PreferenciaDestino';
import { usePruebaAvisos } from './usePruebaAvisos';
import estilos from './AvisosTaller.module.css';

/**
 * Perfil → «Avisos del taller» (solo dueño y administrador; quien la monta decide, no se oculta con CSS).
 * Es configuración del taller, no de la cuenta: a qué correos llegan los avisos de solicitudes nuevas.
 * Un correo nuevo, o el que reemplaza a otro, queda pendiente hasta que alguien abre el enlace que le llega;
 * mientras tanto el anterior sigue recibiendo los avisos.
 */
export function SeccionAvisosTaller() {
  const consulta = useConsulta('avisos-taller', (senal) => avisosTaller.obtener({ senal }));
  const { anunciar } = useAvisos();
  const [formulario, setFormulario] = useState(null); // null | { idReemplaza?, correoAnterior? }
  const [quitando, setQuitando] = useState(null);
  const [falla, setFalla] = useState(null); // { id, mensaje }
  const [ocupado, setOcupado] = useState(null);
  const refAgregar = useRef(null);
  const datos = consulta.datos;
  const destinos = ordenarDestinos(datos?.destinos ?? []);

  async function ejecutar(id, accion, exito) {
    setOcupado(id);
    setFalla(null);
    try {
      await accion();
      anunciar(exito);
      consulta.recargar();
    } catch (error) {
      setFalla({
        id,
        mensaje: error?.estado === 429 ? 'Ya pediste varios enlaces seguidos. Espera un minuto y vuelve a intentarlo.' : mensajeFallaGestion(error, 'No pudimos hacerlo.'),
      });
      if (error?.estado === 409 || error?.estado === 404) consulta.recargar();
    } finally {
      setOcupado(null);
    }
  }

  async function enviarCorreo(correo, passwordActual) {
    const idReemplaza = formulario?.idReemplaza;
    await avisosTaller.agregar({ correo, idReemplaza, passwordActual });
    anunciar({
      tono: 'exito',
      titulo: `Enviamos un enlace a ${correo}`,
      texto: idReemplaza
        ? 'El cambio se aplica cuando lo confirmen. Mientras tanto, el correo anterior sigue recibiendo los avisos.'
        : 'Empezará a recibir avisos cuando lo confirmen desde ese correo.',
    });
    setFormulario(null);
    consulta.recargar();
    // El formulario desaparece: el foco vuelve a «Agregar correo», que siempre está (el aviso ya se anunció).
    setTimeout(() => refAgregar.current?.focus());
  }

  return (
    <section aria-labelledby="titulo-avisos-taller" className={estilos.seccion}>
      <header className={estilos.cabecera}>
        <span className={estilos.marcaIcono} aria-hidden="true"><BellRing size={20} strokeWidth={1.75} /></span>
        <div className={estilos.textosCabecera}>
          <div className={estilos.lineaTitulo}>
            <h2 id="titulo-avisos-taller" className={estilos.titulo}>Avisos del taller</h2>
            <Insignia tono="neutro">Configuración del taller</Insignia>
          </div>
          <p className={estilos.explicacion}>
            Estos correos reciben los avisos de solicitudes nuevas para todo el equipo; cada uno elige si recibe las en línea, las
            presenciales o ambas. No cambia tu correo de ingreso.
          </p>
        </div>
      </header>

      {consulta.cargando && (
        <>
          <p role="status" className="solo-lectores">Cargando los avisos del taller…</p>
          <Esqueleto lineas={3} />
        </>
      )}
      {consulta.error && !datos && (
        <Aviso tono="peligro" titulo="No pudimos cargar los avisos del taller"
          accion={<Boton variante="secundario" tamano="compacto" iconoInicio={RefreshCw} onClick={consulta.recargar}>Reintentar</Boton>}>
          {consulta.error.message}
        </Aviso>
      )}

      {datos && (
        <>
          {datos.usaRespaldo && (
            <Aviso tono="aviso" titulo="Hoy los avisos van al correo de la configuración del servidor">
              Agrega uno aquí para administrarlo desde el panel.
            </Aviso>
          )}

          <div className={estilos.bloque}>
            <h3 className={estilos.subtitulo}>Correos que reciben los avisos</h3>
            {destinos.length > 0 ? (
              <ul className={estilos.destinos} aria-busy={consulta.recargando ? 'true' : undefined}>
                {destinos.map((destino) => (
                  <Destino key={destino.idDestinatario} destino={destino} destinos={destinos} ocupado={ocupado === destino.idDestinatario}
                    falla={falla?.id === destino.idDestinatario ? falla.mensaje : null}
                    reemplazando={formulario?.idReemplaza === destino.idDestinatario}
                    alReemplazar={() => setFormulario({ idReemplaza: destino.idDestinatario, correoAnterior: destino.correo })}
                    alCerrarFormulario={() => setFormulario(null)}
                    alEnviarCorreo={enviarCorreo}
                    alQuitar={() => setQuitando(destino)}
                    alRecargar={consulta.recargar}
                    alReenviar={() => ejecutar(destino.idDestinatario, () => avisosTaller.reenviar(destino.idDestinatario),
                      { tono: 'exito', titulo: `Reenviamos el enlace a ${destino.correo}`, texto: 'El enlace anterior ya no sirve.' })}
                    alCancelar={() => ejecutar(destino.idDestinatario, () => avisosTaller.cancelar(destino.idDestinatario),
                      { tono: 'exito', titulo: 'Cambio cancelado', texto: `${destino.correo} no recibirá avisos.` })} />
                ))}
              </ul>
            ) : (
              <p className={estilos.nota}>Aún no hay correos guardados en el panel.</p>
            )}

            {formulario && !formulario.idReemplaza ? (
              <FormularioCorreo etiqueta="Correo nuevo" textoBoton="Enviar enlace de confirmación" alEnviar={enviarCorreo}
                alCancelar={() => { setFormulario(null); refAgregar.current?.focus(); }}
                ayuda="Le llegará un enlace para confirmar. Vence en 24 horas." />
            ) : (
              <div>
                <Boton ref={refAgregar} variante="secundario" iconoInicio={MailPlus} onClick={() => setFormulario({})}>
                  Agregar correo
                </Boton>
              </div>
            )}
          </div>

          <CorreoDePrueba usaRespaldo={datos.usaRespaldo} alEncolar={consulta.recargar} />

          <Historial historial={datos.historial ?? []} />
        </>
      )}

      <DialogoQuitar destino={quitando} alCerrar={() => setQuitando(null)}
        alQuitar={async (passwordActual) => {
          try {
            await avisosTaller.quitar(quitando.idDestinatario, passwordActual);
          } catch (error) {
            if (error?.estado === 409 || error?.estado === 404) consulta.recargar();
            throw error;
          }
          anunciar({ tono: 'exito', titulo: `${quitando.correo} ya no recibe los avisos` });
          setQuitando(null);
          consulta.recargar();
        }} />
    </section>
  );
}

/** Un correo largo se corta primero en la @, no en medio del dominio. */
function CorreoPartido({ correo }) {
  const arroba = String(correo ?? '').lastIndexOf('@');
  if (arroba < 1) return correo;
  return <>{correo.slice(0, arroba)}<wbr />{correo.slice(arroba)}</>;
}

function Destino({
  destino, destinos, ocupado, falla, reemplazando, alReemplazar, alCerrarFormulario, alEnviarCorreo, alQuitar, alReenviar, alCancelar, alRecargar,
}) {
  const idMotivo = useId();
  const refReemplazar = useRef(null);
  const activo = destino.estado === 'activo';
  const pendiente = reemplazoPendiente(destino, destinos);
  const sePuedeQuitar = puedeQuitar(destino, destinos);
  const vencido = enlaceVencido(destino);

  return (
    <li className={estilos.destino}>
      <div className={estilos.filaDestino}>
        <div className={estilos.datosDestino}>
          <span className={estilos.correo}><CorreoPartido correo={destino.correo} /></span>
          {activo
            ? <Insignia tono="exito" icono={CircleCheck}>Activo</Insignia>
            : <Insignia tono="aviso" icono={Clock3}>Pendiente de confirmar</Insignia>}
        </div>
        <div className={estilos.acciones}>
          {activo && !pendiente && !reemplazando && (
            <Boton ref={refReemplazar} variante="secundario" tamano="compacto" iconoInicio={Replace} onClick={alReemplazar} disabled={ocupado}
              aria-label={`Reemplazar ${destino.correo}`}>
              Reemplazar
            </Boton>
          )}
          {activo && (
            <Boton variante="fantasma" tamano="compacto" iconoInicio={Trash2} onClick={sePuedeQuitar ? alQuitar : undefined}
              aria-disabled={sePuedeQuitar ? undefined : 'true'} aria-describedby={sePuedeQuitar ? undefined : idMotivo}
              className={sePuedeQuitar ? undefined : estilos.inactivo} aria-label={`Quitar ${destino.correo}`}>
              Quitar
            </Boton>
          )}
          {!activo && (
            <>
              <Boton variante="secundario" tamano="compacto" iconoInicio={Send} onClick={alReenviar} cargando={ocupado} textoCargando="Enviando…"
                aria-label={ocupado ? undefined : `Reenviar enlace a ${destino.correo}`}>
                Reenviar enlace
              </Boton>
              <Boton variante="fantasma" tamano="compacto" iconoInicio={Undo2} onClick={alCancelar} disabled={ocupado}
                aria-label={`Cancelar cambio a ${destino.correo}`}>
                Cancelar cambio
              </Boton>
            </>
          )}
        </div>
      </div>

      <div className={estilos.detalles}>
        {activo && destino.fechaConfirmacionChile && <p>Confirmado el <span className="cifras">{formatearFechaHora(destino.fechaConfirmacionChile)}</span>.</p>}
        {activo && pendiente && (
          <p>Sigue recibiendo los avisos hasta que se confirme el cambio a <strong className={estilos.correo}>{pendiente.correo}</strong>.</p>
        )}
        {!activo && destino.reemplazaA && (
          <p>Reemplazará a <span className={estilos.correo}>{destino.reemplazaA.correo}</span> cuando se confirme.</p>
        )}
        {!activo && !destino.reemplazaA && <p>Se sumará a los correos activos cuando se confirme.</p>}
        {!activo && destino.venceChile && (
          vencido
            ? <p className={estilos.vencido}>El enlace venció el <span className="cifras">{formatearFechaHora(destino.venceChile)}</span>. Reenvíalo o cancela el cambio.</p>
            : <p>El enlace vence el <span className="cifras">{formatearFechaHora(destino.venceChile)}</span>.</p>
        )}
        {activo && !sePuedeQuitar && <p id={idMotivo}>{MOTIVO_NO_QUITAR}</p>}
      </div>

      {activo && <PreferenciaDestino destino={destino} alGuardado={alRecargar} />}

      {reemplazando && (
        <FormularioCorreo etiqueta={`Correo que reemplaza a ${destino.correo}`} textoBoton="Enviar enlace de confirmación"
          ayuda="Hasta que lo confirmen, los avisos siguen llegando al correo actual."
          alEnviar={alEnviarCorreo} alCancelar={() => { alCerrarFormulario(); setTimeout(() => refReemplazar.current?.focus()); }} />
      )}
      {falla && <Aviso tono="peligro" titulo={falla} className={estilos.fallaDestino} />}
    </li>
  );
}

const MENSAJES_CONFLICTO = {
  ya_existe: 'Ese correo ya está activo o esperando confirmación.',
  demasiados_pendientes: 'Ya hay 3 correos esperando confirmación. Cancela uno o espera a que lo confirmen.',
};

/** El texto de un 409: el de la API, salvo que sea el genérico de «otra persona cambió». */
function textoConflictoAviso(falla) {
  const generico = !falla?.message || /^Otra persona cambió/.test(falla.message);
  if (!generico) return falla.message;
  return MENSAJES_CONFLICTO[falla?.codigo] ?? 'Ese correo ya está activo o esperando confirmación, o ese destino ya tiene un cambio pendiente.';
}

/** Campo de la contraseña de quien hace el cambio (la API la exige para agregar, reemplazar y quitar). */
function CampoPassword({ valor, alCambiar, error, deshabilitado, refCampo }) {
  return (
    <Campo ref={refCampo} etiqueta={ETIQUETA_PASSWORD} type="password" autoComplete="current-password" requerido maxLength={72}
      value={valor} onChange={(e) => alCambiar(e.target.value)} error={error} disabled={deshabilitado} />
  );
}

/**
 * Un correo y la contraseña de quien pide el cambio, con su validación y los errores de la API junto a su campo.
 * El foco entra al correo al abrirse.
 */
function FormularioCorreo({ etiqueta, ayuda, textoBoton, alEnviar, alCancelar }) {
  const [correo, setCorreo] = useState('');
  const [password, setPassword] = useState('');
  const [errores, setErrores] = useState({});
  const [enviando, setEnviando] = useState(false);
  const refCampo = useRef(null);
  const refPassword = useRef(null);

  useEffect(() => {
    refCampo.current?.focus();
  }, []);

  async function enviar(evento) {
    evento.preventDefault();
    const problemas = { correo: errorCorreoAviso(correo), password: password ? null : PASSWORD_VACIA };
    if (problemas.correo || problemas.password) {
      setErrores(problemas);
      (problemas.correo ? refCampo : refPassword).current?.focus();
      return;
    }
    setEnviando(true);
    setErrores({});
    try {
      await alEnviar(correo.trim().toLowerCase(), password);
    } catch (falla) {
      setEnviando(false);
      if (esPasswordIncorrecta(falla)) {
        setPassword('');
        setErrores({ password: textoPasswordIncorrecta(falla) });
        setTimeout(() => refPassword.current?.focus());
        return;
      }
      const delCampo = falla?.errores ? Object.entries(falla.errores).find(([c]) => c.toLowerCase() === 'correo')?.[1] : null;
      const mensaje = Array.isArray(delCampo) ? delCampo[0] : delCampo;
      if (mensaje || (falla?.estado === 409 && falla.codigo !== 'demasiados_pendientes')) {
        setErrores({ correo: mensaje || textoConflictoAviso(falla) });
        setTimeout(() => refCampo.current?.focus());
        return;
      }
      setErrores({ general: falla?.estado === 409 ? textoConflictoAviso(falla) : mensajeFallaGestion(falla, 'No pudimos enviar el enlace.') });
    }
  }

  return (
    <form className={estilos.formulario} onSubmit={enviar} noValidate>
      <Campo ref={refCampo} etiqueta={etiqueta} type="email" autoComplete="off" inputMode="email" maxLength={254} requerido
        value={correo} onChange={(e) => setCorreo(e.target.value)} error={errores.correo} ayuda={ayuda} disabled={enviando} />
      <CampoPassword refCampo={refPassword} valor={password} alCambiar={setPassword} error={errores.password} deshabilitado={enviando} />
      {errores.general && <Aviso tono="peligro" titulo={errores.general} />}
      <div className={estilos.acciones}>
        <Boton type="submit" tamano="compacto" iconoInicio={Send} cargando={enviando} textoCargando="Enviando…">{textoBoton}</Boton>
        <Boton variante="fantasma" tamano="compacto" onClick={alCancelar} disabled={enviando}>Cancelar</Boton>
      </div>
    </form>
  );
}

/** Quitar un activo pide confirmar y la contraseña de quien lo quita. El foco empieza en «Cancelar». */
function DialogoQuitar({ destino, alCerrar, alQuitar }) {
  const abierto = Boolean(destino);
  const [password, setPassword] = useState('');
  const [estado, setEstado] = useState({ enviando: false, errorPassword: null, error: null });
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  const refCancelar = useRef(null);
  const refPassword = useRef(null);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    if (abierto) {
      setPassword('');
      setEstado({ enviando: false, errorPassword: null, error: null });
    }
  }

  async function quitar(evento) {
    evento.preventDefault();
    if (!password) {
      setEstado({ enviando: false, errorPassword: PASSWORD_VACIA, error: null });
      refPassword.current?.focus();
      return;
    }
    setEstado({ enviando: true, errorPassword: null, error: null });
    try {
      await alQuitar(password);
    } catch (falla) {
      if (esPasswordIncorrecta(falla)) {
        setPassword('');
        setEstado({ enviando: false, errorPassword: textoPasswordIncorrecta(falla), error: null });
        setTimeout(() => refPassword.current?.focus());
        return;
      }
      setEstado({ enviando: false, errorPassword: null, error: mensajeFallaGestion(falla, 'No pudimos quitar el correo.') });
    }
  }

  return (
    <Dialogo abierto={abierto} alCerrar={() => !estado.enviando && alCerrar()} titulo="Quitar este correo" enfoqueInicial={refCancelar} ancho="30rem"
      descripcion={destino ? `${destino.correo} dejará de recibir los avisos de solicitudes nuevas. Le avisaremos del cambio.` : ''}
      pie={(
        <>
          <Boton ref={refCancelar} variante="secundario" onClick={alCerrar} disabled={estado.enviando}>Cancelar</Boton>
          <Boton type="submit" form="form-quitar-destino" variante="peligro" iconoInicio={Trash2} cargando={estado.enviando} textoCargando="Quitando…">
            Quitar correo
          </Boton>
        </>
      )}>
      <form id="form-quitar-destino" className={estilos.formulario} onSubmit={quitar} noValidate>
        <CampoPassword refCampo={refPassword} valor={password} alCambiar={setPassword} error={estado.errorPassword} deshabilitado={estado.enviando} />
        {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
      </form>
    </Dialogo>
  );
}

function CorreoDePrueba({ usaRespaldo, alEncolar }) {
  // Al encolar la prueba se recarga la sección: la prueba queda en «Cambios recientes».
  const prueba = usePruebaAvisos(alEncolar);
  const terminado = prueba.fase === 'terminada';
  const respaldo = prueba.envio?.usaRespaldo ?? usaRespaldo;

  return (
    <div className={estilos.bloque}>
      <h3 className={estilos.subtitulo}>Correo de prueba</h3>
      <p className={estilos.nota}>
        Envía un aviso de ejemplo, marcado como prueba, a los correos activos{usaRespaldo ? ' (hoy, al de la configuración del servidor)' : ''}.
      </p>
      <div>
        <Boton variante="secundario" iconoInicio={Send} onClick={prueba.enviar} cargando={prueba.ocupado}
          textoCargando={prueba.fase === 'enviando' ? 'Enviando…' : 'Revisando el envío…'}>
          Enviar correo de prueba
        </Boton>
      </div>
      {prueba.error && <Aviso tono="peligro" titulo={prueba.error.message || 'No pudimos enviar la prueba.'} />}
      {prueba.filas.length > 0 && (
        <ul className={estilos.resultados} aria-label="Resultado del correo de prueba">
          {prueba.filas.map((fila) => {
            const { tono, texto } = textoEstadoPrueba(fila, { terminado });
            return (
              <li key={fila.idCorreo} className={estilos.resultado}>
                <span className={estilos.correo}>{respaldo ? enmascararCorreo(fila.destinatario) : fila.destinatario}</span>
                <Insignia tono={tono}>{texto}</Insignia>
              </li>
            );
          })}
        </ul>
      )}
      <p role="status" aria-atomic="true" className="solo-lectores">
        {terminado && prueba.filas.length > 0
          ? prueba.filas.map((f) => `${f.destinatario}: ${textoEstadoPrueba(f, { terminado: true }).texto}`).join('. ')
          : ''}
      </p>
    </div>
  );
}

function Historial({ historial }) {
  return (
    <div className={estilos.bloque}>
      <h3 className={estilos.subtitulo}>Cambios recientes</h3>
      {historial.length === 0 ? (
        <p className={estilos.nota}>Todavía no hay cambios registrados.</p>
      ) : (
        <ol className={estilos.historial}>
          {historial.map((h, i) => (
            <li key={`${h.fechaChile}-${i}`} className={estilos.registro}>
              <span className={`cifras ${estilos.fechaRegistro}`}>{formatearFechaHora(h.fechaChile)}</span>
              <span>
                {TEXTOS_ACCION[h.accion] ?? h.accion} <span className={estilos.correo}>{h.correo}</span>
                {h.detalle ? <span className={estilos.detalleRegistro}> · {h.detalle}</span> : null}
              </span>
              <span className={estilos.autor}>{h.empleado || 'Desde el enlace del correo'}</span>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
