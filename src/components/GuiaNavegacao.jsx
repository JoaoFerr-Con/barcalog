export default function GuiaNavegacao({ itens, aoFechar }) {
  return (
    <div
      onClick={aoFechar}
      style={{
        position: "fixed", inset: 0, background: "rgba(15, 23, 42, 0.45)",
        display: "flex", alignItems: "center", justifyContent: "center", zIndex: 1000,
        padding: 20
      }}
    >
      <div
        onClick={e => e.stopPropagation()}
        className="cartao"
        style={{ maxWidth: 560, width: "100%", maxHeight: "80vh", overflow: "hidden", display: "flex", flexDirection: "column" }}
      >
        <div className="cartao__cabecalho" style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
          <div>
            <h3>Guia de navegação</h3>
            <p>O que cada área do BarcaLog faz</p>
          </div>
          <button className="botao botao--fantasma" onClick={aoFechar} style={{ padding: "6px 10px" }}>
            <span className="material-symbols-outlined" style={{ fontSize: 18 }}>close</span>
          </button>
        </div>
        <div className="cartao__corpo" style={{ overflowY: "auto" }}>
          {itens.map(item => (
            <div key={item.chave} style={{ display: "flex", gap: 12, padding: "12px 0", borderBottom: "1px solid var(--superficie-alt)" }}>
              <div style={{
                width: 36, height: 36, borderRadius: 10, background: "var(--azul-100)",
                display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0
              }}>
                <span className="material-symbols-outlined" style={{ fontSize: 19, color: "var(--navio-700)" }}>{item.icone}</span>
              </div>
              <div>
                <div style={{ fontWeight: 700, fontSize: 14 }}>{item.rotulo}</div>
                <div style={{ fontSize: 13, color: "var(--tinta-suave)", marginTop: 2, lineHeight: 1.45 }}>{item.descricao}</div>
              </div>
            </div>
          ))}
          <div style={{ display: "flex", gap: 12, padding: "12px 0" }}>
            <div style={{
              width: 36, height: 36, borderRadius: 10, background: "var(--verde-100)",
              display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0
            }}>
              <span className="material-symbols-outlined" style={{ fontSize: 19, color: "var(--verde-500)" }}>open_in_new</span>
            </div>
            <div>
              <div style={{ fontWeight: 700, fontSize: 14 }}>Portal do Transportador</div>
              <div style={{ fontSize: 13, color: "var(--tinta-suave)", marginTop: 2, lineHeight: 1.45 }}>
                Área externa (em /portal) onde a própria transportadora consulta seu status, vê motivos de negativação e abre contestações no GED — sem precisar de acesso à central interna.
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
