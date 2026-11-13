import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { Send } from 'lucide-react';
import { Boton } from '../componentes/ui';
import { ModalSolicitud } from '../paginas/publicas/ModalSolicitud';

const ContextoSolicitud = createContext(null);

/**
 * Abre el modal de solicitud desde cualquier parte del portal: la barra, la portada o una tarjeta
 * de la vitrina (que lo abre con ese trabajo elegido como referencia).
 */
export function ProveedorSolicitud({ children }) {
  const [estado, setEstado] = useState({ abierto: false, trabajo: null });
  const abrir = useCallback((trabajo = null) => setEstado({ abierto: true, trabajo }), []);
  const cerrar = useCallback(() => setEstado((e) => ({ ...e, abierto: false })), []);
  const valor = useMemo(() => ({ abrir }), [abrir]);

  return (
    <ContextoSolicitud.Provider value={valor}>
      {children}
      <ModalSolicitud abierto={estado.abierto} trabajo={estado.trabajo} alCerrar={cerrar} />
    </ContextoSolicitud.Provider>
  );
}

export function useSolicitud() {
  const contexto = useContext(ContextoSolicitud);
  if (!contexto) throw new Error('useSolicitud debe usarse dentro de ProveedorSolicitud');
  return contexto;
}

export function BotonSolicitar({ trabajo, children = 'Solicitar cotización', ...resto }) {
  const { abrir } = useSolicitud();
  return (
    <Boton iconoInicio={Send} onClick={() => abrir(trabajo)} {...resto}>
      {children}
    </Boton>
  );
}
