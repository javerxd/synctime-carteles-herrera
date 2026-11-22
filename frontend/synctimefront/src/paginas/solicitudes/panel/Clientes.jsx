import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { ArrowLeft, ClipboardList, Plus, Save } from 'lucide-react';
import {
  Aviso, BarraFiltros, Boton, Campo, Enlace, Esqueleto, EstadoVacio, Insignia, Paginacion, TablaAdaptable, Tarjeta,
} from '../../../componentes/ui';
import { clientes } from '../../../api/solicitudes';
import { useAvisos } from '../../../avisos/Avisos';
import { useSesion } from '../../../sesion/sesion';
import { MAXIMO_CARACTERES } from '../../../utilidades/reglaNombre';
import { formatearRut } from '../../../utilidades/rut';
import { useConsulta } from '../../../utilidades/useConsulta';
import { useDebounce } from '../../../utilidades/useDebounce';
import { AvisoConflicto } from '../../compartidos/AvisoConflicto';
import { useConflictoDeVersion } from '../../compartidos/useConflictoDeVersion';
import { useFormulario } from '../../cuenta/useFormulario';
import { nombreCompleto } from '../reglasSolicitud';
import { telefonoVisible } from '../reglasSolicitud';
import { clienteParaApi, EDITAN_CLIENTES, ORDEN_CLIENTE, validarCliente } from './reglasClientes';
import estilosPanel from '../../panel/Panel.module.css';
import estilos from '../Solicitudes.module.css';

const POR_PAGINA = 20;
const correoDe = (c) => c.email ?? c.correo ?? null;

/** /panel/clientes: listado con búsqueda por teléfono, RUT o nombre. */
export default function Clientes() {
  const [parametros, setParametros] = useSearchParams();
  const q = parametros.get('q') ?? '';
  const pagina = Math.max(1, Number.parseInt(parametros.get('pagina') ?? '1', 10) || 1);
  const [texto, setTexto] = useState(q);
  const buscado = useDebounce(texto);
  const refTitulo = useRef(null);
  const consulta = useConsulta(`clientes:${q}:${pagina}`, () => clientes.listar({ q: q.trim() || undefined, pagina, tamano: POR_PAGINA }));

  const ir = (cambios) => {
    const siguiente = { q, pagina: 1, ...cambios };
    setParametros(Object.fromEntries(Object.entries(siguiente)
      .filter(([c, v]) => v !== '' && !(c === 'pagina' && v === 1)).map(([c, v]) => [c, String(v)])), { replace: true });
  };
  const refIr = useRef(ir);
  useEffect(() => {
    refIr.current = ir;
  });
  // El campo sigue a la dirección (menú, atrás, limpiar) y la búsqueda se aplica solo al teclear.
  const [qVisto, setQVisto] = useState(q);
  if (q !== qVisto) {
    setQVisto(q);
    setTexto(q);
  }
  const refTecleo = useRef(false);
  useEffect(() => {
    if (!refTecleo.current) return;
    refTecleo.current = false;
    if (buscado.trim() !== q.trim()) refIr.current({ q: buscado.trim() });
  }, [buscado, q]);

  const datos = consulta.datos;
  const ultimaPagina = datos?.totalPaginas ?? 0;
  useEffect(() => {
    if (ultimaPagina > 0 && pagina > ultimaPagina) refIr.current({ pagina: ultimaPagina });
  }, [ultimaPagina, pagina]);
  const columnas = [
    {
      clave: 'nombre', encabezado: 'Cliente', principal: true,
      render: (c) => <Link to={`/panel/clientes/${c.idCliente}`} className={estilos.enlaceFila}>{nombreCompleto(c)}</Link>,
    },
    { clave: 'telefono', encabezado: 'Teléfono', render: (c) => <span className="cifras">{telefonoVisible(c.telefono)}</span> },
    { clave: 'rut', encabezado: 'RUT', render: (c) => <span className="cifras">{c.rut ? formatearRut(c.rut) : '—'}</span> },
    { clave: 'correo', encabezado: 'Correo', render: (c) => <span className={estilos.correo}>{correoDe(c) ?? '—'}</span> },
    {
      clave: 'estado', encabezado: 'Estado',
      render: (c) => (c.activo === false ? <Insignia tono="neutro">Inactivo</Insignia> : <Insignia tono="exito">Activo</Insignia>),
    },
    {
      clave: 'cuenta', encabezado: 'Cuenta web',
      render: (c) => (c.registrado ? (c.correoConfirmado ? 'Correo confirmado' : 'Correo sin confirmar') : 'Sin cuenta'),
    },
  ];

  return (
    <div className={estilosPanel.pagina}>
      <div className={estilosPanel.cabecera}>
        <div>
          <h1 ref={refTitulo} tabIndex={-1} className={estilosPanel.titulo}>Clientes</h1>
          <p className={estilosPanel.bajada}>Busca por teléfono, RUT o nombre. Para registrar uno nuevo, parte por una solicitud presencial.</p>
        </div>
        <Boton como={Link} to="/panel/solicitudes/nueva" variante="secundario" iconoInicio={Plus}>Solicitud presencial</Boton>
      </div>

      <BarraFiltros titulo="Búsqueda de clientes" hayFiltros={Boolean(q)} alLimpiar={() => { setTexto(''); ir({ q: '' }); }}
        resumen={datos ? `${datos.total} ${datos.total === 1 ? 'cliente' : 'clientes'}` : ''}>
        <Campo etiqueta="Buscar cliente" type="search" ayuda="Teléfono, RUT o nombre" value={texto} onChange={(e) => { refTecleo.current = true; setTexto(e.target.value); }} />
      </BarraFiltros>

      {consulta.error ? (
        <Aviso tono="peligro" titulo="No pudimos cargar los clientes"
          accion={<Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {consulta.error.message}
        </Aviso>
      ) : (
        <TablaAdaptable titulo="Clientes" columnas={columnas} filas={datos?.elementos ?? []} claveFila="idCliente" cargando={consulta.cargando}
          vacio={(
            <EstadoVacio
              titulo={q ? `Ningún cliente coincide con «${q}»` : 'Aún no hay clientes'}
              descripcion={q ? 'Revisa el número o prueba con el RUT o el apellido.' : 'Los clientes se crean al registrarse en el portal o en una solicitud presencial.'}
            />
          )} />
      )}
      <Paginacion pagina={pagina} totalPaginas={consulta.cargando ? 0 : datos?.totalPaginas ?? 0} etiqueta="Paginación de clientes"
        alCambiar={(numero) => { ir({ pagina: numero }); refTitulo.current?.focus(); }} />
    </div>
  );
}

const formularioDesde = (cliente) => ({
  nombre: cliente.nombre ?? '', apellido: cliente.apellido ?? '', telefono: telefonoVisible(cliente.telefono).replace('—', ''),
  rut: cliente.rut ?? '', email: correoDe(cliente) ?? '', direccion: cliente.direccion ?? '',
});

/**
 * Los datos del cliente, con la versión vista (If-Match). Fase 11: si otra persona lo cambió mientras se editaba,
 * lo escrito se queda y se ofrece «Guardar sobre la versión actual» o descartar, como en los mantenedores.
 */
function FormularioCliente({ cliente, alGuardar, alDescartar }) {
  const {
    aplicarErroresApi, comprobar, datos, propiedades, refFormulario, reiniciar,
  } = useFormulario({
    inicial: formularioDesde(cliente),
    validar: validarCliente,
    orden: ORDEN_CLIENTE,
  });
  const [estado, setEstado] = useState({ guardando: false, error: null, exito: null });
  const version = useConflictoDeVersion({ releer: () => clientes.obtener(cliente.idCliente), versionInicial: cliente.rowVersion });
  const refResultado = useRef(null);
  useEffect(() => {
    if (!estado.guardando && (estado.error || estado.exito)) refResultado.current?.focus();
  }, [estado]);

  async function guardar(evento, sobreActual = false) {
    evento?.preventDefault();
    if (estado.guardando || !comprobar()) return;
    setEstado({ guardando: true, error: null, exito: null });
    try {
      const base = sobreActual ? version.conflicto?.actual ?? cliente : cliente;
      const actualizado = await clientes.actualizar(cliente.idCliente, { ...clienteParaApi(datos), activo: base.activo ?? true },
        sobreActual ? version.version : cliente.rowVersion);
      version.olvidar();
      setEstado({ guardando: false, error: null, exito: 'Guardamos los datos del cliente.' });
      alGuardar(actualizado);
    } catch (error) {
      if (error?.codigo === 'version_obsoleta') {
        setEstado({ guardando: false, error: null, exito: null });
        version.detectar();
      } else if (aplicarErroresApi(error)) setEstado({ guardando: false, error: null, exito: null });
      else setEstado({ guardando: false, error: error?.message || 'No pudimos guardar. Inténtalo de nuevo.', exito: null });
    }
  }

  function descartar() {
    const vigente = version.conflicto?.actual;
    version.olvidar();
    if (vigente) reiniciar(formularioDesde(vigente));
    alDescartar?.();
  }

  const campo = (nombre) => ({ ...propiedades(nombre), disabled: estado.guardando });
  const vigente = version.conflicto?.actual;
  return (
    <form ref={refFormulario} className={estilosPanel.formulario} onSubmit={guardar} noValidate>
      <div ref={refResultado} tabIndex={-1} className={estilos.foco}>
        <div role="status">{estado.exito && <Aviso tono="exito" titulo={estado.exito} />}</div>
        {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
      </div>
      <AvisoConflicto conflicto={version.conflicto} entidad="este cliente" guardando={estado.guardando} refAccion={version.refAccion}
        resumen={vigente ? [nombreCompleto(vigente), telefonoVisible(vigente.telefono), correoDe(vigente), vigente.direccion].filter(Boolean).join(' · ') : null}
        alGuardarSobre={() => guardar(null, true)} alReintentar={version.detectar} alDescartar={descartar} />
      <div className={estilosPanel.dosColumnas}>
        <Campo etiqueta="Nombre" requerido maxLength={MAXIMO_CARACTERES} autoComplete="off" {...campo('nombre')} />
        <Campo etiqueta="Apellido" requerido maxLength={MAXIMO_CARACTERES} autoComplete="off" {...campo('apellido')} />
        <Campo etiqueta="Teléfono" requerido type="tel" inputMode="tel" autoComplete="off" {...campo('telefono')} />
        <Campo etiqueta="RUT (opcional)" autoComplete="off" {...campo('rut')} />
      </div>
      <Campo etiqueta="Correo" type="email" inputMode="email" maxLength={100} autoComplete="off"
        ayuda={cliente.registrado ? 'Tiene cuenta web: si cambias el correo, deberá confirmarlo de nuevo.' : 'Opcional. Si lo escribes, le llegarán sus comprobantes.'}
        {...campo('email')} />
      <Campo etiqueta="Dirección" maxLength={255} autoComplete="off" ayuda="Opcional." {...campo('direccion')} />
      <div className={estilosPanel.pieFormulario}>
        <Boton type="submit" iconoInicio={Save} cargando={estado.guardando} textoCargando="Guardando…">Guardar datos</Boton>
      </div>
    </form>
  );
}

/** /panel/clientes/:id: datos del cliente (los corrigen dueño, administrador y operario) y acceso a sus solicitudes. */
export function FichaCliente() {
  const { id } = useParams();
  const navegar = useNavigate();
  const { tieneRol } = useSesion();
  const { anunciar } = useAvisos();
  const consulta = useConsulta(`cliente:${id}`, () => clientes.obtener(id));
  const cliente = consulta.datos;
  const puedeEditar = tieneRol(EDITAN_CLIENTES);

  return (
    <div className={estilosPanel.pagina}>
      <div className={estilosPanel.cabecera}>
        <div>
          <Enlace como={Link} to="/panel/clientes" className={estilos.volver}><ArrowLeft size={16} aria-hidden="true" />Clientes</Enlace>
          <h1 className={estilosPanel.titulo}>{cliente ? nombreCompleto(cliente) : 'Cliente'}</h1>
          {cliente && (
            <p className={estilosPanel.bajada}>
              {[telefonoVisible(cliente.telefono), cliente.rut && formatearRut(cliente.rut)].filter(Boolean).join(' · ')}
            </p>
          )}
        </div>
        {cliente && (
          <div className={estilos.acciones}>
            <Boton variante="secundario" iconoInicio={ClipboardList}
              onClick={() => navegar(`/panel/solicitudes?q=${encodeURIComponent(String(cliente.telefono ?? ''))}`)}>
              Ver sus solicitudes
            </Boton>
            <Boton como={Link} to={`/panel/solicitudes/nueva?cliente=${cliente.idCliente}`} variante="secundario" iconoInicio={Plus}>
              Nueva solicitud
            </Boton>
          </div>
        )}
      </div>

      {consulta.cargando && (
        <Tarjeta>
          <p role="status" className="solo-lectores">Cargando el cliente…</p>
          <Esqueleto lineas={5} />
        </Tarjeta>
      )}
      {consulta.error && !cliente && (
        <Aviso tono="peligro" titulo={consulta.error.estado === 404 ? 'No encontramos este cliente' : 'No pudimos cargar el cliente'}
          accion={consulta.error.estado === 404 ? null : <Boton variante="secundario" tamano="compacto" onClick={consulta.recargar}>Reintentar</Boton>}>
          {consulta.error.estado === 404 ? 'Vuelve al listado y búscalo por su teléfono o RUT.' : consulta.error.message}
        </Aviso>
      )}

      {cliente && (
        <Tarjeta como="section" aria-labelledby="titulo-datos-cliente" className={estilos.seccion}>
          <h2 id="titulo-datos-cliente" className={estilos.subtitulo}>Datos del cliente</h2>
          {cliente.activo === false && <Aviso tono="aviso" titulo="Este cliente está inactivo" />}
          {puedeEditar ? (
            <FormularioCliente key={cliente.idCliente} cliente={cliente} alDescartar={consulta.recargar}
              alGuardar={() => { anunciar({ tono: 'exito', titulo: 'Datos del cliente guardados' }); consulta.recargar(); }} />
          ) : (
            <dl className={estilos.datos}>
              <div className={estilos.dato}><dt>Teléfono</dt><dd>{telefonoVisible(cliente.telefono)}</dd></div>
              <div className={estilos.dato}><dt>RUT</dt><dd>{cliente.rut ? formatearRut(cliente.rut) : '—'}</dd></div>
              <div className={estilos.dato}><dt>Correo</dt><dd>{correoDe(cliente) ?? '—'}</dd></div>
              <div className={estilos.dato}><dt>Dirección</dt><dd>{cliente.direccion || '—'}</dd></div>
            </dl>
          )}
        </Tarjeta>
      )}
    </div>
  );
}
