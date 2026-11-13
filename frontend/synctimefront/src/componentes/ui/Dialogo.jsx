import { useEffect, useId, useRef, useState } from 'react';
import { X } from 'lucide-react';
import { Boton } from './Boton';
import { unirClases } from './clases';
import estilos from './Dialogo.module.css';

// Lo que dura la animación de salida (tokens: --duracion-salida), con margen.
const ESPERA_SALIDA_MS = 220;

/**
 * Diálogo modal sobre el <dialog> nativo: el navegador atrapa el foco, lo pone en la capa superior
 * y deja inerte el resto de la página.
 *
 * Contrato: el estado lo controla quien lo usa. Esc, el botón Cerrar y un clic en el telón llaman a
 * `alCerrar`, que debe poner `abierto` en false (para confirmar antes de cerrar, abrir otro diálogo
 * desde `alCerrar`). Al cerrarse, el foco vuelve al elemento que lo abrió.
 * - `enfoqueInicial`: ref del control que recibe el foco al abrir (por ejemplo, el primer campo de
 *   un formulario, o «Cancelar» en una confirmación destructiva). Sin él, el navegador enfoca el
 *   primer control, que es «Cerrar».
 * - El contenido se monta al abrir y se desmonta al terminar la salida.
 */
export function Dialogo({
  abierto,
  alCerrar,
  titulo,
  descripcion,
  pie,
  cerrarAlPulsarFondo = true,
  enfoqueInicial,
  ancho,
  className,
  children,
}) {
  const refDialogo = useRef(null);
  const refOrigen = useRef(null);
  const refInicioGesto = useRef(null);
  const idTitulo = useId();
  const idDescripcion = useId();

  // Estado derivado durante el render: el contenido sigue montado mientras dura la salida.
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  const [saliendo, setSaliendo] = useState(false);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    setSaliendo(!abierto);
  }

  useEffect(() => {
    if (!saliendo) return undefined;
    const temporizador = setTimeout(() => setSaliendo(false), ESPERA_SALIDA_MS);
    return () => clearTimeout(temporizador);
  }, [saliendo]);

  // Sincroniza el <dialog> con `abierto` en cada render, por si el navegador lo cerró por su cuenta.
  useEffect(() => {
    const dialogo = refDialogo.current;
    if (!dialogo) return;
    if (abierto && !dialogo.open) {
      refOrigen.current = document.activeElement;
      dialogo.showModal();
      enfoqueInicial?.current?.focus();
    } else if (!abierto && dialogo.open) {
      dialogo.close();
    }
  });

  // Al cerrar, el foco vuelve a quien abrió el diálogo; si ya no existe, al contenido principal.
  useEffect(() => {
    if (abierto || !refOrigen.current) return;
    const origen = refOrigen.current;
    refOrigen.current = null;
    const destino = origen.isConnected ? origen : document.querySelector('main[tabindex]');
    destino?.focus?.();
  }, [abierto]);

  // Esc dispara "cancel": se evita el cierre nativo para que el estado siga en manos de React.
  function alCancelar(evento) {
    evento.preventDefault();
    alCerrar?.();
  }

  // Un clic en el telón cierra, salvo que el gesto haya empezado dentro (al seleccionar texto).
  function alPulsar(evento) {
    const empezoEnElTelon = refInicioGesto.current === evento.currentTarget;
    refInicioGesto.current = null;
    if (cerrarAlPulsarFondo && empezoEnElTelon && evento.target === evento.currentTarget) alCerrar?.();
  }

  return (
    <dialog
      ref={refDialogo}
      aria-labelledby={idTitulo}
      aria-describedby={descripcion ? idDescripcion : undefined}
      onCancel={alCancelar}
      onClose={() => abierto && alCerrar?.()}
      onPointerDown={(evento) => { refInicioGesto.current = evento.target; }}
      onClick={alPulsar}
      className={unirClases(estilos.dialogo, className)}
      style={ancho ? { '--_ancho': ancho } : undefined}
    >
      {(abierto || saliendo) && (
        <div className={estilos.caja}>
          <header className={estilos.cabecera}>
            <div className={estilos.encabezado}>
              <h2 id={idTitulo} className={estilos.titulo}>{titulo}</h2>
              {descripcion && <p id={idDescripcion} className={estilos.descripcion}>{descripcion}</p>}
            </div>
            <Boton variante="fantasma" tamano="compacto" aria-label="Cerrar" onClick={() => alCerrar?.()} className={estilos.cerrar}>
              <X size={20} strokeWidth={1.75} aria-hidden="true" />
            </Boton>
          </header>
          <div className={estilos.contenido}>{children}</div>
          {pie && <footer className={estilos.pie}>{pie}</footer>}
        </div>
      )}
    </dialog>
  );
}
