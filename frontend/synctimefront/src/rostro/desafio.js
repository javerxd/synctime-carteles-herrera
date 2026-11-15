import { ErrorFacial } from './motorFacial';

const GIROS = ['izquierda', 'derecha'];

/**
 * Los pasos del flujo según el desafío del servidor (POST /auth/login-facial/desafio): frente y luego los dos
 * giros en el orden que eligió el servidor. Si la respuesta no trae un desafío y los dos lados una vez cada uno,
 * no se usa (ErrorFacial 'desafio_invalido').
 */
export function pasosDelDesafio(respuesta) {
  const orden = respuesta?.orden;
  const valido = typeof respuesta?.desafio === 'string' && respuesta.desafio.length > 0 && Array.isArray(orden)
    && orden.length === 2 && GIROS.every((lado) => orden.includes(lado));
  if (!valido) throw new ErrorFacial('desafio_invalido');
  return ['frente', ...orden];
}
