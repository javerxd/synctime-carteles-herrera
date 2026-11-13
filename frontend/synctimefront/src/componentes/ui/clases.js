// Une nombres de clase e ignora los vacíos: unirClases('a', cond && 'b') -> 'a' o 'a b'.
export function unirClases(...clases) {
  return clases.filter(Boolean).join(' ');
}
