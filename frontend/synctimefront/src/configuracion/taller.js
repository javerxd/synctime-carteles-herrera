// Datos del taller que muestra el portal. Los que todavía no entrega el usuario quedan en null y la
// interfaz no los muestra: no se inventan (llegan en la fase 5).
export const TALLER = {
  nombre: 'Letrería Los Herrera',
  direccion: 'Villagrán 810',
  ciudad: 'Los Ángeles, Región del Biobío',
  codigoPostal: '4440000',
  // Enlace a OpenStreetMap: el visitante decide abrirlo; la página no pide nada a terceros sola.
  mapa: 'https://www.openstreetmap.org/search?query=Villagr%C3%A1n%20810%2C%20Los%20%C3%81ngeles%2C%20Chile',
  // De prueba (13-11-2026): numero del desarrollador. Se cambia por el del taller antes de la entrega.
  whatsapp: '56930667967',
  telefono: '+56 9 3066 7967',
  // Correo para que la gente escriba al taller. Los avisos automaticos usan otra cuenta
  // (letrerosherrera.avisos@gmail.com), que no se publica.
  correo: 'contacto.letrerosherrera@gmail.com',
};

/** Enlace de WhatsApp con un mensaje inicial, o null si el número aún no está configurado. */
export function enlaceWhatsapp(mensaje = 'Hola, quiero cotizar un trabajo.') {
  return TALLER.whatsapp ? `https://wa.me/${TALLER.whatsapp}?text=${encodeURIComponent(mensaje)}` : null;
}
