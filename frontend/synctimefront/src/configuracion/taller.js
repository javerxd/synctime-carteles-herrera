// Datos del taller que muestra el portal. Los que todavía no entrega el usuario quedan en null y la
// interfaz no los muestra: no se inventan.
export const TALLER = {
  nombre: 'Letreros Herrera',
  direccion: 'Villagrán 810',
  ciudad: 'Los Ángeles, Región del Biobío',
  codigoPostal: '4440000',
  // Enlace a Google Maps entregado por el taller: el visitante decide abrirlo; la página no pide nada a
  // terceros por su cuenta.
  mapa: 'https://maps.app.goo.gl/HzJtEy3jetsBmztNA',
  // Punto exacto del taller, entregado el 13-11-2026. Si cambia, regenerar la imagen del mapa con
  // scripts/mapa-estatico.py.
  coordenadas: { latitud: -37.46377531272541, longitud: -72.34832848822964 },
  // Vista de la fachada en Google Street View (inserción entregada por el taller). Se carga solo si el
  // visitante pulsa «Ver la fachada».
  fachada: 'https://www.google.com/maps/embed?pb=!3m2!1ses!2scl!4v1794591098723!5m2!1ses!2scl!6m8!1m7!1swMmqLZzETI8WB-cDKNUSzA!2m2!1d-37.46377903842519!2d-72.34823461084247!3f242.78394900186683!4f4.498664960572668!5f0.7820865974627469',
  // De prueba (13-11-2026): numero del desarrollador. Se cambia por el del taller antes de la entrega.
  whatsapp: '56930667967',
  telefono: '+56 9 3066 7967',
  // Correo para que la gente escriba al taller. Los avisos automaticos usan otra cuenta
  // (letrerosherrera.avisos@gmail.com), que no se publica.
  correo: 'contacto.letrerosherrera@gmail.com',
  // Dirección pública del portal, para el código QR del cartel. Se fija al publicar (VITE_URL_PORTAL);
  // mientras tanto, la del sitio que se está viendo.
  urlPortal: import.meta.env.VITE_URL_PORTAL || `${window.location.origin}/`,
};

/** Enlace para llamar al taller (tel:+569...), o null. */
export function enlaceTelefono() {
  return TALLER.telefono ? `tel:${TALLER.telefono.replace(/[^+\d]/g, '')}` : null;
}

/** Enlace de WhatsApp con un mensaje inicial, o null si el número aún no está configurado. */
export function enlaceWhatsapp(mensaje = 'Hola, quiero cotizar un trabajo.') {
  return TALLER.whatsapp ? `https://wa.me/${TALLER.whatsapp}?text=${encodeURIComponent(mensaje)}` : null;
}
