// RUT chileno con dígito verificador (RNFO06). La misma regla que valida el backend (Negocio/Validacion.cs).
function limpiar(rut) {
  return String(rut ?? '').replace(/[.\s-]/g, '').toUpperCase();
}

function digitoVerificador(cuerpo) {
  let suma = 0;
  let multiplicador = 2;
  for (let i = cuerpo.length - 1; i >= 0; i -= 1) {
    suma += Number(cuerpo[i]) * multiplicador;
    multiplicador = multiplicador === 7 ? 2 : multiplicador + 1;
  }
  const resto = 11 - (suma % 11);
  return resto === 11 ? '0' : resto === 10 ? 'K' : String(resto);
}

export function validarRut(rut) {
  const limpio = limpiar(rut);
  if (!/^\d{7,8}[\dK]$/.test(limpio)) return false;
  return digitoVerificador(limpio.slice(0, -1)) === limpio.slice(-1);
}

/** 123456785 -> 12.345.678-5. Si no tiene forma de RUT, se devuelve tal cual. */
export function formatearRut(rut) {
  const limpio = limpiar(rut);
  if (!/^\d{1,8}[\dK]$/.test(limpio)) return String(rut ?? '');
  const cuerpo = limpio.slice(0, -1).replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  return `${cuerpo}-${limpio.slice(-1)}`;
}
