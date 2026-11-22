import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { MAXIMO_CARACTERES, MAXIMO_ESPACIOS, MENSAJES_NOMBRE, errorNombre, revisarNombre } from '../utilidades/reglaNombre';

// Los mismos casos que usa ReglaNombre.cs en el servidor: un solo archivo para los dos lados.
// Vitest corre desde frontend/synctimefront.
const ruta = resolve(process.cwd(),
  '../../backend/Proyecto-Carteles-herrera-synctime-main/LetrerosHerreraSYNCTIMEapi.UnitTests/CasosNombre.json');
const datos = JSON.parse(readFileSync(ruta, 'utf-8'));

describe('regla de nombres con los casos compartidos con el servidor', () => {
  it.each(datos.casos.map((c) => [c.nombre, c]))('%s', (_nombre, caso) => {
    expect(revisarNombre(caso.valor)).toBe(caso.codigo);
  });

  it('usa los mismos límites', () => {
    expect(MAXIMO_CARACTERES).toBe(datos.maximoCaracteres);
    expect(MAXIMO_ESPACIOS).toBe(datos.maximoEspacios);
  });

  it.each(Object.entries(datos.mensajes))('el mensaje «%s» es el mismo que el del servidor', (codigo, mensaje) => {
    expect(MENSAJES_NOMBRE[codigo]).toBe(mensaje);
  });

  it('el campo vacío usa el mensaje del formulario y un valor válido no da error', () => {
    expect(errorNombre('', 'Escribe tu nombre.')).toBe('Escribe tu nombre.');
    expect(errorNombre(undefined, 'Escribe tu nombre.')).toBe('Escribe tu nombre.');
    expect(errorNombre('María José', 'Escribe tu nombre.')).toBeNull();
    expect(errorNombre('Ana2', 'Escribe tu nombre.')).toBe(MENSAJES_NOMBRE.caracteres);
  });
});
