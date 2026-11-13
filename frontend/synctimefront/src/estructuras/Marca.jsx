import { Link } from 'react-router-dom';
import { unirClases } from '../componentes/ui/clases';
import estilos from './Marca.module.css';

/**
 * Logotipo de Letrería Los Herrera (anillo y letras LH en tinta, a partir del original del taller)
 * con el nombre en texto. El nombre es el texto del enlace; la imagen es decorativa.
 */
export function Marca({ destino = '/', subtitulo, invertida = false, className }) {
  return (
    <Link to={destino} className={unirClases(estilos.marca, invertida && estilos.invertida, className)}>
      <img
        src={invertida ? '/marca/logo-lh-blanco-256.webp' : '/marca/logo-lh-256.webp'}
        alt=""
        width="40"
        height="40"
        className={estilos.logo}
      />
      <span className={estilos.textos}>
        <span className={estilos.nombre}>Letrería Los Herrera</span>
        {subtitulo && <span className={estilos.subtitulo}>{subtitulo}</span>}
      </span>
    </Link>
  );
}
