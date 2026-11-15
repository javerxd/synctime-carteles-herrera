import { Campo } from '../../componentes/ui';
import { todasLasPaginas } from '../../api/mantenedores';
import { useConsulta } from '../../utilidades/useConsulta';
import { LIMITES } from './reglasMantenedores';
import estilos from './Mantenedores.module.css';

/** Formulario del tipo de letrero (E13): solo se ofrecen categorías activas, más la que ya tenía si se desactivó. */
export function CamposProducto({ campo, form, registro }) {
  const lista = useConsulta('categorias-activas', () => todasLasPaginas('/categorias', { activo: true }));
  const opciones = [...(lista.datos ?? [])];
  const actual = registro?.categoria ?? (registro?.idCategoria ? { idCategoria: registro.idCategoria, nombre: registro.nombreCategoria } : null);
  if (actual?.idCategoria && !opciones.some((c) => String(c.idCategoria) === String(actual.idCategoria))) {
    opciones.push({ ...actual, nombre: `${actual.nombre ?? 'Categoría'} (inactiva)` });
  }
  const propsCategoria = campo('idCategoria');
  return (
    <>
      <div className={estilos.dosColumnas}>
        <Campo etiqueta="Categoría" como="select" requerido {...propsCategoria} disabled={propsCategoria.disabled || lista.cargando}>
          <option value="">{lista.cargando ? 'Cargando…' : 'Elige una categoría'}</option>
          {opciones.map((c) => <option key={c.idCategoria} value={c.idCategoria}>{c.nombre}</option>)}
        </Campo>
        <Campo etiqueta="Nombre" requerido maxLength={LIMITES.nombreProducto} autoComplete="off" {...campo('nombre')}
          ayuda="Único dentro de su categoría." />
      </div>
      {lista.error && <p role="alert" className={estilos.errorLista}>No pudimos cargar las categorías: {lista.error.message}</p>}
      <Campo etiqueta="Descripción" como="textarea" rows={3} maxLength={LIMITES.descripcionProducto} {...campo('descripcion')}
        ayuda="Opcional." />
      <Campo etiqueta="Precio base de referencia" inputMode="numeric" className={estilos.campoCorto} {...campo('precioBaseRef')}
        ayuda={`Opcional, en pesos. ${form.precioBaseRef ? '' : 'Si lo dejas en blanco, no se muestra un precio.'}`.trim()} />
    </>
  );
}
