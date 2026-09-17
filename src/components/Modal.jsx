export default function Modal({ titulo, subtitulo, children, aoFechar }) {
  return (
    <div onClick={aoFechar} style={{
      position: "fixed", inset: 0, background: "rgba(15, 23, 42, 0.5)",
      display: "flex", alignItems: "center", justifyContent: "center", zIndex: 1000, padding: 20
    }}>
      <div onClick={e => e.stopPropagation()} className="cartao" style={{
        maxWidth: 700, width: "100%", maxHeight: "85vh", overflow: "hidden", display: "flex", flexDirection: "column"
      }}>
        <div className="cartao__cabecalho" style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
          <div>
            <h3>{titulo}</h3>
            {subtitulo && <p>{subtitulo}</p>}
          </div>
          <button className="botao botao--fantasma" onClick={aoFechar} style={{ padding: "6px 10px" }}>
            <span className="material-symbols-outlined" style={{ fontSize: 18 }}>close</span>
          </button>
        </div>
        <div className="cartao__corpo" style={{ overflowY: "auto" }}>
          {children}
        </div>
      </div>
    </div>
  );
}
