// Gráfico de linhas simples em SVG puro (sem lib externa) — usado pra
// comparar séries ao longo de uma mesma escala. Suporta pontos "nulos"
// (undefined/null) dentro de uma série, quebrando a linha em segmentos —
// é o que permite desenhar histórico (sólido) e projeção (tracejado) como
// a mesma série, sem inventar dado onde não tem.
export default function GraficoLinhas({ series, rotulosX, altura = 160, destacarIndices = [] }) {
  const largura = 100;
  const todosValores = series.flatMap(s => s.valores.filter(v => v !== null && v !== undefined));
  const maiorValor = Math.max(1, ...todosValores);
  const n = rotulosX.length;
  const passoX = n > 1 ? largura / (n - 1) : largura;

  function pontosPara(valores) {
    return valores.map((v, i) => {
      if (v === null || v === undefined) return null;
      const x = i * passoX;
      const y = altura - (v / maiorValor) * (altura - 22) - 4;
      return { x, y, v, i };
    });
  }

  // Agrupa pontos consecutivos não-nulos em segmentos separados — cada
  // segmento vira um <path> próprio, então um "buraco" na série não conecta
  // pontos que não deveriam se ligar.
  function segmentos(pontos) {
    const grupos = [];
    let atual = [];
    pontos.forEach(p => {
      if (p === null) {
        if (atual.length) grupos.push(atual);
        atual = [];
      } else {
        atual.push(p);
      }
    });
    if (atual.length) grupos.push(atual);
    return grupos;
  }

  function caminhoSuave(pontos) {
    if (pontos.length < 2) return pontos.length === 1 ? `M ${pontos[0].x} ${pontos[0].y}` : "";
    let d = `M ${pontos[0].x} ${pontos[0].y}`;
    for (let i = 1; i < pontos.length; i++) {
      const p0 = pontos[i - 1];
      const p1 = pontos[i];
      const mx = (p0.x + p1.x) / 2;
      d += ` C ${mx} ${p0.y}, ${mx} ${p1.y}, ${p1.x} ${p1.y}`;
    }
    return d;
  }

  return (
    <div>
      <div style={{ display: "flex", gap: 16, marginBottom: 8, flexWrap: "wrap" }}>
        {series.map(s => (
          <span key={s.nome} style={{ fontSize: 11.5, color: "var(--tinta-suave)", display: "flex", alignItems: "center", gap: 5 }}>
            <span style={{
              width: 14, height: 0, borderTop: `2.5px ${s.tracejado ? "dashed" : "solid"} ${s.cor}`, display: "inline-block"
            }} />
            {s.nome}
          </span>
        ))}
      </div>
      <svg viewBox={`0 0 ${largura} ${altura}`} preserveAspectRatio="none" style={{ width: "100%", height: altura, overflow: "visible" }}>
        {[0.25, 0.5, 0.75, 1].map(f => (
          <line key={f} x1={0} x2={largura} y1={altura - f * (altura - 22) - 4} y2={altura - f * (altura - 22) - 4} stroke="var(--superficie-alt)" strokeWidth="0.3" />
        ))}
        {series.map((s, idx) => {
          const pontosOuNull = pontosPara(s.valores);
          const grupos = segmentos(pontosOuNull);
          const todosPontos = pontosOuNull.filter(p => p !== null);
          return (
            <g key={s.nome}>
              {idx === 0 && grupos.map((g, gi) => (
                <path key={`fill-${gi}`} d={`${caminhoSuave(g)} L ${g[g.length - 1].x} ${altura} L ${g[0].x} ${altura} Z`} fill={s.cor} opacity="0.07" stroke="none" />
              ))}
              {grupos.map((g, gi) => (
                <path key={gi} d={caminhoSuave(g)} fill="none" stroke={s.cor} strokeWidth="1.6" strokeDasharray={s.tracejado ? "3,2" : undefined} vectorEffect="non-scaling-stroke" />
              ))}
              {todosPontos.map((p) => {
                const destacado = destacarIndices.includes(p.i);
                return (
                  <circle key={p.i} cx={p.x} cy={p.y} r={destacado ? 2.8 : 1.6} fill={destacado ? s.cor : "#fff"} stroke={s.cor} strokeWidth="1.4" vectorEffect="non-scaling-stroke">
                    <title>{`${rotulosX[p.i]}: ${p.v.toLocaleString("pt-BR")}`}</title>
                  </circle>
                );
              })}
            </g>
          );
        })}
      </svg>
      <div style={{ display: "flex", justifyContent: "space-between", marginTop: 4 }}>
        {rotulosX.map((r, i) => (
          <span key={i} style={{ fontSize: 9, color: "var(--tinta-fraca)", fontFamily: "'JetBrains Mono', monospace" }}>{i % 2 === 0 || n <= 10 ? r : ""}</span>
        ))}
      </div>
    </div>
  );
}
