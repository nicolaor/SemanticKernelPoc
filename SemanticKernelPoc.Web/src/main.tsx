import React from 'react'
import ReactDOM from 'react-dom/client'
import { MsalProvider } from "@azure/msal-react";
import { PublicClientApplication } from "@azure/msal-browser";
import { msalConfig } from './config/authConfig';
import App from './App.tsx'
import './index.css'

const msalInstance = new PublicClientApplication(msalConfig);

// Initialize MSAL before rendering the app
msalInstance.initialize().then(() => {
  console.log('MSAL initialized successfully');
  ReactDOM.createRoot(document.getElementById('root')!).render(
    <React.StrictMode>
      <MsalProvider instance={msalInstance}>
        <App />
      </MsalProvider>
    </React.StrictMode>,
  )
}).catch((error) => {
  console.error('MSAL initialization failed:', error);
  // Still render the app even if MSAL initialization fails
  // This ensures the app doesn't completely break if there are auth issues
  ReactDOM.createRoot(document.getElementById('root')!).render(
    <React.StrictMode>
      <MsalProvider instance={msalInstance}>
        <App />
      </MsalProvider>
    </React.StrictMode>,
  )
});
