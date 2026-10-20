import { useEffect } from "react";
import { createPortal } from "react-dom";
import { duracao, numero, percentual, mesCurto, dataCurta, dataHoraLocal, variacaoDuracao, variacaoPct } from "../utils/formatar.js";

// Relatório do período para impressão / "Salvar como PDF" do navegador.
// Usa exatamente os números da Visão Geral (mesma API, mesmo recorte) —
// nada é recalculado nem estimado aqui. Regras em docs/INDICADORES.md.

export default function RelatorioPeriodo({ aoFechar, periodo, comparacao, terminal, resumo, terminais, mensal, alertas, frescor }) {
  useEffect(() => {
    const fechar = () => aoFechar();
    window.addEventListener("afterprint", fechar);
    const t = setTimeout(() => window.print(), 150);
    return () => { clearTimeout(t); window.removeEventListener("afterprint", fechar); };
  }, [aoFechar]);

  const a = resumo.atual;
  const b = resumo.anterior;
  const limite = resumo.limiteEsperaLongaHoras;
  const porMes = Object.values(mensal.reduce((acc, m) => {
    const l = (acc[m.mes] ||= { mes: m.mes, total: 0, parcial: false });
    l.total += m.movimentacoes;
    l.parcial ||= m.parcial;
    return acc;
  }, {})).sort((x, y) => x.mes.localeCompare(y.mes));
  const maior = Math.max(1, ...porMes.map(m => m.total));

  const kpis = [
    ["Movimentações", numero(a.movimentacoes), b ? `${variacaoPct(a.movimentacoes, b.movimentacoes) ?? ""} vs. ${numero(b.movimentacoes)}` : `${numero(a.mediaDiaria)}/dia`],
    ["Espera mediana", duracao(a.esperaMediana), b ? `${variacaoDuracao(a.esperaMediana, b.esperaMediana) ?? ""} vs. ${duracao(b.esperaMediana)}` : "metade esperou menos"],
    ["Espera P90", duracao(a.esperaP90), b ? `vs. ${duracao(b.esperaP90)}` : "10% esperaram mais"],
    ["Liberadas ≤ 24 h", percentual(a.liberadasAte24hPct), b ? `vs. ${percentual(b.liberadasAte24hPct)}` : "das liberadas"],
    [`Esperas > ${limite} h`, numero(a.esperasLongas), b ? `vs. ${numero(b.esperasLongas)}` : "carretas"]
  ];

  return createPortal(
    <div className="relatorio-imprimir">
      <div className="ri-pagina">
        <div className="ri-banner">
          <h1>BarcaLog — Fluxo de carretas · {periodo}</h1>
          <p>{terminal || "Todos os terminais"}{comparacao ? ` · comparação com ${comparacao}` : ""} · gerado em {dataHoraLocal(new Date().toISOString())}</p>
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />Indicadores do período</h2>
          <div className="ri-kpis">
            {kpis.map(([r, v, n]) => (
              <div key={r} className="ri-kpi"><span className="ri-kpi__rotulo">{r}</span><span className="ri-kpi__valor">{v}</span><span className="ri-kpi__nota">{n}</span></div>
            ))}
          </div>
          <p className="ri-texto">
            Período pelas datas de marcação (horário do porto). Espera = liberação − marcação, só para carretas já liberadas
            ({numero(a.liberadas)} de {numero(a.movimentacoes)}{a.semLiberacao ? `; ${numero(a.semLiberacao)} sem liberação` : ""}).
            P90 por interpolação linear (igual ao PERCENTIL.INC do Excel).
          </p>
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />Comparação entre terminais</h2>
          <table className="ri-tabela">
            <thead><tr><th>Terminal</th><th>Movimentações</th><th>Média/dia</th><th>Pico diário</th><th>Espera mediana</th><th>P90</th><th>Liberadas ≤ 24 h</th><th>Esperas &gt; {limite} h</th></tr></thead>
            <tbody>
              {terminais.map(t => (
                <tr key={t.terminalId}>
                  <td>{t.terminal}</td><td>{numero(t.atual.movimentacoes)}</td><td>{numero(t.atual.mediaDiaria)}</td>
                  <td>{t.picoDiario ? `${numero(t.picoDiario)} (${dataCurta(t.picoData)})` : "—"}</td>
                  <td>{duracao(t.atual.esperaMediana)}</td><td>{duracao(t.atual.esperaP90)}</td>
                  <td>{percentual(t.atual.liberadasAte24hPct)}</td><td>{numero(t.atual.esperasLongas)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />Análise de Volume — Histórico</h2>
          <div className="ri-grafico">
            {porMes.map(m => (
              <div key={m.mes} className="ri-grafico__col">
                <span className="ri-grafico__valor">{numero(m.total)}</span>
                <div className="ri-grafico__barra" style={{ height: `${(m.total / maior) * 100}%`, opacity: m.parcial ? 0.5 : 1 }} />
                <span className="ri-grafico__rotulo">{mesCurto(m.mes)}{m.parcial ? "*" : ""}</span>
              </div>
            ))}
          </div>
          <p className="ri-texto">Movimentações registradas por mês de marcação. * mês parcial (os dados não cobrem o mês inteiro). Histórico — não é projeção.</p>
        </div>

        {alertas.length > 0 && (
          <div className="ri-secao">
            <h2><span className="ri-barra" />Alertas abertos na emissão</h2>
            <table className="ri-tabela">
              <thead><tr><th>Tipo</th><th>Prioridade</th><th>Terminal</th><th>Alerta</th><th>Evidência</th></tr></thead>
              <tbody>
                {alertas.map(al => (
                  <tr key={al.id}><td>{{ Historico: "Histórico", Operacional: "Operacional", Previsao: "Previsão", Dados: "Dados" }[al.tipo]}</td><td>{al.prioridade === "Media" ? "Média" : al.prioridade}</td><td>{al.terminal || "Todos"}</td><td>{al.titulo}</td><td>{al.evidencia}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="ri-secao">
          <h2><span className="ri-barra" />Fonte e atualização dos dados</h2>
          <p className="ri-texto">
            Planilhas de marcação e liberação dos terminais importadas no BarcaLog. Dados disponíveis até:{" "}
            {frescor.map(f => `${f.terminal} ${f.ultimaMarcacao ? dataCurta(f.ultimaMarcacao) : "—"}`).join(" · ")}.
            Capacidade dos terminais e custos não entram neste relatório quando não estão cadastrados.
          </p>
        </div>
      </div>
    </div>,
    document.body
  );
}
