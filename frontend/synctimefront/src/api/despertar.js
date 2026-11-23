import { useSyncExternalStore } from 'react';

// Fase 16C-1 (H13, H14): el estado de «el sistema está despertando». Render gratuito se duerme tras ~15 minutos sin tráfico y la base de Azure SQL
// gratuita se pausa sola: la primera visita del día recibe un 502/503/504 hasta que ambos despiertan. El cliente de la API (cliente.js) avisa aquí
// cuando empieza a esperar y cuando termina; AvisoDespertando lo muestra. Varias lecturas pueden esperar a la vez: el aviso se queda hasta la última.
let esperando = 0;
let instantanea = { activo: false, intento: 0 };
const oyentes = new Set();

function publicar(nueva) {
  // La instantánea solo cambia si cambió algo: React la compara por identidad.
  if (nueva.activo === instantanea.activo && nueva.intento === instantanea.intento) return;
  instantanea = nueva;
  for (const oyente of [...oyentes]) oyente();
}

export const estadoDespertar = {
  /** Una lectura empieza a esperar que el servidor despierte. */
  iniciar() {
    esperando += 1;
    publicar({ activo: true, intento: instantanea.intento });
  },
  /** Esa lectura terminó (respondió, se rindió o se canceló). */
  terminar() {
    esperando = Math.max(0, esperando - 1);
    if (esperando === 0) publicar({ activo: false, intento: 0 });
  },
  /** El intento más alto que se lleva (para el aviso). Sin espera activa, no hace nada. */
  registrarIntento(intento) {
    if (esperando > 0 && intento > instantanea.intento) publicar({ activo: true, intento });
  },
  obtener: () => instantanea,
  suscribir(oyente) {
    oyentes.add(oyente);
    return () => oyentes.delete(oyente);
  },
  /** Solo para las pruebas. */
  reiniciar() {
    esperando = 0;
    publicar({ activo: false, intento: 0 });
  },
};

/** Para el aviso: se vuelve a dibujar cuando el sistema empieza o termina de despertar. */
export function useDespertando() {
  return useSyncExternalStore(estadoDespertar.suscribir, estadoDespertar.obtener, estadoDespertar.obtener);
}
