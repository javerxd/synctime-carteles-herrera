import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { ArrowLeft, Save, Search, UserPlus, UserRound } from 'lucide-react';
import { Aviso, Boton, Campo, Enlace, Esqueleto, Tarjeta } from '../../../componentes/ui';
import { publico } from '../../../api/publico';
import { clientes, solicitudes } from '../../../api/solicitudes';
import { useAvisos } from '../../../avisos/Avisos';
import { useSesion } from '../../../sesion/sesion';
import { formatearRut } from '../../../utilidades/rut';
import { useConsulta } from '../../../utilidades/useConsulta';
import { useFormulario } from '../../cuenta/useFormulario';
import { EditorItems } from '../EditorItems';
import { LIMITES_SOLICITUD, nombreCompleto, numeroSolicitud } from '../reglasSolicitud';
import { telefonoVisible } from '../reglasSolicitud';
import { useSolicitudEditable } from '../useSolicitudEditable';
import {
  clienteParaApi, EDITAN_CLIENTES, ORDEN_CLIENTE, rutNormalizado, telefonoNormalizado, validarBusqueda, validarCliente,
} from './reglasClientes';
import estilosPanel from '../../panel/Panel.module.css';
import estilos from '../Solicitudes.module.css';
import { fallaDeClave, useClaveIdempotencia } from '../../compartidos/useClaveIdempotencia';

/** Busca al cliente por teléfono o RUT antes de crearlo (así no se duplica). También lo usa el pedido directo. */
export function BuscarCliente({ alEncontrar, alNoEncontrar, primario }) {
  const {
    comprobar, datos, propiedades, refFormulario,
  } = useFormulario({ inicial: { telefono: '', rut: '' }, validar: validarBusqueda, orden: ['telefono', 'rut'] });
  const [estado, setEstado] = useState({ buscando: false, error: null });

  async function buscar(evento) {
    evento.preventDefault();
    if (!comprobar()) return;
    setEstado({ buscando: true, error: null });
    const criterio = {
      telefono: datos.telefono.trim() ? telefonoNormalizado(datos.telefono) : undefined,
      rut: datos.rut.trim() ? rutNormalizado(datos.rut) : undefined,
    };
    try {
      const encontrado = await clientes.buscar(criterio);
      setEstado({ buscando: false, error: null });
      if (encontrado) alEncontrar(encontrado);
      else alNoEncontrar(datos);
    } catch (error) {
      setEstado({ buscando: false, error: error?.message || 'No pudimos buscar al cliente. Inténtalo de nuevo.' });
    }
  }

  return (
    <form ref={refFormulario} className={estilosPanel.formulario} onSubmit={buscar} noValidate>
      {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
      <div className={estilosPanel.dosColumnas}>
        <Campo etiqueta="Teléfono" type="tel" inputMode="tel" autoComplete="off" ayuda="9 dígitos, por ejemplo 9 1234 5678."
          disabled={estado.buscando} {...propiedades('telefono')} />
        <Campo etiqueta="RUT" autoComplete="off" ayuda="Con dígito verificador, por ejemplo 12.345.678-5."
          disabled={estado.buscando} {...propiedades('rut')} />
      </div>
      <div>
        <Boton type="submit" variante={primario ? 'primario' : 'secundario'} iconoInicio={Search} cargando={estado.buscando} textoCargando="Buscando…">
          Buscar cliente
        </Boton>
      </div>
    </form>
  );
}

/** Crea el cliente que no existía, con lo que ya se escribió al buscar. */
export function CrearCliente({ inicial, alCrear, alCancelar, paraQue = 'la solicitud' }) {
  const {
    aplicarErroresApi, comprobar, datos, propiedades, refFormulario,
  } = useFormulario({
    inicial: { nombre: '', apellido: '', telefono: inicial.telefono ?? '', rut: inicial.rut ?? '', email: '', direccion: '' },
    validar: validarCliente,
    orden: ORDEN_CLIENTE,
  });
  const [estado, setEstado] = useState({ guardando: false, error: null });
  const refTitulo = useRef(null);
  useEffect(() => {
    refTitulo.current?.focus();
  }, []);

  async function crear(evento) {
    evento.preventDefault();
    if (!comprobar()) return;
    setEstado({ guardando: true, error: null });
    try {
      alCrear(await clientes.crear(clienteParaApi(datos)));
    } catch (error) {
      if (aplicarErroresApi(error)) setEstado({ guardando: false, error: null });
      else setEstado({ guardando: false, error: error?.message || 'No pudimos crear al cliente. Inténtalo de nuevo.' });
    }
  }

  const campo = (nombre) => ({ ...propiedades(nombre), disabled: estado.guardando });
  return (
    <form ref={refFormulario} className={estilosPanel.formulario} onSubmit={crear} noValidate aria-labelledby="titulo-crear-cliente">
      <h3 ref={refTitulo} tabIndex={-1} id="titulo-crear-cliente" className={estilos.subtituloBloque}>Nuevo cliente</h3>
      <Aviso tono="info" titulo="No hay un cliente con esos datos">Regístralo para seguir con {paraQue}.</Aviso>
      {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
      <div className={estilosPanel.dosColumnas}>
        <Campo etiqueta="Nombre" requerido autoComplete="off" maxLength={80} {...campo('nombre')} />
        <Campo etiqueta="Apellido" requerido autoComplete="off" maxLength={80} ayuda="O el nombre del negocio." {...campo('apellido')} />
        <Campo etiqueta="Teléfono" requerido type="tel" inputMode="tel" autoComplete="off" {...campo('telefono')} />
        <Campo etiqueta="RUT (opcional)" autoComplete="off" {...campo('rut')} />
      </div>
      <Campo etiqueta="Correo" type="email" inputMode="email" autoComplete="off" maxLength={100}
        ayuda="Opcional. Si lo escribes, le llegará el comprobante." {...campo('email')} />
      <div className={estilosPanel.pieFormulario}>
        <Boton variante="secundario" onClick={alCancelar} disabled={estado.guardando}>Buscar otra vez</Boton>
        <Boton type="submit" iconoInicio={UserPlus} cargando={estado.guardando} textoCargando="Creando…">Crear cliente</Boton>
      </div>
    </form>
  );
}

export function ClienteElegido({ cliente, alCambiar }) {
  return (
    <div className={estilos.clienteElegido}>
      <UserRound size={22} strokeWidth={1.5} aria-hidden="true" className={estilos.iconoCliente} />
      <div className={estilos.datosCliente}>
        <p className={estilos.nombreCliente}>{nombreCompleto(cliente)}</p>
        <p className={estilos.detalleLetrero}>
          {[telefonoVisible(cliente.telefono), cliente.rut && formatearRut(cliente.rut), cliente.email ?? cliente.correo].filter(Boolean).join(' · ')}
        </p>
      </div>
      <Boton variante="fantasma" tamano="compacto" onClick={alCambiar}>Cambiar cliente</Boton>
    </div>
  );
}

/**
 * /panel/solicitudes/nueva: registro presencial (RFS04). Primero se busca al cliente por teléfono o RUT y,
 * si no existe, se crea; luego se describen los letreros. Al registrar se abre la ficha, donde se adjuntan
 * los archivos que trajo el cliente. Las presenciales no avisan al taller; el cliente con correo recibe su
 * comprobante. `?cliente=ID` llega desde la ficha de un cliente.
 */
export default function RegistroPresencial() {
  const navegar = useNavigate();
  const [parametros] = useSearchParams();
  const idClienteInicial = parametros.get('cliente');
  const { tieneRol } = useSesion();
  const { anunciar } = useAvisos();
  const productos = useConsulta('productos', publico.productos);
  const clienteInicial = useConsulta(`cliente:${idClienteInicial ?? 'ninguno'}`, () => (idClienteInicial ? clientes.obtener(idClienteInicial) : null));
  const [cliente, setCliente] = useState(null);
  const [paso, setPaso] = useState('buscar');
  const [busqueda, setBusqueda] = useState({});
  const {
    agregarItem, aplicarErroresApi, cambiarItem, comprobar, items, paraApi, propiedades, quitarItem, refFormulario, tocar, visibles,
  } = useSolicitudEditable();
  const [estado, setEstado] = useState({ enviando: false, error: null });
  const refError = useRef(null);
  const refSeccionSolicitud = useRef(null);
  // Fase 11: la misma clave en cada reintento de este formulario abierto.
  const idempotencia = useClaveIdempotencia();
  const puedeCrear = tieneRol(EDITAN_CLIENTES);

  // El cliente que llega desde su ficha queda elegido al cargarse.
  const [inicialAplicado, setInicialAplicado] = useState(false);
  if (!inicialAplicado && clienteInicial.datos) {
    setInicialAplicado(true);
    setCliente(clienteInicial.datos);
  }

  useEffect(() => {
    if (estado.error && !estado.enviando) refError.current?.focus();
  }, [estado]);

  function elegir(elegido) {
    setCliente(elegido);
    setPaso('buscar');
    anunciar({ tono: 'exito', titulo: `Cliente: ${nombreCompleto(elegido)}` });
    requestAnimationFrame(() => refSeccionSolicitud.current?.querySelector('select, input, textarea')?.focus());
  }

  async function registrar(evento) {
    evento.preventDefault();
    if (estado.enviando || !comprobar()) return;
    setEstado({ enviando: true, error: null });
    try {
      const comprobante = await solicitudes.crear({ idCliente: cliente.idCliente, ...paraApi() }, { clave: idempotencia.clave });
      idempotencia.renovar();
      anunciar({ tono: 'exito', titulo: `Solicitud ${numeroSolicitud(comprobante)} registrada` });
      navegar(`/panel/solicitudes/${comprobante.idSolicitud}`, { state: { registrada: true } });
    } catch (falla) {
      const error = fallaDeClave(falla, idempotencia.renovar);
      if (aplicarErroresApi(error)) setEstado({ enviando: false, error: null });
      else setEstado({ enviando: false, error: error?.message || 'No pudimos registrar la solicitud. Inténtalo de nuevo.' });
    }
  }

  return (
    <div className={estilosPanel.pagina}>
      <div>
        <Enlace como={Link} to="/panel/solicitudes" className={estilos.volver}><ArrowLeft size={16} aria-hidden="true" />Solicitudes</Enlace>
        <h1 className={estilosPanel.titulo}>Registrar solicitud presencial</h1>
        <p className={estilosPanel.bajada}>Para quien pide en el local o por teléfono. Busca primero al cliente para no duplicarlo.</p>
      </div>

      <div className={estilos.columnaFormulario}>
        <Tarjeta como="section" aria-labelledby="titulo-cliente" className={estilos.seccion}>
          <h2 id="titulo-cliente" className={estilos.subtitulo}>1. Cliente</h2>
          {idClienteInicial && clienteInicial.cargando && <Esqueleto lineas={2} />}
          {cliente ? (
            <ClienteElegido cliente={cliente} alCambiar={() => { setCliente(null); setPaso('buscar'); }} />
          ) : paso === 'crear' ? (
            puedeCrear ? (
              <CrearCliente inicial={busqueda} alCrear={elegir} alCancelar={() => setPaso('buscar')} />
            ) : (
              <>
                <Aviso tono="aviso" titulo="No hay un cliente con esos datos">
                  Crear clientes lo hacen el dueño, el administrador o un operario. Pídeles que lo registren y vuelve a buscarlo.
                </Aviso>
                <div><Boton variante="secundario" onClick={() => setPaso('buscar')}>Buscar otra vez</Boton></div>
              </>
            )
          ) : (
            <BuscarCliente primario alEncontrar={elegir} alNoEncontrar={(datos) => { setBusqueda(datos); setPaso('crear'); }} />
          )}
        </Tarjeta>

        <Tarjeta como="section" aria-labelledby="titulo-solicitud" className={estilos.seccion} ref={refSeccionSolicitud}>
          <h2 id="titulo-solicitud" className={estilos.subtitulo}>2. Letreros y descripción</h2>
          {!cliente ? (
            <p className={estilos.explicacion}>Elige o crea al cliente para describir lo que necesita.</p>
          ) : (
            <form ref={refFormulario} className={estilosPanel.formulario} onSubmit={registrar} noValidate>
              <EditorItems items={items} productos={productos.datos} cargandoProductos={productos.cargando}
                errores={visibles} alCambiar={cambiarItem} alTocar={tocar}
                alAgregar={agregarItem} alQuitar={quitarItem} deshabilitado={estado.enviando} />
              <Campo etiqueta="Descripción" como="textarea" requerido rows={4} maxLength={LIMITES_SOLICITUD.descripcionMaxima}
                ayuda="Lo que pidió el cliente: textos, colores, instalación y plazos." disabled={estado.enviando}
                {...propiedades('descripcionGeneral')} />
              <p className={estilos.nota}>Los archivos que trajo el cliente se adjuntan en la ficha, después de registrar.</p>
              <div ref={refError} tabIndex={-1} className={estilos.foco}>
                {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
              </div>
              <div className={estilosPanel.pieFormulario}>
                <Boton type="submit" iconoInicio={Save} cargando={estado.enviando} textoCargando="Registrando…">Registrar solicitud</Boton>
              </div>
            </form>
          )}
        </Tarjeta>
      </div>
    </div>
  );
}
