import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@fontsource-variable/manrope/wght.css'
import '@fontsource/marcellus/400.css'
import './index.css'
import App from './App.tsx'
import { applyTheme, savedTheme } from './lib/theme'

applyTheme(savedTheme())

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
