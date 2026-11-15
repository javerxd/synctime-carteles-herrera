import { Search } from 'lucide-react';
import { Aviso, Boton } from '../../componentes/ui';
import { TRAS_CORTE } from './trasCorte';

/** El aviso de «no supimos si se guardó», con el botón para revisar de nuevo cuando no se pudo confirmar. */
export function AvisoTrasCorte({ falla, alRevisar, revisando = false }) {
  if (!falla || (falla.codigo !== TRAS_CORTE.noRegistrado && falla.codigo !== TRAS_CORTE.sinConfirmar)) return null;
  const confirmar = falla.codigo === TRAS_CORTE.sinConfirmar;
  return (
    <Aviso tono={confirmar ? 'peligro' : 'aviso'} titulo={falla.message}
      accion={confirmar && alRevisar ? (
        <Boton variante="secundario" tamano="compacto" iconoInicio={Search} onClick={alRevisar} cargando={revisando} textoCargando="Revisando…">
          Revisar de nuevo
        </Boton>
      ) : null} />
  );
}
