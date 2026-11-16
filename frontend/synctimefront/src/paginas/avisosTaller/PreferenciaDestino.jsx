import { useId, useRef, useState } from 'react';
import { Save } from 'lucide-react';
import { Aviso, Boton, Casilla } from '../../componentes/ui';
import { avisosTaller } from '../../api/avisosTaller';
import { useAvisos } from '../../avisos/Avisos';
import { AvisoConflicto } from '../compartidos/AvisoConflicto';
import { mensajeFallaGestion } from '../compartidos/reautenticacion';
import { useConflictoDeVersion } from '../compartidos/useConflictoDeVersion';
import { errorPreferencia, preferenciaDe, textoPreferencia } from './reglasAvisosTaller';
import estilos from './AvisosTaller.module.css';

/**
 * Qué solicitudes avisa este destino (fase 11, decisión 1): las en línea, las presenciales o ambas; al menos una.
 * Se guarda con la versión vista (If-Match). Si otra persona lo cambió, lo marcado se queda y se ofrece guardar
 * sobre la versión actual o descartar, como en los mantenedores. No pide contraseña.
 */
export function PreferenciaDestino({ destino, alGuardado }) {
  const { anunciar } = useAvisos();
  const [form, setForm] = useState(() => preferenciaDe(destino));
  const [base, setBase] = useState(() => preferenciaDe(destino));
  const [destinoVisto, setDestinoVisto] = useState(destino);
  const [estado, setEstado] = useState({ guardando: false, error: null, errorGrupo: null });
  const refPrimera = useRef(null);
  const idError = useId();
  const id = destino.idDestinatario;
  const version = useConflictoDeVersion({
    releer: async () => (await avisosTaller.obtener())?.destinos?.find((d) => d.idDestinatario === id) ?? null,
    versionInicial: destino.rowVersion,
  });
  const sucio = form.recibeEnLinea !== base.recibeEnLinea || form.recibePresenciales !== base.recibePresenciales;

  // Si la sección se recarga y aquí no hay cambios sin guardar, se muestra lo nuevo.
  if (destino !== destinoVisto) {
    setDestinoVisto(destino);
    setBase(preferenciaDe(destino));
    if (!sucio) setForm(preferenciaDe(destino));
  }

  function cambiar(campo, valor) {
    setForm((f) => ({ ...f, [campo]: valor }));
    setEstado((e) => ({ ...e, errorGrupo: null }));
  }

  async function guardar(evento, sobreActual = false) {
    evento?.preventDefault();
    if (estado.guardando) return;
    const problema = errorPreferencia(form);
    if (problema) {
      setEstado({ guardando: false, error: null, errorGrupo: problema });
      refPrimera.current?.focus();
      return;
    }
    setEstado({ guardando: true, error: null, errorGrupo: null });
    try {
      await avisosTaller.preferencia(id, form, sobreActual ? version.version : destino.rowVersion);
      version.olvidar();
      setBase(form);
      setEstado({ guardando: false, error: null, errorGrupo: null });
      anunciar({ tono: 'exito', titulo: 'Preferencia guardada', texto: `${destino.correo} recibirá ${textoPreferencia(form)}.` });
      alGuardado?.();
    } catch (falla) {
      if (falla?.codigo === 'version_obsoleta') {
        setEstado({ guardando: false, error: null, errorGrupo: null });
        version.detectar();
        return;
      }
      const delCampo = Object.entries(falla?.errores ?? {}).find(([c]) => c.toLowerCase() === 'preferencia')?.[1];
      if (falla?.estado === 400 && delCampo) {
        setEstado({ guardando: false, error: null, errorGrupo: Array.isArray(delCampo) ? delCampo[0] : String(delCampo) });
        refPrimera.current?.focus();
        return;
      }
      setEstado({ guardando: false, error: mensajeFallaGestion(falla, 'No pudimos guardar la preferencia.'), errorGrupo: null });
      if (falla?.estado === 404) alGuardado?.();
    }
  }

  function descartar() {
    const vigente = version.conflicto?.actual;
    version.olvidar();
    if (vigente) {
      setForm(preferenciaDe(vigente));
      setBase(preferenciaDe(vigente));
    }
    alGuardado?.();
  }

  const vigente = version.conflicto?.actual;
  const describe = estado.errorGrupo ? idError : undefined;
  return (
    <form className={estilos.preferencia} onSubmit={guardar} noValidate>
      {/* El nombre del grupo incluye el correo: con varios destinos, el lector sabe de cuál es cada par de casillas. */}
      <fieldset className={estilos.grupoPreferencia} aria-label={`Avisos que recibe ${destino.correo}`} aria-describedby={describe}>
        <legend className={estilos.leyenda} aria-hidden="true">Avisos que recibe</legend>
        <div className={estilos.casillas}>
          <Casilla ref={refPrimera} etiqueta="Solicitudes en línea" checked={form.recibeEnLinea} disabled={estado.guardando}
            aria-describedby={describe} onChange={(e) => cambiar('recibeEnLinea', e.target.checked)} />
          <Casilla etiqueta="Solicitudes presenciales" checked={form.recibePresenciales} disabled={estado.guardando}
            aria-describedby={describe} onChange={(e) => cambiar('recibePresenciales', e.target.checked)} />
        </div>
        <p id={idError} className={estilos.errorGrupo} aria-live="polite">{estado.errorGrupo ?? ''}</p>
        <AvisoConflicto conflicto={version.conflicto} entidad="este destino" guardando={estado.guardando} refAccion={version.refAccion}
          resumen={vigente ? `recibe ${textoPreferencia(preferenciaDe(vigente))}.` : null}
          alGuardarSobre={() => guardar(null, true)} alReintentar={version.detectar} alDescartar={descartar} />
        {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
        <div className={estilos.acciones}>
          <Boton type="submit" variante="secundario" tamano="compacto" iconoInicio={Save} cargando={estado.guardando} textoCargando="Guardando…">
            Guardar preferencia
          </Boton>
        </div>
      </fieldset>
    </form>
  );
}
