import axios from 'axios';

/**
 * Cliente HTTP centralizado.
 * Todas las peticiones a la API pasan por esta instancia,
 * lo que permite adjuntar el JWT y normalizar errores en un solo lugar.
 *
 * ─── Contratos / DTOs de la API ───
 *
 * @typedef {Object} LoginRequest
 * @property {string} email
 * @property {string} password
 *
 * @typedef {Object} LoginResponse
 * @property {string} token  — JWT Bearer
 * @property {{ idUsuario: number, nombre: string, email: string, rol: string }} usuario
 *
 * @typedef {Object} SolicitudRequest
 * @property {number}  idCliente
 * @property {number|null} idProducto
 * @property {string|null} tamano
 * @property {string|null} materialSolicitado
 * @property {number}  cantidad
 * @property {string}  telefonoContacto
 * @property {string|null} notasWhatsapp
 *
 * @typedef {Object} SolicitudResponse
 * @property {number}  idSolicitud
 * @property {number}  idCliente
 * @property {number|null} idProducto
 * @property {string|null} tamano
 * @property {string|null} materialSolicitado
 * @property {number}  cantidad
 * @property {string}  telefonoContacto
 * @property {string}  fechaSolicitud  — ISO 8601
 * @property {string}  estado
 * @property {string|null} fechaDecision — ISO 8601 o null
 * @property {string|null} notasWhatsapp
 *
 * @typedef {Object} ClienteResponse
 * @property {number}  idCliente
 * @property {string}  nombre
 * @property {string}  telefono
 * @property {string|null} email
 * @property {string|null} direccion
 * @property {string}  canalPreferido
 *
 * @typedef {Object} ProductoResponse
 * @property {number}  idProducto
 * @property {string}  nombre
 * @property {string}  tipo
 * @property {string|null} descripcion
 * @property {string}  unidadMedida
 * @property {number}  precioBase
 */

/*
 * La URL base se determina así:
 * 1. Si VITE_API_URL está definido (ej: "https://localhost:7112"),
 *    se concatena con "/api/v1" para apuntar directo al backend.
 * 2. Si no está definido, se usa "/api/v1" y el proxy de Vite
 *    lo reenvía al backend configurado en vite.config.js.
 */
const envBaseUrl = import.meta.env.VITE_API_URL;
const baseURL = envBaseUrl ? `${envBaseUrl}/api/v1` : '/api/v1';

const httpClient = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 15000,
});

/* ───── Request interceptor: adjunta Bearer token ───── */
httpClient.interceptors.request.use((config) => {
  const token = sessionStorage.getItem('synctime_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

/* ───── Response interceptor: normaliza errores ───── */
httpClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (!error.response) {
      return Promise.reject({
        status: 0,
        message: 'No se pudo conectar con el servidor. Verifique que la API esté en ejecución.',
        errors: {},
      });
    }

    const { status, data } = error.response;

    // 401 — sesión inválida o expirada
    if (status === 401) {
      sessionStorage.removeItem('synctime_token');
      sessionStorage.removeItem('synctime_user');
      // Solo redirigir si no estamos ya en login
      if (!window.location.pathname.includes('/login')) {
        window.location.href = '/login';
      }
      return Promise.reject({
        status: 401,
        message: 'Sesión expirada o credenciales inválidas.',
        errors: {},
      });
    }

    // 403 — sin permisos para la operación
    if (status === 403) {
      return Promise.reject({
        status: 403,
        message: 'No tiene permisos para realizar esta acción.',
        errors: {},
      });
    }

    // 429 — rate limit
    if (status === 429) {
      return Promise.reject({
        status: 429,
        message: 'Ha excedido el límite de peticiones. Espere un momento e intente de nuevo.',
        errors: {},
      });
    }

    // 409 — conflicto (duplicado)
    if (status === 409) {
      return Promise.reject({
        status: 409,
        message: data?.message || 'El recurso ya existe o hay un conflicto.',
        errors: {},
      });
    }

    // 400 / 422 — validación (ProblemDetails)
    if (status === 400 || status === 422) {
      const fieldErrors = data?.errors || {};
      const firstError = Object.values(fieldErrors)[0];
      return Promise.reject({
        status,
        message: (firstError && firstError[0]) || data?.title || 'Datos inválidos.',
        errors: fieldErrors,
      });
    }

    // 404 — no encontrado
    if (status === 404) {
      return Promise.reject({
        status: 404,
        message: data?.message || 'El recurso solicitado no existe.',
        errors: {},
      });
    }

    // Cualquier otro error
    return Promise.reject({
      status,
      message: data?.message || data?.title || 'Error inesperado del servidor.',
      errors: data?.errors || {},
    });
  }
);

export default httpClient;
