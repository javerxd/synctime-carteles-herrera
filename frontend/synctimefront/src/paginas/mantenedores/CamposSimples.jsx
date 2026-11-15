import { Campo, Insignia } from '../../componentes/ui';
import { LIMITES } from './reglasMantenedores';
import estilos from './Mantenedores.module.css';

// Piezas pequeñas de los mantenedores: el estado de una fila y los formularios de categoría y material.

export function Tenue({ valor }) {
  return valor || <span className={estilos.tenue}>—</span>;
}
export function EstadoActivo({ activo }) {
  return activo === false ? <Insignia tono="neutro">Inactivo</Insignia> : <Insignia tono="exito">Activo</Insignia>;
}

export function CamposCategoria({ campo }) {
  return (
    <>
      <Campo etiqueta="Nombre" requerido maxLength={LIMITES.nombreCategoria} autoComplete="off" {...campo('nombre')} />
      <Campo etiqueta="Descripción" como="textarea" rows={3} maxLength={LIMITES.descripcionCategoria} {...campo('descripcion')}
        ayuda="Opcional. La ve el personal al elegir la categoría." />
    </>
  );
}

export function CamposMaterial({ campo }) {
  return (
    <>
      <Campo etiqueta="Nombre" requerido maxLength={LIMITES.nombreMaterial} autoComplete="off" {...campo('nombre')} />
      <div className={estilos.dosColumnas}>
        <Campo etiqueta="Unidad de medida" requerido maxLength={LIMITES.unidad} ayuda="Por ejemplo m2, ml o unidad." {...campo('unidadMedida')} />
        <Campo etiqueta="Costo unitario aproximado" requerido inputMode="numeric" ayuda="En pesos. El editor de cotizaciones lo sugiere."
          {...campo('costoUnitarioAprox')} />
      </div>
    </>
  );
}

