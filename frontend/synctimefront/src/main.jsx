import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './estilos/base.css';
import App from './App.jsx';
import { almacenSesion } from './sesion/sesion';

// Si el navegador tenía sesión, se recupera antes del primer dibujo (el token vive solo en memoria).
almacenSesion.iniciar().catch(() => almacenSesion.cerrar());

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
