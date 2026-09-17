import { useState, useEffect, useCallback } from "react";
import Aplicativo from "./App.jsx";
import PortalTransportadora from "./pages/PortalTransportadora.jsx";
import ToastHost from "./components/ToastHost.jsx";

// Troca entre o painel interno (equipe do porto) e o Portal do Transportador
// (área externa) sem recarregar a página — usa history.pushState pra manter
// a URL (/  ou /portal) sincronizada, então dá pra linkar/compartilhar cada
// área separadamente mesmo sem uma lib de rotas.
export default function Root() {
  const [modo, setModo] = useState(() =>
    window.location.pathname.startsWith("/portal") ? "portal" : "interno"
  );

  const irPara = useCallback(novoModo => {
    setModo(novoModo);
    const caminho = novoModo === "portal" ? "/portal" : "/";
    window.history.pushState({}, "", caminho);
  }, []);

  useEffect(() => {
    function aoNavegar() {
      setModo(window.location.pathname.startsWith("/portal") ? "portal" : "interno");
    }
    window.addEventListener("popstate", aoNavegar);
    return () => window.removeEventListener("popstate", aoNavegar);
  }, []);

  return (
    <>
      {modo === "portal"
        ? <PortalTransportadora aoVoltarParaInterno={() => irPara("interno")} />
        : <Aplicativo aoAbrirPortal={() => irPara("portal")} />}
      <ToastHost />
    </>
  );
}
