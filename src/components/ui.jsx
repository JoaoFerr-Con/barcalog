// Componentes de interface compartilhados por todas as telas.
// Visual: styles.css (tokens e classes). Regras: cor só para estado;
// números com dígitos fixos; todo controle é um <button>/<a>/<input> real.

import { useState } from "react";

export function Icone({ nome, tamanho, className = "" }) {
  return <span className={`material-symbols-outlined ${className}`} aria-hidden="true" style={tamanho ? { fontSize: tamanho } : undefined}>{nome}</span>;
}

export function CabecalhoPagina({ titulo, subtitulo, children }) {
  return (
    <header className="cabecalho-pagina">
      <div>
        <h2>{titulo}</h2>
        {subtitulo && <p>{subtitulo}</p>}
      </div>
      {children && <div className="cabecalho-pagina__acoes">{children}</div>}
    </header>
  );
}

export function Cartao({ titulo, subtitulo, acao, children, className = "", id, corpo = true }) {
  const idTitulo = id ? `${id}-titulo` : undefined;
  return (
    <section className={`cartao ${className}`} aria-labelledby={idTitulo}>
      {(titulo || acao) && (
        <div className="cartao__cabecalho">
          {titulo && <h3 id={idTitulo}>{titulo}</h3>}
          {acao}
          {subtitulo && <p>{subtitulo}</p>}
        </div>
      )}
      {corpo ? <div className="cartao__corpo">{children}</div> : children}
    </section>
  );
}

const CLASSE_SELO = {
  critico: "selo--critico", alto: "selo--alto", atencao: "selo--atencao", ok: "selo--ok",
  info: "selo--info", previsao: "selo--previsao", operacao: "selo--operacao", neutro: ""
};

export function Selo({ tipo = "neutro", icone, children, titulo }) {
  return (
    <span className={`selo ${CLASSE_SELO[tipo] ?? ""}`} title={titulo}>
      {icone && <Icone nome={icone} />}
      {children}
    </span>
  );
}

/** Ponto de cor + texto: o estado nunca depende só da cor. */
export function Estado({ cor, children }) {
  return (
    <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
      <span className="ponto-estado" style={{ background: cor }} aria-hidden="true" />
      {children}
    </span>
  );
}

/**
 * Indicador com nome, valor, unidade, comparação (com base explícita) e regra
 * de cálculo acessível pelo botão (i).
 * comparacao: { texto: "+2h18", sentido: "pior" | "melhor" | "neutro", base: "vs. 7h06 em julho" }
 */
export function Kpi({ rotulo, valor, unidade, comparacao, nota, regra }) {
  const [verRegra, setVerRegra] = useState(false);
  return (
    <article className="kpi">
      <div className="kpi__topo">
        <h3 className="kpi__rotulo">{rotulo}</h3>
        {regra && (
          <button type="button" className="kpi__ajuda" aria-expanded={verRegra} aria-label={`Como é calculado: ${rotulo}`}
            onClick={() => setVerRegra(v => !v)} title="Como é calculado">
            <Icone nome="info" />
          </button>
        )}
      </div>
      <div className="kpi__valor">{valor}{unidade && <small>{unidade}</small>}</div>
      {(comparacao || nota) && (
        <div className="kpi__nota">
          {comparacao?.texto && <><b className={comparacao.sentido || "neutro"}>{comparacao.texto}</b>{" "}</>}
          {comparacao?.base || nota}
        </div>
      )}
      {verRegra && <p className="kpi__regra">{regra}</p>}
    </article>
  );
}

export function Paginacao({ pagina, totalPaginas, total, tamanho, aoMudar, rotulo = "itens" }) {
  if (!total) return null;
  const inicio = (pagina - 1) * tamanho + 1;
  const fim = Math.min(pagina * tamanho, total);
  const paginas = [];
  const de = Math.max(1, Math.min(pagina - 2, totalPaginas - 4));
  for (let p = de; p <= Math.min(totalPaginas, de + 4); p++) paginas.push(p);
  return (
    <nav className="paginacao num" aria-label="Paginação">
      <span>{inicio.toLocaleString("pt-BR")}–{fim.toLocaleString("pt-BR")} de {total.toLocaleString("pt-BR")} {rotulo}</span>
      <span className="paginacao__paginas">
        <button type="button" className="pagina-btn" aria-label="Página anterior" disabled={pagina <= 1} onClick={() => aoMudar(pagina - 1)}>‹</button>
        {paginas.map(p => (
          <button type="button" key={p} className={`pagina-btn ${p === pagina ? "ativo" : ""}`} aria-current={p === pagina ? "page" : undefined}
            onClick={() => aoMudar(p)}>{p}</button>
        ))}
        <button type="button" className="pagina-btn" aria-label="Próxima página" disabled={pagina >= totalPaginas} onClick={() => aoMudar(pagina + 1)}>›</button>
      </span>
    </nav>
  );
}

export function Abas({ abas, ativa, aoMudar, rotulo }) {
  return (
    <div role="tablist" aria-label={rotulo} className="abas">
      {abas.map(a => (
        <button key={a.id} type="button" role="tab" aria-selected={ativa === a.id} className={`aba-botao ${ativa === a.id ? "ativo" : ""}`}
          onClick={() => aoMudar(a.id)}>
          {a.rotulo}{a.contagem != null && <span className="contagem num">{a.contagem}</span>}
        </button>
      ))}
    </div>
  );
}

export function Segmentos({ opcoes, valor, aoMudar, rotulo }) {
  return (
    <div className="segmentos" role="radiogroup" aria-label={rotulo}>
      {opcoes.map(o => (
        <button key={o.id} type="button" role="radio" aria-checked={valor === o.id} className={valor === o.id ? "ativo" : ""} onClick={() => aoMudar(o.id)}>
          {o.rotulo}
        </button>
      ))}
    </div>
  );
}

export function Campo({ rotulo, children, ajuda, erro, className = "" }) {
  return (
    <label className={`campo ${className}`}>
      <span className="rotulo">{rotulo}</span>
      {children}
      {ajuda && !erro && <span className="ajuda">{ajuda}</span>}
      {erro && <span className="erro">{erro}</span>}
    </label>
  );
}

export function Aviso({ tipo = "info", icone, children }) {
  const padrao = { info: "info", atencao: "warning", critico: "error", ok: "check_circle" }[tipo];
  return (
    <div className={`aviso aviso--${tipo}`} role={tipo === "critico" ? "alert" : "status"}>
      <Icone nome={icone || padrao} />
      <div>{children}</div>
    </div>
  );
}

/** Cores fixas das séries de terminal (diferem em luminosidade, não só no tom). */
export const COR_TERMINAL = { unitapajos: "var(--serie-unitapajos)", tgpm: "var(--serie-tgpm)", hidrovias: "var(--serie-hidrovias)" };
export const HEX_TERMINAL = { unitapajos: "#1F4E88", tgpm: "#E0861A", hidrovias: "#2A9D8F" };
