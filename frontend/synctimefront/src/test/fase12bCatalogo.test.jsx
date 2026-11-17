import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { EditorItems } from '../paginas/solicitudes/EditorItems';
import { detallesParaApi, gruposDeOpciones, itemVacio, itemsDesdeDetalles, opcionesElegidas, textoMedidas } from '../paginas/solicitudes/reglasSolicitud';

// Fase 12B: el catálogo real del taller en el formulario: medidas con sus límites y opciones elegibles.
const ROLL_UP = { idProducto: 5, nombre: 'Roll Up / Pendones Roller', anchoMinMm: 800, anchoMaxMm: 1500, altoMinMm: 2000, altoMaxMm: 3000, medidasFijas: [], opciones: [{ grupo: 'Diseño', valor: 'Del cliente' }, { grupo: 'Diseño', valor: 'Desarrollado por la oficina' }] };
const X_BANNER = { idProducto: 6, nombre: 'X Banner / banner araña', anchoMinMm: null, medidasFijas: [{ anchoMm: 800, altoMm: 2000 }], opciones: [] };
const CABALLETE = { idProducto: 12, nombre: 'Caballete publicitario / Paloma', anchoMinMm: null, medidasFijas: [{ anchoMm: 1000, altoMm: 500 }, { anchoMm: 1200, altoMm: 600 }], opciones: [] };
const BANNERS = { idProducto: 2, nombre: 'Banners', anchoMinMm: 10, anchoMaxMm: 50000, altoMinMm: 10, altoMaxMm: 50000, medidasFijas: [], opciones: [{ grupo: 'Material', valor: 'Tela PVC de 13 onzas' }, { grupo: 'Material', valor: 'Papel vinílico de una micra' }, { grupo: 'Terminaciones', valor: 'Solo tela impresa' }] };
const PRODUCTOS = [ROLL_UP, X_BANNER, CABALLETE, BANNERS];

describe('reglas del catálogo real', () => {
  it('describe en centímetros lo que el taller hace de cada producto', () => {
    expect(textoMedidas(ROLL_UP)).toBe('Ancho de 80 a 150 cm y alto de 200 a 300 cm.');
    expect(textoMedidas(X_BANNER)).toBe('Medida única: 80 × 200 cm.');
    expect(textoMedidas(CABALLETE)).toBe('Medidas disponibles: 100 × 50, 120 × 60 cm.');
    expect(textoMedidas({ idProducto: 11, medidasFijas: [], anchoMinMm: null })).toBeNull();
    expect(textoMedidas(undefined)).toBeNull();
  });

  it('agrupa las opciones planas de la API por grupo, en su orden', () => {
    expect(gruposDeOpciones(BANNERS)).toEqual([
      { grupo: 'Material', valores: ['Tela PVC de 13 onzas', 'Papel vinílico de una micra'] },
      { grupo: 'Terminaciones', valores: ['Solo tela impresa'] },
    ]);
    expect(gruposDeOpciones(undefined)).toEqual([]);
  });

  it('solo envía a la API lo que se eligió', () => {
    expect(opcionesElegidas({ Material: 'Tela PVC de 13 onzas', Terminaciones: '' })).toEqual({ Material: 'Tela PVC de 13 onzas' });
    expect(opcionesElegidas({ Material: '' })).toBeNull();
    const conOpciones = { ...itemVacio(2), ancho: '300', alto: '100', opciones: { Material: 'Tela PVC de 13 onzas' } };
    expect(detallesParaApi([conOpciones])[0].opciones).toEqual({ Material: 'Tela PVC de 13 onzas' });
    expect(detallesParaApi([{ ...itemVacio(2), ancho: '300', alto: '100' }])[0]).not.toHaveProperty('opciones');
  });

  it('al editar, las opciones guardadas vuelven al formulario', () => {
    const [item] = itemsDesdeDetalles([{ idProducto: 2, anchoMm: 3000, altoMm: 1000, cantidad: 1, opciones: { Material: 'Tela PVC de 13 onzas' } }], PRODUCTOS);

    expect(item.opciones).toEqual({ Material: 'Tela PVC de 13 onzas' });
  });
});

describe('editor de letreros con el catálogo real', () => {
  function Editor() {
    const [items, setItems] = useState([itemVacio()]);
    return (
      <EditorItems items={items} productos={PRODUCTOS} errores={{}} alTocar={() => {}} alAgregar={() => {}} alQuitar={() => {}}
        alCambiar={(i, campo, valor) => setItems((lista) => lista.map((it, k) => (k === i ? { ...it, [campo]: valor } : it)))} />
    );
  }

  it('muestra los límites de medidas del producto y sus opciones', () => {
    render(<Editor />);
    fireEvent.change(screen.getByLabelText(/Tipo de letrero/), { target: { value: '5' } });

    expect(screen.getByText('Ancho de 80 a 150 cm y alto de 200 a 300 cm.')).toBeInTheDocument();
    expect(screen.getByLabelText('Diseño')).toBeInTheDocument();
  });

  it('una medida única se completa sola', () => {
    render(<Editor />);
    fireEvent.change(screen.getByLabelText(/Tipo de letrero/), { target: { value: '6' } });

    expect(screen.getByLabelText('Ancho (cm)')).toHaveValue('80');
    expect(screen.getByLabelText('Alto (cm)')).toHaveValue('200');
  });

  it('con varias medidas disponibles se elige una y completa ancho y alto', () => {
    render(<Editor />);
    fireEvent.change(screen.getByLabelText(/Tipo de letrero/), { target: { value: '12' } });
    fireEvent.change(screen.getByLabelText('Medida disponible'), { target: { value: '1' } });

    expect(screen.getByLabelText('Ancho (cm)')).toHaveValue('120');
    expect(screen.getByLabelText('Alto (cm)')).toHaveValue('60');
  });

  it('lo elegido se borra al cambiar de producto, porque era de otro', () => {
    render(<Editor />);
    fireEvent.change(screen.getByLabelText(/Tipo de letrero/), { target: { value: '2' } });
    fireEvent.change(screen.getByLabelText('Material'), { target: { value: 'Tela PVC de 13 onzas' } });
    expect(screen.getByLabelText('Material')).toHaveValue('Tela PVC de 13 onzas');

    fireEvent.change(screen.getByLabelText(/Tipo de letrero/), { target: { value: '5' } });

    expect(screen.queryByLabelText('Material')).toBeNull();
    expect(screen.getByLabelText('Diseño')).toHaveValue('');
  });
});

describe('revisión de la integración (12B)', () => {
  it('el error de opciones que devuelve la API se ve en el letrero', () => {
    function Editor() {
      const [items, setItems] = useState([{ ...itemVacio(2), opciones: { Material: 'Madera' } }]);
      return (
        <EditorItems items={items} productos={PRODUCTOS} errores={{ 'detalles.0.opciones': '«Madera» no es una opción válida de «Material» para «Banners».' }}
          alTocar={() => {}} alAgregar={() => {}} alQuitar={() => {}}
          alCambiar={(i, campo, valor) => setItems((lista) => lista.map((it, k) => (k === i ? { ...it, [campo]: valor } : it)))} />
      );
    }
    render(<Editor />);

    expect(screen.getByText(/no es una opción válida de «Material»/)).toBeInTheDocument();
  });

  it('una fecha con basura en la dirección no pasa a la API', async () => {
    const { filtrosDesde } = await import('../utilidades/filtrosUrl');
    const { DEFINICION_FILTROS } = await import('../paginas/indicadores/reglasIndicadores');

    expect(filtrosDesde(new URLSearchParams('desde=2026-11-01X&hasta=2026-11-30'), DEFINICION_FILTROS).desde).toBe('');
    expect(filtrosDesde(new URLSearchParams('desde=2026-11-01&hasta=2026-11-30'), DEFINICION_FILTROS).desde).toBe('2026-11-01');
  });
});
