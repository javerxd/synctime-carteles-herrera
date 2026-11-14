import { useEffect, useRef, useState } from 'react';

// Primera letra en minúscula: la API nombra los campos igual que el formulario (correo, passwordActual…).
const nombreCampo = (clave) => clave.charAt(0).toLowerCase() + clave.slice(1);

/**
 * Estado de un formulario con el patrón del de contacto: valida al salir de cada campo (y desde entonces
 * mientras se escribe), al enviar con errores lleva el foco al primero y muestra los errores de la API
 * junto a su campo. `orden` es el orden visual de los campos.
 */
export function useFormulario({ inicial, validar, orden }) {
  const [datos, setDatos] = useState(inicial);
  const [tocados, setTocados] = useState({});
  const [erroresApi, setErroresApi] = useState({});
  const refFormulario = useRef(null);
  // Campo que debe recibir el foco cuando vuelva a estar habilitado (tras la respuesta de la API).
  const focoPendiente = useRef(null);

  useEffect(() => {
    const campo = focoPendiente.current;
    if (!campo) return;
    const control = refFormulario.current?.querySelector(`[name="${campo}"]`);
    if (control && !control.matches(':disabled')) {
      control.focus();
      focoPendiente.current = null;
    }
  });

  const errores = { ...validar(datos), ...erroresApi };
  const visibles = Object.fromEntries(Object.entries(errores).filter(([campo]) => tocados[campo]));

  const enfocar = (campo) => refFormulario.current?.querySelector(`[name="${campo}"]`)?.focus();

  function cambiar(campo, valor) {
    setDatos((d) => ({ ...d, [campo]: valor }));
    setErroresApi(({ [campo]: _descartado, ...resto }) => resto);
  }

  const tocar = (campo) => setTocados((t) => ({ ...t, [campo]: true }));

  /** Al enviar: muestra todos los errores y enfoca el primero. true si se puede enviar. */
  function comprobar() {
    const encontrados = validar(datos);
    setTocados(Object.fromEntries(orden.map((c) => [c, true])));
    const primero = orden.find((c) => encontrados[c]);
    if (primero) enfocar(primero);
    return !primero;
  }

  /**
   * Pone los errores por campo de la API (`{ campo: [mensaje] }`) junto a su campo y enfoca el primero
   * en cuanto el formulario vuelva a habilitarse. true si había alguno de este formulario.
   */
  function aplicarErroresApi(error) {
    const porCampo = Object.fromEntries(Object.entries(error?.errores ?? {})
      .map(([c, m]) => [nombreCampo(c), Array.isArray(m) ? m[0] : String(m)])
      .filter(([c]) => orden.includes(c)));
    const primero = orden.find((c) => porCampo[c]);
    if (!primero) return false;
    setErroresApi(porCampo);
    setTocados((t) => ({ ...t, ...Object.fromEntries(Object.keys(porCampo).map((c) => [c, true])) }));
    focoPendiente.current = primero;
    return true;
  }

  function reiniciar(nuevo = inicial) {
    setDatos(nuevo);
    setTocados({});
    setErroresApi({});
  }

  /** Propiedades para un Campo de texto. */
  const propiedades = (campo) => ({
    name: campo,
    value: datos[campo],
    error: visibles[campo],
    onChange: (e) => cambiar(campo, e.target.value),
    onBlur: () => tocar(campo),
  });

  return { datos, errores, visibles, refFormulario, cambiar, tocar, comprobar, aplicarErroresApi, reiniciar, enfocar, propiedades };
}
