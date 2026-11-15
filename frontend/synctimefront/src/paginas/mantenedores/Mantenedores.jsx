import { NavLink, Navigate, Outlet, useParams } from 'react-router-dom';
import { Pagina404 } from '../errores/PaginasDeError';
import { ORDEN_TIPOS, TIPOS } from './tipos';
import estilosPanel from '../panel/Panel.module.css';
import estilos from './Mantenedores.module.css';

/**
 * /panel/mantenedores (fase 9, solo gestión): «Catálogo y mantenedores». Cada mantenedor es una subruta, así el
 * listado con sus filtros y cada ficha tienen su propia dirección (atrás, recargar y compartir funcionan). Las
 * fichas (/panel/mantenedores/:tipo/:id y …/nuevo) son páginas aparte, fuera de este marco.
 */
export default function Mantenedores() {
  const { tipo } = useParams();
  if (tipo && !TIPOS[tipo]) return <Pagina404 />;
  return (
    <div className={estilosPanel.pagina}>
      <div>
        <h1 className={estilosPanel.titulo}>Catálogo y mantenedores</h1>
        <p className={estilosPanel.bajada}>Administra lo que se ofrece en la vitrina y en los formularios, y quién entra al panel.</p>
      </div>
      <nav aria-label="Mantenedores" className={estilos.secciones}>
        <ul>
          {ORDEN_TIPOS.map((clave) => (
            <li key={clave}>
              <NavLink to={`/panel/mantenedores/${clave}`} className={estilos.seccion}>{TIPOS[clave].titulo}</NavLink>
            </li>
          ))}
        </ul>
      </nav>
      <Outlet />
    </div>
  );
}

export function InicioMantenedores() {
  return <Navigate to="/panel/mantenedores/categorias" replace />;
}
