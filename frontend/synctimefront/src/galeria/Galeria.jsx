import { useMemo, useRef, useState } from 'react';
import { ArrowRight, Plus, Search, Send } from 'lucide-react';
import {
  Aviso, BarraFiltros, Boton, Campo, Casilla, Chip, Dialogo, Enlace, Esqueleto, EstadoCartel, EstadoPago,
  EstadoPedido, EstadoVacio, Insignia, Paginacion, Pestanas, TablaAdaptable, Tarjeta,
  TEXTOS_ESTADO_PAGO, TEXTOS_ESTADO_PEDIDO,
} from '../componentes/ui';
import { contrasteDeTokens } from './contraste';
import estilos from './Galeria.module.css';

const pesos = new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'CLP', maximumFractionDigits: 0 });

// Datos de ejemplo para mostrar estados y filtros. No son pedidos reales.
const PEDIDOS = [
  { id: 101, cliente: 'Panadería El Trigal', fecha: '2026-11-04', estadoPedido: 'en_proceso', estadoPago: 'abonado', total: 185000, saldo: 85000 },
  { id: 102, cliente: 'Ferretería Biobío', fecha: '2026-11-02', estadoPedido: 'entregado', estadoPago: 'no_pagado', total: 96000, saldo: 96000 },
  { id: 103, cliente: 'Clínica Dental Sonríe', fecha: '2026-10-28', estadoPedido: 'terminado', estadoPago: 'pagado_total', total: 240000, saldo: 0 },
  { id: 104, cliente: 'Minimarket Don Lucho', fecha: '2026-10-21', estadoPedido: 'aceptado', estadoPago: 'no_pagado', total: 54000, saldo: 54000 },
  { id: 105, cliente: 'Transportes Ruiz', fecha: '2026-10-15', estadoPedido: 'entregado', estadoPago: 'pagado_total', total: 410000, saldo: 0 },
  { id: 106, cliente: 'Café Villagrán', fecha: '2026-09-30', estadoPedido: 'anulado', estadoPago: 'no_pagado', total: 72000, saldo: 72000 },
  { id: 107, cliente: 'Peluquería Estilo', fecha: '2026-09-18', estadoPedido: 'entregado', estadoPago: 'abonado', total: 128000, saldo: 28000 },
];
const POR_PAGINA = 5;

const SECCIONES = [
  ['color', 'Color'], ['tipografia', 'Tipografía'], ['botones', 'Botones'], ['campos', 'Campos'],
  ['estados', 'Estados'], ['tarjetas', 'Tarjetas'], ['avisos', 'Avisos'], ['listados', 'Listados'], ['dialogo', 'Diálogo'],
];

export function Galeria() {
  return (
    <>
      <a href="#contenido" className="enlace-salto">Saltar al contenido</a>
      <header className={estilos.barra}>
        <div className={estilos.barraInterior}>
          <p className={estilos.marca}>
            Letrería Los Herrera <span className={estilos.marcaSistema}>SyncTime · Sistema de diseño</span>
          </p>
          <nav aria-label="Secciones de la galería" className={estilos.navegacion}>
            <ul>
              {SECCIONES.map(([id, nombre]) => (
                <li key={id}><a href={`#${id}`}>{nombre}</a></li>
              ))}
            </ul>
          </nav>
        </div>
      </header>

      <main id="contenido" tabIndex={-1} className={estilos.principal}>
        <section className={estilos.portada} aria-labelledby="titulo-galeria">
          <div className={estilos.contenedor}>
            <Insignia tono="aviso">Solo en desarrollo</Insignia>
            <h1 id="titulo-galeria" className={estilos.tituloPortada}>Sistema de diseño de SyncTime</h1>
            <p className={estilos.bajada}>
              Tokens y componentes en modo claro para el portal y el panel del taller. Cada componente muestra sus
              estados: normal, al pasar el cursor, con foco, presionado, deshabilitado, cargando, con error y vacío.
              Las reglas están en <code>DESIGN.md</code>.
            </p>
          </div>
        </section>

        <SeccionColor />
        <SeccionTipografia />
        <SeccionBotones />
        <SeccionCampos />
        <SeccionEstados />
        <SeccionTarjetas />
        <SeccionAvisos />
        <SeccionListados />
        <SeccionDialogo />
      </main>
    </>
  );
}

function Seccion({ id, titulo, descripcion, alterna = false, children }) {
  return (
    <section id={id} aria-labelledby={`titulo-${id}`} className={alterna ? estilos.seccionAlterna : estilos.seccion}>
      <div className={estilos.contenedor}>
        <div className={estilos.encabezadoSeccion}>
          <h2 id={`titulo-${id}`}>{titulo}</h2>
          {descripcion && <p className={estilos.descripcion}>{descripcion}</p>}
        </div>
        {children}
      </div>
    </section>
  );
}

function SeccionColor() {
  // Los estilos ya están cargados al montar (main.jsx los importa antes), así que se mide una vez.
  const [medidas] = useState(contrasteDeTokens);
  return (
    <Seccion id="color" titulo="Color" descripcion="Blanco pulcro, gris seda y tinta profunda. El ámbar aparece solo en la acción principal y en lo activo. Los contrastes se miden en vivo sobre los tokens.">
      <ul className={estilos.muestras}>
        {medidas.map((m) => (
          <li key={m.token} className={estilos.muestra}>
            <span className={estilos.color} style={{ background: `var(${m.token})` }} aria-hidden="true" />
            <span className={estilos.nombreToken}>{m.nombre}</span>
            <code className={estilos.valor}>{m.valor}</code>
            {m.contraste && <span className={estilos.contraste}>{m.contraste} sobre blanco</span>}
          </li>
        ))}
      </ul>
    </Seccion>
  );
}

function SeccionTipografia() {
  return (
    <Seccion id="tipografia" alterna titulo="Tipografía" descripcion="Plus Jakarta Sans, alojada en el propio sitio, en cuatro grosores. Títulos con interletrado de −0,02 em.">
      <div className={estilos.escala}>
        <p className={estilos.muestraPortada}>Letreros que se ven de lejos</p>
        <h1>Título de página · 36</h1>
        <h2>Título de sección · 30</h2>
        <h3>Subtítulo · 24</h3>
        <h4>Encabezado de bloque · 20</h4>
        <p className={estilos.parrafo}>
          Texto de lectura a 16 px con interlineado de 1,6. Fabricamos letreros luminosos, rotulación de
          vehículos y gráfica para locales en Los Ángeles. La medida de línea se limita a unas 68 letras.
        </p>
        <p className={estilos.pequeno}>Texto secundario a 14 px, para ayudas y datos de apoyo.</p>
        <p className="cifras">Cifras tabulares: {pesos.format(1250000)} · {pesos.format(85000)} · 12-11-2026</p>
      </div>
    </Seccion>
  );
}

function SeccionBotones() {
  const [cargando, setCargando] = useState(false);
  function simular() {
    setCargando(true);
    setTimeout(() => setCargando(false), 1800);
  }
  return (
    <Seccion id="botones" titulo="Botones" descripcion="El primario es la acción principal de cada pantalla; hay uno solo por vista. Al presionar se comprimen apenas. Miden 44 px de alto; el compacto baja a 36 px solo con puntero fino.">
      <div className={estilos.grupo}>
        <Boton iconoInicio={Send}>Enviar solicitud</Boton>
        <Boton variante="secundario">Cancelar</Boton>
        <Boton variante="fantasma" iconoFin={ArrowRight}>Ver detalle</Boton>
        <Boton variante="peligro">Anular pedido</Boton>
      </div>
      <div className={estilos.grupo}>
        <Boton tamano="compacto" iconoInicio={Plus}>Nuevo pedido</Boton>
        <Boton tamano="compacto" variante="secundario">Exportar</Boton>
        <Boton cargando={cargando} textoCargando="Guardando…" onClick={simular}>Guardar (simula carga)</Boton>
        <Boton disabled>Deshabilitado</Boton>
        <Boton variante="secundario" disabled>Deshabilitado</Boton>
      </div>
      <p className={estilos.pequeno}>
        Un enlace de texto: <Enlace href="#tipografia">ir a tipografía</Enlace> · uno externo:{' '}
        <Enlace href="https://www.openstreetmap.org" externo>mapa de OpenStreetMap</Enlace>
      </p>
    </Seccion>
  );
}

function SeccionCampos() {
  return (
    <Seccion id="campos" alterna titulo="Campos" descripcion="La etiqueta siempre visible y vinculada. La ayuda va antes del campo y el error después, anunciado al lector de pantalla.">
      <div className={estilos.formulario}>
        <Campo etiqueta="Nombre" requerido placeholder="Ej.: María Herrera" autoComplete="name" />
        <Campo etiqueta="Teléfono" ayuda="Nueve dígitos; te llamamos o escribimos por WhatsApp." type="tel" inputMode="tel" autoComplete="tel" />
        <Campo etiqueta="Correo" type="email" defaultValue="maria@correo" error="Escribe un correo completo, por ejemplo maria@correo.cl." />
        <Campo etiqueta="RUT" disabled defaultValue="12.345.678-5" ayuda="No se puede cambiar." />
        <Campo etiqueta="Tipo de letrero" como="select" defaultValue="">
          <option value="" disabled>Elige uno</option>
          <option>Letrero luminoso</option>
          <option>Rotulación de vehículo</option>
          <option>Letras corpóreas</option>
        </Campo>
        <Campo etiqueta="Cuéntanos qué necesitas" como="textarea" ayuda="Medidas aproximadas, colores y dónde va instalado." />
        <Casilla etiqueta="Acepto la política de privacidad" descripcion="Usaremos tus datos solo para responder esta solicitud." />
        <Casilla etiqueta="Acepto los términos y condiciones" error="Debes aceptar los términos para enviar la solicitud." />
      </div>
    </Seccion>
  );
}

function SeccionEstados() {
  const [abierto, setAbierto] = useState(true);
  return (
    <Seccion id="estados" titulo="Estados" descripcion="Texto, icono y color juntos: la información nunca depende solo del color. El estado del pedido y el de pago son independientes.">
      <div className={estilos.bloqueEstados}>
        <div>
          <h3 className={estilos.subtitulo}>Estado del pedido</h3>
          <div className={estilos.grupo}>
            {Object.keys(TEXTOS_ESTADO_PEDIDO).map((e) => <EstadoPedido key={e} estado={e} />)}
          </div>
        </div>
        <div>
          <h3 className={estilos.subtitulo}>Estado de pago</h3>
          <div className={estilos.grupo}>
            {Object.keys(TEXTOS_ESTADO_PAGO).map((e) => <EstadoPago key={e} estado={e} />)}
          </div>
        </div>
        <div>
          <h3 className={estilos.subtitulo}>Cartel electrónico</h3>
          <div className={estilos.cartel}>
            <EstadoCartel abierto={abierto} grande />
            <Boton variante="secundario" tamano="compacto" onClick={() => setAbierto((a) => !a)}>
              Dar vuelta el cartel
            </Boton>
          </div>
        </div>
      </div>
    </Seccion>
  );
}

const TRABAJOS = [
  { id: 1, titulo: 'Letrero luminoso para panadería', tipo: 'Caja de luz en acrílico', medidas: '240 × 80 cm', costo: 385000 },
  { id: 2, titulo: 'Rotulación de camioneta', tipo: 'Vinilo impreso y laminado', medidas: 'Puertas y portalón', costo: 290000 },
  { id: 3, titulo: 'Letras corpóreas para clínica', tipo: 'Acero inoxidable con luz trasera', medidas: '180 × 40 cm', costo: 640000 },
];

function SeccionTarjetas() {
  return (
    <Seccion id="tarjetas" alterna titulo="Tarjetas" descripcion="Un filo de 1 px en reposo; al pasar el cursor, la tarjeta de la vitrina se eleva con una sombra difusa. Todo el recuadro es pulsable a través de su título.">
      <ul className={estilos.vitrina}>
        {TRABAJOS.map((t) => (
          <li key={t.id}>
            <Tarjeta como="article" interactiva sinRelleno className={estilos.trabajo}>
              <div className={estilos.foto} role="img" aria-label="Espacio para la fotografía del trabajo (pendiente)">
                <span aria-hidden="true">Foto del trabajo</span>
              </div>
              <div className={estilos.cuerpoTrabajo}>
                <h3 className={estilos.tituloTrabajo}>
                  <a href="#dialogo" className={estilos.enlaceTarjeta}>{t.titulo}</a>
                </h3>
                <p className={estilos.pequeno}>{t.tipo} · {t.medidas}</p>
                <p className={estilos.costo}>
                  <span className={estilos.pequeno}>Desde</span> <strong className="cifras">{pesos.format(t.costo)}</strong>
                </p>
                <Insignia tono="neutro">Datos de ejemplo</Insignia>
              </div>
            </Tarjeta>
          </li>
        ))}
      </ul>
    </Seccion>
  );
}

function SeccionAvisos() {
  return (
    <Seccion id="avisos" titulo="Avisos" descripcion="Mensajes en línea. Un error interrumpe al lector de pantalla; una confirmación espera su turno.">
      <div className={estilos.pila}>
        <Aviso tono="info" titulo="Los indicadores muestran datos de ejemplo">Se conectan a los datos reales en la fase 12.</Aviso>
        <Aviso tono="exito" titulo="Solicitud enviada">Te responderemos por correo en un máximo de dos días hábiles.</Aviso>
        <Aviso tono="aviso" titulo="Para esta solicitud es preferible contactarse directamente con el local"
          accion={<Boton variante="secundario" tamano="compacto">Ver horario</Boton>}>
          Llámanos o escríbenos por WhatsApp dentro del horario de atención.
        </Aviso>
        <Aviso tono="peligro" titulo="No pudimos guardar el pedido">Otra persona lo modificó. Recarga la página para ver los cambios.</Aviso>
      </div>
    </Seccion>
  );
}

function SeccionListados() {
  const [estadoPedido, setEstadoPedido] = useState('');
  const [estadoPago, setEstadoPago] = useState('');
  const [cliente, setCliente] = useState('');
  const [porCobrar, setPorCobrar] = useState(false);
  const [pagina, setPagina] = useState(1);
  const [cargando, setCargando] = useState(false);
  const [periodo, setPeriodo] = useState('mes');

  const filtrados = useMemo(() => PEDIDOS.filter((p) =>
    (!estadoPedido || p.estadoPedido === estadoPedido)
    && (!estadoPago || p.estadoPago === estadoPago)
    && (!cliente || p.cliente.toLowerCase().includes(cliente.trim().toLowerCase()))
    && (!porCobrar || (p.estadoPedido !== 'anulado' && p.estadoPago !== 'pagado_total'))), [estadoPedido, estadoPago, cliente, porCobrar]);

  const totalPaginas = Math.max(1, Math.ceil(filtrados.length / POR_PAGINA));
  const paginaActual = Math.min(pagina, totalPaginas);
  const visibles = filtrados.slice((paginaActual - 1) * POR_PAGINA, paginaActual * POR_PAGINA);
  const porCobrarTotal = filtrados.filter((p) => p.estadoPedido !== 'anulado').reduce((s, p) => s + p.saldo, 0);
  const hayFiltros = Boolean(estadoPedido || estadoPago || cliente || porCobrar);

  function limpiar() {
    setEstadoPedido(''); setEstadoPago(''); setCliente(''); setPorCobrar(false); setPagina(1);
  }

  const columnas = [
    { clave: 'cliente', encabezado: 'Cliente', principal: true },
    { clave: 'id', encabezado: 'N.º', render: (p) => <span className="cifras">{p.id}</span> },
    { clave: 'fecha', encabezado: 'Fecha', render: (p) => p.fecha.split('-').reverse().join('-') },
    { clave: 'estadoPedido', encabezado: 'Pedido', render: (p) => <EstadoPedido estado={p.estadoPedido} /> },
    { clave: 'estadoPago', encabezado: 'Pago', render: (p) => <EstadoPago estado={p.estadoPago} /> },
    { clave: 'total', encabezado: 'Monto final', alinear: 'fin', render: (p) => pesos.format(p.total) },
    { clave: 'saldo', encabezado: 'Saldo', alinear: 'fin', render: (p) => pesos.format(p.saldo) },
  ];

  return (
    <Seccion id="listados" alterna titulo="Listados" descripcion="Filtros, tabla adaptable, paginación y pestañas. Bajo 768 px, cada fila se convierte en una ficha sin desplazamiento horizontal.">
      <div className={estilos.pila}>
        <Aviso tono="info" titulo="Datos de ejemplo">Siete pedidos inventados para mostrar los filtros y los estados.</Aviso>
        <BarraFiltros
          titulo="Filtros de pedidos"
          hayFiltros={hayFiltros}
          alLimpiar={limpiar}
          resumen={`${filtrados.length} ${filtrados.length === 1 ? 'pedido' : 'pedidos'} · ${pesos.format(porCobrarTotal)} por cobrar`}
        >
          <Campo etiqueta="Estado del pedido" como="select" value={estadoPedido} onChange={(e) => { setEstadoPedido(e.target.value); setPagina(1); }}>
            <option value="">Todos</option>
            {Object.entries(TEXTOS_ESTADO_PEDIDO).map(([v, t]) => <option key={v} value={v}>{t}</option>)}
          </Campo>
          <Campo etiqueta="Estado de pago" como="select" value={estadoPago} onChange={(e) => { setEstadoPago(e.target.value); setPagina(1); }}>
            <option value="">Todos</option>
            {Object.entries(TEXTOS_ESTADO_PAGO).map(([v, t]) => <option key={v} value={v}>{t}</option>)}
          </Campo>
          <Campo etiqueta="Cliente" type="search" value={cliente} placeholder="Nombre del cliente"
            onChange={(e) => { setCliente(e.target.value); setPagina(1); }} />
          <div className={estilos.chips}>
            <Chip seleccionado={porCobrar} alCambiar={(v) => { setPorCobrar(v); setPagina(1); }}>Por cobrar</Chip>
            <Chip seleccionado={cargando} alCambiar={setCargando}>Simular carga</Chip>
          </div>
        </BarraFiltros>

        <TablaAdaptable
          titulo="Pedidos"
          columnas={columnas}
          filas={visibles}
          cargando={cargando}
          vacio={(
            <EstadoVacio
              icono={Search}
              nivelTitulo={3}
              titulo="Ningún pedido coincide con estos filtros"
              descripcion="Prueba con otro estado o quita el filtro por cliente."
              accion={<Boton variante="secundario" onClick={limpiar}>Limpiar filtros</Boton>}
            />
          )}
        />
        <Paginacion pagina={paginaActual} totalPaginas={cargando ? 0 : totalPaginas} alCambiar={setPagina} etiqueta="Paginación de pedidos" />

        <Tarjeta>
          <Pestanas
            etiqueta="Periodo del resumen"
            activa={periodo}
            alCambiar={setPeriodo}
            pestanas={[
              { id: 'semana', etiqueta: 'Esta semana', contenido: <ResumenPeriodo pedidos={2} monto={281000} /> },
              { id: 'mes', etiqueta: 'Este mes', contenido: <ResumenPeriodo pedidos={4} monto={889000} /> },
              { id: 'anio', etiqueta: 'Este año', contenido: <ResumenPeriodo pedidos={37} monto={8420000} /> },
            ]}
          />
        </Tarjeta>

        <div className={estilos.dosColumnas}>
          <Tarjeta>
            <h3 className={estilos.subtitulo}>Esqueleto de carga</h3>
            <div className={estilos.esqueletoTarjeta}>
              <Esqueleto forma="circulo" />
              <Esqueleto lineas={3} />
            </div>
          </Tarjeta>
          <EstadoVacio nivelTitulo={3} titulo="Aún no hay solicitudes" descripcion="Cuando un cliente envíe una solicitud, aparecerá aquí para cotizarla." />
        </div>
      </div>
    </Seccion>
  );
}

function ResumenPeriodo({ pedidos, monto }) {
  return (
    <p className={estilos.resumenPeriodo}>
      <span className="cifras">{pedidos}</span> pedidos por <strong className="cifras">{pesos.format(monto)}</strong>{' '}
      <Insignia tono="neutro">Datos de ejemplo</Insignia>
    </p>
  );
}

function SeccionDialogo() {
  const [abierto, setAbierto] = useState(false);
  const [acepta, setAcepta] = useState(false);
  const [intento, setIntento] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [enviado, setEnviado] = useState(false);
  const refPrimerCampo = useRef(null);

  function cerrar() {
    setAbierto(false); setIntento(false); setEnviando(false);
  }

  function enviar(evento) {
    evento.preventDefault();
    setIntento(true);
    if (!acepta) return;
    setEnviando(true);
    setTimeout(() => { cerrar(); setEnviado(true); setAcepta(false); }, 1200);
  }

  return (
    <Seccion id="dialogo" titulo="Diálogo" descripcion="Sobre el diálogo nativo del navegador: atrapa el foco, se cierra con Esc y devuelve el foco al botón que lo abrió. Cancelar y aceptar son igual de visibles.">
      <div className={estilos.pila}>
        <div className={estilos.grupo}>
          <Boton onClick={() => { setEnviado(false); setAbierto(true); }}>Solicitar este trabajo</Boton>
        </div>
        {enviado && <Aviso tono="exito" titulo="Solicitud enviada (simulada)">En la fase 7 se guarda de verdad.</Aviso>}
      </div>
      <Dialogo
        abierto={abierto}
        alCerrar={cerrar}
        titulo="Nueva solicitud"
        descripcion="Cuéntanos qué necesitas y te enviaremos una cotización."
        enfoqueInicial={refPrimerCampo}
        pie={(
          <>
            <Boton variante="secundario" onClick={cerrar}>Cancelar</Boton>
            <Boton type="submit" form="formulario-solicitud" cargando={enviando} textoCargando="Enviando…" iconoInicio={Send}>
              Enviar solicitud
            </Boton>
          </>
        )}
      >
        <form id="formulario-solicitud" className={estilos.formularioDialogo} onSubmit={enviar} noValidate>
          <Campo ref={refPrimerCampo} etiqueta="Tipo de letrero" como="select" defaultValue="Letrero luminoso">
            <option>Letrero luminoso</option>
            <option>Rotulación de vehículo</option>
            <option>Letras corpóreas</option>
          </Campo>
          <Campo etiqueta="Medidas aproximadas" placeholder="Ej.: 200 × 60 cm" />
          <Campo etiqueta="Detalle" como="textarea" placeholder="Colores, textos y dónde se instala." />
          <Casilla
            etiqueta="Acepto la política de privacidad"
            checked={acepta}
            onChange={(e) => setAcepta(e.target.checked)}
            error={intento && !acepta ? 'Debes aceptar la política de privacidad para enviar la solicitud.' : undefined}
          />
        </form>
      </Dialogo>
    </Seccion>
  );
}
