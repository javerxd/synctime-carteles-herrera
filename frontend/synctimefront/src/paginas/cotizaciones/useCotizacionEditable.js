import { useEffect, useRef, useState } from 'react';
import {
  campoLinea, erroresCotizacionDesdeApi, lineaVacia, ordenCamposCotizacion, validarCotizacion,
} from './reglasCotizacion';

const CAMPO_LINEA = /^materiales\.(\d+)\.(\w+)$/;
const VACIO = { lineas: [], costoManoObra: '', otrosCostos: '', observaciones: '' };

/**
 * Estado del editor de cotización (materiales, mano de obra, otros costos y observaciones), con el patrón
 * de los formularios de la solicitud: valida al salir de cada campo, al enviar muestra todo y enfoca el
 * primer error, y pone los errores de la API junto a su campo. Lo comparten el editor y el pedido directo.
 * Los campos tocados se recuerdan por línea (no por posición), así quitar una línea no mueve los errores.
 */
export function useCotizacionEditable({ inicial = VACIO, materiales = [] } = {}) {
  const [lineas, setLineas] = useState(() => inicial.lineas ?? []);
  const [montos, setMontos] = useState(() => ({
    costoManoObra: inicial.costoManoObra ?? '', otrosCostos: inicial.otrosCostos ?? '', observaciones: inicial.observaciones ?? '',
  }));
  const [tocados, setTocados] = useState({});
  const [intento, setIntento] = useState(false);
  const [erroresApi, setErroresApi] = useState({});
  const refFormulario = useRef(null);
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

  // Una línea que ya traía un material (al editar) lo conserva aunque luego se haya desactivado: la API decide.
  const [idsIniciales] = useState(() => (inicial.lineas ?? []).map((l) => Number(l.idMaterial)));
  const permitidos = new Set([...materiales.filter((m) => m.activo !== false).map((m) => Number(m.idMaterial)), ...idsIniciales]);
  const datos = { lineas, ...montos };
  const errores = { ...validarCotizacion(datos, materiales.length ? { materialesActivos: permitidos } : {}), ...erroresApi };

  function claveTocado(campo) {
    const m = campo.match(CAMPO_LINEA);
    if (!m) return campo;
    const linea = lineas[Number(m[1])];
    return linea ? `${linea.clave}.${m[2]}` : campo;
  }

  const visibles = Object.fromEntries(Object.entries(errores).filter(([campo]) => intento || erroresApi[campo] || tocados[claveTocado(campo)]));

  const limpiarErrorApi = (campo) => setErroresApi((actuales) => {
    if (!(campo in actuales)) return actuales;
    const { [campo]: _descartado, ...resto } = actuales;
    return resto;
  });

  function cambiarLinea(indice, campo, valor) {
    setLineas((lista) => lista.map((linea, i) => (i === indice ? { ...linea, [campo]: valor } : linea)));
    limpiarErrorApi(campoLinea(indice, campo));
  }

  /** Elegir el material sugiere su costo del catálogo, salvo que ya se haya escrito otro a mano. */
  function elegirMaterial(indice, idMaterial) {
    const material = materiales.find((m) => String(m.idMaterial) === String(idMaterial));
    const sugerido = material ? String(Math.round(Number(material.costoUnitarioAprox ?? 0))) : null;
    setLineas((lista) => lista.map((linea, i) => {
      if (i !== indice) return linea;
      const escritoAMano = linea.costoUnitario !== '' && linea.costoUnitario !== linea.sugerido;
      return { ...linea, idMaterial, sugerido, costoUnitario: escritoAMano || sugerido === null ? linea.costoUnitario : sugerido };
    }));
    limpiarErrorApi(campoLinea(indice, 'idMaterial'));
  }

  function agregarLinea() {
    setLineas((lista) => [...lista, lineaVacia()]);
  }

  function quitarLinea(indice) {
    setLineas((lista) => lista.filter((_, i) => i !== indice));
    setErroresApi((actuales) => Object.fromEntries(Object.entries(actuales).filter(([c]) => !c.startsWith('materiales.'))));
  }

  function cambiarMonto(campo, valor) {
    setMontos((m) => ({ ...m, [campo]: valor }));
    limpiarErrorApi(campo);
  }

  const tocar = (campo) => {
    const clave = claveTocado(campo);
    setTocados((t) => (t[clave] ? t : { ...t, [clave]: true }));
  };

  /** Al enviar: muestra todos los errores y enfoca el primero. true si se puede enviar. */
  function comprobar() {
    setIntento(true);
    const primero = ordenCamposCotizacion(lineas).find((c) => errores[c]);
    if (primero) refFormulario.current?.querySelector(`[name="${primero}"]`)?.focus();
    return !primero;
  }

  /** Errores por campo de la API junto a su campo. true si alguno era de este formulario. */
  function aplicarErroresApi(error) {
    const porCampo = erroresCotizacionDesdeApi(error?.errores);
    const primero = ordenCamposCotizacion(lineas).find((c) => porCampo[c]);
    if (!primero) return false;
    setErroresApi(porCampo);
    focoPendiente.current = primero;
    return true;
  }

  const propiedadesMonto = (campo) => ({
    name: campo,
    value: montos[campo],
    error: visibles[campo],
    onChange: (e) => cambiarMonto(campo, e.target.value),
    onBlur: () => tocar(campo),
  });

  return {
    datos, lineas, visibles, refFormulario,
    cambiarLinea, elegirMaterial, agregarLinea, quitarLinea, cambiarMonto, tocar, comprobar, aplicarErroresApi, propiedadesMonto,
  };
}
