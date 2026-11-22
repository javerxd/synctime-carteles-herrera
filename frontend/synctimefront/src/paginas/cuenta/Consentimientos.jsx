import { Link } from 'react-router-dom';
import { Casilla, Enlace } from '../../componentes/ui';
import estilos from './Cuenta.module.css';

/**
 * «Tus autorizaciones» del registro (RFS12, RNFP21: sin patrones engañosos): las dos casillas vienen desmarcadas y separadas.
 * Aceptar los términos y la privacidad es obligatorio para crear la cuenta; recibir novedades, opcional. Lo usan el registro
 * por formulario y «Completa tu registro» del ingreso con Google, con los mismos textos.
 * `formulario` es lo que devuelve useFormulario; `deshabilitado`, mientras se envía.
 */
export function Consentimientos({ formulario, deshabilitado }) {
  const { datos, visibles, cambiar, tocar } = formulario;
  return (
    <fieldset className={estilos.consentimientos} disabled={deshabilitado}>
      <legend>Tus autorizaciones</legend>
      <Casilla
        name="aceptaTerminosYPrivacidad"
        aria-required="true"
        etiqueta="Leí y acepto los términos y condiciones y la política de privacidad"
        descripcion={(
          <>
            Obligatorio para crear la cuenta. Léelos aquí: <Enlace como={Link} to="/terminos" externo>términos y
            condiciones</Enlace> y <Enlace como={Link} to="/privacidad" externo>política de privacidad</Enlace>.
          </>
        )}
        checked={datos.aceptaTerminosYPrivacidad}
        onChange={(e) => { cambiar('aceptaTerminosYPrivacidad', e.target.checked); tocar('aceptaTerminosYPrivacidad'); }}
        error={visibles.aceptaTerminosYPrivacidad}
      />
      <Casilla
        name="aceptaMarketing"
        etiqueta="Quiero recibir novedades y promociones"
        descripcion="Opcional. Puedes cambiarlo cuando quieras desde Mi cuenta."
        checked={datos.aceptaMarketing}
        onChange={(e) => cambiar('aceptaMarketing', e.target.checked)}
      />
    </fieldset>
  );
}
