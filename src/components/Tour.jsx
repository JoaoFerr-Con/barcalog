import { useState, useEffect, useCallback, useMemo } from "react";

const CHAVE_VISTO = "barcalog:tour:visto";

export function tourJaVisto() {
  try { return localStorage.getItem(CHAVE_VISTO) === "1"; } catch { return true; }
}

function marcarTourVisto() {
  try { localStorage.setItem(CHAVE_VISTO, "1"); } catch { /* ignora */ }
}

// Tour guiado com spotlight: destaca um elemento por vez (via atributo
// data-tour="<chave>") com uma "janela" recortada no overlay escuro, e um
// cartão explicando o que aquilo faz. Substitui o antigo botão estático
// "Guia de navegação" por algo que efetivamente mostra onde cada coisa fica.
export default function Tour({ passos, aoTerminar }) {
  const [indice, setIndice] = useState(0);
  const [retangulo, setRetangulo] = useState(null);
  const passo = passos[indice];

  const medir = useCallback(() => {
    const alvo = document.querySelector(`[data-tour="${passo.alvo}"]`);
    if (alvo) setRetangulo(alvo.getBoundingClientRect());
    else setRetangulo(null);
  }, [passo]);

  useEffect(() => {
    medir();
    window.addEventListener("resize", medir);
    return () => window.removeEventListener("resize", medir);
  }, [medir]);

  function encerrar() {
    marcarTourVisto();
    aoTerminar();
  }

  function continuar() {
    if (indice === passos.length - 1) { encerrar(); return; }
    setIndice(i => i + 1);
  }

  const estiloSpotlight = useMemo(() => {
    if (!retangulo) return null;
    const folga = 8;
    return {
      position: "fixed",
      top: retangulo.top - folga,
      left: retangulo.left - folga,
      width: retangulo.width + folga * 2,
      height: retangulo.height + folga * 2,
      borderRadius: 12,
      boxShadow: "0 0 0 9999px rgba(15, 23, 42, 0.62)",
      border: "2px solid var(--ambar-500)",
      zIndex: 1001,
      pointerEvents: "none",
      transition: "top 0.25s ease, left 0.25s ease, width 0.25s ease, height 0.25s ease"
    };
  }, [retangulo]);

  // Cartão fica à direita do alvo se houver espaço, senão abaixo.
  const estiloCartao = useMemo(() => {
    if (!retangulo) {
      return { position: "fixed", top: "50%", left: "50%", transform: "translate(-50%, -50%)" };
    }
    const espacoDireita = window.innerWidth - retangulo.right;
    if (espacoDireita > 340) {
      return { position: "fixed", top: retangulo.top, left: retangulo.right + 20 };
    }
    return { position: "fixed", top: Math.min(retangulo.bottom + 16, window.innerHeight - 200), left: Math.max(20, retangulo.left) };
  }, [retangulo]);

  return (
    <>
      {estiloSpotlight && <div style={estiloSpotlight} />}
      <div style={{ position: "fixed", inset: 0, zIndex: 1000, background: retangulo ? "transparent" : "rgba(15, 23, 42, 0.62)" }} />
      <div className="cartao" style={{ ...estiloCartao, zIndex: 1002, width: 300, animation: "barcalog-toast-in 0.2s ease-out" }}>
        <div className="cartao__corpo" style={{ paddingTop: 18 }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: "var(--ambar-600)", textTransform: "uppercase", letterSpacing: "0.04em", marginBottom: 6 }}>
            {indice + 1} de {passos.length}
          </div>
          <h3 style={{ fontSize: 15, margin: "0 0 6px" }}>{passo.titulo}</h3>
          <p style={{ fontSize: 13, color: "var(--tinta-suave)", lineHeight: 1.5, margin: "0 0 16px" }}>{passo.texto}</p>
          <div style={{ display: "flex", justifyContent: "space-between", gap: 8 }}>
            <button className="botao botao--fantasma" onClick={encerrar} style={{ fontSize: 12.5 }}>Pular</button>
            <button className="botao botao--primario" onClick={continuar} style={{ fontSize: 12.5 }}>
              {indice === passos.length - 1 ? "Concluir" : "Continuar"}
            </button>
          </div>
        </div>
      </div>
    </>
  );
}
