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
