import { render, screen, fireEvent } from '@testing-library/react';
import { Boton, Campo, Casilla, Chip, Enlace } from '../../componentes/ui';

describe('Boton', () => {
  it('es de tipo button por defecto y responde al clic', () => {
    const alPulsar = vi.fn();
    render(<Boton onClick={alPulsar}>Guardar</Boton>);
    const boton = screen.getByRole('button', { name: 'Guardar' });
    expect(boton).toHaveAttribute('type', 'button');
    fireEvent.click(boton);
    expect(alPulsar).toHaveBeenCalledTimes(1);
  });

  it('mientras carga queda ocupado, lo dice con texto, conserva el foco y no repite la acción', () => {
    const alPulsar = vi.fn();
    render(<Boton cargando onClick={alPulsar}>Enviar solicitud</Boton>);
    const boton = screen.getByRole('button', { name: /Enviar solicitud/ });
    expect(boton).toHaveAccessibleName('Enviar solicitud (cargando)');
    expect(boton).toHaveAttribute('aria-busy', 'true');
    expect(boton).toHaveAttribute('aria-disabled', 'true');
    expect(boton).not.toBeDisabled();
    boton.focus();
    expect(boton).toHaveFocus();
    fireEvent.click(boton);
    expect(alPulsar).not.toHaveBeenCalled();
  });

  it('deshabilitado no se puede pulsar', () => {
    const alPulsar = vi.fn();
    render(<Boton disabled onClick={alPulsar}>Anular</Boton>);
    fireEvent.click(screen.getByRole('button', { name: 'Anular' }));
    expect(screen.getByRole('button', { name: 'Anular' })).toBeDisabled();
    expect(alPulsar).not.toHaveBeenCalled();
  });

  it('con textoCargando cambia la etiqueta mientras carga', () => {
    render(<Boton cargando textoCargando="Guardando…">Guardar</Boton>);
    expect(screen.getByRole('button')).toHaveAccessibleName('Guardando…');
  });

  it('como enlace deshabilitado deja de ser un enlace navegable', () => {
    render(<Boton como="a" href="/pedidos" disabled>Ver pedidos</Boton>);
    const enlace = screen.getByText('Ver pedidos').closest('a');
    expect(enlace).not.toHaveAttribute('href');
    expect(enlace).toHaveAttribute('aria-disabled', 'true');
    expect(enlace).toHaveAttribute('tabindex', '-1');
  });

  it('puede dibujarse como enlace sin atributo type', () => {
    render(<Boton como="a" href="/vitrina">Ver trabajos</Boton>);
    const enlace = screen.getByRole('link', { name: 'Ver trabajos' });
    expect(enlace).toHaveAttribute('href', '/vitrina');
    expect(enlace).not.toHaveAttribute('type');
  });
});

describe('Enlace', () => {
  it('avisa cuando abre otra pestaña y protege al sitio de origen', () => {
    render(<Enlace href="https://www.openstreetmap.org" externo>Ver mapa</Enlace>);
    const enlace = screen.getByRole('link', { name: /Ver mapa/ });
    expect(enlace).toHaveAttribute('target', '_blank');
    expect(enlace).toHaveAttribute('rel', 'noopener noreferrer');
    expect(enlace).toHaveTextContent('se abre en otra pestaña');
  });
});

describe('Campo', () => {
  it('vincula la etiqueta al campo y marca los obligatorios', () => {
    render(<Campo etiqueta="Nombre" requerido />);
    const campo = screen.getByRole('textbox', { name: 'Nombre' });
    expect(campo).toBeRequired();
  });

  it('anuncia el error al lector de pantalla y lo asocia al campo', () => {
    render(<Campo etiqueta="Correo" type="email" ayuda="Te escribiremos aquí." error="Escribe un correo válido." />);
    const campo = screen.getByRole('textbox', { name: 'Correo' });
    expect(campo).toHaveAttribute('aria-invalid', 'true');
    expect(campo).toHaveAccessibleDescription(/Escribe un correo válido\./);
    expect(campo).toHaveAccessibleDescription(/Te escribiremos aquí\./);
    expect(screen.getByText('Escribe un correo válido.').closest('[aria-live="polite"]')).not.toBeNull();
  });

  it('sin error no marca el campo como inválido', () => {
    render(<Campo etiqueta="Teléfono" />);
    expect(screen.getByRole('textbox', { name: 'Teléfono' })).not.toHaveAttribute('aria-invalid');
  });

  it('dibuja listas desplegables y áreas de texto con la misma etiqueta', () => {
    render(
      <>
        <Campo etiqueta="Estado del pedido" como="select" defaultValue="">
          <option value="">Todos</option>
          <option value="entregado">Entregado</option>
        </Campo>
        <Campo etiqueta="Detalle" como="textarea" />
      </>,
    );
    expect(screen.getByRole('combobox', { name: 'Estado del pedido' })).toHaveDisplayValue('Todos');
    expect(screen.getByRole('option', { name: 'Entregado' })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Detalle' }).tagName).toBe('TEXTAREA');
  });
});

describe('Casilla', () => {
  it('viene desmarcada y se marca al pulsar su etiqueta', () => {
    render(<Casilla etiqueta="Acepto la política de privacidad" />);
    const casilla = screen.getByRole('checkbox', { name: 'Acepto la política de privacidad' });
    expect(casilla).not.toBeChecked();
    fireEvent.click(screen.getByText('Acepto la política de privacidad'));
    expect(casilla).toBeChecked();
  });

  it('asocia su error', () => {
    render(<Casilla etiqueta="Acepto los términos" error="Debes aceptar para continuar." />);
    const casilla = screen.getByRole('checkbox', { name: 'Acepto los términos' });
    expect(casilla).toHaveAttribute('aria-invalid', 'true');
    expect(casilla).toHaveAccessibleDescription(/Debes aceptar para continuar\./);
  });
});

describe('Chip', () => {
  it('es un botón que informa si está seleccionado', () => {
    const alCambiar = vi.fn();
    const { rerender } = render(<Chip seleccionado={false} alCambiar={alCambiar}>Por cobrar</Chip>);
    const chip = screen.getByRole('button', { name: /Por cobrar/ });
    expect(chip).toHaveAttribute('aria-pressed', 'false');
    fireEvent.click(chip);
    expect(alCambiar).toHaveBeenCalledWith(true);
    rerender(<Chip seleccionado alCambiar={alCambiar}>Por cobrar</Chip>);
    const extra = vi.fn();
    rerender(<Chip seleccionado alCambiar={alCambiar} onClick={extra}>Por cobrar</Chip>);
    fireEvent.click(screen.getByRole('button', { name: /Por cobrar/ }));
    expect(extra).toHaveBeenCalledTimes(1);
    expect(alCambiar).toHaveBeenLastCalledWith(false);
    expect(screen.getByRole('button', { name: /Por cobrar/ })).toHaveAttribute('aria-pressed', 'true');
  });
});
