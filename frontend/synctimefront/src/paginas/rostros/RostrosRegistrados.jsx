import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Gauge, ScanFace, Trash2 } from 'lucide-react';
import { Aviso, Boton, Dialogo, Enlace, Insignia, TablaAdaptable, Tarjeta } from '../../componentes/ui';
import authService from '../../api/authService';
import { rostros } from '../../api/rostros';
import { useAvisos } from '../../avisos/Avisos';
import RegistroRostro from '../../rostro/RegistroRostro';
import { borrarTokenEquipo } from '../../rostro/tokenEquipo';
import { useSesion } from '../../sesion/sesion';
import { useConsulta } from '../../utilidades/useConsulta';
import {
  ESTADOS_ROSTRO, fechaCorta, filasDe, nombreDe, puedeDesactivar, puedeRegistrar,
} from './reglasRostros';
import panel from '../panel/Panel.module.css';
import estilos from './Rostros.module.css';

function EstadoRostro({ estado }) {
  const conocido = ESTADOS_ROSTRO[estado] ?? { texto: String(estado ?? 'Sin estado'), tono: 'neutro' };
  return <Insignia tono={conocido.tono} icono={conocido.icono}>{conocido.texto}</Insignia>;
}

function Historial({ constancias }) {
  if (!constancias?.length) return <span className={estilos.tenue}>Sin constancias anteriores</span>;
  const cantidad = constancias.length;
  return (
    <details className={estilos.historial}>
      <summary>{cantidad} {cantidad === 1 ? 'constancia anterior' : 'constancias anteriores'}</summary>
      <ul>
        {constancias.map((c, i) => (
          <li key={`${c.fechaConsentimiento ?? ''}-${c.fechaDesactivacion ?? ''}-${i}`}>
            Aceptó el {fechaCorta(c.fechaConsentimiento)} · retiró el {fechaCorta(c.fechaDesactivacion ?? c.fechaRevocacion)}
          </li>
        ))}
      </ul>
    </details>
  );
}

/**
 * Rostros registrados (S16, solo gestión): los empleados activos de gestión con el estado de su rostro.
 * Gestión registra el rostro de OTRO empleado de gestión con esa persona presente (ella escribe su contraseña y
 * acepta el consentimiento en este equipo) y desactiva cualquiera. El propio rostro se registra desde Mi perfil.
 */
export default function RostrosRegistrados() {
  const { usuario } = useSesion();
  const { anunciar } = useAvisos();
  const lista = useConsulta('rostros', (senal) => rostros.listar({ senal }));
  const calibracion = useConsulta('calibracion', (senal) => rostros.calibracion.obtener({ senal }));
  const [registrando, setRegistrando] = useState(null);
  const [desactivando, setDesactivando] = useState(null);
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState('');
  const refCancelar = useRef(null);
  const refTituloRegistro = useRef(null);
  const refTitulo = useRef(null);
  const filas = filasDe(lista.datos);

  useEffect(() => {
    if (registrando) refTituloRegistro.current?.focus();
  }, [registrando]);

  const consentimiento = useConsulta(registrando ? 'consentimiento' : null, () => (registrando ? authService.estadoRostro() : null));

  async function alRegistrar({ password, versionConsentimiento, muestras }) {
    await rostros.registrar(registrando.idEmpleado, { passwordTitular: password, versionConsentimiento, muestras });
    anunciar({ tono: 'exito', titulo: `Rostro de ${registrando.nombre} registrado`, texto: 'Se guardaron las tres muestras.' });
    cerrarRegistro();
    lista.recargar();
  }

  // La sección desaparece: el foco vuelve al título de la página, sobre la tabla.
  function cerrarRegistro() {
    setRegistrando(null);
    refTitulo.current?.focus();
  }

  async function alDesactivar() {
    const fila = desactivando;
    setEnviando(true);
    try {
      await rostros.desactivar(fila.idEmpleado);
      if (fila.idEmpleado === usuario?.idEmpleado) borrarTokenEquipo(usuario.email);
      setError('');
      anunciar({ tono: 'exito', titulo: `Rostro de ${fila.nombre} desactivado`, texto: 'Sus muestras fueron borradas.' });
      lista.recargar();
    } catch (err) {
      setError(err?.message || 'No pudimos desactivar el rostro.');
    } finally {
      setEnviando(false);
      setDesactivando(null);
    }
  }

  function acciones(fila) {
    const esPropio = fila.idEmpleado === usuario?.idEmpleado;
    return (
      <div className={estilos.acciones}>
        {puedeRegistrar(fila) && esPropio && (
          <Enlace como={Link} to="/panel/perfil">Registrar tu rostro en Mi perfil</Enlace>
        )}
        {puedeRegistrar(fila) && !esPropio && (
          <Boton variante="secundario" tamano="compacto" iconoInicio={ScanFace} aria-label={`Registrar rostro de ${fila.nombre}`}
            onClick={() => setRegistrando(fila)} disabled={Boolean(registrando)}>
            Registrar rostro
          </Boton>
        )}
        {puedeDesactivar(fila) && (
          <Boton variante="fantasma" tamano="compacto" iconoInicio={Trash2} aria-label={`Desactivar el rostro de ${fila.nombre}`}
            onClick={() => setDesactivando(fila)}>
            Desactivar
          </Boton>
        )}
      </div>
    );
  }

  const columnas = [
    { clave: 'nombre', encabezado: 'Nombre', principal: true },
    { clave: 'rol', encabezado: 'Rol' },
    { clave: 'estado', encabezado: 'Estado', render: (f) => <EstadoRostro estado={f.estado} /> },
    { clave: 'registro', encabezado: 'Registro', render: (f) => fechaCorta(f.fechaRegistro) },
    { clave: 'consentimiento', encabezado: 'Consentimiento', render: (f) => fechaCorta(f.fechaConsentimiento) },
    { clave: 'registradoPor', encabezado: 'Registrado por', render: (f) => nombreDe(f.registradoPor) },
    { clave: 'desactivacion', encabezado: 'Desactivación', render: (f) => fechaCorta(f.fechaDesactivacion) },
    { clave: 'historial', encabezado: 'Historial', render: (f) => <Historial constancias={f.historial} /> },
    { clave: 'acciones', encabezado: 'Acciones', render: acciones },
  ];

  const textosRegistro = registrando && {
    boton: 'Registrar el rostro',
    guardar: 'Guardar el rostro',
    consentimiento: `${registrando.nombre} leyó la información anterior y autoriza el tratamiento de su dato biométrico para esta finalidad.`,
    password: `Contraseña de ${registrando.nombre}`,
    ayudaPassword: 'La escribe esa persona, en este equipo. No se guarda en el navegador.',
    // 'off' no basta en un campo de contraseña: con 'new-password' el navegador no la rellena con la de quien administra.
    autocompletar: 'new-password',
  };

  return (
    <div className={panel.pagina}>
      <div className={panel.cabecera}>
        <div>
          <h1 ref={refTitulo} tabIndex={-1} className={panel.titulo}>Rostros registrados</h1>
          <p className={panel.bajada}>
            Solo dueño y administrador pueden entrar con su rostro. Desactivar borra las tres muestras y conserva la constancia del consentimiento.
          </p>
        </div>
        {calibracion.datos?.activa && (
          <Boton como={Link} to="/panel/rostros/calibrar" variante="secundario" iconoInicio={Gauge}>Calibrar el rostro</Boton>
        )}
      </div>

      {error && <Aviso tono="peligro" titulo={error} />}
      {lista.error && (
        <Aviso tono="peligro" titulo={lista.error.message || 'No pudimos cargar los rostros registrados.'}
          accion={<Boton variante="secundario" tamano="compacto" onClick={lista.recargar}>Reintentar</Boton>} />
      )}

      <TablaAdaptable titulo="Rostros registrados" columnas={columnas} filas={filas} claveFila="idEmpleado"
        cargando={lista.cargando} actualizando={lista.recargando} />

      {registrando && (
        <Tarjeta como="section" aria-labelledby="titulo-registro-otro" className={panel.ficha}>
          <div className={panel.cabecera}>
            <h2 id="titulo-registro-otro" ref={refTituloRegistro} tabIndex={-1} className={panel.subtitulo}>
              Registrar el rostro de {registrando.nombre}
            </h2>
            <Boton variante="fantasma" tamano="compacto" onClick={cerrarRegistro}>Cerrar sin registrar</Boton>
          </div>
          <p className={panel.bajada}>
            {registrando.nombre} debe estar presente: lee el consentimiento, lo acepta y escribe su propia contraseña. Para entrar
            con su rostro, después autoriza su equipo desde su perfil.
          </p>
          {consentimiento.error && <Aviso tono="peligro" titulo={consentimiento.error.message || 'No pudimos cargar el consentimiento.'} />}
          {consentimiento.datos?.consentimiento && (
            <RegistroRostro consentimiento={consentimiento.datos.consentimiento} textos={textosRegistro} alGuardar={alRegistrar} />
          )}
        </Tarjeta>
      )}

      <Dialogo
        abierto={Boolean(desactivando)}
        alCerrar={() => setDesactivando(null)}
        enfoqueInicial={refCancelar}
        titulo={desactivando ? `Desactivar el rostro de ${desactivando.nombre}` : ''}
        descripcion="Se borrarán sus tres muestras y deberá entrar con su contraseña. Se conserva la constancia de las fechas de aceptación y retiro."
        pie={(
          <>
            <Boton ref={refCancelar} variante="secundario" onClick={() => setDesactivando(null)}>Cancelar</Boton>
            <Boton variante="peligro" onClick={alDesactivar} cargando={enviando} textoCargando="Desactivando…">Desactivar</Boton>
          </>
        )}
      />
    </div>
  );
}
