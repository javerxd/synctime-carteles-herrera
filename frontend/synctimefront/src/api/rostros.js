import { api } from './cliente';

// Rostros registrados (S16) y calibración del umbral (fase 10). Contrato: docs/plan/fase-10-especificacion.md.
// Solo dueño y administrador. Nunca viajan imágenes: cada captura son 128 números y 68 puntos.

/** Lo que se envía de una captura: sin el paso ni nada más que agregue el navegador. */
export function comoCaptura({ descriptor, puntos, ancho, alto }) {
  return { descriptor, puntos, ancho, alto };
}

/** Las tres muestras del registro, por lado. */
export function comoMuestras({ frente, izquierda, derecha }) {
  return { frente: comoCaptura(frente), izquierda: comoCaptura(izquierda), derecha: comoCaptura(derecha) };
}

export const rostros = {
  /**
   * GET /rostros: empleados activos de gestión, cada uno { idEmpleado, nombre, rol, estado: 'activo' | 'debe_registrarse'
   * | 'sin_registro' | 'desactivado', fechaRegistro, fechaConsentimiento, registradoPor, fechaDesactivacion, historial }.
   */
  listar: ({ senal } = {}) => api.get('/rostros', { senal }),

  /**
   * PUT /rostros/{id}: gestión registra el rostro de OTRO empleado de gestión, con el titular presente: él escribe
   * su contraseña y acepta el consentimiento. 400 datos_invalidos si la contraseña no es la suya. No entrega token de equipo.
   */
  registrar: (idEmpleado, { passwordTitular, versionConsentimiento, muestras }) => api.put(`/rostros/${idEmpleado}`, {
    passwordTitular, consentimiento: true, versionConsentimiento, muestras: comoMuestras(muestras),
  }),

  /** DELETE /rostros/{id} (204): desactiva y borra las muestras; queda la constancia del consentimiento. */
  desactivar: (idEmpleado) => api.delete(`/rostros/${idEmpleado}`),

  calibracion: {
    /** GET /rostros/calibracion: { activa, umbralVigente, rangos, mediciones, resumen }; 404 si está apagada. */
    obtener: ({ senal } = {}) => api.get('/rostros/calibracion', { senal }),
    /** POST /rostros/calibracion (201): guarda solo las distancias y los giros del intento. */
    medir: ({ idEmpleado, etiqueta, desafio, capturas }) => api.post('/rostros/calibracion', {
      idEmpleado, etiqueta, desafio, capturas: capturas.map(comoCaptura),
    }),
    /** DELETE /rostros/calibracion (204): borra todas las mediciones. */
    borrar: () => api.delete('/rostros/calibracion'),
  },
};
