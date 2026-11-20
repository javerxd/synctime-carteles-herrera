/**
 * Icono de WhatsApp entregado por el taller (WebP liviano). Es decorativo: el botón que lo lleva dice
 * «Escribir por WhatsApp» con texto, así que no depende del color ni de la imagen. Acepta `size` para
 * usarse como `iconoInicio` de Boton.
 */
export function IconoWhatsapp({ size = 18 }) {
  return <img src="/imagenes/icono-whatsapp.webp" alt="" width={size} height={size} aria-hidden="true" draggable="false" />;
}
