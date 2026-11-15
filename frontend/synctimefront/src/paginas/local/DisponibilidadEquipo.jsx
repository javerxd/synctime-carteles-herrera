import { Aviso, Boton, EstadoVacio, TablaAdaptable } from '../../componentes/ui';
import { panel } from '../../api/panel';
import { useConsulta } from '../../utilidades/useConsulta';
import { DIAS, equipoPorPersona, textoFranja } from './reglasLocal';
import estilos from './Local.module.css';

/** Gestión: la disponibilidad de todo el personal, una fila por persona y una columna por día. */
export function DisponibilidadEquipo() {
  const consulta = useConsulta('disponibilidad-equipo', panel.disponibilidadEquipo);
  if (consulta.error) {
    return (
      <Aviso tono="peligro" titulo="No pudimos cargar la disponibilidad del equipo"
        accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
        {consulta.error.message}
      </Aviso>
    );
  }
  const personas = equipoPorPersona(consulta.datos ?? []);
  const columnas = [
    { clave: 'nombre', encabezado: 'Persona', principal: true, render: (p) => p.nombre },
    ...DIAS.map(({ dia, nombre }) => ({
      clave: `d${dia}`,
      encabezado: nombre,
      render: (p) => {
        const delDia = p.franjas.filter((f) => f.diaSemana === dia);
        if (!delDia.length) return <span className={estilos.sinFranja}>—<span className="solo-lectores">Sin franjas</span></span>;
        return (
          <span className={`cifras ${estilos.franjasEquipo}`}>
            {delDia.map((f) => <span key={`${f.horaInicio}-${f.horaFin}`}>{textoFranja(f.horaInicio, f.horaFin)}</span>)}
          </span>
        );
      },
    })),
  ];
  return (
    <TablaAdaptable titulo="Disponibilidad del equipo" columnas={columnas} filas={personas} claveFila="idEmpleado" cargando={consulta.cargando}
      vacio={<EstadoVacio nivelTitulo={3} titulo="Nadie ha registrado su disponibilidad"
        descripcion="Cada persona la registra en «Mi disponibilidad», en esta misma página." />} />
  );
}
