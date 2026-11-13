import { Link } from 'react-router-dom';
import { Compass, LockKeyhole, TriangleAlert } from 'lucide-react';
import { Boton } from '../../componentes/ui';
import estilos from './PaginasDeError.module.css';

function PaginaDeError({ icono: Icono, codigo, titulo, descripcion, acciones }) {
  return (
    <section className={estilos.pagina} aria-labelledby="titulo-error">
      <span className={estilos.icono} aria-hidden="true"><Icono size={28} strokeWidth={1.5} /></span>
      <p className={estilos.codigo}>Error {codigo}</p>
      <h1 id="titulo-error" className={estilos.titulo}>{titulo}</h1>
      <p className={estilos.descripcion}>{descripcion}</p>
      <div className={estilos.acciones}>{acciones}</div>
    </section>
  );
}

export function Pagina404() {
  return (
    <PaginaDeError
      icono={Compass}
      codigo="404"
      titulo="No encontramos esta página"
      descripcion="Puede que el enlace esté mal escrito o que la página ya no exista."
      acciones={<Boton como={Link} to="/">Ir a la portada</Boton>}
    />
  );
}

export function Pagina403() {
  return (
    <PaginaDeError
      icono={LockKeyhole}
      codigo="403"
      titulo="No tienes permiso para ver esta página"
      descripcion="Tu rol no incluye esta sección. Si crees que es un error, pídele acceso al dueño o al administrador."
      acciones={<Boton como={Link} to="/panel" variante="secundario">Volver al inicio del panel</Boton>}
    />
  );
}

export function PaginaErrorGeneral({ alReintentar }) {
  return (
    <PaginaDeError
      icono={TriangleAlert}
      codigo="inesperado"
      titulo="Algo salió mal"
      descripcion="Ocurrió un error al mostrar esta pantalla. Tus datos no se perdieron; vuelve a intentarlo."
      acciones={(
        <>
          {alReintentar && <Boton onClick={alReintentar}>Reintentar</Boton>}
          <Boton como="a" href="/" variante="secundario">Ir a la portada</Boton>
        </>
      )}
    />
  );
}
