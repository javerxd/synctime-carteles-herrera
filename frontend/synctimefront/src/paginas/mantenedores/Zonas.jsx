import { useId, useState } from 'react';
import { Eye, EyeOff, Power, PowerOff, ShieldCheck, ShieldOff } from 'lucide-react';
import { Aviso, Boton, Insignia, Tarjeta } from '../../componentes/ui';
import { ErrorApi } from '../../api/cliente';
import { vitrina } from '../../api/mantenedores';
import { useAvisos } from '../../avisos/Avisos';
import { useSesion } from '../../sesion/sesion';
import { Confirmacion } from '../compartidos/Dialogos';
import { mensajeFallaGestion } from '../compartidos/reautenticacion';
import { MENSAJE_LOGO, puedeSerVisible, textoConflicto } from './reglasMantenedores';
import estilosPanel from '../panel/Panel.module.css';
import estilos from './Mantenedores.module.css';

export const MENSAJE_CAMBIOS = 'Guarda o descarta tus cambios en «Datos» antes de cambiar el estado: al hacerlo, la ficha se recarga.';

/**
 * Con cambios sin guardar en el formulario, los botones de estado quedan inactivos (siguen enfocables) y dicen
 * por qué. `propio`: si el botón ya tiene otro motivo para estar inactivo.
 */
function inactivo({ hayCambios, idCambios, propio = null, accion }) {
  const motivo = hayCambios ? idCambios : propio;
  const bloqueado = Boolean(motivo);
  return {
    'aria-disabled': bloqueado ? 'true' : undefined,
    'aria-describedby': motivo ?? undefined,
    className: bloqueado ? estilos.inactivo : undefined,
    onClick: bloqueado ? undefined : accion,
  };
}

function AvisoCambios({ hayCambios, id }) {
  return hayCambios ? <p id={id} className={estilos.motivo}>{MENSAJE_CAMBIOS}</p> : null;
}

/** Un 409 con el texto que corresponde (propio, ultimo_dueno, logo_sin_autorizacion), para el diálogo o el aviso. */
function conTexto(falla) {
  if (falla?.estado === 403 || falla?.estado === 429) return new ErrorApi({ estado: falla.estado, codigo: falla.codigo, mensaje: mensajeFallaGestion(falla) });
  if (falla?.estado !== 409 || falla.codigo === 'version_obsoleta') return falla;
  return new ErrorApi({ estado: 409, codigo: falla.codigo, mensaje: textoConflicto(falla) });
}

/**
 * Activar o desactivar (no hay borrado: lo desactivado deja de ofrecerse en los formularios). Para el personal,
 * desactivar pide confirmación: se borra su acceso con rostro y se cierran sus sesiones. Nadie se desactiva a sí mismo.
 */
export function ZonaActivo({ tipo, registro, id, alCambiar, hayCambios = false }) {
  const { anunciar } = useAvisos();
  const { usuario } = useSesion();
  const [confirmando, setConfirmando] = useState(false);
  const [estado, setEstado] = useState({ enviando: false, error: null });
  const idMotivo = useId();
  const idCambios = useId();
  const activo = registro.activo !== false;
  const esPersonal = tipo.clave === 'personal';
  // La sesión expone el id del empleado como usuario.id (sesion.js, usuarioDesde).
  const esUnoMismo = esPersonal && Number(registro.idEmpleado) === Number(usuario?.id);
  const nombre = tipo.nombreDe(registro);

  async function cambiar(nuevoActivo) {
    try {
      await tipo.api.cambiarActivo(id, nuevoActivo, registro.rowVersion);
    } catch (falla) {
      if (falla?.estado === 409 || falla?.estado === 404) alCambiar();
      throw conTexto(falla);
    }
    anunciar({ tono: 'exito', titulo: nuevoActivo ? `${nombre} quedó activo` : `${nombre} quedó desactivado` });
    alCambiar();
  }

  async function directo(nuevoActivo) {
    setEstado({ enviando: true, error: null });
    try {
      await cambiar(nuevoActivo);
      setEstado({ enviando: false, error: null });
    } catch (falla) {
      setEstado({
        enviando: false,
        error: falla?.codigo === 'version_obsoleta' ? 'Otra persona modificó este registro. Ya cargamos la versión actual: revísala y vuelve a intentarlo.' : falla?.message,
      });
    }
  }

  return (
    <Tarjeta como="section" aria-labelledby="titulo-estado">
      <div className={estilos.zona}>
        <h2 id="titulo-estado" className={estilosPanel.subtitulo}>Estado</h2>
        <p className={estilos.lineaEstado}>
          {activo ? <Insignia tono="exito">Activo</Insignia> : <Insignia tono="neutro">Inactivo</Insignia>}
          <span>
            {activo
              ? (esPersonal ? 'Puede ingresar al panel.' : 'Se ofrece en los formularios.')
              : (esPersonal ? 'No puede ingresar al panel.' : 'No se ofrece en los formularios. Lo ya registrado no cambia.')}
          </span>
        </p>
        {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
        <AvisoCambios hayCambios={hayCambios} id={idCambios} />
        {activo ? (
          <div className={estilos.accionZona}>
            <Boton variante={esPersonal ? 'peligro' : 'secundario'} iconoInicio={PowerOff} cargando={estado.enviando} textoCargando="Desactivando…"
              {...inactivo({ hayCambios, idCambios, propio: esUnoMismo ? idMotivo : null, accion: () => (esPersonal ? setConfirmando(true) : directo(false)) })}
              aria-describedby={hayCambios ? `${idCambios} ${idMotivo}` : idMotivo}>
              {esPersonal ? 'Desactivar a esta persona' : `Desactivar ${tipo.singular}`}
            </Boton>
            <p id={idMotivo} className={estilos.motivo}>
              {esUnoMismo
                ? 'No puedes desactivarte a ti mismo. Pídeselo a otra persona de gestión.'
                : esPersonal ? 'Se borrará su acceso con rostro y se cerrarán sus sesiones.' : tipo.ayudaDesactivar}
            </p>
          </div>
        ) : (
          <div>
            <Boton variante="secundario" iconoInicio={Power} cargando={estado.enviando} textoCargando="Activando…"
              {...inactivo({ hayCambios, idCambios, accion: () => directo(true) })}>
              {esPersonal ? 'Activar a esta persona' : `Activar ${tipo.singular}`}
            </Boton>
          </div>
        )}
      </div>
      {esPersonal && (
        <Confirmacion abierto={confirmando} alCerrar={() => setConfirmando(false)} titulo={`Desactivar a ${nombre}`}
          descripcion="Se borrará su acceso con rostro y se cerrarán sus sesiones." accion="Desactivar" variante="peligro" icono={PowerOff}
          entidad="esta persona" alConfirmar={async () => { await cambiar(false); setConfirmando(false); }}>
          <p className={estilos.textoDialogo}>
            No podrá ingresar al panel, pero su historial se conserva. Para reactivarla, abre su ficha y pulsa «Activar a esta persona»;
            tendrá que volver a registrar su rostro.
          </p>
        </Confirmacion>
      )}
    </Tarjeta>
  );
}

/**
 * La vitrina: autorización del logo del cliente (RNFO17, queda quién y cuándo) y si se muestra. Con logo y sin
 * autorización, «Mostrar en la vitrina» queda bloqueado y lo explica; la API responde 409 logo_sin_autorizacion.
 */
export function ZonaVitrina({ registro, id, alCambiar, hayCambios = false }) {
  const { anunciar } = useAvisos();
  const [confirmando, setConfirmando] = useState(null); // 'autorizar' | 'retirar'
  const [estado, setEstado] = useState({ enviando: null, error: null });
  const idMotivo = useId();
  const idCambios = useId();
  const visible = Boolean(registro.visible);
  const sePuedeMostrar = puedeSerVisible(registro);

  async function cambiarVisible() {
    setEstado({ enviando: 'visible', error: null });
    try {
      await vitrina.cambiarVisible(id, !visible, registro.rowVersion);
      anunciar({ tono: 'exito', titulo: visible ? 'La publicación quedó oculta' : 'La publicación ya se ve en la vitrina' });
      setEstado({ enviando: null, error: null });
      alCambiar();
    } catch (falla) {
      if (falla?.estado === 409 || falla?.estado === 404) alCambiar();
      setEstado({
        enviando: null,
        error: falla?.codigo === 'version_obsoleta'
          ? 'Otra persona modificó esta publicación. Ya cargamos la versión actual: revísala y vuelve a intentarlo.'
          : conTexto(falla)?.message,
      });
    }
  }

  async function autorizar(autorizado) {
    try {
      await vitrina.autorizarLogo(id, autorizado, registro.rowVersion);
    } catch (falla) {
      if (falla?.estado === 409 || falla?.estado === 404) alCambiar();
      throw conTexto(falla);
    }
    anunciar({ tono: 'exito', titulo: autorizado ? 'Autorización registrada' : 'Autorización retirada' });
    setConfirmando(null);
    alCambiar();
  }

  return (
    <Tarjeta como="section" aria-labelledby="titulo-visibilidad">
      <div className={estilos.zona}>
        <h2 id="titulo-visibilidad" className={estilosPanel.subtitulo}>Autorización y visibilidad</h2>
        <AvisoCambios hayCambios={hayCambios} id={idCambios} />
        {registro.incluyeLogoCliente && (
          <div className={estilos.bloqueZona}>
            <p className={estilos.lineaEstado}>
              {registro.logoAutorizado
                ? <Insignia tono="exito" icono={ShieldCheck}>Autorización registrada</Insignia>
                : <Insignia tono="aviso" icono={ShieldOff}>Falta la autorización del cliente</Insignia>}
              <span>Lleva el logo de un cliente.</span>
            </p>
            <div className={estilos.accionZona}>
              {registro.logoAutorizado ? (
                <Boton variante="secundario" iconoInicio={ShieldOff} {...inactivo({ hayCambios, idCambios, accion: () => setConfirmando('retirar') })}>Retirar autorización</Boton>
              ) : (
                <Boton variante="secundario" iconoInicio={ShieldCheck} {...inactivo({ hayCambios, idCambios, accion: () => setConfirmando('autorizar') })}>
                  Registrar autorización del cliente
                </Boton>
              )}
            </div>
          </div>
        )}
        <div className={estilos.bloqueZona}>
          <p className={estilos.lineaEstado}>
            {visible ? <Insignia tono="exito" icono={Eye}>Visible en la vitrina</Insignia> : <Insignia tono="neutro" icono={EyeOff}>Oculta</Insignia>}
          </p>
          {estado.error && <Aviso tono="peligro" titulo={estado.error} />}
          <div className={estilos.accionZona}>
            {visible ? (
              <Boton variante="secundario" iconoInicio={EyeOff} cargando={estado.enviando === 'visible'} textoCargando="Ocultando…"
                {...inactivo({ hayCambios, idCambios, accion: cambiarVisible })}>
                Ocultar de la vitrina
              </Boton>
            ) : (
              <Boton variante="secundario" iconoInicio={Eye} cargando={estado.enviando === 'visible'} textoCargando="Publicando…"
                {...inactivo({ hayCambios, idCambios, propio: sePuedeMostrar ? null : idMotivo, accion: cambiarVisible })}>
                Mostrar en la vitrina
              </Boton>
            )}
            {!sePuedeMostrar && !visible && <p id={idMotivo} className={estilos.motivo}>{MENSAJE_LOGO}</p>}
          </div>
        </div>
      </div>
      <Confirmacion abierto={confirmando === 'autorizar'} alCerrar={() => setConfirmando(null)} titulo="Registrar la autorización del cliente"
        descripcion="Confirma que el cliente autorizó mostrar su logo en la vitrina. Queda registrado quién lo registró y cuándo."
        accion="Registrar autorización" icono={ShieldCheck} entidad="esta publicación" alConfirmar={() => autorizar(true)} />
      <Confirmacion abierto={confirmando === 'retirar'} alCerrar={() => setConfirmando(null)} titulo="Retirar la autorización"
        descripcion="Sin autorización, la publicación no se puede mostrar en la vitrina." accion="Retirar autorización" variante="peligro"
        icono={ShieldOff} entidad="esta publicación" alConfirmar={() => autorizar(false)} />
    </Tarjeta>
  );
}
