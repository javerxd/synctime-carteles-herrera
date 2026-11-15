import { useFormulario } from '../cuenta/useFormulario';
import {
  ABONO_VACIO, costoInstalacion, DATOS_VACIOS, ORDEN_ABONO, ORDEN_DATOS_PEDIDO, validarAbono, validarDatosPedido,
} from './reglasPedido';

/** Los errores por campo de la API, separados entre los datos del pedido y el abono inicial («abonoInicial.monto»). */
export function separarErrores(errores = {}) {
  const datos = {};
  const abono = {};
  for (const [clave, mensaje] of Object.entries(errores ?? {})) {
    const m = clave.match(/^abonoInicial\.(\w+)$/i);
    if (m) abono[m[1].charAt(0).toLowerCase() + m[1].slice(1)] = mensaje;
    else datos[clave] = mensaje;
  }
  return { datos, abono };
}

/**
 * Los datos de un pedido nuevo (E08) y su abono inicial opcional, en dos formularios. El tope del abono es el
 * monto final: el trabajo (`trabajo`, el total de la cotización) más la instalación. `responsable` llega
 * elegido con quien registra. `aplicarError` pone junto a su campo los errores de la API, incluido el 409
 * abono_excede_saldo.
 */
export function usePedidoNuevo({ hoy, trabajo, responsable }) {
  const datos = useFormulario({
    inicial: { ...DATOS_VACIOS, idEmpleadoResponsable: responsable ? String(responsable) : '' },
    validar: (d) => validarDatosPedido(d, { hoy }),
    orden: ORDEN_DATOS_PEDIDO,
  });
  const final = (Number.isFinite(trabajo) ? trabajo : 0) + costoInstalacion(datos.datos);
  const abono = useFormulario({
    inicial: ABONO_VACIO,
    validar: (a) => validarAbono(a, { hoy, saldo: final, opcional: true }),
    orden: ORDEN_ABONO,
  });

  /** Al enviar: muestra todos los errores y deja el foco en el primero (los datos van antes que el abono). */
  function comprobar() {
    const abonoBien = abono.comprobar();
    const datosBien = datos.comprobar();
    return abonoBien && datosBien;
  }

  function aplicarError(falla) {
    if (falla?.codigo === 'abono_excede_saldo') {
      return abono.aplicarErroresApi({ errores: { monto: [falla.message] } });
    }
    if (falla?.estado !== 400 || !falla.errores) return false;
    const separados = separarErrores(falla.errores);
    const enDatos = datos.aplicarErroresApi({ errores: separados.datos });
    const enAbono = abono.aplicarErroresApi({ errores: separados.abono });
    return enDatos || enAbono;
  }

  return { datos, abono, final, comprobar, aplicarError };
}
