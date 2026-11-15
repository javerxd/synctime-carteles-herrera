import { useId } from 'react';
import { Campo } from '../../componentes/ui';
import { LIMITES as LIMITES_CUENTA } from '../cuenta/reglasCuenta';
import { ETIQUETA_PASSWORD } from '../compartidos/reautenticacion';
import { LIMITES, ROLES } from './reglasMantenedores';
import estilos from './Mantenedores.module.css';

/**
 * Formulario de una persona del taller (E15, RNFO08). En el alta, la contraseña inicial la escribe quien da el
 * alta (no el sistema) y confirma con su propia contraseña. Nadie se cambia el rol a sí mismo.
 */
export function CamposPersonal({ campo, nuevo, esUnoMismo, pidePassword }) {
  const idMotivoRol = useId();
  const propsRol = campo('rol');
  return (
    <>
      <div className={estilos.dosColumnas}>
        <Campo etiqueta="Nombre" requerido maxLength={LIMITES.nombrePersona} autoComplete="off" {...campo('nombre')} />
        <Campo etiqueta="Apellido" requerido maxLength={LIMITES.nombrePersona} autoComplete="off" {...campo('apellido')} />
        <Campo etiqueta="RUT" requerido maxLength={12} autoComplete="off" ayuda="Con dígito verificador, por ejemplo 12.345.678-5." {...campo('rut')} />
        <Campo etiqueta="Teléfono" requerido type="tel" inputMode="tel" autoComplete="off" ayuda="9 dígitos." {...campo('telefono')} />
      </div>
      <Campo etiqueta="Correo" requerido type="email" inputMode="email" autoComplete="off" maxLength={LIMITES.correo}
        ayuda="Con este correo ingresa al panel." {...campo('email')} />
      <Campo etiqueta="Rol" como="select" requerido {...propsRol} disabled={propsRol.disabled || esUnoMismo}
        aria-describedby={esUnoMismo ? idMotivoRol : undefined}
        ayuda="Dueño y administrador ven la gestión; diseñador y operario atienden solicitudes y pedidos.">
        <option value="">Elige un rol</option>
        {Object.entries(ROLES).map(([valor, texto]) => <option key={valor} value={valor}>{texto}</option>)}
      </Campo>
      {esUnoMismo && <p id={idMotivoRol} className={estilos.motivo}>No puedes cambiarte el rol a ti mismo. Pídeselo a otra persona de gestión.</p>}

      {pidePassword && (
        <fieldset className={estilos.grupo}>
          <legend>Confirma el cambio</legend>
          <p className={estilos.motivo}>Al cambiar el correo o el rol se cerrarán las sesiones abiertas de esta persona.</p>
          <Campo etiqueta={ETIQUETA_PASSWORD} requerido type="password" autoComplete="current-password" maxLength={LIMITES_CUENTA.passwordMaxima}
            {...campo('passwordActual')} />
        </fieldset>
      )}

      {nuevo && (
        <fieldset className={estilos.grupo}>
          <legend>Contraseñas</legend>
          <Campo etiqueta="Contraseña inicial" requerido type="password" autoComplete="new-password" maxLength={LIMITES_CUENTA.passwordMaxima}
            ayuda={`Escríbela tú y entrégasela en persona. Entre ${LIMITES_CUENTA.passwordMinima} y ${LIMITES_CUENTA.passwordMaxima} caracteres; podrá cambiarla después.`}
            {...campo('password')} />
          <Campo etiqueta="Tu contraseña actual" requerido type="password" autoComplete="current-password" maxLength={LIMITES_CUENTA.passwordMaxima}
            ayuda="Para confirmar que eres tú quien da el alta." {...campo('passwordActual')} />
        </fieldset>
      )}
    </>
  );
}
