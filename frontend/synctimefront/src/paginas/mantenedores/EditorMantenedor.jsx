import { useEffect, useId, useRef, useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Save } from 'lucide-react';
import { Aviso, Boton, Esqueleto, EstadoVacio, Tarjeta } from '../../componentes/ui';
import { useAvisos } from '../../avisos/Avisos';
import { useSesion } from '../../sesion/sesion';
import { useConsulta } from '../../utilidades/useConsulta';
import { AvisoConflicto } from '../compartidos/AvisoConflicto';
import { useConflictoDeVersion } from '../compartidos/useConflictoDeVersion';
import { Pagina404 } from '../errores/PaginasDeError';
import { esPasswordIncorrecta, mensajeFallaGestion, textoPasswordIncorrecta } from '../compartidos/reautenticacion';
import { campoDeConflicto, erroresDeApi, requierePasswordEmpleado, textoConflicto } from './reglasMantenedores';
import { MedidasYOpciones } from './MedidasYOpciones';
import { baseDe, TIPOS } from './tipos';
import { ZonaActivo, ZonaVitrina } from './Zonas';
import estilosPanel from '../panel/Panel.module.css';
import estilos from './Mantenedores.module.css';

/**
 * /panel/mantenedores/:tipo/nuevo y /panel/mantenedores/:tipo/:id: el formulario de un registro. Guardar envía
 * la versión vista (If-Match); si otra persona lo cambió, lo escrito se queda y se ofrece guardar sobre la versión
 * actual o descartar (el patrón de la fase 8). Más abajo, su estado: activar o desactivar (o, en la vitrina, la
 * autorización del logo y si se muestra).
 */
export default function EditorMantenedor({ tipo: tipoFijo }) {
  const { tipo: claveDeLaDireccion, id } = useParams();
  // El personal (fase 12D) tiene su propia ruta, /panel/empleados: llega con el tipo fijo, sin :tipo en la dirección.
  const claveTipo = tipoFijo ?? claveDeLaDireccion;
  const tipo = TIPOS[claveTipo];
  const nuevo = !id;
  const consulta = useConsulta(`${claveTipo}:${id ?? 'nuevo'}`, () => (nuevo || !tipo ? Promise.resolve(null) : tipo.api.obtener(id)));
  const registro = consulta.datos;
  // Con cambios sin guardar, las acciones de estado (activar, ocultar, autorizar) esperan: recargar la ficha
  // tras ellas no debe borrar lo escrito.
  const [sucio, setSucio] = useState(false);
  const volver = baseDe(claveTipo);

  // /panel/mantenedores/personal… era la dirección anterior del personal.
  if (!tipoFijo && claveTipo === 'personal') return <Navigate to={`/panel/empleados${id ? `/${id}` : ''}`} replace />;
  if (!tipo || (id && !/^[1-9]\d{0,9}$/.test(id))) return <Pagina404 />;
  const titulo = nuevo ? tipo.nuevo : registro ? tipo.nombreDe(registro) : `Editar ${tipo.singular}`;

  return (
    <div className={estilosPanel.pagina}>
      <div>
        <Link to={volver} className={estilos.volver}><ArrowLeft size={16} strokeWidth={1.75} aria-hidden="true" />Volver a {tipo.titulo.toLowerCase()}</Link>
        <h1 className={estilosPanel.titulo}>{titulo}</h1>
        {!nuevo && registro && <p className={estilosPanel.bajada}>Administrar {tipo.singular}</p>}
      </div>

      {consulta.cargando && <><p role="status" className="solo-lectores">Cargando…</p><Esqueleto lineas={6} /></>}
      {consulta.error && (
        consulta.error.estado === 404
          ? <EstadoVacio titulo={`No encontramos ${tipo.entidad}`} descripcion="Puede que la dirección esté incompleta."
            accion={<Boton como={Link} to={volver} variante="secundario">Volver al listado</Boton>} />
          : (
            <Aviso tono="peligro" titulo="No pudimos cargar el registro"
              accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
              {consulta.error.message}
            </Aviso>
          )
      )}
      {(nuevo || registro) && (
        <Formulario key={id ?? 'nuevo'} tipo={tipo} registro={registro} nuevo={nuevo} id={id}
          alGuardar={consulta.recargar} alCambiarSucio={setSucio} />
      )}
      {/* Fase 12D: límites de medida, medidas fijas y opciones del tipo de letrero. */}
      {!nuevo && registro && tipo.clave === 'tipos-de-letrero' && <MedidasYOpciones registro={registro} alGuardar={consulta.recargar} />}
      {!nuevo && registro && (
        tipo.clave === 'vitrina'
          ? <ZonaVitrina registro={registro} id={id} alCambiar={consulta.recargar} hayCambios={sucio} />
          : <ZonaActivo tipo={tipo} registro={registro} id={id} alCambiar={consulta.recargar} hayCambios={sucio} />
      )}
    </div>
  );
}

const MENSAJE_SUBIENDO = 'Espera a que termine de subir la imagen.';

/**
 * El formulario queda montado mientras se edita la ficha: si la ficha se recarga (tras guardar, activar u ocultar),
 * toma los datos nuevos solo si no hay cambios sin guardar, o justo después de guardar o descartar. La versión que
 * se envía es la de la última lectura (o, tras un conflicto, la releída).
 */
function Formulario({ tipo, registro, nuevo, id, alGuardar, alCambiarSucio }) {
  const navegar = useNavigate();
  const { anunciar } = useAvisos();
  const { usuario } = useSesion();
  const desde = (r) => (r ? tipo.desdeApi(r) : { ...tipo.vacio });
  const [form, setForm] = useState(() => desde(registro));
  const [base, setBase] = useState(() => desde(registro));
  const [registroVisto, setRegistroVisto] = useState(registro);
  const [reiniciar, setReiniciar] = useState(false);
  const [errores, setErrores] = useState({});
  const [estado, setEstado] = useState({ guardando: false, error: null });
  const [subiendo, setSubiendo] = useState(false);
  const refFormulario = useRef(null);
  const idMotivoGuardar = useId();
  const version = useConflictoDeVersion({ releer: () => tipo.api.obtener(id), versionInicial: registro?.rowVersion });
  const sucio = JSON.stringify(form) !== JSON.stringify(base);
  // La sesión expone el id del empleado como usuario.id (sesion.js, usuarioDesde).
  const esUnoMismo = tipo.clave === 'personal' && !nuevo && Number(registro?.idEmpleado) === Number(usuario?.id);
  const pidePassword = tipo.clave === 'personal' && !nuevo && requierePasswordEmpleado(form, registro);

  if (registro !== registroVisto) {
    setRegistroVisto(registro);
    if (registro && (reiniciar || !sucio)) {
      setForm(desde(registro));
      setBase(desde(registro));
      setReiniciar(false);
    } else if (registro) {
      setBase(desde(registro));
    }
  }

  useEffect(() => {
    alCambiarSucio?.(sucio && !nuevo);
  }, [sucio, nuevo, alCambiarSucio]);

  function cambiar(campo, valor) {
    setForm((actual) => ({ ...actual, [campo]: valor }));
    setErrores((actual) => {
      if (!actual[campo]) return actual;
      const resto = { ...actual };
      delete resto[campo];
      return resto;
    });
  }

  const campo = (nombre) => ({
    name: nombre,
    value: form[nombre] ?? '',
    onChange: (e) => cambiar(nombre, e.target.value),
    error: errores[nombre],
    disabled: estado.guardando,
  });

  function enfocarPrimerError(lista) {
    const primero = Object.keys(lista)[0];
    if (primero) setTimeout(() => refFormulario.current?.querySelector(`[name="${primero}"], [aria-invalid="true"]`)?.focus());
  }

  async function guardar(evento, sobreActual = false) {
    evento?.preventDefault();
    if (subiendo) return;
    const problemas = tipo.validar(form, { nuevo, registro });
    setErrores(problemas);
    if (Object.keys(problemas).length) {
      enfocarPrimerError(problemas);
      return;
    }
    setEstado({ guardando: true, error: null });
    try {
      const datos = tipo.paraApi(form, { nuevo, registro });
      if (nuevo) {
        const creado = await tipo.api.crear(datos);
        anunciar({ tono: 'exito', titulo: `${tipo.nombreDe(creado) || 'Registro'} quedó guardado` });
        const idNuevo = creado?.[tipo.idCampo];
        navegar(idNuevo ? `${baseDe(tipo.clave)}/${idNuevo}` : baseDe(tipo.clave), { replace: true });
        return;
      }
      await tipo.api.editar(id, datos, sobreActual ? version.version : registro?.rowVersion);
      version.olvidar();
      anunciar({
        tono: 'exito',
        titulo: 'Cambios guardados',
        texto: pidePassword ? 'Se cerraron las sesiones abiertas de esta persona.' : undefined,
      });
      setEstado({ guardando: false, error: null });
      setReiniciar(true);
      alGuardar();
    } catch (falla) {
      setEstado({ guardando: false, error: null });
      if (falla?.codigo === 'version_obsoleta') {
        version.detectar();
        return;
      }
      if (esPasswordIncorrecta(falla)) {
        setForm((actual) => ({ ...actual, passwordActual: '' }));
        setErrores({ passwordActual: textoPasswordIncorrecta(falla) });
        enfocarPrimerError({ passwordActual: true });
        return;
      }
      if (falla?.estado === 400 && falla.errores && Object.keys(falla.errores).length) {
        const deApi = erroresDeApi(falla.errores);
        setErrores(deApi);
        enfocarPrimerError(deApi);
        return;
      }
      const delCampo = campoDeConflicto(falla, tipo.clave);
      if (delCampo) {
        setErrores({ [delCampo]: textoConflicto(falla) });
        enfocarPrimerError({ [delCampo]: true });
        return;
      }
      setEstado({ guardando: false, error: falla?.estado === 409 ? textoConflicto(falla) : mensajeFallaGestion(falla, 'No pudimos guardar.') });
    }
  }

  function descartar() {
    const actual = version.conflicto?.actual;
    version.olvidar();
    if (actual) {
      setForm(desde(actual));
      setBase(desde(actual));
    }
    setReiniciar(true);
    alGuardar();
  }

  const actual = version.conflicto?.actual;
  const { Campos } = tipo;
  return (
    <Tarjeta como="section" aria-labelledby="titulo-datos">
      <form ref={refFormulario} className={estilos.formulario} onSubmit={guardar} noValidate>
        <h2 id="titulo-datos" className={estilosPanel.subtitulo}>Datos</h2>
        <AvisoConflicto conflicto={version.conflicto} entidad={tipo.entidad} guardando={estado.guardando} refAccion={version.refAccion}
          resumen={actual ? `${tipo.nombreDe(actual)}${actual.activo === false ? ' (inactivo)' : ''}.` : null}
          alGuardarSobre={() => guardar(null, true)} alReintentar={version.detectar} alDescartar={descartar} />
        {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
        <Campos campo={campo} form={form} cambiar={cambiar} errores={errores} registro={registro} nuevo={nuevo}
          deshabilitado={estado.guardando} esUnoMismo={esUnoMismo} pidePassword={pidePassword} alSubiendo={setSubiendo} />
        <div className={estilosPanel.pieFormulario}>
          {subiendo && <p id={idMotivoGuardar} className={estilos.motivo}>{MENSAJE_SUBIENDO}</p>}
          <Boton type="submit" iconoInicio={Save} cargando={estado.guardando} textoCargando="Guardando…"
            aria-disabled={subiendo ? 'true' : undefined} aria-describedby={subiendo ? idMotivoGuardar : undefined}
            className={subiendo ? estilos.inactivo : undefined}>
            {nuevo ? 'Guardar' : 'Guardar cambios'}
          </Boton>
        </div>
      </form>
    </Tarjeta>
  );
}
