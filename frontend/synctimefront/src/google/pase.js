// El «pase» del registro con Google (fase 14B): cuando el correo de Google es nuevo, la API no crea nada y entrega un pase
// firmado que vence a los 10 minutos y se usa una sola vez. Vive SOLO en la memoria de esta pestaña: nunca en la URL, ni en
// localStorage, sessionStorage o el estado de navegación (que el navegador guarda y sobrevive a una recarga), ni en los
// registros. Si se recarga la página, se pierde y la persona vuelve a pulsar el botón de Google (falla del lado seguro).
let actual = null;

export const paseGoogle = {
  /** datos: { pase, venceEn, correo, nombre, apellido } tal como los entrega POST /auth/google. */
  guardar(datos) {
    actual = {
      pase: String(datos.pase ?? ''),
      venceEn: datos.venceEn ?? null,
      correo: String(datos.correo ?? ''),
      nombre: String(datos.nombre ?? ''),
      apellido: String(datos.apellido ?? ''),
    };
  },
  /** El pase guardado, o null si no hay, está vacío o ya venció (en ese caso se olvida). */
  obtener() {
    if (!actual?.pase) return null;
    const vence = actual.venceEn ? Date.parse(actual.venceEn) : NaN;
    if (Number.isFinite(vence) && vence <= Date.now()) {
      actual = null;
      return null;
    }
    return actual;
  },
  limpiar() {
    actual = null;
  },
};
