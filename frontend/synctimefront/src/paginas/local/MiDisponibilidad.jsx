import { useRef, useState } from 'react';
import { Plus, Save, X } from 'lucide-react';
import { Aviso, Boton, Campo, Esqueleto } from '../../componentes/ui';
import { panel } from '../../api/panel';
import { useAvisos } from '../../avisos/Avisos';
import { useConsulta } from '../../utilidades/useConsulta';
import { franja } from '../publicas/horario';
import {
  aMinutos, franjasDesdeApi, franjasParaApi, franjaVacia, horaCorta, horarioDesdeApi, validarFranjas,
} from './reglasLocal';
import estilosPanel from '../panel/Panel.module.css';
import estilos from './Local.module.css';

/**
 * «Mi disponibilidad» (RFU20): las franjas de cada día de la semana en que la persona atiende. Se repiten cada
 * semana (decisión 2 del plan). La portada muestra la unión de las franjas de todo el personal, sin nombres.
 */
export function MiDisponibilidad({ horario }) {
  const consulta = useConsulta('mi-disponibilidad', panel.miDisponibilidad);
  if (consulta.cargando) {
    return <><p role="status" className="solo-lectores">Cargando tu disponibilidad…</p><Esqueleto lineas={4} /></>;
  }
  if (consulta.error) {
    return (
      <Aviso tono="peligro" titulo="No pudimos cargar tu disponibilidad"
        accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
        {consulta.error.message}
      </Aviso>
    );
  }
  return <EditorDisponibilidad inicial={consulta.datos ?? []} horario={horarioDesdeApi(horario)} />;
}

function EditorDisponibilidad({ inicial, horario }) {
  const { anunciar } = useAvisos();
  const [franjas, setFranjas] = useState(() => franjasDesdeApi(inicial));
  const [errores, setErrores] = useState({});
  const [estado, setEstado] = useState({ guardando: false, error: null });
  const refFormulario = useRef(null);

  function cambiar(clave, campo, valor) {
    setFranjas((actual) => actual.map((f) => (f.clave === clave ? { ...f, [campo]: valor } : f)));
    // Solo se limpian los errores de la franja que se está corrigiendo.
    setErrores((actual) => {
      const resto = { ...actual };
      delete resto[`${clave}.horaInicio`];
      delete resto[`${clave}.horaFin`];
      return resto;
    });
  }

  function agregar(dia) {
    const delDia = franjas.filter((f) => f.diaSemana === dia.diaSemana);
    // La primera franja del día parte con el horario del taller; las siguientes, desde donde termina la última.
    const ultima = delDia.at(-1);
    const nueva = franjaVacia(dia.diaSemana);
    if (!ultima) Object.assign(nueva, { horaInicio: dia.horaInicio, horaFin: dia.horaFin });
    else if (aMinutos(ultima.horaFin) !== null && aMinutos(ultima.horaFin) < aMinutos(dia.horaFin)) {
      Object.assign(nueva, { horaInicio: horaCorta(ultima.horaFin), horaFin: dia.horaFin });
    }
    setFranjas((actual) => [...actual, nueva]);
    setErrores({});
    setTimeout(() => refFormulario.current?.querySelector(`[name="${nueva.clave}.horaInicio"]`)?.focus());
  }

  function quitar(franjaQuitada, dia) {
    setFranjas((actual) => actual.filter((f) => f.clave !== franjaQuitada.clave));
    setErrores({});
    // El foco vuelve a «Agregar franja» de ese día; un día cerrado no lo tiene, así que va al nombre del día.
    setTimeout(() => {
      const destino = refFormulario.current?.querySelector(`[data-agregar="${dia}"]`) ?? document.getElementById(`dispo-dia-${dia}`);
      destino?.focus();
    });
  }

  async function enviar(evento) {
    evento.preventDefault();
    const problemas = validarFranjas(franjas, horario);
    setErrores(problemas);
    const primero = Object.keys(problemas)[0];
    if (primero) {
      refFormulario.current?.querySelector(`[name="${primero}"]`)?.focus();
      return;
    }
    setEstado({ guardando: true, error: null });
    try {
      await panel.guardarMiDisponibilidad(franjasParaApi(franjas));
      anunciar({ tono: 'exito', titulo: 'Disponibilidad guardada', texto: 'La portada muestra las franjas de atención, sin tu nombre.' });
      setEstado({ guardando: false, error: null });
    } catch (error) {
      setEstado({ guardando: false, error: error?.message || 'No pudimos guardar tu disponibilidad.' });
    }
  }

  return (
    <form ref={refFormulario} className={estilos.formularioHorario} onSubmit={enviar} noValidate>
      <ul className={estilos.listaDias}>
        {horario.map((dia) => {
          const delDia = franjas.filter((f) => f.diaSemana === dia.diaSemana);
          return (
            <li key={dia.diaSemana} className={estilos.diaDisponibilidad} data-cerrado={dia.abierto ? undefined : 'true'}>
              <div className={estilos.encabezadoDia}>
                <h3 className={estilos.nombreDia} id={`dispo-dia-${dia.diaSemana}`} tabIndex={-1}>{dia.nombreDia}</h3>
                <span className={`cifras ${estilos.horarioDia}`}>Taller: {franja(dia)}</span>
              </div>
              {delDia.length > 0 && (
                <ul className={estilos.franjas} aria-labelledby={`dispo-dia-${dia.diaSemana}`}>
                  {delDia.map((f, i) => (
                    <li key={f.clave} className={estilos.franja}>
                      <Campo etiqueta="Desde" type="time" name={`${f.clave}.horaInicio`} value={f.horaInicio} disabled={estado.guardando}
                        onChange={(e) => cambiar(f.clave, 'horaInicio', e.target.value)} error={errores[`${f.clave}.horaInicio`]} />
                      <Campo etiqueta="Hasta" type="time" name={`${f.clave}.horaFin`} value={f.horaFin} disabled={estado.guardando}
                        onChange={(e) => cambiar(f.clave, 'horaFin', e.target.value)} error={errores[`${f.clave}.horaFin`]} />
                      <Boton variante="fantasma" tamano="compacto" iconoInicio={X} onClick={() => quitar(f, dia.diaSemana)} disabled={estado.guardando}
                        className={estilos.quitarFranja} aria-label={`Quitar la franja ${i + 1} del ${dia.nombreDia.toLowerCase()}`}>
                        Quitar
                      </Boton>
                    </li>
                  ))}
                </ul>
              )}
              {dia.abierto ? (
                <div>
                  <Boton variante="secundario" tamano="compacto" iconoInicio={Plus} onClick={() => agregar(dia)} disabled={estado.guardando}
                    data-agregar={dia.diaSemana} aria-label={`Agregar franja el ${dia.nombreDia.toLowerCase()}`}>
                    Agregar franja
                  </Boton>
                </div>
              ) : (
                <p className={estilos.notaDia}>El taller no abre este día.</p>
              )}
            </li>
          );
        })}
      </ul>
      {estado.error && <Aviso tono="peligro" titulo="No pudimos guardar tu disponibilidad">{estado.error}</Aviso>}
      <div className={estilosPanel.acciones}>
        <Boton type="submit" iconoInicio={Save} cargando={estado.guardando} textoCargando="Guardando…">Guardar mi disponibilidad</Boton>
      </div>
    </form>
  );
}
