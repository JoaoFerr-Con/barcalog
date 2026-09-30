import { mensagemErro } from "../api/cliente.js";

// Estados padronizados de carregamento, erro e vazio — acessíveis
// (aria-live) e com botão de tentar de novo.
export function Carregando({ texto = "Carregando…" }) {
  return (
    <p role="status" aria-live="polite" style={{ color: "var(--tinta-suave)", fontSize: 13 }}>
      {texto}
    </p>
  );
}

export function ErroCarga({ erro, aoTentarDeNovo }) {
  return (
    <div role="alert" className="cartao" style={{ marginBottom: 16, borderColor: "var(--vermelho-500)" }}>
      <div className="cartao__corpo" style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap", background: "var(--vermelho-100)", borderRadius: 10 }}>
        <span className="material-symbols-outlined" aria-hidden="true" style={{ color: "var(--vermelho-500)" }}>cloud_off</span>
        <span style={{ fontSize: 13, color: "var(--tinta)", flex: 1 }}>{mensagemErro(erro)}</span>
        {aoTentarDeNovo && (
          <button type="button" className="botao botao--fantasma" onClick={aoTentarDeNovo}>Tentar de novo</button>
        )}
      </div>
    </div>
  );
}

export function Vazio({ icone = "inbox", titulo, texto }) {
  return (
    <div style={{ textAlign: "center", color: "var(--tinta-suave)", padding: "28px 12px" }}>
      <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 30, color: "var(--tinta-fraca)" }}>{icone}</span>
      <p style={{ margin: "8px 0 2px", fontWeight: 600, fontSize: 13.5 }}>{titulo}</p>
      {texto && <p style={{ margin: 0, fontSize: 12.5 }}>{texto}</p>}
    </div>
  );
}
