import { createPortal } from "react-dom";
import {
  obterKpisGerais,
  agruparPorMesDetalhado,
  formatarHoras as fmtH
} from "../data/relatorio.js";
import {
  CAPACIDADE_DIARIA,
  SLA_LIMITE_HORAS,
  JANELAS,
  distribuicaoJanelas,
  outliersEstouroCritico,
  mediaPonderadaPermanencia,
  ritmoOperacional,
  alertasRitmo,
  custoTotalDemurrage,
  heatmapTurnos,
  matrizVolumeMensal,
  top30DiasCompacto,
  alertasSaturacao,
  recomendacoesPrescritivas,
  formatarHoras,
  formatarBRL,
  formatarDataPtBR
} from "../data/metricsEngine.js";

// ====================================================================
// RELATÓRIO EXECUTIVO — 3 PÁGINAS
// Projetado conforme especificação do Arquiteto de Dados / Lead BI.
// Montado fora da árvore visual do app via portal em document.body,
// invisível na tela — só aparece no modo de impressão (@media print).
// ====================================================================
export default function RelatorioImprimivel({ registros, nomeRecorte }) {
  if (!registros || registros.length === 0) return null;

  const kpis = obterKpisGerais(registros);
  const porMes = agruparPorMesDetalhado(registros);
  const maiorVolumeMes = Math.max(...porMes.map(m => m.total), 1);
  const tmpPonderado = mediaPonderadaPermanencia(registros);
  const pctCapacidadeMedia = (kpis.mediaDiaria / CAPACIDADE_DIARIA) * 100;
  const demurrage = custoTotalDemurrage(registros);

  const matriz = matrizVolumeMensal(registros);
  const top30 = top30DiasCompacto(registros);
  const diaMaiorFluxo = top30[0];

  const janelas = distribuicaoJanelas(registros);
  const outliers = outliersEstouroCritico(registros);
  const heat = heatmapTurnos(registros);
  const ritmo = ritmoOperacional(registros);
  const alertasR = alertasRitmo(ritmo);
  const maiorLambda = Math.max(...ritmo.map(r => Math.max(r.lambda, r.mu)), 1);

  const alertasSat = alertasSaturacao(registros);
  const recs = recomendacoesPrescritivas(registros);

  const CORES_PRIORIDADE = { alta: "#DC2626", media: "#F59E0B", baixa: "#22C55E" };

  function corHeat(valor, maximo) {
    if (valor === 0) return "#F8FAFC";
    const intensidade = Math.min(valor / maximo, 1);
    const r = Math.round(11 + (220 - 11) * intensidade);
    const g = Math.round(37 + (37 - 37) * intensidade);
    const b = Math.round(69 + (69 - 69) * intensidade);
    return `rgb(${r},${g},${b})`;
  }

  const conteudo = (
    <div className="relatorio-imprimir">
      {/* ==================== PÁGINA 1 ==================== */}
      <div className="ri-pagina">
        <div className="ri-banner">
          <h1>Relatório Executivo — {nomeRecorte}</h1>
          <p>Análise de Produtividade, Capacidade Operacional e Inteligência Preditiva</p>
        </div>

        <div className="ri-kpis">
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Total Marcações</span>
            <span className="ri-kpi__valor">{kpis.total.toLocaleString("pt-BR")}</span>
            <span className="ri-kpi__nota">{kpis.diasOperados} dias operados</span>
          </div>
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Média Diária</span>
            <span className="ri-kpi__valor">{kpis.mediaDiaria.toFixed(1)}</span>
            <span className="ri-kpi__nota">carretas/dia</span>
          </div>
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Tp Ponderado</span>
            <span className="ri-kpi__valor">{formatarHoras(tmpPonderado)}</span>
            <span className="ri-kpi__nota">Σ(Tp×Vol) / ΣVol</span>
          </div>
          <div className="ri-kpi" style={{ borderColor: pctCapacidadeMedia > 80 ? "#DC2626" : undefined }}>
            <span className="ri-kpi__rotulo">% Capacidade</span>
            <span className="ri-kpi__valor" style={{ color: pctCapacidadeMedia > 80 ? "#DC2626" : undefined }}>{pctCapacidadeMedia.toFixed(1)}%</span>
            <span className="ri-kpi__nota">de {CAPACIDADE_DIARIA.toLocaleString("pt-BR")}/dia</span>
          </div>
          <div className="ri-kpi ri-kpi--alerta">
            <span className="ri-kpi__rotulo">Demurrage Estimado</span>
            <span className="ri-kpi__valor">{formatarBRL(demurrage.custoTotal)}</span>
            <span className="ri-kpi__nota">{demurrage.carretasRetidas} carretas &gt;{SLA_LIMITE_HORAS}h</span>
          </div>
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />1. Matriz Consolidada: Volume por Terminal e Mês</h2>
          <p className="ri-texto">Distribuição mensal por empresa/terminal. Células com fundo escuro indicam os meses de maior concentração.</p>
          <table className="ri-tabela">
            <thead>
              <tr>
                <th>Terminal</th>
                {matriz.meses.map(m => <th key={m.chave}>{m.rotulo}</th>)}
                <th>Total</th>
              </tr>
            </thead>
            <tbody>
              {matriz.empresas.map(empresa => {
                const totalEmpresa = matriz.meses.reduce((s, m) => s + matriz.obterValor(m.chave, empresa), 0);
                const maiorEmpMes = Math.max(...matriz.meses.map(m => matriz.obterValor(m.chave, empresa)), 1);
                return (
                  <tr key={empresa}>
                    <td><b>{empresa}</b></td>
                    {matriz.meses.map(m => {
                      const val = matriz.obterValor(m.chave, empresa);
                      const intensidade = val / maiorEmpMes;
                      return (
                        <td key={m.chave} style={{
                          background: intensidade > 0.85 ? "#0B2545" : intensidade > 0.6 ? "#1E3A5F" : intensidade > 0.3 ? "#D1E0F0" : undefined,
                          color: intensidade > 0.6 ? "#fff" : undefined,
                          fontWeight: intensidade > 0.85 ? 700 : undefined
                        }}>
                          {val.toLocaleString("pt-BR")}
                        </td>
                      );
                    })}
                    <td><b>{totalEmpresa.toLocaleString("pt-BR")}</b></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />2. Capacidade: Volume Real vs. Limite Nominal ({CAPACIDADE_DIARIA.toLocaleString("pt-BR")}/dia)</h2>
          <p className="ri-texto">
            Gráfico mensal com linha de referência da capacidade nominal. Dia de maior fluxo no período:
            {diaMaiorFluxo ? ` ${formatarDataPtBR(diaMaiorFluxo.data)} (${diaMaiorFluxo.empresa}) — ${diaMaiorFluxo.total} carretas.` : " —"}
          </p>
          <div className="ri-grafico" style={{ height: 110, position: "relative" }}>
            {porMes.map(m => (
              <div key={m.chave} className="ri-grafico__col">
                <span className="ri-grafico__valor">{m.total.toLocaleString("pt-BR")}</span>
                <div className="ri-grafico__barra" style={{ height: `${(m.total / maiorVolumeMes) * 100}%` }} />
                <span className="ri-grafico__rotulo">{m.rotulo}</span>
              </div>
            ))}
          </div>
          <table className="ri-tabela" style={{ fontSize: 9 }}>
            <thead>
              <tr><th>Mês</th><th>Total</th><th>Repr.</th><th>Dias</th><th>Média/dia</th><th>% Capacidade</th></tr>
            </thead>
            <tbody>
              {porMes.map(m => (
                <tr key={m.chave}>
                  <td><b>{m.rotulo}</b></td>
                  <td>{m.total.toLocaleString("pt-BR")}</td>
                  <td>{m.representatividade.toFixed(1)}%</td>
                  <td>{m.diasOperados}d</td>
                  <td>{m.mediaDiaria.toFixed(1)}</td>
                  <td style={{ color: m.mediaDiaria > CAPACIDADE_DIARIA * 0.8 ? "#DC2626" : undefined, fontWeight: m.mediaDiaria > CAPACIDADE_DIARIA * 0.8 ? 700 : undefined }}>
                    {((m.mediaDiaria / CAPACIDADE_DIARIA) * 100).toFixed(1)}%
                  </td>
                </tr>
              ))}
              <tr className="ri-tabela__total">
                <td>CONSOLIDADO</td>
                <td>{kpis.total.toLocaleString("pt-BR")}</td>
                <td>100%</td>
                <td>{kpis.diasOperados}d</td>
                <td>{kpis.mediaDiaria.toFixed(1)}</td>
                <td>{pctCapacidadeMedia.toFixed(1)}%</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      {/* ==================== PÁGINA 2 ==================== */}
      <div className="ri-pagina">
        <div className="ri-secao">
          <h2><span className="ri-barra" />3. Diagnóstico de SLA: Distribuição por Janelas de Permanência</h2>
          <p className="ri-texto">
            Categorização de todos os {kpis.total.toLocaleString("pt-BR")} movimentos em janelas de tempo de permanência (D0 a D3, Alerta, Estouro Crítico).
            Veículos com permanência &gt;{SLA_LIMITE_HORAS}h recebem flag automático de infração de SLA.
          </p>
          <div style={{ display: "flex", gap: 8, marginBottom: 10 }}>
            {janelas.map(j => (
              <div key={j.chave} style={{ flex: Math.max(j.pct, 3), background: j.cor, borderRadius: 4, padding: "6px 4px", textAlign: "center", color: "#fff", fontSize: 8, fontWeight: 700 }}>
                {j.pct >= 3 && <>{j.rotulo}<br/>{j.pct.toFixed(1)}%</>}
              </div>
            ))}
          </div>
          <table className="ri-tabela" style={{ fontSize: 9 }}>
            <thead><tr><th>Janela</th><th>Qtd</th><th>%</th><th>Indicador</th></tr></thead>
            <tbody>
              {janelas.map(j => (
                <tr key={j.chave} style={{ background: j.chave === "EC" ? "#FDECEA" : undefined }}>
                  <td><span style={{ display: "inline-block", width: 8, height: 8, borderRadius: 2, background: j.cor, marginRight: 6 }} />{j.rotulo}</td>
                  <td>{j.total.toLocaleString("pt-BR")}</td>
                  <td><b>{j.pct.toFixed(1)}%</b></td>
                  <td>{j.chave === "EC" ? "⚠ INFRAÇÃO SLA" : j.chave === "AL" ? "Atenção" : "OK"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div style={{ display: "flex", gap: 12 }}>
          <div style={{ flex: 1 }}>
            <div className="ri-secao">
              <h2><span className="ri-barra" />4. Heatmap de Retenções: Turno × Dia da Semana</h2>
              <p className="ri-texto">Cruzamento hora do dia × dia da semana para marcações com permanência &gt;24h. Células mais escuras = maior concentração de retenções.</p>
              <table className="ri-tabela" style={{ fontSize: 8 }}>
                <thead>
                  <tr><th>Turno</th>{heat.diasRotulos.map(d => <th key={d}>{d}</th>)}</tr>
                </thead>
                <tbody>
                  {heat.grade.map(turno => (
                    <tr key={turno.rotulo}>
                      <td><b>{turno.rotulo}</b></td>
                      {turno.dias.map((val, i) => (
                        <td key={i} style={{ background: corHeat(val, heat.maiorValor), color: val / heat.maiorValor > 0.5 ? "#fff" : "#1E293B", textAlign: "center", fontWeight: val / heat.maiorValor > 0.7 ? 700 : 400 }}>
                          {val}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div style={{ flex: 1 }}>
            <div className="ri-secao">
              <h2><span className="ri-barra" />5. Outliers Críticos (&gt;{SLA_LIMITE_HORAS}h)</h2>
              <p className="ri-texto">Veículos com maior retenção acima do limiar de SLA.</p>
              {outliers.length === 0 ? (
                <p className="ri-texto" style={{ fontStyle: "italic" }}>Nenhum registro com permanência acima de {SLA_LIMITE_HORAS}h no período.</p>
              ) : (
                <table className="ri-tabela" style={{ fontSize: 8 }}>
                  <thead><tr><th>Senha</th><th>Convênio</th><th>Duração</th></tr></thead>
                  <tbody>
                    {outliers.slice(0, 10).map(m => (
                      <tr key={`${m.empresaId}-${m.id}`} style={{ background: "#FDECEA" }}>
                        <td>{m.senha}</td>
                        <td>{m.convenio}</td>
                        <td><b>{formatarHoras(m.esperaHoras)}</b></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />6. Ritmo Operacional: Taxa de Chegada (λ) vs. Descarga (μ)</h2>
          <p className="ri-texto">
            λ = carretas/hora (entrada), μ = carretas/hora (saída). Faixas vermelhas = λ &gt; μ (acúmulo de fila).
            {alertasR.length > 0 && ` Alerta: ${alertasR.length} sequência(s) de ≥2h consecutivas com λ > μ.`}
          </p>
          <div className="ri-grafico" style={{ height: 80 }}>
            {ritmo.map(r => (
              <div key={r.hora} className="ri-grafico__col" style={{ position: "relative" }}>
                <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "flex-end", height: "100%", width: "100%", position: "relative" }}>
                  <div style={{ position: "absolute", bottom: 0, left: "15%", width: "35%", height: `${(r.lambda / maiorLambda) * 100}%`, background: r.congestionado ? "#DC2626" : "#0B2545", borderRadius: "2px 2px 0 0", opacity: 0.85 }} />
                  <div style={{ position: "absolute", bottom: 0, right: "15%", width: "35%", height: `${(r.mu / maiorLambda) * 100}%`, background: "#22C55E", borderRadius: "2px 2px 0 0", opacity: 0.7 }} />
                </div>
                <span className="ri-grafico__rotulo" style={{ fontSize: 7 }}>{r.hora}h</span>
              </div>
            ))}
          </div>
          <div style={{ display: "flex", gap: 16, fontSize: 8, color: "#64748B" }}>
            <span><span style={{ display: "inline-block", width: 8, height: 8, background: "#0B2545", borderRadius: 2, marginRight: 4 }} />λ (chegadas)</span>
            <span><span style={{ display: "inline-block", width: 8, height: 8, background: "#22C55E", borderRadius: 2, marginRight: 4 }} />μ (saídas)</span>
            <span><span style={{ display: "inline-block", width: 8, height: 8, background: "#DC2626", borderRadius: 2, marginRight: 4 }} />λ &gt; μ (congestionado)</span>
          </div>
        </div>
      </div>

      {/* ==================== PÁGINA 3 ==================== */}
      <div className="ri-pagina">
        <div className="ri-secao">
          <h2><span className="ri-barra" />7. Top 30 Dias com Maior Volume (por período e terminal)</h2>
          <p className="ri-texto">
            Ranking dos 30 dias mais movimentados. % Capacidade = volume do dia ÷ {CAPACIDADE_DIARIA.toLocaleString("pt-BR")}.
            Linhas com fundo vermelho indicam utilização &gt;80%.
          </p>
          <table className="ri-tabela" style={{ fontSize: 8.5 }}>
            <thead><tr><th>#</th><th>Data</th><th>Terminal</th><th>Volume</th><th>% Capacidade</th><th>Status</th></tr></thead>
            <tbody>
              {top30.map((d, i) => (
                <tr key={`${d.data}-${d.empresa}`} style={{ background: d.gargalo ? "#FDECEA" : undefined }}>
                  <td>#{i + 1}</td>
                  <td>{formatarDataPtBR(d.data)}</td>
                  <td>{d.empresa}</td>
                  <td><b>{d.total.toLocaleString("pt-BR")}</b></td>
                  <td style={{ color: d.pctCapacidade > 80 ? "#DC2626" : undefined, fontWeight: d.pctCapacidade > 80 ? 700 : undefined }}>
                    {d.pctCapacidade.toFixed(1)}%
                  </td>
                  <td>{d.gargalo ? "⚠ Gargalo" : "OK"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />8. Alertas Preditivos (Projeção 7 Dias)</h2>
          <p className="ri-texto">Projeção de saturação com base na tendência dos últimos 7 dias operados.</p>
          {alertasSat.map((a, i) => (
            <div key={i} className="ri-destaque" style={{
              borderColor: a.tipo === "critico" ? "#DC2626" : a.tipo === "atencao" ? "#F59E0B" : "#22C55E",
              background: a.tipo === "critico" ? "#FDECEA" : a.tipo === "atencao" ? "#FEF3C7" : "#F0FDF4"
            }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <span style={{ fontSize: 16 }}>{a.tipo === "critico" ? "🔴" : a.tipo === "atencao" ? "🟡" : "🟢"}</span>
                <div>
                  <div style={{ fontSize: 11, fontWeight: 700 }}>{a.tipo === "critico" ? "RISCO CRÍTICO" : a.tipo === "atencao" ? "ATENÇÃO" : "ESTÁVEL"}</div>
                  <div style={{ fontSize: 10, color: "#475569" }}>{a.mensagem}</div>
                </div>
              </div>
            </div>
          ))}
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />9. Recomendações Prescritivas</h2>
          <p className="ri-texto">Sugestões automáticas de balanceamento e otimização, derivadas das métricas acima.</p>
          {recs.map((r, i) => (
            <div key={i} style={{ display: "flex", gap: 10, padding: "8px 0", borderBottom: "1px solid #E2E8F0" }}>
              <span style={{
                display: "inline-block", width: 8, height: 8, borderRadius: "50%", marginTop: 5, flexShrink: 0,
                background: CORES_PRIORIDADE[r.prioridade]
              }} />
              <div>
                <div style={{ fontSize: 10.5, fontWeight: 700 }}>{r.acao}</div>
                <div style={{ fontSize: 9.5, color: "#475569" }}>{r.detalhe}</div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );

  return createPortal(conteudo, document.body);
}
