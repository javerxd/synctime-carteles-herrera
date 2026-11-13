import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '../estilos/base.css';
import { Galeria } from './Galeria';

createRoot(document.getElementById('galeria')).render(
  <StrictMode>
    <Galeria />
  </StrictMode>,
);
