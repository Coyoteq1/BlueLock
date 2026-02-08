
import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.tsx'
import { setup } from 'goober';

setup(React.createElement);

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)
