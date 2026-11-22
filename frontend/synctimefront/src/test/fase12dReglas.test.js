import { describe, expect, it } from 'vitest';
import {
  agregarMedidaFija, agregarValor, medidasDesdeApi, medidasParaApi, MODOS, quitarGrupo, validarMedidasYOpciones,
} from '../paginas/mantenedores/reglasMedidasYOpciones';

// Fase 12D (decisión 6): el editor de medidas y opciones de un producto. Mismas reglas que
// Negocio/Mantenedores/ValidacionMedidasYOpciones.cs. Medidas en centímetros en pantalla y en milímetros en la API.

const ROLL_UP = {
  anchoMinMm: 800, anchoMaxMm: 1500, altoMinMm: 2000, altoMaxMm: 3000, medidasFijas: [],
  opciones: [{ grupo: 'Diseño', valor: 'Del cliente' }, { grupo: 'Diseño', valor: 'Desarrollado por la oficina' }],
};
const CABALLETE = {
  anchoMinMm: null, anchoMaxMm: null, altoMinMm: null, altoMaxMm: null,
  medidasFijas: [{ anchoMm: 1000, altoMm: 500 }, { anchoMm: 1200, altoMm: 600 }], opciones: [],
};
const SENALIZACION = { anchoMinMm: null, anchoMaxMm: null, altoMinMm: null, altoMaxMm: null, medidasFijas: [], opciones: [{ grupo: 'Tipo', valor: 'Vial' }] };

const conRangos = (cambios = {}) => ({
  modo: MODOS.rangos, anchoMin: '10', anchoMax: '200', altoMin: '10', altoMax: '300', fijas: [], grupos: [{ grupo: 'Acabado', valores: ['Mate', 'Brillante'] }], ...cambios,
});

describe('del producto de la API al formulario', () => {
  it('un producto con límites va en centímetros, con coma decimal', () => {
    const f = medidasDesdeApi({ ...ROLL_UP, anchoMinMm: 805 });
    expect(f.modo).toBe(MODOS.rangos);
    expect([f.anchoMin, f.anchoMax, f.altoMin, f.altoMax]).toEqual(['80,5', '150', '200', '300']);
  });

  it('un producto con medidas fijas no muestra límites', () => {
    const f = medidasDesdeApi(CABALLETE);
    expect(f.modo).toBe(MODOS.fijas);
    expect(f.fijas).toEqual([{ ancho: '100', alto: '50' }, { ancho: '120', alto: '60' }]);
  });

  it('un producto sin medidas queda en «sin medidas»', () => {
    expect(medidasDesdeApi(SENALIZACION).modo).toBe(MODOS.ninguno);
  });

  it('las opciones planas de la API quedan agrupadas, en el orden en que aparecen', () => {
    const f = medidasDesdeApi({
      ...SENALIZACION,
      opciones: [{ grupo: 'Material', valor: 'Acrílico' }, { grupo: 'Espesor', valor: '2 mm' }, { grupo: 'Material', valor: 'Aluminio' }],
    });
    expect(f.grupos).toEqual([{ grupo: 'Material', valores: ['Acrílico', 'Aluminio'] }, { grupo: 'Espesor', valores: ['2 mm'] }]);
  });
});

describe('del formulario a la API', () => {
  it('los límites viajan en milímetros y no se mandan medidas fijas', () => {
    expect(medidasParaApi(conRangos({ anchoMin: '80,5' }))).toEqual({
      anchoMinMm: 805, anchoMaxMm: 2000, altoMinMm: 100, altoMaxMm: 3000, medidasFijas: [],
      opciones: [{ grupo: 'Acabado', valores: ['Mate', 'Brillante'] }],
    });
  });

  it('con medidas fijas no viajan límites', () => {
    const cuerpo = medidasParaApi({ ...conRangos(), modo: MODOS.fijas, fijas: [{ ancho: '100', alto: '50' }], grupos: [] });
    expect(cuerpo).toEqual({
      anchoMinMm: null, anchoMaxMm: null, altoMinMm: null, altoMaxMm: null, medidasFijas: [{ anchoMm: 1000, altoMm: 500 }], opciones: [],
    });
  });

  it('sin medidas no viaja nada de medidas, y los textos van sin espacios de más', () => {
    const cuerpo = medidasParaApi({ ...conRangos(), modo: MODOS.ninguno, grupos: [{ grupo: ' Tipo ', valores: [' Vial ', '  '] }] });
    expect(cuerpo.anchoMinMm).toBeNull();
    expect(cuerpo.medidasFijas).toEqual([]);
    expect(cuerpo.opciones).toEqual([{ grupo: 'Tipo', valores: ['Vial'] }]);
  });

  it('ida y vuelta: lo que llega de la API sale igual', () => {
    expect(medidasParaApi(medidasDesdeApi(ROLL_UP))).toEqual({ ...ROLL_UP, opciones: [{ grupo: 'Diseño', valores: ['Del cliente', 'Desarrollado por la oficina'] }] });
    expect(medidasParaApi(medidasDesdeApi(CABALLETE))).toEqual({ ...CABALLETE, opciones: [] });
  });
});

describe('validación (las mismas reglas que la API)', () => {
  it('un formulario correcto no tiene errores', () => {
    expect(validarMedidasYOpciones(conRangos())).toEqual({});
    expect(validarMedidasYOpciones({ ...conRangos(), modo: MODOS.ninguno, grupos: [] })).toEqual({});
  });

  it('el mínimo no puede ser mayor que el máximo: el error queda en el máximo', () => {
    const errores = validarMedidasYOpciones(conRangos({ anchoMin: '50', anchoMax: '40', altoMin: '90', altoMax: '10' }));
    expect(Object.keys(errores).sort()).toEqual(['altoMaxMm', 'anchoMaxMm']);
  });

  it.each(['', '0', '-5', 'abc', '5000,1', '80,55'])('el límite «%s» se rechaza en su campo', (valor) => {
    expect(validarMedidasYOpciones(conRangos({ anchoMin: valor, anchoMax: '5000' }))).toHaveProperty('anchoMinMm');
  });

  it('5.000 cm es el tope exacto y se acepta', () => {
    expect(validarMedidasYOpciones(conRangos({ anchoMin: '1', anchoMax: '5000', altoMin: '1', altoMax: '5000' }))).toEqual({});
  });

  it('con medidas fijas se necesita al menos una; sin repetir; con ancho y alto válidos', () => {
    const fijas = (lista) => validarMedidasYOpciones({ ...conRangos(), modo: MODOS.fijas, fijas: lista, grupos: [] });
    expect(fijas([])).toHaveProperty('medidasFijas');
    expect(fijas([{ ancho: '100', alto: '50' }, { ancho: '100,0', alto: '50' }])).toHaveProperty('medidasFijas');
    expect(fijas([{ ancho: '0', alto: '50' }])).toHaveProperty('medidasFijas');
    expect(fijas([{ ancho: '', alto: '50' }])).toHaveProperty('medidasFijas');
    expect(fijas([{ ancho: '100', alto: '50' }, { ancho: '120', alto: '60' }])).toEqual({});
  });

  it('las medidas fijas tienen tope de cantidad', () => {
    const muchas = Array.from({ length: 21 }, (_, i) => ({ ancho: String(i + 1), alto: '10' }));
    expect(validarMedidasYOpciones({ ...conRangos(), modo: MODOS.fijas, fijas: muchas, grupos: [] })).toHaveProperty('medidasFijas');
  });

  it('cada grupo necesita nombre y al menos un valor; nada repetido, sin distinguir mayúsculas', () => {
    const grupos = (lista) => validarMedidasYOpciones({ ...conRangos(), grupos: lista });
    expect(grupos([{ grupo: '', valores: ['A'] }])).toHaveProperty('opciones');
    expect(grupos([{ grupo: 'Acabado', valores: [] }])).toHaveProperty('opciones');
    expect(grupos([{ grupo: 'Acabado', valores: ['  ', ''] }])).toHaveProperty('opciones');
    expect(grupos([{ grupo: 'Acabado', valores: ['Mate', ' mate '] }])).toHaveProperty('opciones');
    expect(grupos([{ grupo: 'Diseño', valores: ['A'] }, { grupo: 'diseño', valores: ['B'] }])).toHaveProperty('opciones');
    expect(grupos([{ grupo: 'Acabado', valores: ['Mate'] }])).toEqual({});
  });

  it('los textos y las cantidades tienen el tope de la API', () => {
    const grupos = (lista) => validarMedidasYOpciones({ ...conRangos(), grupos: lista });
    expect(grupos([{ grupo: 'g'.repeat(61), valores: ['A'] }])).toHaveProperty('opciones');
    expect(grupos([{ grupo: 'g'.repeat(60), valores: ['v'.repeat(80)] }])).toEqual({});
    expect(grupos([{ grupo: 'G', valores: ['v'.repeat(81)] }])).toHaveProperty('opciones');
    expect(grupos(Array.from({ length: 11 }, (_, i) => ({ grupo: `G${i}`, valores: ['A'] })))).toHaveProperty('opciones');
    expect(grupos([{ grupo: 'G', valores: Array.from({ length: 31 }, (_, i) => `V${i}`) }])).toHaveProperty('opciones');
  });

  it('el mensaje de opciones nombra el grupo con el problema', () => {
    const errores = validarMedidasYOpciones({ ...conRangos(), grupos: [{ grupo: 'Acabado', valores: ['Mate'] }, { grupo: 'Material', valores: [] }] });
    expect(errores.opciones).toContain('Material');
  });
});

describe('cambios sobre el formulario', () => {
  it('agregar una medida fija y un valor, y quitar un grupo, sin tocar el resto', () => {
    const base = conRangos();
    expect(agregarMedidaFija(base).fijas).toEqual([{ ancho: '', alto: '' }]);
    expect(agregarValor(base, 0).grupos[0].valores).toEqual(['Mate', 'Brillante', '']);
    expect(quitarGrupo(base, 0).grupos).toEqual([]);
    expect(base.grupos[0].valores).toEqual(['Mate', 'Brillante']);
  });
});
