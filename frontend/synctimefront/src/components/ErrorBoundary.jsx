import { Component } from 'react';

/**
 * ErrorBoundary
 * —————————————
 * Si un componente hijo falla al dibujarse, React desmonta toda la aplicación y deja la pantalla
 * en blanco. Este límite contiene el fallo: muestra un aviso en su lugar y el resto sigue en pie.
 *
 * - `resetKey`: al cambiar (por ejemplo, la ruta) se descarta el error y se vuelve a intentar.
 * - El aviso no muestra el mensaje del error: puede contener datos internos. React ya lo registra
 *   en la consola para quien desarrolla.
 *
 * Solo captura errores de dibujado. Los errores de peticiones o eventos se siguen manejando donde ocurren.
 */
export default class ErrorBoundary extends Component {
  state = { fallo: false, clave: this.props.resetKey };

  static getDerivedStateFromError() {
    return { fallo: true };
  }

  // Un cambio de resetKey descarta el error en el mismo render, sin pasar por una pantalla de aviso intermedia.
  static getDerivedStateFromProps(props, state) {
    return props.resetKey === state.clave ? null : { fallo: false, clave: props.resetKey };
  }

  reintentar = () => this.setState({ fallo: false });

  render() {
    if (!this.state.fallo) return this.props.children;

    // `recargar` es para el límite exterior: ahí ya no hay menú y no se recupera solo al navegar.
    const { recargar } = this.props;
    return (
      <div className="alert alert-danger" role="alert">
        <div>
          <strong>No se pudo mostrar {recargar ? 'la aplicación' : 'esta pantalla'}.</strong> Ocurrió un error
          inesperado. {recargar ? 'Recargue la página para continuar.' : 'Puede reintentar o ir a otra sección del menú.'}
        </div>
        <button
          type="button"
          className="btn btn-secondary btn-sm"
          onClick={recargar ? () => window.location.reload() : this.reintentar}
          style={{ marginTop: 'var(--space-2)' }}
        >
          {recargar ? 'Recargar' : 'Reintentar'}
        </button>
      </div>
    );
  }
}
