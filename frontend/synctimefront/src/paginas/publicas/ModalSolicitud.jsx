import { useRef, useState } from 'react';
import { Send } from 'lucide-react';
import { Aviso, Boton, Campo, Casilla, Dialogo } from '../../componentes/ui';
import { publico } from '../../api/publico';
import { TALLER } from '../../configuracion/taller';
import { useConsulta } from '../../utilidades/useConsulta';
import estilos from './ModalSolicitud.module.css';

const VACIO = { idProducto: '', ancho: '', alto: '', cantidad: '1', detalle: '', nombre: '', telefono: '', email: '', privacidad: false };

function validar(datos) {
  const errores = {};
  if (!datos.idProducto) errores.idProducto = 'Elige el tipo de letrero.';
  for (const [campo, nombre] of [['ancho', 'el ancho'], ['alto', 'el alto']]) {
    const valor = Number(datos[campo]);
    if (datos[campo] !== '' && (!Number.isFinite(valor) || valor <= 0 || valor > 5000)) {
      errores[campo] = `Escribe ${nombre} en centímetros, entre 1 y 5.000.`;
    }
  }
  const cantidad = Number(datos.cantidad);
  if (!Number.isInteger(cantidad) || cantidad < 1 || cantidad > 999) errores.cantidad = 'La cantidad va de 1 a 999.';
  if (datos.detalle.trim().length < 10) errores.detalle = 'Cuéntanos un poco más: al menos 10 letras sobre lo que necesitas.';
  if (!datos.nombre.trim()) errores.nombre = 'Escribe tu nombre.';
  if (!/^(\+?56)?\s*9\d{8}$/.test(datos.telefono.replace(/\s/g, ''))) {
    errores.telefono = 'Escribe un celular de 9 dígitos que empiece con 9, por ejemplo 912345678.';
  }
  if (datos.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(datos.email.trim())) {
    errores.email = 'Escribe un correo completo, por ejemplo maria@correo.cl.';
  }
  if (!datos.privacidad) errores.privacidad = 'Debes aceptar la política de privacidad para enviar la solicitud.';
  return errores;
}

/**
 * Modal de solicitud de la portada (RFS04). En esta fase se completa y se valida, pero no se envía:
 * el envío en línea llega en la fase 7 y exigirá una cuenta con correo verificado (RNFP33). Al enviar
 * se muestra el aviso de contacto directo (S25), el mismo que verá quien no pueda enviar en línea.
 */
export function ModalSolicitud({ abierto, trabajo, alCerrar }) {
  return (
    <Dialogo
      abierto={abierto}
      alCerrar={alCerrar}
      titulo="Solicitar una cotización"
      descripcion="Cuéntanos qué necesitas. Te respondemos con una cotización sin compromiso."
      ancho="36rem"
    >
      <FormularioSolicitud trabajo={trabajo} alCerrar={alCerrar} />
    </Dialogo>
  );
}

function FormularioSolicitud({ trabajo, alCerrar }) {
  const productos = useConsulta('productos', publico.productos);
  const [datos, setDatos] = useState(() => ({ ...VACIO, idProducto: trabajo?.idProducto ? String(trabajo.idProducto) : '' }));
  const [errores, setErrores] = useState({});
  const [enviado, setEnviado] = useState(false);
  const refAviso = useRef(null);

  const cambiar = (campo) => (evento) => {
    const valor = evento.target.type === 'checkbox' ? evento.target.checked : evento.target.value;
    setDatos((d) => ({ ...d, [campo]: valor }));
  };

  function alEnviar(evento) {
    evento.preventDefault();
    const encontrados = validar(datos);
    setErrores(encontrados);
    const primero = Object.keys(encontrados)[0];
    if (primero) {
      evento.currentTarget.querySelector(`[name="${primero}"]`)?.focus();
      return;
    }
    setEnviado(true);
    requestAnimationFrame(() => refAviso.current?.focus());
  }

  if (enviado) {
    return (
      <div className={estilos.resultado} ref={refAviso} tabIndex={-1}>
        <Aviso tono="aviso" titulo="Para esta solicitud es preferible contactarse directamente con el local">
          Visítanos en {TALLER.direccion}, {TALLER.ciudad}, dentro del horario de atención.
          En esta versión de demostración la solicitud aún no se envía: el envío en línea se activa en la fase 7.
        </Aviso>
        <div className={estilos.pie}>
          <Boton variante="secundario" onClick={() => setEnviado(false)}>Revisar mis datos</Boton>
          <Boton onClick={alCerrar}>Entendido</Boton>
        </div>
      </div>
    );
  }

  return (
    <form className={estilos.formulario} onSubmit={alEnviar} noValidate>
      {trabajo && <p className={estilos.referencia}>Trabajo de referencia: <strong>{trabajo.titulo}</strong></p>}

      <fieldset className={estilos.grupo}>
        <legend className={estilos.leyenda}>El trabajo</legend>
        <Campo etiqueta="Tipo de letrero" como="select" name="idProducto" requerido value={datos.idProducto}
          onChange={cambiar('idProducto')} error={errores.idProducto} disabled={productos.cargando}>
          <option value="">{productos.cargando ? 'Cargando…' : 'Elige uno'}</option>
          {(productos.datos ?? []).map((p) => <option key={p.idProducto} value={p.idProducto}>{p.nombre}</option>)}
        </Campo>
        <div className={estilos.medidas}>
          <Campo etiqueta="Ancho (cm)" name="ancho" inputMode="decimal" value={datos.ancho} onChange={cambiar('ancho')} error={errores.ancho} />
          <Campo etiqueta="Alto (cm)" name="alto" inputMode="decimal" value={datos.alto} onChange={cambiar('alto')} error={errores.alto} />
          <Campo etiqueta="Cantidad" name="cantidad" inputMode="numeric" requerido value={datos.cantidad} onChange={cambiar('cantidad')} error={errores.cantidad} />
        </div>
        <Campo etiqueta="Detalle" como="textarea" name="detalle" requerido maxLength={1000}
          ayuda="Textos, colores, si es luminoso y dónde se instala." value={datos.detalle} onChange={cambiar('detalle')} error={errores.detalle} />
      </fieldset>

      <fieldset className={estilos.grupo}>
        <legend className={estilos.leyenda}>Tus datos</legend>
        <Campo etiqueta="Nombre" name="nombre" requerido autoComplete="name" value={datos.nombre} onChange={cambiar('nombre')} error={errores.nombre} />
        <div className={estilos.contacto}>
          <Campo etiqueta="Celular" name="telefono" requerido type="tel" inputMode="tel" autoComplete="tel"
            value={datos.telefono} onChange={cambiar('telefono')} error={errores.telefono} />
          <Campo etiqueta="Correo" name="email" type="email" autoComplete="email" ayuda="Opcional"
            value={datos.email} onChange={cambiar('email')} error={errores.email} />
        </div>
      </fieldset>

      <Casilla name="privacidad" etiqueta="Acepto la política de privacidad" checked={datos.privacidad}
        onChange={cambiar('privacidad')} error={errores.privacidad}
        descripcion="Usaremos tus datos solo para responder esta solicitud." />

      <Aviso tono="info" titulo="Envío en línea: próximamente">
        Se activa en la fase 7 y pedirá una cuenta con correo verificado, para evitar solicitudes falsas.
      </Aviso>

      <div className={estilos.pie}>
        <Boton variante="secundario" onClick={alCerrar}>Cancelar</Boton>
        <Boton type="submit" iconoInicio={Send}>Enviar solicitud</Boton>
      </div>
    </form>
  );
}
