import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import esriConfig from '@arcgis/core/config';
import './app.css';
import { App } from './App';

esriConfig.apiKey = import.meta.env.VITE_ARCGIS_API_KEY ?? '';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
