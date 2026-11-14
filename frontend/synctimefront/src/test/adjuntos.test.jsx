import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { Adjuntos } from '../componentes/ui';
import { ErrorApi } from '../api/cliente';
import { mensajeSubida, validarArchivo } from '../componentes/ui/reglasAdjuntos';

const MB = 1024 * 1024;

function archivo(nombre, tipo, bytes = 2048) {
  const a = new File(['x'], nombre, { type: tipo });
  Object.defineProperty(a, 'size', { value: bytes });
  return a;
}

let siguienteId = 100;
const respuestaDe = (a) => ({
  idAdjunto: siguienteId++, tipo: a.type === 'application/pdf' ? 'pdf' : 'imagen', nombre: a.name, bytes: a.size,
  urlMiniatura: a.type === 'application/pdf' ? null : '/api/v1/adjuntos/1/miniatura',
});

function dibujar(props = {}) {
  const subir = props.subir ?? vi.fn((a) => Promise.resolve(respuestaDe(a)));
  const quitar = props.quitar ?? vi.fn(() => Promise.resolve());
  const alCambiar = vi.fn();
  const utilidades = render(
    <Adjuntos subir={subir} quitar={quitar} alCambiar={alCambiar} cargarMiniatura={vi.fn(() => Promise.resolve(new Blob(['x'])))}
      cargarArchivo={vi.fn()} {...props} />,
  );
  const elegir = (...archivos) => fireEvent.change(screen.getByTestId('entrada-adjuntos'), { target: { files: archivos } });
  return { ...utilidades, subir, quitar, alCambiar, elegir };
}

const lista = () => screen.queryByRole('list', { name: 'Archivos adjuntados' });

beforeEach(() => {
  URL.createObjectURL = vi.fn(() => 'blob:vista-local');
  URL.revokeObjectURL = vi.fn();
});

describe('reglas de los adjuntos', () => {
  it('acepta JPG, PNG, WebP y PDF de hasta 10 MB', () => {
    for (const [nombre, tipo] of [['a.jpg', 'image/jpeg'], ['a.png', 'image/png'], ['a.webp', 'image/webp'], ['a.pdf', 'application/pdf']]) {
      expect(validarArchivo(archivo(nombre, tipo, 10 * MB))).toBeNull();
    }
  });

  it('rechaza SVG, AI y archivos de más de 10 MB con un mensaje que dice cómo seguir', () => {
    expect(validarArchivo(archivo('logo.svg', 'image/svg+xml'))).toMatch(/expórtalo a PDF o PNG/);
    expect(validarArchivo(archivo('logo.ai', 'application/postscript'))).toMatch(/Formato no aceptado/);
    expect(validarArchivo(archivo('foto.jpg', 'image/jpeg', 10 * MB + 1))).toMatch(/demasiado grande.*10 MB/);
  });

  it('sin tipo informado (algunos teléfonos), decide la extensión', () => {
    expect(validarArchivo(archivo('foto.JPG', ''))).toBeNull();
    expect(validarArchivo(archivo('diseño.cdr', ''))).toMatch(/Formato no aceptado/);
  });
});

describe('zona de adjuntos', () => {
  it('muestra la zona, el contador y un botón para elegir que acepta solo los formatos permitidos', () => {
    dibujar();
    expect(screen.getByRole('group', { name: /Adjunta tu logo o diseño/ })).toBeInTheDocument();
    expect(screen.getByText('0 de 5')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Elegir archivos' })).not.toHaveAttribute('aria-disabled');
    expect(screen.getByTestId('entrada-adjuntos')).toHaveAttribute('accept', 'image/jpeg,image/png,image/webp,application/pdf');
  });

  it('rechaza en el navegador el formato y el tamaño, por archivo, sin perder el que sí sirve', async () => {
    const { subir, elegir } = dibujar();
    elegir(archivo('logo.svg', 'image/svg+xml'), archivo('enorme.png', 'image/png', 12 * MB), archivo('logo.png', 'image/png'));

    expect(screen.getByRole('status')).toHaveTextContent(/logo\.svg no se adjuntó: Formato no aceptado/);
    await waitFor(() => expect(subir).toHaveBeenCalledTimes(1));
    expect(subir.mock.calls[0][0].name).toBe('logo.png');
    expect(screen.getByText(/logo\.svg/).closest('li')).toHaveTextContent(/Formato no aceptado/);
    expect(screen.getByText(/enorme\.png/).closest('li')).toHaveTextContent(/demasiado grande \(12 MB\)/);
    await waitFor(() => expect(screen.getByText('1 de 5')).toBeInTheDocument());
    expect(within(lista()).getByRole('button', { name: 'Quitar logo.png' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('logo.png adjuntado. 1 de 5.');
  });

  it('el tope es 5: el sexto se rechaza y la zona queda inactiva', async () => {
    const { subir, elegir, alCambiar } = dibujar();
    elegir(...[1, 2, 3, 4, 5, 6].map((n) => archivo(`foto${n}.jpg`, 'image/jpeg')));
    await waitFor(() => expect(subir).toHaveBeenCalledTimes(5));
    expect(screen.getByText(/foto6\.jpg/).closest('li')).toHaveTextContent(/máximo por solicitud/);
    await waitFor(() => expect(alCambiar).toHaveBeenLastCalledWith(expect.arrayContaining([expect.objectContaining({ nombre: 'foto5.jpg' })]), { subiendo: false }));
    expect(screen.getByText('5 de 5')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Elegir archivos' })).toHaveAttribute('aria-disabled', 'true');
    expect(screen.getByText(/Llegaste al máximo de 5 archivos/)).toBeInTheDocument();
  });

  it('muestra el avance de cada subida y lo quita al terminar', async () => {
    let terminar;
    let avisar;
    const subir = vi.fn((a, { alProgreso }) => {
      avisar = alProgreso;
      return new Promise((resolver) => { terminar = () => resolver(respuestaDe(a)); });
    });
    const { elegir, alCambiar } = dibujar({ subir });
    elegir(archivo('logo.png', 'image/png'));
    await waitFor(() => expect(subir).toHaveBeenCalled());
    act(() => avisar(0.5));
    expect(screen.getByRole('progressbar', { name: 'Subiendo logo.png' })).toHaveAttribute('value', '50');
    expect(alCambiar).toHaveBeenLastCalledWith([], { subiendo: true });
    await act(async () => terminar());
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(alCambiar).toHaveBeenLastCalledWith([expect.objectContaining({ nombre: 'logo.png', tipo: 'imagen' })], { subiendo: false });
  });

  it('un error del servidor afecta solo a ese archivo', async () => {
    const subir = vi.fn((a) => (a.name === 'malo.pdf'
      ? Promise.reject(new ErrorApi({ estado: 400, mensaje: 'Formato no aceptado. Sube JPG, PNG, WebP o PDF.' }))
      : Promise.resolve(respuestaDe(a))));
    const { elegir } = dibujar({ subir });
    elegir(archivo('malo.pdf', 'application/pdf'), archivo('bueno.pdf', 'application/pdf'));
    await waitFor(() => expect(screen.getByText(/malo\.pdf/).closest('li')).toHaveTextContent(/Formato no aceptado/));
    await waitFor(() => expect(within(lista()).queryByRole('button', { name: 'Quitar malo.pdf' })).not.toBeInTheDocument());
    expect(within(lista()).getByRole('button', { name: 'Quitar bueno.pdf' })).toBeInTheDocument();
    expect(within(lista()).getByText('bueno.pdf')).toBeInTheDocument();
    expect(screen.getByText('1 de 5')).toBeInTheDocument();
  });

  it('«×» quita el borrador en la API, lo retira de la lista y devuelve el foco a «Elegir archivos»', async () => {
    const { elegir, quitar, alCambiar } = dibujar();
    elegir(archivo('logo.png', 'image/png'));
    const boton = await within(lista()).findByRole('button', { name: 'Quitar logo.png' });
    await waitFor(() => expect(alCambiar).toHaveBeenLastCalledWith([expect.objectContaining({ nombre: 'logo.png' })], { subiendo: false }));
    const { idAdjunto } = alCambiar.mock.lastCall[0][0];
    fireEvent.click(boton);
    await waitFor(() => expect(quitar).toHaveBeenCalledWith(idAdjunto));
    await waitFor(() => expect(lista()).not.toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Elegir archivos' })).toHaveFocus();
    expect(screen.getByRole('status')).toHaveTextContent('Quitaste logo.png. 0 de 5.');
    expect(alCambiar).toHaveBeenLastCalledWith([], { subiendo: false });
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:vista-local');
  });

  it('si la API no puede quitarlo, el archivo se queda y se explica', async () => {
    const quitar = vi.fn(() => Promise.reject(new ErrorApi({ estado: 404, mensaje: 'No encontramos lo que buscas.' })));
    const { elegir } = dibujar({ quitar });
    elegir(archivo('logo.png', 'image/png'));
    fireEvent.click(await within(lista()).findByRole('button', { name: 'Quitar logo.png' }));
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('logo.png: No pudimos quitarlo. No encontramos lo que buscas.'));
    expect(screen.getAllByText(/No pudimos quitarlo/)[0].closest('li')).toHaveTextContent('logo.png');
    expect(within(lista()).getByRole('button', { name: 'Quitar logo.png' })).toBeInTheDocument();
  });

  it('cancelar una subida en curso no llama a la API de borrado', async () => {
    const subir = vi.fn((_, { senal }) => new Promise((_r, rechazar) => senal.addEventListener('abort', () => rechazar(new DOMException('x', 'AbortError')))));
    const { elegir, quitar } = dibujar({ subir });
    elegir(archivo('logo.png', 'image/png'));
    fireEvent.click(await within(lista()).findByRole('button', { name: 'Quitar logo.png' }));
    await waitFor(() => expect(lista()).not.toBeInTheDocument());
    expect(quitar).not.toHaveBeenCalled();
  });

  it('arrastrar y soltar también adjunta', async () => {
    const { subir, container } = dibujar();
    const zona = container.querySelector('[class*="zona"]');
    fireEvent.drop(zona, { dataTransfer: { files: [archivo('plano.pdf', 'application/pdf')] } });
    await waitFor(() => expect(subir).toHaveBeenCalledTimes(1));
    expect(await within(lista()).findByText('plano.pdf')).toBeInTheDocument();
  });

  it('los adjuntos ya guardados se muestran con su miniatura privada (descargada con la sesión)', async () => {
    const cargarMiniatura = vi.fn(() => Promise.resolve(new Blob(['x'], { type: 'image/webp' })));
    dibujar({ cargarMiniatura, iniciales: [{ idAdjunto: 5, tipo: 'imagen', nombre: 'fachada.jpg', bytes: 1000, urlMiniatura: '/api/v1/adjuntos/5/miniatura' }] });
    await waitFor(() => expect(cargarMiniatura).toHaveBeenCalledWith(expect.objectContaining({ idAdjunto: 5 })));
    expect(screen.getByText('1 de 5')).toBeInTheDocument();
    await waitFor(() => expect(lista().querySelector('img')).toHaveAttribute('src', 'blob:vista-local'));
  });

  it('403 correo sin confirmar y 429 se explican por archivo con un texto claro', async () => {
    const subir = vi.fn((a) => Promise.reject(a.name === 'uno.png'
      ? new ErrorApi({ estado: 403, codigo: 'correo_sin_confirmar', mensaje: 'x' })
      : new ErrorApi({ estado: 429, codigo: 'demasiados_intentos', mensaje: 'x' })));
    const { elegir } = dibujar({ subir });
    elegir(archivo('uno.png', 'image/png'), archivo('dos.png', 'image/png'));
    await waitFor(() => expect(screen.getByText('uno.png', { selector: 'strong' }).closest('li')).toHaveTextContent(/Confirma tu correo para adjuntar archivos/));
    expect(screen.getByText('dos.png', { selector: 'strong' }).closest('li')).toHaveTextContent(/Subiste muchos archivos seguidos/);
  });

  it('al salir de la pantalla cancela las subidas en curso', async () => {
    let senal;
    const subir = vi.fn((_, opciones) => { senal = opciones.senal; return new Promise(() => {}); });
    const { elegir, unmount } = dibujar({ subir });
    elegir(archivo('logo.png', 'image/png'));
    await waitFor(() => expect(subir).toHaveBeenCalled());
    expect(senal.aborted).toBe(false);
    unmount();
    expect(senal.aborted).toBe(true);
  });

  it('un archivo que se está quitando no cuenta como listo y deja el formulario ocupado', async () => {
    let terminar;
    const quitar = vi.fn(() => new Promise((r) => { terminar = r; }));
    const { elegir, alCambiar } = dibujar({ quitar });
    elegir(archivo('logo.png', 'image/png'));
    fireEvent.click(await within(lista()).findByRole('button', { name: 'Quitar logo.png' }));
    await waitFor(() => expect(alCambiar).toHaveBeenLastCalledWith([], { subiendo: true }));
    await act(async () => terminar());
    await waitFor(() => expect(alCambiar).toHaveBeenLastCalledWith([], { subiendo: false }));
  });

  it('tras quitar, el foco va al «×» del archivo vecino', async () => {
    const { elegir } = dibujar();
    elegir(archivo('a.png', 'image/png'), archivo('b.png', 'image/png'));
    const quitarA = await within(lista()).findByRole('button', { name: 'Quitar a.png' });
    await within(lista()).findByRole('button', { name: 'Quitar b.png' });
    await waitFor(() => expect(screen.getByText('2 de 5', { exact: true })).toBeInTheDocument());
    fireEvent.click(quitarA);
    await waitFor(() => expect(within(lista()).getByRole('button', { name: 'Quitar b.png' })).toHaveFocus());
  });

  it('un anuncio igual al anterior se vuelve a anunciar', async () => {
    const { elegir } = dibujar();
    elegir(archivo('logo.svg', 'image/svg+xml'));
    const primero = screen.getByRole('status').textContent;
    elegir(archivo('logo.svg', 'image/svg+xml'));
    const segundo = screen.getByRole('status').textContent;
    expect(segundo).not.toBe(primero);
    expect(segundo).toMatch(/logo\.svg no se adjuntó/);
  });

  it('una subida que tarda demasiado se explica', () => {
    expect(mensajeSubida({ codigo: 'tiempo_agotado' })).toMatch(/tardó demasiado/);
  });
});
