import { useEffect, useRef, useState } from 'react';

/**
 * Un 409 version_obsoleta al guardar (RNFP10): otra persona cambió el registro mientras se editaba. Lo escrito
 * se queda en el formulario; se relee la versión actual sin desmontarlo y se guarda su versión, para que
 * «Guardar sobre la versión actual» reintente con ella (If-Match nuevo) o «Descartar» cargue la actual.
 * - `releer()`: la versión actual (GET con su rowVersion).
 * - `version`: la que se debe enviar ahora (la vista al abrir o, tras el conflicto, la releída).
 * - `refAccion`: el botón que recibe el foco cuando la versión actual llega (el aviso ya se anuncia solo).
 */
export function useConflictoDeVersion({ releer, versionInicial }) {
  const [conflicto, setConflicto] = useState(null); // null | { estado: 'buscando' | 'listo' | 'error', actual }
  const [version, setVersion] = useState(versionInicial);
  const refAccion = useRef(null);
  const refReleer = useRef(releer);
  useEffect(() => {
    refReleer.current = releer;
  });

  useEffect(() => {
    if (conflicto?.estado === 'listo' || conflicto?.estado === 'error') refAccion.current?.focus();
  }, [conflicto]);

  async function detectar() {
    setConflicto({ estado: 'buscando', actual: null });
    try {
      const actual = await refReleer.current();
      setVersion(actual?.rowVersion ?? version);
      setConflicto({ estado: 'listo', actual });
    } catch {
      setConflicto({ estado: 'error', actual: null });
    }
  }

  return { conflicto, version, refAccion, detectar, olvidar: () => setConflicto(null) };
}
