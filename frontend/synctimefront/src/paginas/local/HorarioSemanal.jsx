import { useState } from 'react';
import { Save } from 'lucide-react';
import { Aviso, Boton, Campo, Casilla } from '../../componentes/ui';
import { panel } from '../../api/panel';
import { useAvisos } from '../../avisos/Avisos';
import { franja } from '../publicas/horario';
import { horarioDesdeApi, horarioParaApi, validarHorario } from './reglasLocal';
import estilosPanel from '../panel/Panel.module.css';
import estilos from './Local.module.css';

/** El horario de la semana, de solo lectura (lo que ve el personal que no es de gestión). */
export function HorarioLectura({ dias, hoy }) {
  return (
    <dl className={estilos.semana}>
      {dias.map((dia) => (
        <div key={dia.diaSemana} className={estilos.diaLectura} data-hoy={dia.diaSemana === hoy ? 'true' : undefined}>
          <dt>{dia.nombreDia}{dia.diaSemana === hoy ? ' (hoy)' : ''}</dt>
          <dd className="cifras">{franja(dia)}</dd>
        </div>
      ))}
    </dl>
  );
}

/**
 * Edición del horario (E10, solo gestión): los 7 días en un solo envío. Un día abierto pide apertura y cierre,
 * con el cierre después de la apertura. La portada lo muestra en cuanto se guarda.
 */
export function EditorHorario({ dias, alGuardar }) {
  const { anunciar } = useAvisos();
  const [form, setForm] = useState(() => horarioDesdeApi(dias));
  const [errores, setErrores] = useState({});
  const [estado, setEstado] = useState({ guardando: false, error: null });

  function cambiar(diaSemana, campo, valor) {
    setForm((actual) => actual.map((d) => (d.diaSemana === diaSemana ? { ...d, [campo]: valor } : d)));
    setErrores((actual) => {
      const resto = { ...actual };
      delete resto[`${diaSemana}.${campo}`];
      return resto;
    });
  }

  async function enviar(evento) {
    evento.preventDefault();
    const problemas = validarHorario(form);
    setErrores(problemas);
    const primero = Object.keys(problemas)[0];
    if (primero) {
      evento.currentTarget.querySelector(`[name="${primero}"]`)?.focus();
      return;
    }
    setEstado({ guardando: true, error: null });
    try {
      await panel.guardarHorario(horarioParaApi(form));
      anunciar({ tono: 'exito', titulo: 'Horario guardado', texto: 'La portada ya muestra el horario nuevo.' });
      setEstado({ guardando: false, error: null });
      alGuardar?.();
    } catch (error) {
      setEstado({ guardando: false, error: error?.message || 'No pudimos guardar el horario.' });
    }
  }

  return (
    <form className={estilos.formularioHorario} onSubmit={enviar} noValidate>
      <div className={estilos.dias}>
        {form.map((d) => (
          <div key={d.diaSemana} role="group" aria-labelledby={`horario-dia-${d.diaSemana}`} className={estilos.diaEditable}
            data-cerrado={d.abierto ? undefined : 'true'}>
            <span id={`horario-dia-${d.diaSemana}`} className={estilos.nombreDia}>{d.nombreDia}</span>
            <Casilla etiqueta="Abre" aria-label={`Abre el ${d.nombreDia.toLowerCase()}`} checked={d.abierto} onChange={(e) => cambiar(d.diaSemana, 'abierto', e.target.checked)}
              disabled={estado.guardando} className={estilos.casillaDia} />
            <Campo etiqueta="Apertura" type="time" name={`${d.diaSemana}.horaInicio`} value={d.horaInicio} disabled={!d.abierto || estado.guardando}
              onChange={(e) => cambiar(d.diaSemana, 'horaInicio', e.target.value)} error={errores[`${d.diaSemana}.horaInicio`]} />
            <Campo etiqueta="Cierre" type="time" name={`${d.diaSemana}.horaFin`} value={d.horaFin} disabled={!d.abierto || estado.guardando}
              onChange={(e) => cambiar(d.diaSemana, 'horaFin', e.target.value)} error={errores[`${d.diaSemana}.horaFin`]} />
          </div>
        ))}
      </div>
      {estado.error && <Aviso tono="peligro" titulo="No pudimos guardar el horario">{estado.error}</Aviso>}
      <div className={estilosPanel.acciones}>
        <Boton type="submit" iconoInicio={Save} cargando={estado.guardando} textoCargando="Guardando…">Guardar horario</Boton>
      </div>
    </form>
  );
}
