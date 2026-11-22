import EditorMantenedor from '../mantenedores/EditorMantenedor';
import { Listado } from '../mantenedores/ListaMantenedor';
import { TIPOS } from '../mantenedores/tipos';
import estilosPanel from '../panel/Panel.module.css';

/**
 * /panel/empleados (fase 12D, solo dueño y administrador): administrar al personal. Es la pantalla de la fase 9
 * (alta, datos, cambio de rol, desactivar con confirmación), sacada del mantenedor genérico a su propia ruta.
 * Alta y cambios de correo o rol siguen pidiendo la contraseña de quien los hace; «eliminar» es desactivar.
 */
export default function Empleados() {
  return (
    <div className={estilosPanel.pagina}>
      <Listado tipo={TIPOS.personal} h1 />
    </div>
  );
}

/** /panel/empleados/nuevo y /panel/empleados/:id. */
export function FichaEmpleado() {
  return <EditorMantenedor tipo="personal" />;
}
