import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { Aviso } from '../../../componentes/ui';
import { PRIVACIDAD, TERMINOS } from './textos';
import estilos from './DocumentoLegal.module.css';

const DOCUMENTOS = { privacidad: PRIVACIDAD, terminos: TERMINOS };

/**
 * Política de privacidad y términos y condiciones (S22, RFS12). Cada documento muestra su versión: es
 * la que queda registrada cuando alguien lo acepta. Son borradores hasta la revisión legal.
 */
export default function DocumentoLegal({ tipo }) {
  const documento = DOCUMENTOS[tipo];
  const { hash } = useLocation();

  useEffect(() => {
    document.title = `${documento.titulo} · Letreros Herrera`;
    if (hash) document.getElementById(hash.slice(1))?.scrollIntoView();
  }, [documento, hash]);

  return (
    <article className={estilos.documento} aria-labelledby="titulo-documento">
      <header className={estilos.encabezado}>
        <h1 id="titulo-documento" className={estilos.titulo}>{documento.titulo}</h1>
        <p className={estilos.version}>Versión {documento.version} · vigente desde el {documento.vigenteDesde}</p>
        <Aviso tono="aviso" titulo="Borrador pendiente de revisión legal">
          Este texto todavía no fue revisado por un abogado. Lo marcado «[Pendiente de completar]» lo completa el taller.
        </Aviso>
        <p className={estilos.resumen}>{documento.resumen}</p>
      </header>

      <nav aria-label="Contenido" className={estilos.indice}>
        <h2 className={estilos.tituloIndice}>Contenido</h2>
        <ol>
          {documento.secciones.map((s) => (
            <li key={s.id}><a href={`#${s.id}`}>{s.titulo}</a></li>
          ))}
        </ol>
      </nav>

      {documento.secciones.map((s) => (
        <section key={s.id} id={s.id} className={estilos.seccion} aria-labelledby={`titulo-${s.id}`}>
          <h2 id={`titulo-${s.id}`} className={estilos.tituloSeccion}>{s.titulo}</h2>
          {s.parrafos?.map((p) => <p key={p}>{p}</p>)}
          {s.lista && (
            <ul>
              {s.lista.map((elemento) => <li key={elemento}>{elemento}</li>)}
            </ul>
          )}
        </section>
      ))}
    </article>
  );
}
