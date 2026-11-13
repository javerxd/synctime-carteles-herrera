// Mide en vivo el contraste de los tokens de color contra el blanco, leyendo los valores que el
// navegador calculó. Solo para la galería.
const TOKENS = [
  ['--color-texto', 'Tinta'],
  ['--color-texto-secundario', 'Texto secundario'],
  ['--color-texto-tenue', 'Texto tenue'],
  ['--color-acento', 'Acento ámbar'],
  ['--color-acento-hover', 'Acento al pasar'],
  ['--color-borde-control', 'Borde de campos'],
  ['--color-fondo', 'Fondo'],
  ['--color-fondo-alterno', 'Fondo alterno (seda)'],
  ['--color-fondo-hundido', 'Fondo hundido'],
  ['--color-borde', 'Divisor'],
];

function luminancia(hex) {
  const [r, g, b] = [1, 3, 5]
    .map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

export function contrasteDeTokens() {
  const estilo = getComputedStyle(document.documentElement);
  return TOKENS.map(([token, nombre]) => {
    const valor = estilo.getPropertyValue(token).trim();
    const esHex = /^#[0-9a-f]{6}$/i.test(valor);
    const fondo = ['--color-fondo', '--color-fondo-alterno', '--color-fondo-hundido', '--color-borde'].includes(token);
    const relacion = esHex && !fondo ? (1.05 / (luminancia(valor) + 0.05)).toFixed(2).replace('.', ',') : null;
    return { token, nombre, valor, contraste: relacion && `${relacion}:1` };
  });
}
