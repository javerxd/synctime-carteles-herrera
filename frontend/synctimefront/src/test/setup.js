import '@testing-library/jest-dom';

// jsdom no implementa showModal ni close de <dialog>. Este reemplazo minimo solo cambia el atributo
// open y emite "close", como el navegador; el comportamiento real (Esc, foco atrapado, capa superior)
// se prueba en Playwright contra la galeria (e2e/galeria.spec.js).
if (typeof HTMLDialogElement !== 'undefined' && !HTMLDialogElement.prototype.showModal) {
  HTMLDialogElement.prototype.showModal = function showModal() {
    this.setAttribute('open', '');
  };
  HTMLDialogElement.prototype.close = function close() {
    if (!this.hasAttribute('open')) return;
    this.removeAttribute('open');
    this.dispatchEvent(new Event('close'));
  };
}

// jsdom no dibuja en <canvas>: sin este reemplazo, cada getContext escribe «Not implemented» en la consola.
// Devuelve null, como un navegador sin el contexto pedido; el brillo y las vistas previas lo toleran.
if (typeof HTMLCanvasElement !== 'undefined') {
  HTMLCanvasElement.prototype.getContext = function getContext() {
    return null;
  };
}
