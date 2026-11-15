import { Link } from 'react-router-dom';
import { categorias, materialesTaller, personal, productos, vitrina } from '../../api/mantenedores';
import { formatearPesos } from '../../utilidades/formato';
import { formatearRut } from '../../utilidades/rut';
import { medidasCm } from '../solicitudes/reglasSolicitud';
import { CamposCategoria, CamposMaterial, EstadoActivo, Tenue } from './CamposSimples';
import { CamposPersonal } from './CamposPersonal';
import { CamposProducto } from './CamposProducto';
import { CamposVitrina } from './CamposVitrina';
import {
  CATEGORIA_VACIA, categoriaDesdeApi, categoriaParaApi, EMPLEADO_VACIO, empleadoDesdeApi, empleadoParaApi, FILTROS_MANTENEDOR,
  FILTROS_VITRINA, logoVigente, MATERIAL_VACIO, materialDesdeApi, materialParaApi, PRODUCTO_VACIO, productoDesdeApi, productoParaApi,
  PUBLICACION_VACIA, publicacionDesdeApi, requierePasswordEmpleado, publicacionParaApi, ROLES, validarCategoria, validarEmpleado, validarMaterial, validarProducto,
  validarPublicacion,
} from './reglasMantenedores';
import estilos from './Mantenedores.module.css';

const enlace = (tipo, id, texto) => <Link to={`/panel/mantenedores/${tipo}/${id}`} className={estilos.enlaceFila}>{texto}</Link>;
/**
 * Los cinco mantenedores. Cada uno dice cómo se llama, con qué API habla, cómo se ve en el listado y qué
 * campos tiene su formulario. La pantalla genérica (listado y editor) hace el resto.
 */
export const TIPOS = {
  categorias: {
    clave: 'categorias',
    titulo: 'Categorías',
    bajada: 'Agrupan los tipos de letrero en la vitrina y en las solicitudes.',
    nuevo: 'Nueva categoría',
    singular: 'categoría',
    entidad: 'esta categoría',
    api: categorias,
    idCampo: 'idCategoria',
    filtros: FILTROS_MANTENEDOR,
    buscar: 'Nombre',
    vacio: CATEGORIA_VACIA,
    desdeApi: categoriaDesdeApi,
    paraApi: categoriaParaApi,
    validar: validarCategoria,
    Campos: CamposCategoria,
    nombreDe: (r) => r?.nombre ?? '',
    ayudaDesactivar: 'Una categoría desactivada deja de ofrecerse en los formularios. Lo ya registrado no cambia.',
    columnas: [
      { clave: 'nombre', encabezado: 'Nombre', principal: true, render: (r) => enlace('categorias', r.idCategoria, r.nombre) },
      { clave: 'descripcion', encabezado: 'Descripción', render: (r) => <Tenue valor={r.descripcion} /> },
      { clave: 'activo', encabezado: 'Estado', render: (r) => <EstadoActivo activo={r.activo} /> },
    ],
  },
  'tipos-de-letrero': {
    clave: 'tipos-de-letrero',
    titulo: 'Tipos de letrero',
    bajada: 'Lo que el cliente elige al pedir una cotización, con su precio base de referencia.',
    nuevo: 'Nuevo tipo de letrero',
    singular: 'tipo de letrero',
    entidad: 'este tipo de letrero',
    api: productos,
    idCampo: 'idProducto',
    filtros: FILTROS_MANTENEDOR,
    buscar: 'Nombre o categoría',
    vacio: PRODUCTO_VACIO,
    desdeApi: productoDesdeApi,
    paraApi: productoParaApi,
    validar: validarProducto,
    Campos: CamposProducto,
    nombreDe: (r) => r?.nombre ?? '',
    ayudaDesactivar: 'Un tipo desactivado deja de ofrecerse en las solicitudes y su publicación sale de la vitrina. Lo ya registrado no cambia.',
    columnas: [
      { clave: 'nombre', encabezado: 'Nombre', principal: true, render: (r) => enlace('tipos-de-letrero', r.idProducto, r.nombre) },
      { clave: 'categoria', encabezado: 'Categoría', render: (r) => <Tenue valor={r.categoria?.nombre ?? r.nombreCategoria ?? (typeof r.categoria === 'string' ? r.categoria : null)} /> },
      { clave: 'precio', encabezado: 'Precio base', alinear: 'fin', render: (r) => <span className="cifras">{r.precioBaseRef == null ? '—' : formatearPesos(r.precioBaseRef)}</span> },
      { clave: 'activo', encabezado: 'Estado', render: (r) => <EstadoActivo activo={r.activo} /> },
    ],
  },
  vitrina: {
    clave: 'vitrina',
    titulo: 'Vitrina',
    bajada: 'Los trabajos que ven los clientes en la portada. Una publicación con el logo de un cliente necesita su autorización.',
    nuevo: 'Nueva publicación',
    singular: 'publicación',
    entidad: 'esta publicación',
    api: vitrina,
    idCampo: 'idCatalogo',
    filtros: FILTROS_VITRINA,
    buscar: 'Título o tipo de letrero',
    vacio: PUBLICACION_VACIA,
    desdeApi: publicacionDesdeApi,
    paraApi: publicacionParaApi,
    validar: (f, { registro }) => validarPublicacion(f, { logoAutorizado: logoVigente(f, registro) }),
    Campos: CamposVitrina,
    nombreDe: (r) => r?.titulo ?? '',
    sinActivo: true,
    columnas: [
      {
        clave: 'titulo', encabezado: 'Publicación', principal: true,
        render: (r) => (
          <span className={estilos.celdaPublicacion}>
            {r.imagen?.urlMiniatura
              ? <img src={r.imagen.urlMiniatura} alt="" width="48" height="48" loading="lazy" className={estilos.miniatura} />
              : <span className={estilos.sinImagen} aria-hidden="true" />}
            {enlace('vitrina', r.idCatalogo, r.titulo)}
          </span>
        ),
      },
      { clave: 'tipo', encabezado: 'Tipo de letrero', render: (r) => <Tenue valor={r.tipoLetrero} /> },
      { clave: 'medidas', encabezado: 'Medidas', render: (r) => <span className="cifras">{medidasCm(r.anchoMm, r.altoMm)}</span> },
      { clave: 'precio', encabezado: 'Precio ref.', alinear: 'fin', render: (r) => <span className="cifras">{r.precioReferencial == null ? '—' : formatearPesos(r.precioReferencial)}</span> },
    ],
  },
  materiales: {
    clave: 'materiales',
    titulo: 'Materiales',
    bajada: 'El catálogo que usa el editor de cotizaciones, con su costo aproximado.',
    nuevo: 'Nuevo material',
    singular: 'material',
    entidad: 'este material',
    api: materialesTaller,
    idCampo: 'idMaterial',
    filtros: FILTROS_MANTENEDOR,
    buscar: 'Nombre',
    vacio: MATERIAL_VACIO,
    desdeApi: materialDesdeApi,
    paraApi: materialParaApi,
    validar: validarMaterial,
    Campos: CamposMaterial,
    nombreDe: (r) => r?.nombre ?? '',
    ayudaDesactivar: 'Un material desactivado deja de ofrecerse al cotizar. Las cotizaciones ya emitidas no cambian.',
    columnas: [
      { clave: 'nombre', encabezado: 'Nombre', principal: true, render: (r) => enlace('materiales', r.idMaterial, r.nombre) },
      { clave: 'unidad', encabezado: 'Unidad', render: (r) => r.unidadMedida },
      { clave: 'costo', encabezado: 'Costo aprox.', alinear: 'fin', render: (r) => <span className="cifras">{formatearPesos(r.costoUnitarioAprox)}</span> },
      { clave: 'activo', encabezado: 'Estado', render: (r) => <EstadoActivo activo={r.activo} /> },
    ],
  },
  personal: {
    clave: 'personal',
    titulo: 'Personal',
    bajada: 'Quiénes entran al panel y con qué rol. Al desactivar a alguien se borra su acceso con rostro y se cierran sus sesiones.',
    nuevo: 'Agregar persona',
    singular: 'persona',
    entidad: 'esta persona',
    api: personal,
    idCampo: 'idEmpleado',
    filtros: FILTROS_MANTENEDOR,
    buscar: 'Nombre, RUT o correo',
    vacio: EMPLEADO_VACIO,
    desdeApi: empleadoDesdeApi,
    paraApi: (f, { nuevo, registro }) => empleadoParaApi(f, { nuevo, requierePassword: !nuevo && requierePasswordEmpleado(f, registro) }),
    validar: (f, { nuevo, registro }) => validarEmpleado(f, { nuevo, requierePassword: !nuevo && requierePasswordEmpleado(f, registro) }),
    Campos: CamposPersonal,
    nombreDe: (r) => [r?.nombre, r?.apellido].filter(Boolean).join(' '),
    columnas: [
      { clave: 'nombre', encabezado: 'Nombre', principal: true, render: (r) => enlace('personal', r.idEmpleado, [r.nombre, r.apellido].filter(Boolean).join(' ')) },
      { clave: 'rut', encabezado: 'RUT', render: (r) => <span className="cifras">{formatearRut(r.rut)}</span> },
      { clave: 'email', encabezado: 'Correo', render: (r) => <span className={estilos.correo}>{r.email}</span> },
      { clave: 'rol', encabezado: 'Rol', render: (r) => ROLES[r.rol] ?? r.rol },
      { clave: 'activo', encabezado: 'Estado', render: (r) => <EstadoActivo activo={r.activo} /> },
    ],
  },
};

export const ORDEN_TIPOS = ['categorias', 'tipos-de-letrero', 'vitrina', 'materiales', 'personal'];
