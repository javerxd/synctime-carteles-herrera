import { readFileSync } from 'node:fs';
import { cubre, expresionDeRuta, rutasDeApp } from '../../e2e/inventarioRutas.js';
import { PANTALLAS, REDIRECCIONES, VIVEN_EN_OTRA_PRUEBA } from '../../e2e/rutas.js';

// Fase 15B (RNFP15, RNFE08): TODAS las rutas de App.jsx pasan por el recorrido de accesibilidad (axe a 360, 768 y 1440 px, sin desborde
// horizontal). Si alguien agrega una ruta y no la suma a e2e/rutas.js, esta prueba falla.
const JUGUETE = `
  <Routes>
    <Route element={<Estructura />}>
      <Route index element={enCarga(<Portada />)} />
      <Route path="consultas" element={enCarga(<Consultas />)} />
      <Route path="login" element={<Navigate to="/ingreso" replace />} />
      <Route path="*" element={<Pagina404 />} />
    </Route>
    <Route path="cuenta" element={<Guarda><Estructura /></Guarda>}>
      <Route index element={enCarga(<MiCuenta />)} />
      {/* comentario con <Route de mentira */}
      <Route path="solicitudes/:id" element={enCarga(<Una />)} />
    </Route>
    <Route path="panel" element={<Panel />}>
      <Route element={<RequierePersonal roles={GESTION} />}>
        <Route path="mantenedores" element={enCarga(<Mantenedores />)}>
          <Route index element={enCarga(<Inicio />)} />
          <Route path=":tipo" element={enCarga(<Lista />)} />
        </Route>
        <Route path="mantenedores/:tipo/nuevo" element={enCarga(<Editor />)} />
      </Route>
      <Route path="*" element={<Pagina404 />} />
    </Route>
  </Routes>`;

describe('rutasDeApp', () => {
  it('lee las rutas con su dirección completa, sin contar los contenedores', () => {
    expect(rutasDeApp(JUGUETE)).toEqual([
      { ruta: '/', tipo: 'pantalla' },
      { ruta: '/consultas', tipo: 'pantalla' },
      { ruta: '/login', tipo: 'redireccion' },
      { ruta: '/*', tipo: 'noEncontrada' },
      { ruta: '/cuenta', tipo: 'pantalla' },
      { ruta: '/cuenta/solicitudes/:id', tipo: 'pantalla' },
      { ruta: '/panel/mantenedores', tipo: 'pantalla' },
      { ruta: '/panel/mantenedores/:tipo', tipo: 'pantalla' },
      { ruta: '/panel/mantenedores/:tipo/nuevo', tipo: 'pantalla' },
      { ruta: '/panel/*', tipo: 'noEncontrada' },
    ]);
  });

  it('una ruta de índice es la de su contenedor, y el contenedor en sí no cuenta como pantalla', () => {
    const rutas = rutasDeApp(JUGUETE).map((r) => r.ruta);
    expect(rutas.filter((r) => r === '/cuenta')).toHaveLength(1);
    expect(rutas.filter((r) => r === '/panel/mantenedores')).toHaveLength(1);
    expect(rutas).not.toContain('/panel');
  });

  it('el App.jsx verdadero tiene las rutas que se esperan', () => {
    const rutas = rutasDeApp(readFileSync('src/App.jsx', 'utf8'));
    const pantallas = rutas.filter((r) => r.tipo === 'pantalla').map((r) => r.ruta);
    expect(pantallas).toEqual(expect.arrayContaining(['/', '/panel', '/panel/pedidos/:id', '/panel/mantenedores/:tipo/nuevo', '/cuenta/google/completar']));
    expect(rutas.filter((r) => r.tipo === 'redireccion').map((r) => r.ruta).sort()).toEqual(['/dashboard', '/login']);
    expect(rutas.filter((r) => r.tipo === 'noEncontrada').map((r) => r.ruta).sort()).toEqual(['/*', '/panel/*']);
    expect(pantallas.length).toBeGreaterThan(45);
  });
});

describe('cubre', () => {
  it('una dirección concreta cubre su ruta con parámetros, con consulta o sin ella', () => {
    expect(cubre(['/panel/pedidos/1'], '/panel/pedidos/:id')).toBe(true);
    expect(cubre(['/panel/reportes?reporte=gerencial'], '/panel/reportes')).toBe(true);
  });

  it('un parámetro no cubre una palabra fija ni al revés', () => {
    expect(cubre(['/panel/mantenedores/categorias/1'], '/panel/mantenedores/:tipo/nuevo')).toBe(false);
    expect(cubre(['/panel/pedidos/directo'], '/panel/pedidos')).toBe(false);
    expect(expresionDeRuta('/a/:id').test('/a/7/b')).toBe(false);
  });
});

describe('el recorrido de accesibilidad abre todas las rutas de la aplicación', () => {
  const rutas = rutasDeApp(readFileSync('src/App.jsx', 'utf8'));
  const direcciones = PANTALLAS.map(([ruta]) => ruta);

  it('cada pantalla está en el recorrido o se prueba en otra prueba declarada', () => {
    const sinCubrir = rutas
      .filter((r) => r.tipo === 'pantalla')
      .filter((r) => !cubre(direcciones, r.ruta) && !Object.keys(VIVEN_EN_OTRA_PRUEBA).includes(r.ruta))
      .map((r) => r.ruta);
    expect(sinCubrir).toEqual([]);
  });

  it('la página «no encontrada» se recorre en la parte pública y en el panel', () => {
    expect(direcciones).toEqual(expect.arrayContaining(['/ruta-que-no-existe', '/panel/ruta-que-no-existe']));
  });

  it('las direcciones antiguas que redirigen están declaradas con su destino', () => {
    expect(REDIRECCIONES.map(([desde]) => desde).sort()).toEqual(rutas.filter((r) => r.tipo === 'redireccion').map((r) => r.ruta).sort());
  });

  it('lo que vive en otra prueba dice cuál, y esa prueba existe y usa axe', () => {
    for (const [ruta, archivo] of Object.entries(VIVEN_EN_OTRA_PRUEBA)) {
      expect(rutas.map((r) => r.ruta), ruta).toContain(ruta);
      expect(readFileSync(archivo, 'utf8'), `${archivo} debe pasar axe`).toMatch(/AxeBuilder|sinFallasGraves/);
    }
  });

  it('el recorrido no repite direcciones ni deja títulos vacíos', () => {
    expect(new Set(direcciones).size).toBe(direcciones.length);
    expect(PANTALLAS.every(([, titulo]) => titulo.length > 3)).toBe(true);
  });
});
