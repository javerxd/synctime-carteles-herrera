import { describe, expect, it } from 'vitest';
import { AxiosError } from 'axios';
import httpClient from '../api/httpClient';

const responder = (status, data) => (config) =>
  Promise.reject(new AxiosError('x', 'ERR', config, null, { status, data, config, headers: {}, statusText: '' }));

const fallo = (status, data) =>
  httpClient.get('/prueba', { adapter: responder(status, data) }).catch((error) => error);

describe('httpClient: motivo de los errores', () => {
  it.each([400, 409, 503])('lee el campo error de la API en %i', async (status) => {
    const error = await fallo(status, { error: 'La contraseña actual no es correcta.' });

    expect(error.status).toBe(status);
    expect(error.message).toBe('La contraseña actual no es correcta.');
  });

  it('mantiene un mensaje por defecto si no viene motivo', async () => {
    const error = await fallo(400, undefined);

    expect(error.message).toBe('Datos inválidos.');
  });

  it('una contraseña incorrecta (400) no cierra la sesión', async () => {
    sessionStorage.setItem('synctime_token', 'token');

    await fallo(400, { error: 'La contraseña actual no es correcta.' });

    expect(sessionStorage.getItem('synctime_token')).toBe('token');
    sessionStorage.clear();
  });
});
