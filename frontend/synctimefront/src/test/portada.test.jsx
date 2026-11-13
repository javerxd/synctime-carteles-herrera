import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { validarContacto } from '../paginas/publicas/contacto/reglasContacto';
import { textoEstado } from '../paginas/publicas/horario';
import { abrirPreferenciasCookies, analiticaPermitida, leerPreferencia } from '../utilidades/cookies';
import { BannerCookies } from '../estructuras/BannerCookies';
import { FormularioContacto } from '../paginas/publicas/contacto/FormularioContacto';
import { publico } from '../api/publico';

vi.mock('../api/publico', () => ({
  publico: {
    documentosLegales: vi.fn(() => Promise.resolve([{ tipo: 'privacidad', version: '1.0' }, { tipo: 'cookies', version: '1.0' }])),
    enviarContacto: vi.fn(() => Promise.resolve({ recibida: true, mensaje: 'Recibimos tu consulta.' })),
    aceptarCookies: vi.fn(() => Promise.resolve(null)),
    retirarCookies: vi.fn(() => Promise.resolve(null)),
  },
}));

const consultaValida = {
  nombre: 'Pedro Soto', correo: 'pedro@correo.cl', telefono: '', asunto: 'Letrero',
  mensaje: 'Quisiera cotizar un letrero luminoso.', aceptaPrivacidad: true,
};

describe('reglas del formulario de contacto', () => {
  it('una consulta completa es válida, con o sin teléfono', () => {
    expect(validarContacto(consultaValida)).toEqual({});
    expect(validarContacto({ ...consultaValida, telefono: '+56 9 1234 5678' })).toEqual({});
  });

  it('exige la privacidad aceptada y un mensaje de al menos 10 caracteres', () => {
    const errores = validarContacto({ ...consultaValida, aceptaPrivacidad: false, mensaje: 'Hola' });
    expect(Object.keys(errores).sort()).toEqual(['aceptaPrivacidad', 'mensaje']);
  });

  it('rechaza correos con separadores y teléfonos que no son chilenos', () => {
    expect(validarContacto({ ...consultaValida, correo: 'a;b@correo.cl' }).correo).toBeDefined();
    expect(validarContacto({ ...consultaValida, telefono: '123' }).telefono).toMatch(/9 dígitos/);
  });
});

describe('frase del estado del local', () => {
  const cartel = { mensajeEstado: 'Salí a instalar' };

  it('abierto o ausente muestran el mensaje del cartel', () => {
    expect(textoEstado({ motivo: 'abierto', cartel: { mensajeEstado: 'Atendiendo' } })).toBe('Atendiendo');
    expect(textoEstado({ motivo: 'ausente', cartel })).toBe('Salí a instalar');
  });

  it('cerrado dice cuándo abre', () => {
    expect(textoEstado({ motivo: 'fuera_de_horario', proximaApertura: { diaSemana: 2, nombreDia: 'Martes', hora: '09:00:00', esHoy: false } }))
      .toBe('Cerrado. Abrimos el martes a las 9:00.');
    expect(textoEstado({ motivo: 'fuera_de_horario', proximaApertura: { diaSemana: 1, hora: '09:00:00', esHoy: true } }))
      .toBe('Cerrado. Abrimos hoy a las 9:00.');
  });
});

describe('aviso de cookies', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  const mostrar = () => render(<MemoryRouter><BannerCookies /></MemoryRouter>);

  it('aparece sin decisión previa, con la analítica desmarcada y dos acciones del mismo estilo', () => {
    mostrar();
    expect(screen.getByRole('heading', { name: 'Cookies y privacidad' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /analítica/i })).not.toBeChecked();
    const rechazar = screen.getByRole('button', { name: 'Rechazar opcionales' });
    const guardar = screen.getByRole('button', { name: 'Guardar mi elección' });
    expect(rechazar.className).toBe(guardar.className);
  });

  it('rechazar guarda la decisión, no registra nada en la API y no vuelve a aparecer', async () => {
    const { unmount } = mostrar();
    fireEvent.click(screen.getByRole('button', { name: 'Rechazar opcionales' }));
    expect(leerPreferencia()).toMatchObject({ analitica: false });
    expect(analiticaPermitida()).toBe(false);
    expect(publico.aceptarCookies).not.toHaveBeenCalled();
    unmount();
    mostrar();
    expect(screen.queryByRole('heading', { name: 'Cookies y privacidad' })).not.toBeInTheDocument();
  });

  it('aceptar la analítica la registra con la versión vigente', async () => {
    mostrar();
    fireEvent.click(screen.getByRole('checkbox', { name: /analítica/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Guardar mi elección' }));
    expect(analiticaPermitida()).toBe(true);
    await waitFor(() => expect(publico.aceptarCookies).toHaveBeenCalledWith(expect.any(String), '1.0'));
  });

  it('rechazar después de haber aceptado registra el retiro; sin aceptación previa no envía nada', async () => {
    localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: true }));
    mostrar();
    abrirPreferenciasCookies();
    fireEvent.click(await screen.findByRole('button', { name: 'Rechazar opcionales' }));
    await waitFor(() => expect(publico.retirarCookies).toHaveBeenCalledWith(expect.any(String)));
    expect(publico.aceptarCookies).not.toHaveBeenCalled();
  });

  it('al reabrirlo lleva el foco al aviso', async () => {
    localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false }));
    mostrar();
    abrirPreferenciasCookies();
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Cookies y privacidad' })).toHaveFocus());
  });

  it('se puede volver a abrir desde el pie', async () => {
    localStorage.setItem('synctime_cookies', JSON.stringify({ analitica: false }));
    mostrar();
    expect(screen.queryByRole('heading', { name: 'Cookies y privacidad' })).not.toBeInTheDocument();
    abrirPreferenciasCookies();
    expect(await screen.findByRole('heading', { name: 'Cookies y privacidad' })).toBeInTheDocument();
  });
});

describe('formulario de contacto', () => {
  beforeEach(() => vi.clearAllMocks());

  const mostrar = () => render(<MemoryRouter><FormularioContacto /></MemoryRouter>);

  it('sin datos no envía y lleva el foco al primer error', async () => {
    mostrar();
    fireEvent.click(screen.getByRole('button', { name: 'Enviar consulta' }));
    expect(publico.enviarContacto).not.toHaveBeenCalled();
    expect(screen.getByLabelText(/^Nombre/)).toHaveFocus();
    expect(screen.getByText('Escribe tu nombre.')).toBeInTheDocument();
  });

  it('sin la versión vigente de la privacidad no envía nada', async () => {
    publico.documentosLegales.mockImplementation(() => Promise.reject(new Error('sin red')));
    mostrar();
    fireEvent.change(screen.getByLabelText(/^Nombre/), { target: { value: 'Pedro Soto' } });
    fireEvent.change(screen.getByLabelText(/^Correo/), { target: { value: 'pedro@correo.cl' } });
    fireEvent.change(screen.getByLabelText(/^Asunto/), { target: { value: 'Letrero' } });
    fireEvent.change(screen.getByLabelText(/^Mensaje/), { target: { value: 'Quisiera cotizar un letrero luminoso.' } });
    fireEvent.click(screen.getByRole('checkbox', { name: /política de privacidad/i }));
    await waitFor(() => expect(publico.documentosLegales).toHaveBeenCalled());
    fireEvent.click(screen.getByRole('button', { name: 'Enviar consulta' }));
    expect(await screen.findByText(/No pudimos cargar la política de privacidad/)).toBeInTheDocument();
    expect(publico.enviarContacto).not.toHaveBeenCalled();
    publico.documentosLegales.mockImplementation(() => Promise.resolve([{ tipo: 'privacidad', version: '1.0' }, { tipo: 'cookies', version: '1.0' }]));
  });

  it('con la privacidad aceptada envía la consulta con su versión y confirma', async () => {
    mostrar();
    fireEvent.change(screen.getByLabelText(/^Nombre/), { target: { value: 'Pedro Soto' } });
    fireEvent.change(screen.getByLabelText(/^Correo/), { target: { value: 'pedro@correo.cl' } });
    fireEvent.change(screen.getByLabelText(/^Asunto/), { target: { value: 'Letrero' } });
    fireEvent.change(screen.getByLabelText(/^Mensaje/), { target: { value: 'Quisiera cotizar un letrero luminoso.' } });
    const privacidad = screen.getByRole('checkbox', { name: /política de privacidad/i });
    expect(privacidad).not.toBeChecked();
    fireEvent.click(privacidad);
    await waitFor(() => expect(publico.documentosLegales).toHaveBeenCalled());
    fireEvent.click(screen.getByRole('button', { name: 'Enviar consulta' }));

    await waitFor(() => expect(publico.enviarContacto).toHaveBeenCalled());
    expect(publico.enviarContacto.mock.calls[0][0]).toMatchObject({
      nombre: 'Pedro Soto', aceptaPrivacidad: true, versionPrivacidad: '1.0', sitioWeb: '',
    });
    expect(await screen.findByText('Consulta enviada')).toBeInTheDocument();
  });
});
