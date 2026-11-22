import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { OPCIONES_TESELAS, URL_TESELAS } from '../paginas/publicas/mapa/teselas';
import { estadoCartel } from '../paginas/publicas/cartel/estadoCartel';
import { filtrarTrabajos, normalizar } from '../paginas/publicas/vitrina/reglasVitrina';
import { rutaInterna } from '../sesion/rutas';

// Fase 12C: las reglas puras del portal público (búsqueda de la vitrina, cartel, mapa e ingreso).
const leer = (ruta) => readFileSync(resolve(process.cwd(), ruta), 'utf8');

const trabajo = (id, titulo, categoria, tipoLetrero = 'Letrero') => ({
  idCatalogo: id, titulo, tipoLetrero, categoria: { idCategoria: [...categoria].reduce((n, c) => n + c.charCodeAt(0), 0), nombre: categoria },
});
const TRABAJOS = [
  trabajo(1, 'Letrero luminoso para panadería', 'Luminosos', 'Caja de luz'),
  trabajo(2, 'Señalización de bodega', 'Señalética', 'Señal vial'),
  trabajo(3, 'Vinilo para vitrina', 'Vinilos', 'Vinilo de corte'),
  trabajo(4, 'Panel de señal vial', 'Señalética', 'Panel'),
  trabajo(5, 'Rotulación de camioneta', 'Vehículos', 'Vinilo'),
];
const titulos = (lista) => lista.map((t) => t.titulo);

describe('búsqueda de la vitrina (decisión 5: «contiene»)', () => {
  it('normaliza sin tildes ni mayúsculas', () => {
    expect(normalizar('  Señalización ')).toBe('senalizacion');
    expect(normalizar('PANADERÍA')).toBe('panaderia');
    expect(normalizar(null)).toBe('');
  });

  it('sin texto ni categoría devuelve todos, en su orden', () => {
    expect(filtrarTrabajos(TRABAJOS, {})).toEqual(TRABAJOS);
  });

  it('«señal» encuentra «Señalización» sin importar tildes, y no «Vinilo»', () => {
    expect(titulos(filtrarTrabajos(TRABAJOS, { texto: 'senal' }))).toEqual(['Señalización de bodega', 'Panel de señal vial']);
    expect(titulos(filtrarTrabajos(TRABAJOS, { texto: 'SEÑAL' }))).toEqual(['Señalización de bodega', 'Panel de señal vial']);
  });

  it('primero los títulos que empiezan con lo escrito, luego los que lo contienen', () => {
    const lista = titulos(filtrarTrabajos(TRABAJOS, { texto: 'pan' }));
    expect(lista).toEqual(['Panel de señal vial', 'Letrero luminoso para panadería']);
  });

  it('busca también en el tipo de letrero y en la categoría', () => {
    expect(titulos(filtrarTrabajos(TRABAJOS, { texto: 'caja de luz' }))).toEqual(['Letrero luminoso para panadería']);
    expect(titulos(filtrarTrabajos(TRABAJOS, { texto: 'vehiculos' }))).toEqual(['Rotulación de camioneta']);
  });

  it('se combina con la categoría', () => {
    const categoria = TRABAJOS[1].categoria.idCategoria;
    expect(titulos(filtrarTrabajos(TRABAJOS, { categoria }))).toEqual(['Señalización de bodega', 'Panel de señal vial']);
    expect(titulos(filtrarTrabajos(TRABAJOS, { categoria, texto: 'bodega' }))).toEqual(['Señalización de bodega']);
    expect(filtrarTrabajos(TRABAJOS, { categoria, texto: 'vinilo' })).toEqual([]);
  });

  it('con varias palabras exige todas, en cualquier orden', () => {
    expect(titulos(filtrarTrabajos(TRABAJOS, { texto: 'letrero panaderia' }))).toEqual(['Letrero luminoso para panadería']);
    expect(titulos(filtrarTrabajos(TRABAJOS, { texto: 'panaderia luminoso' }))).toEqual(['Letrero luminoso para panadería']);
    expect(filtrarTrabajos(TRABAJOS, { texto: 'letrero bodega' })).toEqual([]);
  });

  it('sin coincidencias devuelve una lista vacía', () => {
    expect(filtrarTrabajos(TRABAJOS, { texto: 'zzz' })).toEqual([]);
  });

  it('un trabajo sin categoría ni tipo no rompe la búsqueda', () => {
    expect(filtrarTrabajos([{ idCatalogo: 9, titulo: 'Solo título' }], { texto: 'solo' })).toHaveLength(1);
  });
});

describe('cartel del local', () => {
  it('dice solo «Abierto» o «Cerrado»', () => {
    expect(estadoCartel({ abiertoAhora: true })).toEqual({ abierto: true, etiqueta: 'Abierto' });
    expect(estadoCartel({ abiertoAhora: false })).toEqual({ abierto: false, etiqueta: 'Cerrado' });
  });

  it('el estado manual manda: dentro del horario pero ausente es cerrado, y fuera de horario también', () => {
    expect(estadoCartel({ abiertoAhora: false, dentroDeHorario: true, motivo: 'ausente' }).abierto).toBe(false);
    expect(estadoCartel({ abiertoAhora: false, motivo: 'fuera_de_horario' }).abierto).toBe(false);
    expect(estadoCartel({ abiertoAhora: false, motivo: 'cerrado_hoy' }).abierto).toBe(false);
    expect(estadoCartel({ abiertoAhora: true, motivo: 'abierto' }).abierto).toBe(true);
  });

  it('mientras no hay datos no afirma nada', () => {
    expect(estadoCartel(undefined)).toBeNull();
  });
});

describe('mapa «Cómo llegar» (diagnóstico del 403)', () => {
  it('las teselas se piden con política de referencia y atribución', () => {
    expect(URL_TESELAS).toMatch(/\{z\}\/\{x\}\/\{y\}/);
    expect(OPCIONES_TESELAS.referrerPolicy).toBe('strict-origin-when-cross-origin');
    expect(OPCIONES_TESELAS.attribution).toMatch(/OpenStreetMap/);
  });

  it('la página ya no apaga el Referer: causa del bloqueo de OpenStreetMap', () => {
    const html = leer('index.html');
    expect(html).not.toMatch(/name="referrer"\s+content="no-referrer"/);
    expect(html).toMatch(/name="referrer"\s+content="strict-origin-when-cross-origin"/);
    // La CSP sigue permitiendo solo ese servidor de teselas.
    expect(html).toMatch(/img-src [^;]*https:\/\/tile\.openstreetmap\.org/);
  });
});

describe('retorno tras el selector de ingreso', () => {
  it('las pantallas de ingreso nunca son un destino de retorno', () => {
    expect(rutaInterna('/ingresar', '/cuenta')).toBe('/cuenta');
    expect(rutaInterna('/ingresar/cliente', '/cuenta')).toBe('/cuenta');
    expect(rutaInterna('/personal/ingresar', '/panel')).toBe('/panel');
    expect(rutaInterna('/cuenta/solicitud?producto=2', '/cuenta')).toBe('/cuenta/solicitud?producto=2');
  });
});

describe('color y animación de los botones', () => {
  const css = leer('src/estilos/tokens.css');
  const tokens = Object.fromEntries([...css.matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)].map(([, n, v]) => [n, v.trim()]));

  it('el naranja #C2410C es el acento y el foco', () => {
    expect(tokens['--primitivo-naranja-700'].toLowerCase()).toBe('#c2410c');
    expect(tokens['--color-acento']).toBe('var(--primitivo-naranja-700)');
    expect(tokens['--color-foco']).toBe('var(--primitivo-naranja-700)');
  });

  it('el botón anima solo con puntero real, con propiedades baratas y respeta la reducción de movimiento', () => {
    const boton = leer('src/componentes/ui/Boton.module.css');
    expect(boton).toMatch(/@media \(hover: hover\) and \(pointer: fine\)/);
    expect(boton).toMatch(/prefers-reduced-motion: reduce/);
    expect(boton).not.toMatch(/transition:[^;]*\b(width|height|margin|top|left)\b/);
    // El panel del taller no lleva estas animaciones: solo donde hay una zona animada.
    expect(boton).toMatch(/\[data-animar\]/);
  });

  it('el zoom de las tarjetas de la vitrina se ve con puntero real aunque se reduzca el movimiento, y solo en la zona animada', () => {
    const portada = leer('src/paginas/publicas/Portada.module.css');
    const regla = portada.match(/@media \(hover: hover\) and \(pointer: fine\)\s*\{\s*:global\(\[data-animar\]\) \.trabajo:hover\s*\{([^}]*)\}/);
    expect(regla).not.toBeNull();
    expect(regla[1]).toMatch(/transform:\s*scale\(1\.03\)/);
    // Con «reducir movimiento» no desaparece: base.css solo lo hace instantáneo (sin recorrido animado).
    expect(portada).not.toMatch(/prefers-reduced-motion:\s*no-preference\)\s*\{\s*:global\(\[data-animar\]\) \.trabajo/);
    expect(leer('src/estilos/base.css')).toMatch(/prefers-reduced-motion: reduce[\s\S]*--duracion-entrada:\s*1ms/);
    // La tarjeta anima solo transform y sombra (propiedades baratas).
    expect(leer('src/componentes/ui/Tarjeta.module.css')).toMatch(/transition:\s*transform[\s\S]*box-shadow/);
  });
});
