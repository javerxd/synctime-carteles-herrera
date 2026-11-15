import { useState } from 'react';

/** Estado de envío de un diálogo que se reinicia cada vez que se abre. */
export function useEnvioDialogo(abierto) {
  const [estado, setEstado] = useState({ enviando: false, falla: null });
  const [abiertoAntes, setAbiertoAntes] = useState(abierto);
  if (abierto !== abiertoAntes) {
    setAbiertoAntes(abierto);
    if (abierto) setEstado({ enviando: false, falla: null });
  }
  /** Ejecuta la acción; devuelve null si resultó o el error (que queda en `falla` para mostrarlo). */
  async function ejecutar(accion) {
    setEstado({ enviando: true, falla: null });
    try {
      await accion();
      setEstado({ enviando: false, falla: null });
      return null;
    } catch (falla) {
      setEstado({ enviando: false, falla });
      return falla ?? new Error('Error desconocido');
    }
  }
  /** Quita el aviso general (por ejemplo, cuando el error ya se muestra junto a su campo). */
  const olvidarFalla = () => setEstado({ enviando: false, falla: null });
  return { ...estado, ejecutar, olvidarFalla };
}
