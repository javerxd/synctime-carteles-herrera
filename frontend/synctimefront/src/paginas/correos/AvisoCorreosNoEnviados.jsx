import { Link } from 'react-router-dom';
import { Aviso, Boton } from '../../componentes/ui';
import { correos } from '../../api/correos';
import { useConsulta } from '../../utilidades/useConsulta';
import { textoSinRevisar } from './reglasCorreos';

/**
 * Inicio del panel (gestión, fase 11): si hay correos que no salieron y nadie revisó, lo dice con un enlace a la
 * vista filtrada. Si la consulta falla no muestra nada: no es lo principal del inicio y la vista tiene su propio error.
 * Quien lo monta decide el rol; aquí no se consulta nada más.
 */
export function AvisoCorreosNoEnviados() {
  const consulta = useConsulta('correos-no-enviados-resumen', (senal) => correos.resumen({ senal }));
  const sinRevisar = Number(consulta.datos?.sinRevisar ?? 0);
  if (!Number.isInteger(sinRevisar) || sinRevisar <= 0) return null;
  return (
    <Aviso tono="aviso" titulo={textoSinRevisar(sinRevisar)}
      accion={(
        <Boton como={Link} to="/panel/correos-no-enviados?revisado=false" variante="secundario" tamano="compacto">
          Ver los correos no enviados
        </Boton>
      )}>
      Revisa a quién iban y reenvíalos, o márcalos como revisados.
    </Aviso>
  );
}
