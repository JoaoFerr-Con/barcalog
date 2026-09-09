import { createPortal } from "react-dom";
import {
  obterKpisGerais,
  agruparPorMesDetalhado,
  formatarHoras as fmtH
} from "../data/relatorio.js";
import {
  CAPACIDADE_DIARIA,
  SLA_LIMITE_HORAS,
  distribuicaoJanelas,
  outliersEstouroCritico,
  mediaPonderadaPermanencia,
  ritmoOperacional,
  alertasRitmo,
  custoTotalDemurrage,
  concentracaoPorTurno,
  horariosCriticos,
  matrizVolumeMensal,
  top30DiasCompacto,
  alertasSaturacao,
  recomendacoesPrescritivas,
  custoAmpliadoGargalo,
  fatoresUtilizacaoCriticos,
  slaPorEtapa,
  simulacaoROI,
  impactoESG,
  formatarHoras,
  formatarBRL,
  formatarDataPtBR
} from "../data/metricsEngine.js";

// ====================================================================
// RELATÓRIO EXECUTIVO — ESTRITAMENTE 3 PÁGINAS
// 100% em português, sem jargões em inglês ou símbolos acadêmicos.
// Projetado para clareza em impressão/PDF, tom institucional.
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
  const turnos = concentracaoPorTurno(registros);
  const criticos = horariosCriticos(registros);
  const ritmo = ritmoOperacional(registros);
  const alertasR = alertasRitmo(ritmo);
  const maiorLambda = Math.max(...ritmo.map(r => Math.max(r.lambda, r.mu)), 1);

  const alertasSat = alertasSaturacao(registros);
  const recs = recomendacoesPrescritivas(registros);
  const custoAmpliado = custoAmpliadoGargalo(registros);
  const fatoresRho = fatoresUtilizacaoCriticos(registros);
  const etapasSLA = slaPorEtapa(tmpPonderado);
  const roi = simulacaoROI(registros);
  const esg = impactoESG(registros);

  const CORES_PRIORIDADE = { alta: "#DC2626", media: "#F59E0B", baixa: "#22C55E" };

  const conteudo = (
    <div className="relatorio-imprimir">
      {/* ==================== CABEÇALHO + METODOLOGIA ==================== */}
        <div className="ri-banner">
          <h1>Relatório Executivo — {nomeRecorte}</h1>
          <p>Análise de Produtividade, Capacidade Operacional e Inteligência Preditiva</p>
        </div>

        <div className="ri-secao" style={{ marginBottom: 8 }}>
          <h2><span className="ri-barra" />Metodologia e Fonte de Dados</h2>
          <div style={{ display: "flex", gap: 12, fontSize: 9, color: "#475569" }}>
            <div style={{ flex: 1 }}>
              <p className="ri-texto"><b>Base de dados:</b> Registros reais de marcação e liberação de veículos nos terminais portuários de Barcarena/PA, extraídos dos sistemas operacionais dos terminais Unitapajós, TGPM e Hidrovias do Brasil.</p>
              <p className="ri-texto"><b>Período analisado:</b> Dezembro/2025 a Julho/2026 ({kpis.diasOperados} dias operados).</p>
              <p className="ri-texto"><b>Amostra:</b> {kpis.total.toLocaleString("pt-BR")} movimentos de carga (marcação → liberação), representando a totalidade dos registros disponíveis no período.</p>
            </div>
            <div style={{ flex: 1 }}>
              <p className="ri-texto"><b>Métricas derivadas:</b> Tempo de permanência (Tp), janelas de operação (D0–D3), taxa de chegada/liberação por hora, custo de retenção (base ANTT: R$600/dia), fator de utilização (ρ) e projeções de tendência por regressão linear.</p>
              <p className="ri-texto"><b>Limitações:</b> O dataset não contém timestamps por etapa interna (triagem, pesagem, tombador) — a decomposição por etapa na seção 12 é estimada proporcionalmente. Projeções de saturação assumem continuidade da tendência recente e não consideram sazonalidade de safra.</p>
            </div>
          </div>
        </div>

      {/* ==================== PÁGINA 1 ==================== */}
      {/* VISÃO EXECUTIVA E CAPACIDADE OPERACIONAL */}

        <div className="ri-kpis">
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Total de Marcações</span>
            <span className="ri-kpi__valor">{kpis.total.toLocaleString("pt-BR")}</span>
            <span className="ri-kpi__nota">{kpis.diasOperados} dias operados</span>
          </div>
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Média Diária</span>
            <span className="ri-kpi__valor">{kpis.mediaDiaria.toFixed(1)}</span>
            <span className="ri-kpi__nota">carretas/dia</span>
          </div>
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Tempo Ponderado de Permanência</span>
            <span className="ri-kpi__valor">{formatarHoras(tmpPonderado)}</span>
            <span className="ri-kpi__nota">ponderado pelo volume diário</span>
          </div>
          <div className="ri-kpi" style={{ borderColor: pctCapacidadeMedia > 80 ? "#DC2626" : undefined }}>
            <span className="ri-kpi__rotulo">Utilização da Capacidade</span>
            <span className="ri-kpi__valor" style={{ color: pctCapacidadeMedia > 80 ? "#DC2626" : undefined }}>{pctCapacidadeMedia.toFixed(1)}%</span>
            <span className="ri-kpi__nota">de {CAPACIDADE_DIARIA.toLocaleString("pt-BR")}/dia</span>
          </div>
          <div className="ri-kpi ri-kpi--alerta">
            <span className="ri-kpi__rotulo">Custo Estimado de Retenção</span>
            <span className="ri-kpi__valor">{formatarBRL(demurrage.custoTotal)}</span>
            <span className="ri-kpi__nota">{demurrage.carretasRetidas} carretas retidas além de {SLA_LIMITE_HORAS}h</span>
          </div>
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />1. Matriz Consolidada: Volume por Terminal e Mês</h2>
          <p className="ri-texto">Distribuição mensal por empresa/terminal. Células com fundo escuro indicam os meses de maior concentração de tráfego.</p>
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
          <h2><span className="ri-barra" />2. Volume Real vs. Capacidade Nominal ({CAPACIDADE_DIARIA.toLocaleString("pt-BR")} carretas/dia)</h2>
          <p className="ri-texto">
            Dia de maior fluxo no período: {diaMaiorFluxo ? `${formatarDataPtBR(diaMaiorFluxo.data)} (${diaMaiorFluxo.empresa}) — ${diaMaiorFluxo.total} carretas.` : "—"}
          </p>
          <div className="ri-grafico" style={{ height: 100 }}>
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
              {porMes.map(m => {
                const pctCap = (m.mediaDiaria / CAPACIDADE_DIARIA) * 100;
                return (
                  <tr key={m.chave}>
                    <td><b>{m.rotulo}</b></td>
                    <td>{m.total.toLocaleString("pt-BR")}</td>
                    <td>{m.representatividade.toFixed(1)}%</td>
                    <td>{m.diasOperados}d</td>
                    <td>{m.mediaDiaria.toFixed(1)}</td>
                    <td style={{ color: pctCap > 80 ? "#DC2626" : undefined, fontWeight: pctCap > 80 ? 700 : undefined }}>
                      {pctCap.toFixed(1)}%
                    </td>
                  </tr>
                );
              })}
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

      {/* ==================== PÁGINA 2 ==================== */}
      {/* DIAGNÓSTICO DE SLA, RITMO E CASOS CRÍTICOS */}
        <div className="ri-secao">
          <h2><span className="ri-barra" />3. Distribuição por Janelas de Permanência</h2>
          <p className="ri-texto">
            Categorização dos {kpis.total.toLocaleString("pt-BR")} movimentos por tempo de permanência.
            Veículos com permanência acima de {SLA_LIMITE_HORAS}h recebem sinalização automática de estouro de tempo.
          </p>
          <div style={{ display: "flex", gap: 6, marginBottom: 8 }}>
            {janelas.map(j => (
              <div key={j.chave} style={{ flex: Math.max(j.pct, 2), background: j.cor, borderRadius: 4, padding: "5px 3px", textAlign: "center", color: "#fff", fontSize: 7.5, fontWeight: 700 }}>
                {j.pct >= 4 && <>{j.rotulo.split("(")[0].trim()}<br/>{j.pct.toFixed(1)}%</>}
              </div>
            ))}
          </div>
          <table className="ri-tabela" style={{ fontSize: 9 }}>
            <thead><tr><th>Janela</th><th>Quantidade</th><th>Percentual</th><th>Situação</th></tr></thead>
            <tbody>
              {janelas.map(j => (
                <tr key={j.chave} style={{ background: j.chave === "EC" ? "#FDECEA" : undefined }}>
                  <td><span style={{ display: "inline-block", width: 8, height: 8, borderRadius: 2, background: j.cor, marginRight: 6 }} />{j.rotulo}</td>
                  <td>{j.total.toLocaleString("pt-BR")}</td>
                  <td><b>{j.pct.toFixed(1)}%</b></td>
                  <td>{j.chave === "EC" ? "⚠ ESTOURO DE TEMPO" : j.chave === "AL" ? "Atenção" : "Regular"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div style={{ display: "flex", gap: 12 }}>
          <div style={{ flex: 1 }}>
            <div className="ri-secao">
              <h2><span className="ri-barra" />4. Casos Críticos de Estouro ({'>'}144h)</h2>
              <p className="ri-texto">Veículos com maior tempo retido acima do limite de SLA.</p>
              {outliers.length === 0 ? (
                <p className="ri-texto" style={{ fontStyle: "italic" }}>Nenhum registro acima de {SLA_LIMITE_HORAS}h no período.</p>
              ) : (
                <table className="ri-tabela" style={{ fontSize: 8.5 }}>
                  <thead><tr><th>Senha</th><th>Convênio / Terminal</th><th>Tempo Retido</th></tr></thead>
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

          <div style={{ flex: 1 }}>
            <div className="ri-secao">
              <h2><span className="ri-barra" />5. Concentração de Tráfego por Turno</h2>
              <p className="ri-texto">Percentual de retenção ({'>'}24h) por faixa horária do dia.</p>
              <table className="ri-tabela" style={{ fontSize: 8.5 }}>
                <thead><tr><th>Turno</th><th>Total</th><th>Retidos ({'>'}24h)</th><th>% Retenção</th></tr></thead>
                <tbody>
                  {turnos.map(t => (
                    <tr key={t.rotulo} style={{ background: t.pctRetencao > 20 ? "#FDECEA" : undefined }}>
                      <td><b>{t.rotulo}</b></td>
                      <td>{t.total.toLocaleString("pt-BR")}</td>
                      <td>{t.retidos.toLocaleString("pt-BR")}</td>
                      <td style={{ color: t.pctRetencao > 20 ? "#DC2626" : undefined, fontWeight: t.pctRetencao > 20 ? 700 : undefined }}>
                        {t.pctRetencao.toFixed(1)}%
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              <h2 style={{ marginTop: 10 }}><span className="ri-barra" />Horários Mais Críticos</h2>
              <table className="ri-tabela" style={{ fontSize: 8.5 }}>
                <thead><tr><th>Dia da Semana</th><th>Faixa Horária</th><th>Retenções</th></tr></thead>
                <tbody>
                  {criticos.map((c, i) => (
                    <tr key={i}>
                      <td><b>{c.diaSemana}</b></td>
                      <td>{c.turno}</td>
                      <td><b>{c.total}</b></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />6. Ritmo Operacional: Taxa de Chegada vs. Taxa de Liberação</h2>
          <p className="ri-texto">
            Comparação entre carretas entrando por hora e carretas liberadas por hora, ao longo do dia.
            Faixas em vermelho indicam horários onde a entrada supera a liberação (acúmulo de fila).
            {alertasR.length > 0 && ` Identificadas ${alertasR.length} sequência(s) de 2 ou mais horas consecutivas com acúmulo.`}
          </p>
          <div className="ri-grafico" style={{ height: 70 }}>
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
            <span><span style={{ display: "inline-block", width: 8, height: 8, background: "#0B2545", borderRadius: 2, marginRight: 4 }} />Entrada (carretas/hora)</span>
            <span><span style={{ display: "inline-block", width: 8, height: 8, background: "#22C55E", borderRadius: 2, marginRight: 4 }} />Liberação (carretas/hora)</span>
            <span><span style={{ display: "inline-block", width: 8, height: 8, background: "#DC2626", borderRadius: 2, marginRight: 4 }} />Acúmulo (entrada {'>'} liberação)</span>
          </div>
        </div>

      {/* ==================== PÁGINA 3 ==================== */}
      {/* TOP 30, ALERTAS E RECOMENDAÇÕES — TUDO NUMA SÓ PÁGINA */}
        <div className="ri-secao">
          <h2><span className="ri-barra" />7. Top 30 Dias com Maior Volume (por período e terminal)</h2>
          <p className="ri-texto">
            Ranking dos 30 dias mais movimentados. % Capacidade = volume do dia ÷ {CAPACIDADE_DIARIA.toLocaleString("pt-BR")}.
            Linhas sinalizadas em vermelho indicam utilização acima de 80%.
          </p>
          <table className="ri-tabela" style={{ fontSize: 7.5 }}>
            <thead><tr><th>#</th><th>Data</th><th>Terminal</th><th>Volume</th><th>% Capacidade</th><th>Status</th></tr></thead>
            <tbody>
              {top30.map((d, i) => (
                <tr key={`${d.data}-${d.empresa}`} style={{ background: d.gargalo ? "#FDECEA" : undefined }}>
                  <td>{i + 1}</td>
                  <td>{formatarDataPtBR(d.data)}</td>
                  <td>{d.empresa}</td>
                  <td><b>{d.total}</b></td>
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
          <h2><span className="ri-barra" />8. Alertas Preditivos (Projeção de 7 a 14 dias)</h2>
          <p className="ri-texto">Projeção de tendência com base no ritmo dos últimos 7 dias operados.</p>
          {alertasSat.map((a, i) => (
            <div key={i} className="ri-destaque" style={{
              borderColor: a.tipo === "critico" ? "#DC2626" : a.tipo === "atencao" ? "#F59E0B" : "#22C55E",
              background: a.tipo === "critico" ? "#FDECEA" : a.tipo === "atencao" ? "#FEF3C7" : "#F0FDF4",
              marginBottom: 8
            }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <span style={{ fontSize: 14 }}>{a.tipo === "critico" ? "🔴" : a.tipo === "atencao" ? "🟡" : "🟢"}</span>
                <div>
                  <div style={{ fontSize: 10, fontWeight: 700 }}>{a.tipo === "critico" ? "RISCO CRÍTICO" : a.tipo === "atencao" ? "ATENÇÃO" : "OPERAÇÃO ESTÁVEL"}</div>
                  <div style={{ fontSize: 9, color: "#475569" }}>{a.mensagem}</div>
                </div>
              </div>
            </div>
          ))}
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />9. Recomendações de Ação Preventiva</h2>
          <p className="ri-texto">Sugestões automáticas de balanceamento e escalonamento de janelas, derivadas das métricas operacionais.</p>
          {recs.map((r, i) => (
            <div key={i} style={{ display: "flex", gap: 8, padding: "6px 0", borderBottom: "1px solid #E2E8F0" }}>
              <span style={{
                display: "inline-block", width: 8, height: 8, borderRadius: "50%", marginTop: 4, flexShrink: 0,
                background: CORES_PRIORIDADE[r.prioridade]
              }} />
              <div>
                <div style={{ fontSize: 9.5, fontWeight: 700 }}>{r.acao}</div>
                <div style={{ fontSize: 8.5, color: "#475569" }}>{r.detalhe}</div>
              </div>
            </div>
          ))}
        </div>

      {/* ==================== PÁGINA 4 ==================== */}
      {/* BUSINESS CASE: IMPACTO FINANCEIRO, ROI E ESG */}
        <div className="ri-banner" style={{ background: "linear-gradient(135deg, #7F1D1D, #991B1B)" }}>
          <h1>Business Case — Impacto Financeiro e Ambiental</h1>
          <p>Análise de custo do gargalo, retorno sobre investimento e indicadores ESG</p>
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />10. Custo Ampliado do Gargalo Logístico</h2>
          <p className="ri-texto">
            Além do custo direto de estouro extremo ({formatarBRL(demurrage.custoTotal)}),
            a ineficiência acumulada nas faixas D1 a D3 impõe uma perda estimada de {formatarBRL(custoAmpliado.totalImpacto)} em
            diárias e ociosidade de frota ao ecossistema logístico no período analisado.
            Base de cálculo: R$ 600,00/dia por caminhão parado (referência ANTT), SLA alvo de 12 horas.
          </p>
          <table className="ri-tabela" style={{ fontSize: 8.5 }}>
            <thead><tr><th>Faixa de Permanência</th><th>Veículos</th><th>Tempo Médio Excedente</th><th>Custo/Veículo</th><th>Impacto Total</th></tr></thead>
            <tbody>
              {custoAmpliado.faixas.map(f => (
                <tr key={f.chave} style={{ background: f.chave === "EC" || f.chave === "AL" ? "#FDECEA" : undefined }}>
                  <td><b>{f.rotulo}</b></td>
                  <td>{f.qtd.toLocaleString("pt-BR")}</td>
                  <td>{formatarHoras(f.mediaExcedente)}</td>
                  <td>{formatarBRL(f.custoPorVeiculo)}</td>
                  <td><b>{formatarBRL(f.impactoTotal)}</b></td>
                </tr>
              ))}
              <tr className="ri-tabela__total">
                <td>TOTAL ESTIMADO</td>
                <td>{custoAmpliado.totalVeiculos.toLocaleString("pt-BR")}</td>
                <td></td>
                <td></td>
                <td><b>{formatarBRL(custoAmpliado.totalImpacto)}</b></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div style={{ display: "flex", gap: 12 }}>
          <div style={{ flex: 1 }}>
            <div className="ri-secao">
              <h2><span className="ri-barra" />11. Fator de Utilização nos Horários Críticos</h2>
              <p className="ri-texto">ρ = chegadas/hora ÷ liberações/hora. Quando ρ ≥ 1,0 a fila cresce indefinidamente.</p>
              <table className="ri-tabela" style={{ fontSize: 8 }}>
                <thead><tr><th>Hora</th><th>Chegadas/h</th><th>Liberações/h</th><th>ρ</th><th>Diagnóstico</th></tr></thead>
                <tbody>
                  {fatoresRho.map(f => (
                    <tr key={f.hora} style={{ background: f.rho >= 1.4 ? "#FDECEA" : undefined }}>
                      <td><b>{f.hora}h</b></td>
                      <td>{f.lambda.toFixed(1)}</td>
                      <td>{f.mu.toFixed(1)}</td>
                      <td style={{ color: f.rho >= 1.0 ? "#DC2626" : undefined, fontWeight: 700 }}>{f.rho.toFixed(2)}</td>
                      <td>{f.diagnostico}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div style={{ flex: 1 }}>
            <div className="ri-secao">
              <h2><span className="ri-barra" />12. SLA por Etapa Operacional</h2>
              <p className="ri-texto">Decomposição do Tp de {formatarHoras(tmpPonderado)} e meta com BarcaLog.</p>
              <table className="ri-tabela" style={{ fontSize: 8 }}>
                <thead><tr><th>Etapa</th><th>Atual</th><th>Meta</th></tr></thead>
                <tbody>
                  {etapasSLA.map(e => (
                    <tr key={e.etapa}>
                      <td>{e.etapa}</td>
                      <td><b>{e.tempoAtual}</b></td>
                      <td style={{ color: "#22C55E" }}>{e.slaAlvo}</td>
                    </tr>
                  ))}
                  <tr className="ri-tabela__total">
                    <td>TEMPO TOTAL DE CICLO</td>
                    <td>{formatarHoras(tmpPonderado)}</td>
                    <td style={{ color: "#22C55E" }}>3h 30min</td>
                  </tr>
                </tbody>
              </table>
              <p className="ri-texto" style={{ marginTop: 6, fontWeight: 700 }}>Redução projetada de 75% no tempo de ciclo com a implantação completa.</p>
            </div>
          </div>
        </div>

        <div style={{ display: "flex", gap: 12 }}>
          <div style={{ flex: 1 }}>
            <div className="ri-secao">
              <h2><span className="ri-barra" />13. Simulação de ROI por Cenário</h2>
              <table className="ri-tabela" style={{ fontSize: 8 }}>
                <thead><tr><th>Cenário</th><th>Novo Tp</th><th>h Economizadas</th><th>Economia/Safra</th></tr></thead>
                <tbody>
                  {roi.map(r => (
                    <tr key={r.rotulo}>
                      <td><b>{r.rotulo}</b></td>
                      <td>{r.novoTmp}</td>
                      <td>{r.horasEconomizadas.toFixed(1)}h</td>
                      <td style={{ fontWeight: 700, color: "#22C55E" }}>{formatarBRL(r.economiaSafra)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div style={{ flex: 1 }}>
            <div className="ri-secao">
              <h2><span className="ri-barra" />14. Impacto Ambiental (ESG)</h2>
              <p className="ri-texto">
                Premissa: caminhão pesado em fila consome ~2,5 L/h de diesel (30% do tempo com motor ligado).
              </p>
              <div className="ri-destaque" style={{ background: "#F0FDF4", borderColor: "#22C55E" }}>
                <div>
                  <div style={{ fontSize: 9, fontWeight: 700 }}>Diesel desperdiçado no período</div>
                  <div style={{ fontSize: 16, fontWeight: 700, color: "#0B2545", fontFamily: "'Space Grotesk', sans-serif" }}>
                    ~{(esg.litrosDiesel / 1000).toFixed(0)} mil litros
                  </div>
                </div>
                <div>
                  <div style={{ fontSize: 9, fontWeight: 700 }}>CO₂ emitido desnecessariamente</div>
                  <div style={{ fontSize: 16, fontWeight: 700, color: "#DC2626", fontFamily: "'Space Grotesk', sans-serif" }}>
                    ~{esg.toneladasCO2.toLocaleString("pt-BR")} toneladas
                  </div>
                </div>
              </div>
              <p className="ri-texto" style={{ marginTop: 6, fontStyle: "italic" }}>
                A implementação das janelas dinâmicas do BarcaLog pode evitar a emissão de mais de {esg.toneladasCO2.toLocaleString("pt-BR")} toneladas
                de CO₂ no complexo de Barcarena, alinhando a operação portuária às diretrizes globais de descarbonização.
              </p>
            </div>
          </div>
        </div>

        <div className="ri-secao" style={{ marginTop: 12 }}>
          <h2><span className="ri-barra" />Glossário</h2>
          <table className="ri-tabela" style={{ fontSize: 8 }}>
            <thead><tr><th>Termo</th><th>Definição</th></tr></thead>
            <tbody>
              <tr><td><b>Tp (Tempo de Permanência)</b></td><td>Diferença entre a hora de liberação e a hora de marcação de um veículo no terminal. É o indicador principal de eficiência operacional.</td></tr>
              <tr><td><b>Tp Ponderado</b></td><td>Média do tempo de permanência ponderada pelo volume diário de cada terminal — terminais com mais movimento pesam mais no cálculo.</td></tr>
              <tr><td><b>D0, D1, D2, D3</b></td><td>Janelas de permanência: D0 = 0–24h (regular), D1 = 24–48h, D2 = 48–72h, D3 = 72–96h. Quanto maior a janela, maior o gargalo.</td></tr>
              <tr><td><b>SLA</b></td><td>Service Level Agreement — nível de serviço acordado. No contexto deste relatório, o limiar de 144h (6 dias) além do qual o veículo é considerado em estouro crítico.</td></tr>
              <tr><td><b>ρ (Fator de Utilização)</b></td><td>Razão entre taxa de chegada e taxa de liberação por hora (ρ = λ/μ). Quando ρ ≥ 1,0, a fila cresce indefinidamente; acima de 1,4 é fila exponencial.</td></tr>
              <tr><td><b>λ (Taxa de Chegada)</b></td><td>Média de carretas entrando por hora, calculada sobre o total de dias operados no período.</td></tr>
              <tr><td><b>μ (Taxa de Liberação)</b></td><td>Média de carretas liberadas por hora, mesma base de cálculo de λ.</td></tr>
              <tr><td><b>Demurrage / Retenção</b></td><td>Custo estimado por manter um caminhão parado além do tempo operacional aceitável. Base: R$ 600/dia (referência ANTT).</td></tr>
              <tr><td><b>Capacidade Nominal</b></td><td>Limite de referência de {CAPACIDADE_DIARIA.toLocaleString("pt-BR")} carretas/dia por terminal, usado como parâmetro de saturação.</td></tr>
              <tr><td><b>ESG</b></td><td>Environmental, Social and Governance — indicadores de impacto ambiental, social e de governança corporativa.</td></tr>
            </tbody>
          </table>
          <p className="ri-texto" style={{ marginTop: 8, fontSize: 7.5, color: "#94A3B8" }}>
            Documento gerado automaticamente pelo sistema BarcaLog — Central de Operações Portuárias, Porto de Barcarena/PA.
            Os dados apresentados refletem os registros operacionais reais dos terminais no período indicado. Para dúvidas sobre a metodologia ou os dados,
            entre em contato com a equipe de tecnologia da SEICOMTEC — Prefeitura Municipal de Barcarena.
          </p>
        </div>
    </div>
  );

  return createPortal(conteudo, document.body);
}
