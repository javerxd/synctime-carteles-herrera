import { act, waitFor } from '@testing-library/react';

// Ayudas de las pruebas del ingreso con Google (fase 14B). Nada llama a Google: se reemplaza `google.accounts.id`
// por una copia que guarda lo que la página le pide, y el «script» se entrega a mano.

// Un ID inventado: el verdadero vive solo en .env.local y en los User Secrets, nunca en el repositorio.
export const ID_PRUEBA = 'id-de-prueba-14b.apps.googleusercontent.com';
export const URL_SCRIPT_GOOGLE = 'https://accounts.google.com/gsi/client';

export const scriptDeGoogle = () => document.querySelector(`script[src="${URL_SCRIPT_GOOGLE}"]`);

/** Pone una copia de `google.accounts.id` en la ventana y devuelve lo que la página le va pidiendo. */
export function instalarGoogleFalso() {
  const estado = { configuraciones: [], botones: [], callback: null };
  window.google = {
    accounts: {
      id: {
        initialize: vi.fn((configuracion) => {
          estado.configuraciones.push(configuracion);
          estado.callback = configuracion.callback;
        }),
        renderButton: vi.fn((elemento, opciones) => {
          elemento.innerHTML = '';
          const boton = document.createElement('button');
          boton.type = 'button';
          boton.textContent = 'Botón oficial de Google';
          elemento.appendChild(boton);
          estado.botones.push(opciones);
        }),
        cancel: vi.fn(),
        disableAutoSelect: vi.fn(),
      },
    },
  };
  return estado;
}

/** Espera a que la página pida el script, instala la copia de Google y simula que terminó de cargar. */
export async function entregarScript() {
  await waitFor(() => expect(scriptDeGoogle()).not.toBeNull());
  const estado = instalarGoogleFalso();
  await act(async () => { scriptDeGoogle().dispatchEvent(new Event('load')); });
  return estado;
}

/** Simula un bloqueador o una caída de la red: el script nunca llega. */
export async function bloquearScript() {
  await waitFor(() => expect(scriptDeGoogle()).not.toBeNull());
  await act(async () => { scriptDeGoogle().dispatchEvent(new Event('error')); });
}

/** Google entrega el token de identidad: se ejecuta el `callback` que la página le dio al inicializar. */
export async function entregarCredencial(estado, credential = 'TOKEN-DE-GOOGLE-SECRETO') {
  await act(async () => { await estado.callback({ credential }); });
}

/** Lo último que la página le pasó a Google como nonce. */
export const ultimoNonce = (estado) => estado.configuraciones.at(-1)?.nonce;

export function limpiarGoogle() {
  delete window.google;
  document.querySelectorAll('script[src*="accounts.google.com"]').forEach((s) => s.remove());
}

/** Todo lo que el navegador guardó en localStorage y sessionStorage, en un solo texto. */
export const almacenamientos = () => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage });
