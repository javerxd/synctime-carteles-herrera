// Configuración del ingreso con Google (fase 14B, RNFP17). El ID de cliente OAuth no es un secreto (cualquiera lo ve en el
// navegador), pero tampoco vive en el repositorio: sale de VITE_GOOGLE_CLIENT_ID (frontend/synctimefront/.env.local, ignorado
// por git). Sin él, no se muestra ningún botón de Google ni se pide nada a Google.
// Se lee al usarlo (no al cargar el módulo): así una prueba puede definirlo o quitarlo.

/** El ID de cliente configurado, o '' si no hay. */
export const idClienteGoogle = () => String(import.meta.env.VITE_GOOGLE_CLIENT_ID ?? '').trim();

/** true si hay un ID de cliente: solo entonces se ofrece el ingreso con Google. */
export const googleDisponible = () => idClienteGoogle().length > 0;
