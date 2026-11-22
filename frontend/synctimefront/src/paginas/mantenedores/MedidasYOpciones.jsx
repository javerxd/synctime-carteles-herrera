import { useId, useRef, useState } from 'react';
import { Plus, Save, Trash2 } from 'lucide-react';
import { Aviso, Boton, Campo, Opciones, Tarjeta } from '../../componentes/ui';
import { productos } from '../../api/mantenedores';
import { useAvisos } from '../../avisos/Avisos';
import { mensajeFallaGestion } from '../compartidos/reautenticacion';
import { erroresDeApi } from './reglasMantenedores';
import {
  agregarGrupo, agregarMedidaFija, agregarValor, cambiarGrupo, cambiarMedidaFija, cambiarValor, LARGO_GRUPO, LARGO_VALOR, MAX_FIJAS,
  MAX_GRUPOS, MAX_VALORES, medidasDesdeApi, medidasParaApi, MODOS, quitarGrupo, quitarMedidaFija, quitarValor, validarMedidasYOpciones,
} from './reglasMedidasYOpciones';
import estilosPanel from '../panel/Panel.module.css';
import estilos from './MedidasYOpciones.module.css';

const FORMAS = [
  { valor: MODOS.rangos, etiqueta: 'Límites de ancho y alto', descripcion: 'El cliente escribe la medida, dentro de un mínimo y un máximo.' },
  { valor: MODOS.fijas, etiqueta: 'Medidas fijas', descripcion: 'Solo se hace en los tamaños de la lista (por ejemplo, un X Banner).' },
  { valor: MODOS.ninguno, etiqueta: 'Sin medidas', descripcion: 'No se piden medidas (por ejemplo, las señalizaciones).' },
];

const CAMPOS_RANGO = [
  ['anchoMin', 'anchoMinMm', 'Ancho mínimo (cm)'],
  ['anchoMax', 'anchoMaxMm', 'Ancho máximo (cm)'],
  ['altoMin', 'altoMinMm', 'Alto mínimo (cm)'],
  ['altoMax', 'altoMaxMm', 'Alto máximo (cm)'],
];

/**
 * Medidas y opciones de un tipo de letrero (fase 12D, decisión 6): los límites de ancho y alto en centímetros, las
 * medidas fijas y las opciones por grupo. Se guardan aparte de los datos básicos, con la versión vista (If-Match). Las
 * solicitudes ya hechas conservan lo que el cliente eligió; el formulario nuevo solo ofrece lo vigente.
 */
export function MedidasYOpciones({ registro, alGuardar }) {
  const { anunciar } = useAvisos();
  const [form, setForm] = useState(() => medidasDesdeApi(registro));
  const [base, setBase] = useState(() => medidasDesdeApi(registro));
  const [visto, setVisto] = useState(registro);
  const [errores, setErrores] = useState({});
  const [estado, setEstado] = useState({ guardando: false, error: null });
  const refSeccion = useRef(null);
  const refAgregarMedida = useRef(null);
  const refAgregarGrupo = useRef(null);
  const idTitulo = useId();
  const idErrorFijas = useId();
  const idErrorOpciones = useId();
  const sucio = JSON.stringify(form) !== JSON.stringify(base);

  // Si la ficha se recarga (tras guardar o por un cambio ajeno), el formulario toma lo vigente salvo que haya cambios sin guardar.
  if (registro !== visto) {
    setVisto(registro);
    if (!sucio) setForm(medidasDesdeApi(registro));
    setBase(medidasDesdeApi(registro));
  }

  function cambiar(siguiente, campoLimpiar) {
    setForm(siguiente);
    if (campoLimpiar && errores[campoLimpiar]) {
      setErrores((actual) => {
        const resto = { ...actual };
        delete resto[campoLimpiar];
        return resto;
      });
    }
  }

  function enfocarPrimerError() {
    setTimeout(() => {
      const objetivo = refSeccion.current?.querySelector('[aria-invalid="true"], [data-error-lista]');
      objetivo?.focus();
    });
  }

  // Tras quitar un valor, el foco pasa al «Agregar valor» de su grupo (el botón quitado ya no existe).
  function enfocarAgregarValor(indiceGrupo) {
    setTimeout(() => refSeccion.current?.querySelector(`[data-agregar-valor="${indiceGrupo}"]`)?.focus());
  }

  async function guardar(evento) {
    evento.preventDefault();
    const problemas = validarMedidasYOpciones(form);
    setErrores(problemas);
    if (Object.keys(problemas).length) {
      enfocarPrimerError();
      return;
    }
    setEstado({ guardando: true, error: null });
    try {
      await productos.editarMedidasYOpciones(registro.idProducto, medidasParaApi(form), registro.rowVersion);
      anunciar({ tono: 'exito', titulo: 'Medidas y opciones guardadas', texto: 'Las solicitudes ya hechas conservan lo que el cliente eligió.' });
      setEstado({ guardando: false, error: null });
      setBase(form);
      alGuardar();
    } catch (falla) {
      if (falla?.codigo === 'version_obsoleta') {
        // Otra persona lo cambió: se carga la versión vigente y lo escrito aquí se conserva (como en «Datos»), sin aplicarse a ciegas.
        setEstado({ guardando: false, error: 'Otra persona modificó este tipo de letrero mientras lo veías. Cargamos la versión actual y conservamos lo que escribiste: revisa y, si sigues de acuerdo, guarda de nuevo (reemplazará lo que cambió esa persona).' });
        alGuardar();
        return;
      }
      if (falla?.estado === 400 && falla.errores && Object.keys(falla.errores).length) {
        setErrores(erroresDeApi(falla.errores));
        setEstado({ guardando: false, error: null });
        enfocarPrimerError();
        return;
      }
      setEstado({ guardando: false, error: mensajeFallaGestion(falla, 'No pudimos guardar las medidas y las opciones.') });
    }
  }

  const deshabilitado = estado.guardando;

  return (
    <Tarjeta como="section" aria-labelledby={idTitulo}>
      <form ref={refSeccion} className={estilos.formulario} onSubmit={guardar} noValidate>
        <h2 id={idTitulo} className={estilosPanel.subtitulo}>Medidas y opciones</h2>
        <p className={estilos.explicacion}>
          Lo que el cliente puede elegir al pedir este tipo de letrero. Las medidas van en centímetros. Cada cambio queda en la auditoría.
        </p>
        {estado.error && <Aviso tono="peligro" titulo={estado.error} />}

        <Opciones leyenda="Cómo se miden" nombre="forma-de-medir" opciones={FORMAS} valor={form.modo} deshabilitado={deshabilitado}
          alCambiar={(modo) => { setForm({ ...form, modo }); setErrores({}); }} />

        {form.modo === MODOS.rangos && (
          <div className={estilos.rangos}>
            {CAMPOS_RANGO.map(([campo, clave, etiqueta]) => (
              <Campo key={campo} etiqueta={etiqueta} requerido inputMode="decimal" autoComplete="off" disabled={deshabilitado}
                value={form[campo]} error={errores[clave]} onChange={(e) => cambiar({ ...form, [campo]: e.target.value }, clave)} />
            ))}
          </div>
        )}

        {form.modo === MODOS.fijas && (
          <fieldset className={estilos.lista}>
            <legend className={estilos.leyenda}>Medidas fijas</legend>
            {form.fijas.map((medida, i) => (
              <div key={i} className={estilos.fila} role="group" aria-label={`Medida ${i + 1}`}>
                <Campo etiqueta={`Ancho de la medida ${i + 1} (cm)`} requerido inputMode="decimal" autoComplete="off" disabled={deshabilitado}
                  value={medida.ancho} aria-invalid={errores.medidasFijas ? 'true' : undefined}
                  aria-describedby={errores.medidasFijas ? idErrorFijas : undefined}
                  onChange={(e) => cambiar(cambiarMedidaFija(form, i, 'ancho', e.target.value), 'medidasFijas')} />
                <Campo etiqueta={`Alto de la medida ${i + 1} (cm)`} requerido inputMode="decimal" autoComplete="off" disabled={deshabilitado}
                  value={medida.alto} aria-invalid={errores.medidasFijas ? 'true' : undefined}
                  aria-describedby={errores.medidasFijas ? idErrorFijas : undefined}
                  onChange={(e) => cambiar(cambiarMedidaFija(form, i, 'alto', e.target.value), 'medidasFijas')} />
                <Boton variante="secundario" tamano="compacto" iconoInicio={Trash2} disabled={deshabilitado} className={estilos.quitar}
                  aria-label={`Quitar la medida ${i + 1}`}
                  onClick={() => { cambiar(quitarMedidaFija(form, i), 'medidasFijas'); refAgregarMedida.current?.focus(); }}>
                  Quitar
                </Boton>
              </div>
            ))}
            {errores.medidasFijas && <p id={idErrorFijas} role="alert" tabIndex={-1} data-error-lista className={estilos.errorLista}>{errores.medidasFijas}</p>}
            <div>
              <Boton ref={refAgregarMedida} variante="secundario" tamano="compacto" iconoInicio={Plus} aria-disabled={deshabilitado || form.fijas.length >= MAX_FIJAS ? 'true' : undefined}
                onClick={() => (form.fijas.length < MAX_FIJAS ? cambiar(agregarMedidaFija(form)) : null)}>
                Agregar medida
              </Boton>
            </div>
          </fieldset>
        )}

        <fieldset className={estilos.lista}>
          <legend className={estilos.leyenda}>Opciones que elige el cliente</legend>
          {form.grupos.length === 0 && <p className={estilos.vacio}>Este tipo de letrero no tiene opciones. Agrega un grupo, por ejemplo «Material» o «Acabado».</p>}
          {form.grupos.map((grupo, i) => (
            <div key={i} className={estilos.grupo} role="group" aria-label={`Grupo de opciones ${i + 1}`}>
              <div className={estilos.cabeceraGrupo}>
                <Campo etiqueta={`Nombre del grupo ${i + 1}`} requerido maxLength={LARGO_GRUPO} autoComplete="off" disabled={deshabilitado}
                  value={grupo.grupo} aria-invalid={errores.opciones ? 'true' : undefined}
                  aria-describedby={errores.opciones ? idErrorOpciones : undefined}
                  onChange={(e) => cambiar(cambiarGrupo(form, i, e.target.value), 'opciones')} />
                <Boton variante="secundario" tamano="compacto" iconoInicio={Trash2} disabled={deshabilitado} className={estilos.quitar}
                  aria-label={`Quitar grupo ${grupo.grupo.trim() || i + 1}`}
                  onClick={() => { cambiar(quitarGrupo(form, i), 'opciones'); refAgregarGrupo.current?.focus(); }}>
                  Quitar grupo
                </Boton>
              </div>
              {grupo.valores.map((valor, j) => (
                <div key={j} className={estilos.fila}>
                  <Campo etiqueta={`Valor ${j + 1} de ${grupo.grupo.trim() || `grupo ${i + 1}`}`} maxLength={LARGO_VALOR} autoComplete="off" disabled={deshabilitado}
                    value={valor} aria-invalid={errores.opciones ? 'true' : undefined}
                    aria-describedby={errores.opciones ? idErrorOpciones : undefined}
                    onChange={(e) => cambiar(cambiarValor(form, i, j, e.target.value), 'opciones')} />
                  <Boton variante="fantasma" tamano="compacto" iconoInicio={Trash2} disabled={deshabilitado} className={estilos.quitar}
                    aria-label={`Quitar valor ${j + 1} de ${grupo.grupo.trim() || `grupo ${i + 1}`}`}
                    onClick={() => { cambiar(quitarValor(form, i, j), 'opciones'); enfocarAgregarValor(i); }}>
                    Quitar
                  </Boton>
                </div>
              ))}
              <div>
                <Boton variante="fantasma" tamano="compacto" iconoInicio={Plus} data-agregar-valor={i}
                  aria-disabled={deshabilitado || grupo.valores.length >= MAX_VALORES ? 'true' : undefined}
                  aria-label={`Agregar valor a ${grupo.grupo.trim() || `grupo ${i + 1}`}`}
                  onClick={() => (grupo.valores.length < MAX_VALORES ? cambiar(agregarValor(form, i)) : null)}>
                  Agregar valor
                </Boton>
              </div>
            </div>
          ))}
          {errores.opciones && <p id={idErrorOpciones} role="alert" tabIndex={-1} data-error-lista className={estilos.errorLista}>{errores.opciones}</p>}
          <div>
            <Boton ref={refAgregarGrupo} variante="secundario" tamano="compacto" iconoInicio={Plus} aria-disabled={deshabilitado || form.grupos.length >= MAX_GRUPOS ? 'true' : undefined}
              onClick={() => (form.grupos.length < MAX_GRUPOS ? cambiar(agregarGrupo(form)) : null)}>
              Agregar grupo
            </Boton>
          </div>
        </fieldset>

        <div className={estilosPanel.pieFormulario}>
          <Boton type="submit" iconoInicio={Save} cargando={estado.guardando} textoCargando="Guardando…">
            Guardar medidas y opciones
          </Boton>
        </div>
      </form>
    </Tarjeta>
  );
}
