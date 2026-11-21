const ALTERNATIVA = 'Ingresa con tu correo y contraseña, que siguen disponibles.';

export const MENSAJE_CUENTA_EXISTENTE = 'Ya tienes una cuenta con este correo. Ingresa con tu contraseña y vincula Google desde Mi cuenta.';

/**
 * Lo que se le dice a la persona cuando algo falla con Google (ingreso, registro, vincular o desvincular).
 * Devuelve { tono, titulo, texto? } para un Aviso. Nunca incluye el token, el nonce ni el pase: solo el mensaje de la API,
 * que ya viene redactado para personas.
 */
export function avisoDeErrorGoogle(error) {
  switch (error?.codigo) {
    case 'cuenta_existente':
      return { tono: 'aviso', titulo: 'Ya tienes una cuenta con este correo.', texto: 'Ingresa con tu contraseña y vincula Google desde Mi cuenta.' };
    case 'cuenta_personal_existente':
      // Fase 14C: el correo es de un empleado que aún no vinculó su Google.
      return { tono: 'aviso', titulo: 'Este correo es de una cuenta del personal.', texto: 'Ingresa con tu contraseña y vincula Google desde Mi perfil.' };
    case 'sin_cuenta_personal':
      // Fase 14C: en el acceso del personal, un Google que no es de nadie del equipo no crea una cuenta de cliente.
      return {
        tono: 'aviso',
        titulo: 'Este Google no está vinculado a una cuenta del personal.',
        texto: 'Ingresa con tu contraseña y vincula Google desde Mi perfil. Si eres cliente, ingresa desde la página de clientes.',
      };
    case 'sin_acceso_web':
      // El mensaje de la API explica que pida en el local que activen su acceso.
      return { tono: 'aviso', titulo: error.message };
    case 'google_script':
      return { tono: 'aviso', titulo: 'No pudimos cargar Google', texto: `${error.message} ${ALTERNATIVA}` };
    case 'google_token_invalido':
      return { tono: 'peligro', titulo: 'No pudimos verificar tu cuenta de Google', texto: `Inténtalo de nuevo. ${ALTERNATIVA}` };
    case 'google_no_disponible':
      return { tono: 'aviso', titulo: 'Google no responde en este momento', texto: `Inténtalo de nuevo más tarde. ${ALTERNATIVA}` };
    case 'google_no_configurado':
      return { tono: 'aviso', titulo: 'El ingreso con Google no está disponible por ahora', texto: ALTERNATIVA };
    default:
      return { tono: 'peligro', titulo: error?.message || 'No pudimos continuar con Google. Inténtalo de nuevo.' };
  }
}
