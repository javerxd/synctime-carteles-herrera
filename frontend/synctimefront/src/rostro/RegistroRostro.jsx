import { useEffect, useId, useRef, useState } from 'react';
import { RotateCcw, ScanFace } from 'lucide-react';
import { Aviso, Boton, Campo, Casilla } from '../componentes/ui';
import FlujoFacial from './FlujoFacial';
import { mensajeDeError } from './motorFacial';
import estilos from './RegistroRostro.module.css';

const PASOS = ['frente', 'izquierda', 'derecha'];
const NOMBRE_MUESTRA = {
  frente: 'Muestra de frente',
  izquierda: 'Muestra con giro hacia tu izquierda',
  derecha: 'Muestra con giro hacia tu derecha',
};

/** Los errores de cada muestra que devuelve el 400: errores.muestras.<lado>, o con la clave plana «muestras.<lado>». */
function erroresDeMuestras(errores) {
  const resultado = {};
  for (const paso of PASOS) {
    const valor = errores?.muestras?.[paso] ?? errores?.[`muestras.${paso}`];
    const texto = Array.isArray(valor) ? valor.join(' ') : valor;
    if (texto) resultado[paso] = String(texto);
  }
  return resultado;
}

/** Copia en pantalla la vista previa local (un canvas en memoria). Nunca se envía. */
function VistaMuestra({ paso, lienzo }) {
  const ref = useRef(null);
  useEffect(() => {
    const destino = ref.current;
    const contexto = destino?.getContext('2d');
    if (contexto && lienzo) contexto.drawImage(lienzo, 0, 0, destino.width, destino.height);
  }, [lienzo]);
  return <canvas ref={ref} width={240} height={180} role="img" aria-label={NOMBRE_MUESTRA[paso]} className={estilos.lienzo} />;
}

/**
 * RegistroRostro
 * ——————————————
 * Registro del rostro con tres muestras (frente, izquierda y derecha), para el titular desde Mi perfil o para otro
 * empleado de gestión desde «Rostros registrados». Pide el consentimiento y la contraseña del titular, hace el flujo
 * guiado, muestra una vista previa local de cada muestra con «Repetir esta muestra» y solo entonces guarda.
 *
 * - `alGuardar({ password, versionConsentimiento, muestras })`: hace la petición; si lanza un ErrorApi 400 con
 *   errores.muestras.<lado>, el texto aparece junto a esa muestra. Las vistas previas nunca salen de la pantalla.
 * - `textos`: { boton, guardar, consentimiento, password, ayudaPassword }.
 */
export default function RegistroRostro({ consentimiento, textos, alGuardar, alCancelar }) {
  const id = useId();
  const refRevision = useRef(null);
  const refIniciar = useRef(null);
  const refCasilla = useRef(null);
  const refPassword = useRef(null);
  const [aceptado, setAceptado] = useState(false);
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [erroresCampo, setErroresCampo] = useState({});
  const [capturando, setCapturando] = useState(null); // pasos de la captura en curso, o null
  const [intento, setIntento] = useState(0); // la `key` del flujo: cada captura empieza de cero
  const [muestras, setMuestras] = useState({});
  const [erroresMuestra, setErroresMuestra] = useState({});
  const [enviando, setEnviando] = useState(false);
  const completas = PASOS.every((paso) => muestras[paso]);

  // Al terminar, fallar o cancelar una captura, el botón «Cancelar» desaparece: el foco va a la revisión si ya
  // están las tres muestras, o al botón para empezar.
  const capturabaAntes = useRef(false);
  useEffect(() => {
    if (capturabaAntes.current && !capturando) (completas ? refRevision.current : refIniciar.current)?.focus();
    capturabaAntes.current = Boolean(capturando);
  }, [capturando, completas]);

  function capturar(pasos) {
    setError('');
    setIntento((n) => n + 1);
    setCapturando(pasos);
  }

  /** Errores del consentimiento y la contraseña, junto a su campo; el foco va al primero. */
  function validarCampos() {
    const encontrados = {};
    if (!aceptado) encontrados.consentimiento = 'Debes aceptar el consentimiento para registrar el rostro.';
    if (!password) encontrados.password = 'Escribe la contraseña para confirmar que es el titular.';
    setErroresCampo(encontrados);
    if (encontrados.consentimiento) refCasilla.current?.focus();
    else if (encontrados.password) refPassword.current?.focus();
    return Object.keys(encontrados).length === 0;
  }

  function alIniciar(evento) {
    evento.preventDefault();
    if (!validarCampos()) return;
    // Con las tres muestras ya tomadas, Enter en la contraseña guarda; no vuelve a empezar la captura.
    if (completas) alGuardarMuestras();
    else capturar(PASOS);
  }

  function alTerminarFlujo(capturas, vistas) {
    setMuestras((anteriores) => {
      const siguientes = { ...anteriores };
      for (const captura of capturas) siguientes[captura.paso] = { captura, vista: vistas[captura.paso] ?? null };
      return siguientes;
    });
    setErroresMuestra((anteriores) => {
      const siguientes = { ...anteriores };
      for (const captura of capturas) delete siguientes[captura.paso];
      return siguientes;
    });
    setCapturando(null);
  }

  function alFallarFlujo(fallo) {
    setCapturando(null);
    setError(mensajeDeError(fallo));
  }

  function limpiar() {
    setPassword('');
    setAceptado(false);
    setMuestras({});
    setErroresMuestra({});
    setErroresCampo({});
  }

  async function alGuardarMuestras() {
    if (!validarCampos()) return;
    setError('');
    setEnviando(true);
    try {
      await alGuardar({
        password,
        versionConsentimiento: consentimiento.version,
        muestras: Object.fromEntries(PASOS.map((paso) => [paso, muestras[paso].captura])),
      });
      limpiar();
    } catch (fallo) {
      const porMuestra = erroresDeMuestras(fallo?.errores);
      setErroresMuestra(porMuestra);
      setError(Object.keys(porMuestra).length > 0
        ? 'Una o más muestras no sirven. Repítelas siguiendo la indicación de cada una.'
        : fallo?.message || 'No pudimos registrar el rostro.');
    } finally {
      setEnviando(false);
    }
  }

  // Descartar no deja en la pantalla la contraseña (que puede ser de otra persona) ni el consentimiento.
  function descartar() {
    setCapturando(null);
    setError('');
    limpiar();
    alCancelar?.();
  }

  const ocupado = enviando || Boolean(capturando);

  return (
    <div className={estilos.registro}>
      {error && <Aviso tono="peligro" titulo={error} />}
      {/* Durante la captura solo se ve la cámara: así no queda más abajo en una pantalla angosta. */}
      {!capturando && (
        <form className={estilos.formulario} onSubmit={alIniciar} noValidate>
          <p>Es opcional: siempre se podrá entrar con la contraseña. Lee lo siguiente antes de registrar el rostro:</p>
          <ul className={estilos.consentimiento}>{consentimiento.texto.map((parrafo) => <li key={parrafo}>{parrafo}</li>)}</ul>
          <Casilla ref={refCasilla} etiqueta={textos.consentimiento} checked={aceptado} onChange={(e) => setAceptado(e.target.checked)}
            disabled={ocupado} error={erroresCampo.consentimiento} />
          <Campo
            ref={refPassword}
            etiqueta={textos.password}
            error={erroresCampo.password}
            ayuda={textos.ayudaPassword}
            type="password"
            maxLength={100}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete={textos.autocompletar ?? 'current-password'}
            disabled={ocupado}
          />
          <p className={estilos.nota}>
            Se toman tres muestras seguidas: de frente y con un giro moderado hacia cada lado. Solo se guardan números
            calculados en este equipo; la imagen no se envía.
          </p>
          {!completas && (
            <div>
              <Boton ref={refIniciar} type="submit" iconoInicio={ScanFace}>{textos.boton}</Boton>
            </div>
          )}
        </form>
      )}

      {capturando && (
        <FlujoFacial
          key={intento}
          pasos={capturando}
          conVistaPrevia
          alTerminar={alTerminarFlujo}
          alError={alFallarFlujo}
          alCancelar={() => setCapturando(null)}
        />
      )}

      {!capturando && completas && (
        <section ref={refRevision} tabIndex={-1} aria-labelledby={`${id}-revision`} className={estilos.revision}>
          <h3 id={`${id}-revision`} className={estilos.tituloRevision}>Revisa las tres muestras</h3>
          <p className={estilos.nota}>Las imágenes solo se ven aquí y se borran al salir. Si alguna no se ve bien, repítela.</p>
          <div className={estilos.muestras}>
            {PASOS.map((paso) => (
              <figure key={paso} className={estilos.muestra} data-error={erroresMuestra[paso] ? 'true' : undefined}
                aria-describedby={erroresMuestra[paso] ? `${id}-error-${paso}` : undefined}>
                <VistaMuestra paso={paso} lienzo={muestras[paso].vista} />
                <figcaption className={estilos.leyenda}>{NOMBRE_MUESTRA[paso]}</figcaption>
                {erroresMuestra[paso] && <p id={`${id}-error-${paso}`} className={estilos.errorMuestra}>{erroresMuestra[paso]}</p>}
                <Boton variante="secundario" tamano="compacto" iconoInicio={RotateCcw} onClick={() => capturar([paso])} disabled={enviando}
                  aria-label={`Repetir esta muestra: ${NOMBRE_MUESTRA[paso].toLowerCase()}`}>
                  Repetir esta muestra
                </Boton>
              </figure>
            ))}
          </div>
          <div className={estilos.acciones}>
            <Boton variante="secundario" onClick={descartar} disabled={enviando}>Descartar</Boton>
            <Boton onClick={alGuardarMuestras} cargando={enviando} textoCargando="Guardando…">{textos.guardar}</Boton>
          </div>
        </section>
      )}
    </div>
  );
}
