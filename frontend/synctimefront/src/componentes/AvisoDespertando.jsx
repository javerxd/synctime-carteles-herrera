import { LoaderCircle } from 'lucide-react';
import { useDespertando } from '../api/despertar';
import estilos from './AvisoDespertando.module.css';

const TITULO = 'El sistema está despertando';
const TEXTO = 'Esto puede tardar hasta un minuto la primera vez del día. Reintentamos solos: no necesitas recargar la página.';

/**
 * Fase 16C-1 (H13, H14): aviso mientras el servidor gratuito despierta (Render se duerme sin tráfico y la base de Azure SQL se pausa).
 * La región viva existe desde el inicio y vacía, para que el lector de pantalla oiga el cambio (una región que nace con su texto no siempre se
 * anuncia); la copia visual es aria-hidden, así no se anuncia dos veces. El giro se detiene con «reducir movimiento».
 */
export default function AvisoDespertando() {
  const { activo } = useDespertando();
  return (
    <>
      <div role="status" aria-live="polite" aria-atomic="true" className="solo-lectores">
        {activo ? `${TITULO}. Estamos reintentando solos.` : ''}
      </div>
      {activo && (
        <div className={estilos.contenedor} aria-hidden="true">
          <div className={estilos.aviso}>
            <LoaderCircle size={20} strokeWidth={1.75} className={estilos.giro} />
            <div className={estilos.cuerpo}>
              <p className={estilos.titulo}>{TITULO}</p>
              <p className={estilos.texto}>{TEXTO}</p>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
