import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App.jsx";
import PortalTransportadora from "./pages/PortalTransportadora.jsx";
import "./styles.css";

// Roteamento simples, sem lib de router: /portal carrega a área externa da
// transportadora, qualquer outra URL carrega o painel interno de operação.
// Isso separa de verdade o "portal externo" do sistema interno, mesmo sem
// backend/autenticação real ainda (ver negativacaoStore.js).
const raiz = window.location.pathname.startsWith("/portal") ? PortalTransportadora : App;

ReactDOM.createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    {React.createElement(raiz)}
  </React.StrictMode>
);
