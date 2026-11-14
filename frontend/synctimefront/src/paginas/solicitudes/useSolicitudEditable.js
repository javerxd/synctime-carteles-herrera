import { useEffect, useRef, useState } from 'react';
import {
  detallesParaApi, erroresDesdeApi, itemVacio, ordenCampos, validarSolicitud,
} from './reglasSolicitud';

const CAMPO_ITEM = /^detalles\.(\d+)\.(\w+)$/;

/** Lee el texto guardado en esta pestaña (letreros y descripción), o null. */
function leerGuardado(clave) {
  if (!clave) return null;
  try {
    const guardado = JSON.parse(sessionStorage.getItem(clave) ?? 'null');
    if (!guardado || !Array.isArray(guardado.items) || !guardado.items.length) return null;
    return {
      // Cada letrero recibe una clave nueva para React; los valores vuelven tal cual.
      items: guardado.items.slice(0, 10).map((item) => ({
        ...itemVacio(item.idProducto ?? ''),
        ancho: String(item.ancho ?? ''), alto: String(item.alto ?? ''), cantidad: String(item.cantidad ?? '1'), descripcion: String(item.descripcion ?? ''),
      })),
      descripcionGeneral: String(guardado.descripcionGeneral ?? ''),
    };
  } catch {
    return null;
  }
}

/**
 * Estado del formulario de una solicitud (letreros + descripción), con el patrón de los formularios de
 * la cuenta: valida al salir de cada campo, al enviar muestra todo y enfoca el primer error, y pone los
 * errores de la API junto a su campo. Lo comparten el cliente, el registro presencial y la edición.
 *
 * - Los campos tocados se recuerdan por letrero (no por posición): quitar el letrero 2 no le pasa al
 *   3 lo que se tocó en el 2, y un letrero recién agregado no muestra errores hasta tocarlo o enviar.
 * - `guardarEn`: clave de sessionStorage donde se guarda el texto mientras se escribe (sin adjuntos);
 *   al volver se restaura. `olvidar()` lo borra (tras enviar con éxito).
 */
export function useSolicitudEditable({ productoInicial = '', descripcionInicial = '', itemsIniciales = null, guardarEn = null } = {}) {
  const [inicio] = useState(() => (productoInicial ? null : leerGuardado(guardarEn)));
  const [items, setItems] = useState(() => inicio?.items ?? (itemsIniciales?.length ? itemsIniciales : [itemVacio(productoInicial)]));
  const [descripcionGeneral, setDescripcionGeneral] = useState(() => inicio?.descripcionGeneral ?? descripcionInicial);
  const [tocados, setTocados] = useState({});
  const [intento, setIntento] = useState(false);
  // Letreros agregados después del último intento de envío: sus errores esperan a que se toquen.
  const [nuevos, setNuevos] = useState(() => new Set());
  const [erroresApi, setErroresApi] = useState({});
  const refFormulario = useRef(null);
  const focoPendiente = useRef(null);
  const restaurado = Boolean(inicio);

  // El campo con error de la API recibe el foco en cuanto el formulario vuelve a habilitarse.
  useEffect(() => {
    const campo = focoPendiente.current;
    if (!campo) return;
    const control = refFormulario.current?.querySelector(`[name="${campo}"]`);
    if (control && !control.matches(':disabled')) {
      control.focus();
      focoPendiente.current = null;
    }
  });

  // Guarda el texto en la pestaña mientras se escribe.
  useEffect(() => {
    if (!guardarEn) return;
    try {
      sessionStorage.setItem(guardarEn, JSON.stringify({
        items: items.map(({ idProducto, ancho, alto, cantidad, descripcion }) => ({ idProducto, ancho, alto, cantidad, descripcion })),
        descripcionGeneral,
      }));
    } catch {
      // Sin almacenamiento (modo privado estricto): el formulario funciona igual.
    }
  }, [guardarEn, items, descripcionGeneral]);

  /** «detalles.1.ancho» → «i7.ancho» (la clave estable del letrero). */
  function claveTocado(campo) {
    const m = campo.match(CAMPO_ITEM);
    if (!m) return campo;
    const item = items[Number(m[1])];
    return item ? `${item.clave}.${m[2]}` : campo;
  }

  function esVisible(campo) {
    if (erroresApi[campo] || tocados[claveTocado(campo)]) return true;
    if (!intento) return false;
    const m = campo.match(CAMPO_ITEM);
    return !(m && nuevos.has(items[Number(m[1])]?.clave));
  }

  const errores = { ...validarSolicitud({ items, descripcionGeneral }), ...erroresApi };
  const visibles = Object.fromEntries(Object.entries(errores).filter(([campo]) => esVisible(campo)));

  const limpiarErrorApi = (campo) => setErroresApi((actuales) => {
    if (!(campo in actuales)) return actuales;
    const { [campo]: _descartado, ...resto } = actuales;
    return resto;
  });

  function cambiarItem(indice, campo, valor) {
    setItems((lista) => lista.map((item, i) => (i === indice ? { ...item, [campo]: valor } : item)));
    limpiarErrorApi(`detalles.${indice}.${campo}`);
  }

  function agregarItem() {
    const nuevo = itemVacio();
    setItems((lista) => [...lista, nuevo]);
    setNuevos((actuales) => new Set(actuales).add(nuevo.clave));
  }

  function quitarItem(indice) {
    setItems((lista) => lista.filter((_, i) => i !== indice));
    // Los números de los letreros cambian: los errores de la API de los letreros ya no calzan.
    setErroresApi((actuales) => Object.fromEntries(Object.entries(actuales).filter(([c]) => !c.startsWith('detalles.'))));
  }

  const tocar = (campo) => {
    const clave = claveTocado(campo);
    setTocados((t) => (t[clave] ? t : { ...t, [clave]: true }));
  };

  const enfocar = (campo) => refFormulario.current?.querySelector(`[name="${campo}"]`)?.focus();

  /** Al enviar: muestra todos los errores y enfoca el primero. true si se puede enviar. */
  function comprobar() {
    setIntento(true);
    setNuevos(new Set());
    const encontrados = { ...validarSolicitud({ items, descripcionGeneral }), ...erroresApi };
    const primero = ordenCampos(items).find((c) => encontrados[c]);
    if (primero) enfocar(primero);
    return !primero;
  }

  /** Errores por campo de la API junto a su campo. true si alguno era de este formulario. */
  function aplicarErroresApi(error) {
    const porCampo = erroresDesdeApi(error?.errores);
    const primero = ordenCampos(items).find((c) => porCampo[c]);
    if (!primero) return false;
    setErroresApi(porCampo);
    focoPendiente.current = primero;
    return true;
  }

  function reiniciar(nuevosItems = [itemVacio()], nuevaDescripcion = '') {
    setItems(nuevosItems);
    setDescripcionGeneral(nuevaDescripcion);
    setTocados({});
    setIntento(false);
    setNuevos(new Set());
    setErroresApi({});
  }

  /** Borra el texto guardado en la pestaña (tras enviar con éxito). */
  function olvidar() {
    if (!guardarEn) return;
    try {
      sessionStorage.removeItem(guardarEn);
    } catch {
      // Sin almacenamiento: no había nada guardado.
    }
  }

  const paraApi = () => ({ descripcionGeneral: descripcionGeneral.trim(), detalles: detallesParaApi(items) });

  const propiedades = (campo) => ({
    name: campo,
    value: descripcionGeneral,
    error: visibles[campo],
    onChange: (e) => {
      setDescripcionGeneral(e.target.value);
      limpiarErrorApi(campo);
    },
    onBlur: () => tocar(campo),
  });

  return {
    items, descripcionGeneral, visibles, refFormulario, restaurado,
    cambiarItem, agregarItem, quitarItem, tocar, comprobar, aplicarErroresApi, limpiarErrorApi, reiniciar, olvidar, paraApi, propiedades,
  };
}
