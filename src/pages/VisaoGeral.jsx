import { useState, useMemo } from "react";
import {
  ResponsiveContainer, ComposedChart, LineChart, Line, Area, XAxis, YAxis,
  CartesianGrid, Tooltip, Legend
} from "recharts";
import CartaoIndicador from "../components/CartaoIndicador.jsx";
import SeletorEmpresa from "../components/SeletorEmpresa.jsx";
import Modal from "../components/Modal.jsx";
import GraficoLinhas from "../components/GraficoLinhas.jsx";
import { useRegistrosReais } from "../hooks/useRegistrosReais.js";
import {
  obterKpisGerais, agruparPorMes, agruparPorMesDetalhado, totaisPorEmpresa,
  totaisPorOperador, totaisPorCarga, rankingMaioresEsperas,
  tendenciaSLA, scoreEficienciaPorOperador, atrasosRecorrentes,
  formatarHoras
} from "../data/relatorio.js";
import {
  analisePreditiva, tmaPorTerminal, distribuicaoJanelas, JANELAS,
  CAPACIDADE_DIARIA, picosEntradaSaida, indicadoresPerformance
} from "../data/metricsEngine.js";
import { exportarCSV, exportarPDF } from "../utils/exportar.js";
import { EMPRESAS } from "../data/registry.js";
import RelatorioImprimivel from "../components/RelatorioImprimivel.jsx";

const ABAS = [
  { chave: "resumo", rotulo: "Resumo" },
  { chave: "detalhes", rotulo: "Ranking & Detalhes" },
  { chave: "operacional", rotulo: "Eficiência Operacional" },
  { chave: "analises", rotulo: "Análises" }
];

export default function VisaoGeral() {
  const [empresaId, setEmpresaId] = useState("todas");
  const [aba, setAba] = useState("resumo");
  const [modal, setModal] = useState(null);
  const { registros, carregando } = useRegistrosReais(empresaId);

  const kpis = useMemo(() => obterKpisGerais(registros), [registros]);
  const porMes = useMemo(() => agruparPorMes(registros), [registros]);
  const porMesDet = useMemo(() => agruparPorMesDetalhado(registros), [registros]);
  const porEmpresa = useMemo(() => totaisPorEmpresa(registros), [registros]);
  const porOperador = useMemo(() => totaisPorOperador(registros), [registros]);
  const porCarga = useMemo(() => totaisPorCarga(registros), [registros]);
  const top10 = useMemo(() => rankingMaioresEsperas(registros, 10), [registros]);
  const recentes = useMemo(() => [...registros].sort((a, b) => new Date(b.marcadoEm) - new Date(a.marcadoEm)).slice(0, 8), [registros]);
  const preditiva = useMemo(() => analisePreditiva(registros, 3), [registros]);
  const tma = useMemo(() => tmaPorTerminal(registros), [registros]);
  const sla = useMemo(() => tendenciaSLA(registros), [registros]);
  const scoreOp = useMemo(() => scoreEficienciaPorOperador(registros), [registros]);
  const recorrentes = useMemo(() => atrasosRecorrentes(registros, 24, 3), [registros]);
  const janelas = useMemo(() => distribuicaoJanelas(registros), [registros]);
  const [mesFiltroPico, setMesFiltroPico] = useState("todos");
  const registrosDoMesPico = useMemo(
    () => mesFiltroPico === "todos" ? registros : registros.filter(r => r.marcadoEm.slice(0, 7) === mesFiltroPico),
    [registros, mesFiltroPico]
  );
  const picos = useMemo(() => picosEntradaSaida(registrosDoMesPico), [registrosDoMesPico]);
  const dadosPicos = useMemo(() => picos.entrada.map((h, i) => ({
    hora: h.hora,
    entrada: h.total,
    saida: picos.saida[i]?.total ?? 0
  })), [picos]);
  const desempenho = useMemo(() => indicadoresPerformance(registros), [registros]);
  const dadosPreditiva = useMemo(() => {
    const hist = porMes.map((m, i) => ({
      mes: m.rotulo,
      historico: m.total,
      projecao: i === porMes.length - 1 ? m.total : null,
      banda: null
    }));
    const proj = preditiva.projecoes.map(p => ({
      mes: p.rotulo,
      historico: null,
      projecao: p.total,
      banda: [p.margemInferior, p.margemSuperior]
    }));
    return [...hist, ...proj];
  }, [porMes, preditiva]);

  if (carregando) return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Carregando dados reais…</p>;
  if (!kpis) return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Nenhum registro para essa empresa.</p>;

  const maiorMes = Math.max(...porMes.map(m => m.total), 1);
  const maiorEmpresa = Math.max(...porEmpresa.map(e => e.total), 1);
  const maiorOperador = Math.max(...porOperador.map(o => o.total), 1);
  const maiorCarga = Math.max(...porCarga.map(c => c.total), 1);
  const todosComProjecao = [...porMes, ...preditiva.projecoes];
  const maiorComProj = Math.max(...todosComProjecao.map(m => m.total), 1);
  const pctCapacidade = (kpis.mediaDiaria / CAPACIDADE_DIARIA) * 100;

  function exportarRegistrosCSV() {
    exportarCSV(registros, [
      { rotulo: "Movimento", chave: "id" }, { rotulo: "Senha", chave: "senha" },
      { rotulo: "Convênio", chave: "convenio" }, { rotulo: "Empresa", chave: "empresaNome" },
      { rotulo: "Operador", chave: "operador" }, { rotulo: "Carga", chave: "carga" },
      { rotulo: "Ciclo", chave: "ciclo" },
      { rotulo: "Marcação", valor: r => new Date(r.marcadoEm).toLocaleString("pt-BR") },
      { rotulo: "Liberação", valor: r => new Date(r.liberadoEm).toLocaleString("pt-BR") },
      { rotulo: "Espera (h)", chave: "esperaHoras" }
    ], `marcacoes_${empresaId}`);
  }

  function KpiClicavel({ rotulo, valor, icone, corIcone, corFundoIcone, nota, modalId }) {
    return (
      <div onClick={() => setModal(modalId)} style={{ cursor: "pointer" }}>
        <CartaoIndicador rotulo={rotulo} valor={valor} icone={icone} corIcone={corIcone} corFundoIcone={corFundoIcone} nota={nota} />
      </div>
    );
  }

  return (
    <>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16, flexWrap: "wrap", gap: 12 }}>
        <div className="abas">
          {ABAS.map(a => (
            <button key={a.chave} onClick={() => setAba(a.chave)} className={`aba-botao ${aba === a.chave ? "ativo" : ""}`}>{a.rotulo}</button>
          ))}
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <SeletorEmpresa valor={empresaId} aoMudar={setEmpresaId} />
          <button className="botao botao--fantasma botao-exportar" onClick={exportarRegistrosCSV} title="Exportar CSV">
            <span className="material-symbols-outlined" style={{ fontSize: 16 }}>download</span> CSV
          </button>
          <button className="botao botao--fantasma botao-exportar" onClick={exportarPDF} title="Exportar PDF">
            <span className="material-symbols-outlined" style={{ fontSize: 16 }}>picture_as_pdf</span> PDF
          </button>
        </div>
      </div>

      <div className="grade-kpi">
        <KpiClicavel rotulo="Total de Marcações" valor={kpis.total.toLocaleString("pt-BR")} icone="fact_check" corIcone="var(--navio-700)" corFundoIcone="var(--azul-100)" nota="Período importado" modalId="total" />
        <KpiClicavel rotulo="Média Diária" valor={kpis.mediaDiaria.toFixed(1)} icone="calendar_month" corIcone="var(--ambar-600)" corFundoIcone="#FBEBD1" nota={`${kpis.diasOperados} dias operados`} modalId="media" />
        <KpiClicavel rotulo="Tempo Médio de Espera" valor={formatarHoras(kpis.tempoMedioEspera)} icone="schedule" corIcone="var(--tinta-suave)" corFundoIcone="var(--superficie-alt)" nota="marcação → liberação" modalId="espera" />
        <KpiClicavel rotulo="Mês Mais Movimentado" valor={kpis.mesMaisMovimentado.rotulo} icone="trending_up" corIcone="var(--verde-500)" corFundoIcone="var(--verde-100)" nota={`${kpis.mesMaisMovimentado.total.toLocaleString("pt-BR")} marcações`} modalId="mesMov" />
      </div>

      {/* ===== MODAIS DOS KPIs ===== */}
      {modal === "total" && (
        <Modal titulo="Total de Marcações" subtitulo="Decomposição do volume total por terminal e período" aoFechar={() => setModal(null)}>
          <p style={{ fontSize: 13, color: "var(--tinta-suave)", marginBottom: 12 }}>
            O total de <b>{kpis.total.toLocaleString("pt-BR")}</b> marcações representa todos os movimentos de entrada e saída registrados
            nos terminais no período de {kpis.diasOperados} dias operados. Cada marcação é um veículo que entrou, foi processado e liberado.
          </p>
          {porEmpresa.map(e => (
            <div key={e.empresa} style={{ marginBottom: 10 }}>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
                <span>{e.empresa}</span>
                <b>{e.total.toLocaleString("pt-BR")} ({((e.total / kpis.total) * 100).toFixed(1)}%)</b>
              </div>
              <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}>
                <div style={{ width: `${(e.total / maiorEmpresa) * 100}%`, background: "var(--navio-700)", height: "100%", borderRadius: 6 }} />
              </div>
            </div>
          ))}
        </Modal>
      )}
      {modal === "media" && (
        <Modal titulo="Média Diária" subtitulo="Volume diário médio e utilização da capacidade" aoFechar={() => setModal(null)}>
          <p style={{ fontSize: 13, color: "var(--tinta-suave)", marginBottom: 12 }}>
            A média de <b>{kpis.mediaDiaria.toFixed(1)}</b> carretas/dia representa <b>{pctCapacidade.toFixed(1)}%</b> da capacidade
            nominal de {CAPACIDADE_DIARIA.toLocaleString("pt-BR")} carretas/dia. Acima de 80% é sinal de alerta.
          </p>
          <table style={{ width: "100%", fontSize: 13 }}>
            <thead><tr><th style={{ textAlign: "left" }}>Mês</th><th>Média/dia</th><th>% Capacidade</th></tr></thead>
            <tbody>
              {porMesDet.map(m => (
                <tr key={m.chave}><td><b>{m.rotulo}</b></td><td>{m.mediaDiaria.toFixed(1)}</td><td style={{ color: m.mediaDiaria > CAPACIDADE_DIARIA * 0.8 ? "var(--vermelho-500)" : undefined }}>{((m.mediaDiaria / CAPACIDADE_DIARIA) * 100).toFixed(1)}%</td></tr>
              ))}
            </tbody>
          </table>
        </Modal>
      )}
      {modal === "espera" && (
        <Modal titulo="Tempo Médio de Espera" subtitulo="TMA por terminal e distribuição por janelas" aoFechar={() => setModal(null)}>
          <p style={{ fontSize: 13, color: "var(--tinta-suave)", marginBottom: 14 }}>
            O tempo médio de <b>{formatarHoras(kpis.tempoMedioEspera)}</b> é o intervalo entre a marcação (entrada) e a liberação (saída) de cada veículo.
            {sla && sla.piorou && ` O SLA piorou ${sla.variacaoPct.toFixed(1)}% no último mês comparado aos 3 anteriores.`}
            {sla && sla.melhorou && ` O SLA melhorou ${Math.abs(sla.variacaoPct).toFixed(1)}% no último mês comparado aos 3 anteriores.`}
          </p>
          <h4 style={{ fontSize: 12.5, textTransform: "uppercase", letterSpacing: "0.03em", color: "var(--tinta-fraca)", marginBottom: 10 }}>TMA por Terminal</h4>
          {tma.map(t => {
            const maiorTMA = Math.max(...tma.map(x => x.tmaGeral), 1);
            return (
              <div key={t.terminal} style={{ marginBottom: 10 }}>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 3 }}>
                  <span>{t.terminal}</span>
                  <span><b>{formatarHoras(t.tmaGeral)}</b> {t.variacao !== null && <span style={{ color: t.variacao > 0 ? "var(--vermelho-500)" : "var(--verde-500)", fontSize: 11, fontWeight: 700 }}>({t.variacao > 0 ? "▲" : "▼"} {Math.abs(t.variacao).toFixed(1)}%)</span>}</span>
                </div>
                <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}>
                  <div style={{ width: `${(t.tmaGeral / maiorTMA) * 100}%`, background: "var(--navio-700)", height: "100%", borderRadius: 6 }} />
                </div>
              </div>
            );
          })}
          <h4 style={{ fontSize: 12.5, textTransform: "uppercase", letterSpacing: "0.03em", color: "var(--tinta-fraca)", marginTop: 18, marginBottom: 10 }}>Distribuição por Janelas de Permanência</h4>
          {janelas.map(j => (
            <div key={j.chave} style={{ marginBottom: 8 }}>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12, marginBottom: 3 }}>
                <span><span style={{ display: "inline-block", width: 8, height: 8, borderRadius: 2, background: j.cor, marginRight: 6 }} />{j.rotulo}</span>
                <span><b>{j.total.toLocaleString("pt-BR")}</b> ({j.pct.toFixed(1)}%)</span>
              </div>
              <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 6 }}>
                <div style={{ width: `${Math.max(j.pct, 1)}%`, background: j.cor, height: "100%", borderRadius: 6 }} />
              </div>
            </div>
          ))}
        </Modal>
      )}
      {modal === "mesMov" && (
        <Modal titulo={`Mês Mais Movimentado: ${kpis.mesMaisMovimentado.rotulo}`} subtitulo="Detalhamento do mês de pico" aoFechar={() => setModal(null)}>
          <p style={{ fontSize: 13, color: "var(--tinta-suave)", marginBottom: 14 }}>
            O mês de <b>{kpis.mesMaisMovimentado.rotulo}</b> teve <b>{kpis.mesMaisMovimentado.total.toLocaleString("pt-BR")}</b> marcações,
            representando o maior volume do período — <b>{(((kpis.mesMaisMovimentado.total - kpis.mediaDiaria * 30.4) / (kpis.mediaDiaria * 30.4)) * 100).toFixed(0)}%</b> acima
            da média mensal do período.
          </p>
          <GraficoLinhas
            altura={140}
            rotulosX={porMes.map(m => m.rotulo.replace("/", "\u200a/\u200a"))}
            destacarIndices={[porMes.findIndex(m => m.chave === kpis.mesMaisMovimentado.chave)]}
            series={[{ nome: "Marcações por mês", cor: "var(--ambar-500)", valores: porMes.map(m => m.total) }]}
          />
          <h4 style={{ fontSize: 12.5, textTransform: "uppercase", letterSpacing: "0.03em", color: "var(--tinta-fraca)", marginTop: 18, marginBottom: 8 }}>Top 3 Meses</h4>
          {[...porMes].sort((a, b) => b.total - a.total).slice(0, 3).map((m, i) => (
            <div key={m.chave} style={{ display: "flex", justifyContent: "space-between", padding: "6px 0", borderBottom: "1px solid var(--superficie-alt)", fontSize: 13 }}>
              <span>#{i + 1} {m.rotulo}</span>
              <b>{m.total.toLocaleString("pt-BR")}</b>
            </div>
          ))}
        </Modal>
      )}

      {/* ===== ABA RESUMO ===== */}
      {aba === "resumo" && (
        <div className="grade-painel conteudo-pagina">
          <div className="pilha">
            <div className="cartao" style={{ cursor: "pointer" }} onClick={() => setModal("mesMov")}>
              <div className="cartao__cabecalho">
                <h3>Volume de Marcações por Mês <span className="material-symbols-outlined" style={{ fontSize: 14, verticalAlign: "middle", color: "var(--tinta-fraca)" }}>open_in_new</span></h3>
                <p>Variação percentual mês a mês · clique pra ver o detalhamento do mês de pico</p>
              </div>
              <div className="cartao__corpo">
                <div className="grafico-fluxo">
                  {porMes.map((m, i) => {
                    const anterior = porMes[i - 1];
                    const variacao = anterior && anterior.total >= 100 ? ((m.total - anterior.total) / anterior.total) * 100 : null;
                    return (
                      <div key={m.chave} className="grafico-fluxo__col">
                        <span className="grafico-fluxo__valor">{m.total.toLocaleString("pt-BR")}</span>
                        {variacao !== null && (
                          <span style={{ fontSize: 9.5, fontWeight: 700, color: variacao >= 0 ? "var(--verde-500)" : "var(--vermelho-500)", marginBottom: 2 }}>
                            {variacao >= 0 ? "▲" : "▼"} {Math.abs(variacao).toFixed(0)}%
                          </span>
                        )}
                        <div className="grafico-fluxo__barra" style={{ height: `${(m.total / maiorMes) * 100}%`, background: m.chave === kpis.mesMaisMovimentado.chave ? "var(--ambar-500)" : undefined }} />
                        <span className="grafico-fluxo__rotulo">{m.rotulo}</span>
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>

            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Últimas Marcações</h3>
                <p>Registros mais recentes no recorte selecionado</p>
              </div>
              <div className="cartao__corpo" style={{ overflowX: "auto" }}>
                <table>
                  <thead><tr><th>Movimento</th><th>Convênio</th>{empresaId === "todas" && <th>Empresa</th>}<th>Marcação</th><th>Espera</th></tr></thead>
                  <tbody>
                    {recentes.map(m => (
                      <tr key={`${m.empresaId}-${m.id}`}>
                        <td className="mono">{m.id}</td><td>{m.convenio}</td>
                        {empresaId === "todas" && <td>{m.empresaNome}</td>}
                        <td className="mono">{new Date(m.marcadoEm).toLocaleString("pt-BR")}</td>
                        <td>{formatarHoras(m.esperaHoras)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>

          <div className="pilha">
            <div className="cartao">
              <div className="cartao__cabecalho"><h3>Desempenho por Terminal</h3><p>Volume, participação, TMA e Taxa D0 (≤24h) — com variação vs mês anterior</p></div>
              <div className="cartao__corpo" style={{ overflowX: "auto" }}>
                <table style={{ fontSize: 12.5 }}>
                  <thead>
                    <tr>
                      <th style={{ textAlign: "left" }}>Terminal</th>
                      <th style={{ textAlign: "left" }}>Volume</th>
                      <th>TMA</th>
                      <th>Δ TMA</th>
                      <th>Taxa D0</th>
                      <th>Δ D0</th>
                    </tr>
                  </thead>
                  <tbody>
                    {desempenho.sort((a, b) => b.totalGeral - a.totalGeral).map(d => {
                      const maiorVolumeDesempenho = Math.max(...desempenho.map(x => x.totalGeral), 1);
                      return (
                        <tr key={d.terminal}>
                          <td><b>{d.terminal}</b></td>
                          <td style={{ minWidth: 150 }}>
                            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                              <span style={{ whiteSpace: "nowrap" }}>{d.totalGeral.toLocaleString("pt-BR")}</span>
                              <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 6, flex: 1, minWidth: 40 }}>
                                <div style={{ width: `${(d.totalGeral / maiorVolumeDesempenho) * 100}%`, background: "var(--navio-700)", height: "100%", borderRadius: 6 }} />
                              </div>
                            </div>
                          </td>
                          <td>{formatarHoras(d.tmaAtual)}</td>
                          <td style={{ color: d.deltaTMA === null ? "var(--tinta-fraca)" : d.deltaTMA > 0 ? "var(--vermelho-500)" : "var(--verde-500)", fontWeight: 700 }}>
                            {d.deltaTMA === null ? "—" : `${d.deltaTMA > 0 ? "▲" : "▼"} ${Math.abs(d.deltaTMA).toFixed(1)}%`}
                          </td>
                          <td>{d.taxaD0Atual.toFixed(1)}%</td>
                          <td style={{ color: d.deltaD0 === null ? "var(--tinta-fraca)" : d.deltaD0 >= 0 ? "var(--verde-500)" : "var(--vermelho-500)", fontWeight: 700 }}>
                            {d.deltaD0 === null ? "—" : `${d.deltaD0 >= 0 ? "▲" : "▼"} ${Math.abs(d.deltaD0).toFixed(1)}pp`}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
                <p style={{ fontSize: 11, color: "var(--tinta-fraca)", marginTop: 10 }}>
                  TMA e Taxa D0 referentes ao último mês com dados. Δ TMA vermelho = piorou vs mês anterior. Δ D0 verde = mais veículos liberados dentro de 24h (pp = pontos percentuais).
                </p>
              </div>
            </div>
            <div className="cartao">
              <div className="cartao__cabecalho"><h3>Marcações por Operador</h3></div>
              <div className="cartao__corpo">
                {porOperador.map(o => (
                  <div key={o.operador} style={{ marginBottom: 12 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
                      <span>{o.operador}</span><b>{o.total.toLocaleString("pt-BR")}</b>
                    </div>
                    <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}>
                      <div style={{ width: `${(o.total / maiorOperador) * 100}%`, background: "var(--ambar-500)", height: "100%", borderRadius: 6 }} />
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ===== ABA RANKING & DETALHES ===== */}
      {aba === "detalhes" && (
        <div className="grade-painel conteudo-pagina">
          <div className="pilha">
            <div className="cartao">
              <div className="cartao__cabecalho"><h3>Ranking das Maiores Esperas</h3><p>Movimentos com maior diferença entre marcação e liberação — barra proporcional ao tempo</p></div>
              <div className="cartao__corpo" style={{ overflowX: "auto" }}>
                <table style={{ width: "100%", borderCollapse: "collapse" }}>
                  <thead>
                    <tr style={{ fontSize: 11, color: "var(--tinta-fraca)", textAlign: "left" }}>
                      <th style={{ padding: "0 8px 8px 0", width: 28 }}>#</th>
                      <th style={{ padding: "0 8px 8px 0" }}>Convênio</th>
                      <th style={{ padding: "0 0 8px" }}></th>
                      <th style={{ padding: "0 0 8px 8px", textAlign: "right" }}>Tempo</th>
                    </tr>
                  </thead>
                  <tbody>
                    {top10.map((m, i) => {
                      const maiorEspera = top10[0].esperaHoras;
                      const pctBarra = Math.max(6, (m.esperaHoras / maiorEspera) * 100);
                      const cor = m.esperaHoras > 144 ? "var(--vermelho-500)" : m.esperaHoras > 48 ? "var(--ambar-500)" : "var(--verde-500)";
                      return (
                        <tr key={`${m.empresaId}-${m.id}`} style={{ borderBottom: "1px solid var(--superficie-alt)" }}>
                          <td style={{ padding: "9px 8px 9px 0", fontSize: 13, fontWeight: 700, color: "var(--tinta-fraca)", verticalAlign: "middle" }}>#{i + 1}</td>
                          <td style={{ padding: "9px 8px 9px 0", verticalAlign: "middle", minWidth: 150 }}>
                            <div style={{ fontSize: 12.5, fontWeight: 600 }}>{m.convenio}</div>
                            <div style={{ fontSize: 10.5, color: "var(--tinta-fraca)" }}>
                              Senha {m.senha}{empresaId === "todas" ? ` · ${m.empresaNome}` : ""}
                            </div>
                          </td>
                          <td style={{ padding: "9px 0", verticalAlign: "middle", width: "40%" }}>
                            <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 14, position: "relative", minWidth: 80 }}>
                              <div style={{ width: `${pctBarra}%`, background: cor, height: "100%", borderRadius: 6 }} />
                            </div>
                          </td>
                          <td style={{ padding: "9px 0 9px 8px", textAlign: "right", verticalAlign: "middle", whiteSpace: "nowrap" }}>
                            <div style={{ fontSize: 14, fontWeight: 700, fontFamily: "'Space Grotesk', sans-serif", color: cor }}>{formatarHoras(m.esperaHoras)}</div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
                <div style={{ display: "flex", gap: 14, marginTop: 12, fontSize: 11, color: "var(--tinta-suave)" }}>
                  <span><span style={{ display: "inline-block", width: 8, height: 8, borderRadius: 2, background: "var(--verde-500)", marginRight: 4 }} />até 48h</span>
                  <span><span style={{ display: "inline-block", width: 8, height: 8, borderRadius: 2, background: "var(--ambar-500)", marginRight: 4 }} />48–144h</span>
                  <span><span style={{ display: "inline-block", width: 8, height: 8, borderRadius: 2, background: "var(--vermelho-500)", marginRight: 4 }} />acima de 144h (estouro crítico)</span>
                </div>
              </div>
            </div>
          </div>
          <div className="pilha">
            <div className="cartao">
              <div className="cartao__cabecalho"><h3>Marcações por Tipo de Carga</h3></div>
              <div className="cartao__corpo">
                {porCarga.map(c => (
                  <div key={c.carga} style={{ marginBottom: 12 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}><span>{c.carga}</span><b>{c.total.toLocaleString("pt-BR")}</b></div>
                    <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}><div style={{ width: `${(c.total / maiorCarga) * 100}%`, background: "var(--ambar-500)", height: "100%", borderRadius: 6 }} /></div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ===== ABA EFICIÊNCIA OPERACIONAL ===== */}
      {aba === "operacional" && (
        <div className="grade-painel conteudo-pagina">
          <div className="pilha">
            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Eficiência por Terminal</h3>
                <p>TMA, Taxa D0 (liberados em até 24h) e tendência mensal — ajuda a identificar qual terminal está degradando</p>
              </div>
              <div className="cartao__corpo">
                {desempenho.map(d => {
                  const maiorTMASerie = Math.max(...d.serieTMA.map(s => s.valor), 1);
                  return (
                    <div key={d.terminal} style={{ marginBottom: 18, padding: "14px 16px", borderRadius: 12, background: "var(--superficie-alt)" }}>
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 10 }}>
                        <h4 style={{ margin: 0, fontSize: 14 }}>{d.terminal}</h4>
                        {d.deltaTMA !== null && (
                          <span style={{ fontSize: 12, fontWeight: 700, color: d.deltaTMA > 0 ? "var(--vermelho-500)" : "var(--verde-500)" }}>
                            TMA {d.deltaTMA > 0 ? "▲" : "▼"} {Math.abs(d.deltaTMA).toFixed(1)}% vs mês anterior
                          </span>
                        )}
                      </div>
                      <div style={{ display: "flex", gap: 24, flexWrap: "wrap", marginBottom: 12 }}>
                        <div>
                          <div style={{ fontSize: 10.5, color: "var(--tinta-fraca)", textTransform: "uppercase" }}>TMA Geral</div>
                          <div style={{ fontSize: 17, fontWeight: 700, fontFamily: "'Space Grotesk', sans-serif" }}>{formatarHoras(d.tmaGeral)}</div>
                        </div>
                        <div>
                          <div style={{ fontSize: 10.5, color: "var(--tinta-fraca)", textTransform: "uppercase" }}>Taxa D0 (≤24h)</div>
                          <div style={{ fontSize: 17, fontWeight: 700, color: d.taxaD0Geral > 80 ? "var(--verde-500)" : d.taxaD0Geral > 60 ? "var(--ambar-600)" : "var(--vermelho-500)" }}>{d.taxaD0Geral.toFixed(1)}%</div>
                        </div>
                        <div>
                          <div style={{ fontSize: 10.5, color: "var(--tinta-fraca)", textTransform: "uppercase" }}>Volume Total</div>
                          <div style={{ fontSize: 17, fontWeight: 700 }}>{d.totalGeral.toLocaleString("pt-BR")}</div>
                        </div>
                      </div>
                      <div style={{ fontSize: 10.5, color: "var(--tinta-fraca)", marginBottom: 4 }}>Tendência de TMA por mês</div>
                      <div style={{ display: "flex", alignItems: "flex-end", gap: 3, height: 40 }}>
                        {d.serieTMA.map(s => (
                          <div key={s.mes} title={`${s.mes}: ${formatarHoras(s.valor)}`} style={{ flex: 1, height: `${Math.max(8, (s.valor / maiorTMASerie) * 100)}%`, background: "var(--navio-700)", borderRadius: "2px 2px 0 0" }} />
                        ))}
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>

          <div className="pilha">
            <div className="cartao">
              <div className="cartao__cabecalho"><h3>Distribuição de Janelas por Terminal</h3><p>D0 a Estouro Crítico (&gt;144h), por terminal</p></div>
              <div className="cartao__corpo">
                {EMPRESAS.map(e => {
                  const doTerminal = registros.filter(r => r.empresaId === e.id);
                  if (doTerminal.length === 0) return null;
                  const janelasTerminal = distribuicaoJanelas(doTerminal);
                  return (
                    <div key={e.id} style={{ marginBottom: 14 }}>
                      <div style={{ fontSize: 12.5, fontWeight: 600, marginBottom: 6 }}>{e.nome}</div>
                      <div style={{ display: "flex", gap: 2, borderRadius: 6, overflow: "hidden", height: 18 }}>
                        {janelasTerminal.map(j => (
                          <div key={j.chave} title={`${j.rotulo}: ${j.pct.toFixed(1)}%`} style={{ flex: Math.max(j.pct, 0.5), background: j.cor }} />
                        ))}
                      </div>
                    </div>
                  );
                })}
                <div style={{ display: "flex", gap: 10, flexWrap: "wrap", marginTop: 10 }}>
                  {JANELAS.map(j => (
                    <span key={j.chave} style={{ fontSize: 10.5, color: "var(--tinta-suave)" }}>
                      <span style={{ display: "inline-block", width: 8, height: 8, borderRadius: 2, background: j.cor, marginRight: 4 }} />{j.rotulo}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ===== ABA ANÁLISES ===== */}
      {aba === "analises" && (
        <div className="grade-painel conteudo-pagina">
          <div className="pilha">
            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Análise de Volume — Histórico</h3>
              </div>
              <div className="cartao__corpo">
                <ResponsiveContainer width="100%" height={220}>
                  <ComposedChart data={dadosPreditiva} margin={{ top: 6, right: 8, left: -18, bottom: 0 }}>
                    <defs>
                      <linearGradient id="bandaConfianca" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="var(--ambar-500)" stopOpacity={0.28} />
                        <stop offset="100%" stopColor="var(--ambar-500)" stopOpacity={0.04} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" stroke="var(--superficie-alt)" />
                    <XAxis dataKey="mes" tick={{ fontSize: 10.5, fill: "var(--tinta-fraca)" }} axisLine={{ stroke: "var(--borda)" }} tickLine={false} />
                    <YAxis tick={{ fontSize: 10.5, fill: "var(--tinta-fraca)" }} axisLine={false} tickLine={false} width={54} tickFormatter={v => v.toLocaleString("pt-BR")} />
                    <Tooltip
                      formatter={(valor, nome) => [valor?.toLocaleString ? valor.toLocaleString("pt-BR") : valor, nome]}
                      contentStyle={{ fontSize: 12, borderRadius: 8, border: "1px solid var(--borda)" }}
                    />
                    <Legend wrapperStyle={{ fontSize: 11.5 }} />
                    <Area dataKey="banda" name="Faixa de confiança" stroke="none" fill="url(#bandaConfianca)" connectNulls />
                    <Line dataKey="historico" name="Histórico" stroke="var(--navio-700)" strokeWidth={2} dot={{ r: 2.5 }} connectNulls={false} />
                    <Line dataKey="projecao" name="Projeção" stroke="var(--ambar-500)" strokeWidth={2} strokeDasharray="5 3" dot={{ r: 2.5 }} connectNulls />
                  </ComposedChart>
                </ResponsiveContainer>
                <p style={{ fontSize: 10.5, color: "var(--tinta-fraca)", marginTop: 4 }}>
                  Faixa sombreada = intervalo de confiança (~80%), crescendo quanto mais distante a projeção. Passe o mouse num ponto pra ver o valor exato.
                </p>
              </div>
            </div>
            <div className="cartao">
              <div className="cartao__cabecalho"><h3>Atraso Recorrente</h3><p>Convênios com 3+ marcações acima de 24h — padrão, não outlier</p></div>
              <div className="cartao__corpo" style={{ overflowX: "auto" }}>
                {recorrentes.length === 0 ? <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Nenhum convênio com atraso recorrente.</p> : (
                  <table><thead><tr><th>Convênio</th>{empresaId === "todas" && <th>Empresa</th>}<th>Ocorrências</th><th>Espera Média</th></tr></thead>
                    <tbody>{recorrentes.map(r => (<tr key={r.convenio}><td>{r.convenio}</td>{empresaId === "todas" && <td>{r.empresaNome}</td>}<td><b>{r.ocorrencias}</b></td><td>{formatarHoras(r.esperaMedia)}</td></tr>))}</tbody>
                  </table>
                )}
              </div>
            </div>
          </div>

          <div className="pilha">
            {sla && (
              <div className="cartao">
                <div className="cartao__cabecalho"><h3>Tendência de SLA</h3><p>Espera média do último mês vs média dos 3 anteriores</p></div>
                <div className="cartao__corpo">
                  <div style={{
                    display: "flex", alignItems: "center", gap: 12, padding: "12px 14px", borderRadius: 12,
                    background: sla.piorou ? "var(--vermelho-100)" : sla.melhorou ? "var(--verde-100)" : "var(--superficie-alt)"
                  }}>
                    <span className="material-symbols-outlined" style={{ fontSize: 24, color: sla.piorou ? "var(--vermelho-500)" : sla.melhorou ? "var(--verde-500)" : "var(--tinta-suave)" }}>
                      {sla.piorou ? "trending_up" : sla.melhorou ? "trending_down" : "trending_flat"}
                    </span>
                    <div>
                      <div style={{ fontWeight: 700, fontSize: 14 }}>{sla.piorou ? "SLA piorando" : sla.melhorou ? "SLA melhorando" : "SLA estável"} ({sla.variacaoPct > 0 ? "+" : ""}{sla.variacaoPct.toFixed(1)}%)</div>
                      <div style={{ fontSize: 12, color: "var(--tinta-suave)" }}>{sla.ultimoMesRotulo}: {formatarHoras(sla.esperaUltimoMes)} — anteriores: {formatarHoras(sla.esperaMediaAnterior)}</div>
                    </div>
                  </div>
                </div>
              </div>
            )}
            <div className="cartao">
              <div className="cartao__cabecalho"><h3>Score de Eficiência por Operador</h3><p>Volume (40%) + espera média (40%) + consistência (20%)</p></div>
              <div className="cartao__corpo">
                {scoreOp.map(o => (
                  <div key={o.operador} style={{ marginBottom: 12 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}><span>{o.operador}</span><b>{o.score}/100</b></div>
                    <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}>
                      <div style={{ width: `${o.score}%`, height: "100%", borderRadius: 6, background: o.score >= 70 ? "var(--verde-500)" : o.score >= 40 ? "var(--ambar-500)" : "var(--vermelho-500)" }} />
                    </div>
                    <div style={{ fontSize: 11, color: "var(--tinta-fraca)", marginTop: 3 }}>{o.total.toLocaleString("pt-BR")} marcações · espera média {formatarHoras(o.esperaMedia)}</div>
                  </div>
                ))}
              </div>
            </div>
            <div className="cartao">
              <div className="cartao__cabecalho" style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 8 }}>
                <div>
                  <h3>Horários de Pico — Entrada x Saída</h3>
                  <p>Marcações (entrada) e liberações (saída) por hora do dia{mesFiltroPico !== "todos" ? ` — ${porMes.find(m => m.chave === mesFiltroPico)?.rotulo}` : " — período completo"}</p>
                </div>
                <select
                  value={mesFiltroPico}
                  onChange={e => setMesFiltroPico(e.target.value)}
                  style={{ padding: "6px 10px", borderRadius: 8, border: "1px solid var(--borda)", fontSize: 12.5, background: "#fff" }}
                >
                  <option value="todos">Todos os meses</option>
                  {porMes.map(m => <option key={m.chave} value={m.chave}>{m.rotulo}</option>)}
                </select>
              </div>
              <div className="cartao__corpo">
                <div style={{ display: "flex", gap: 16, marginBottom: 14, fontSize: 12 }}>
                  <div style={{ padding: "8px 12px", borderRadius: 10, background: "var(--azul-100)", flex: 1 }}>
                    <div style={{ fontSize: 10.5, color: "var(--tinta-fraca)", textTransform: "uppercase" }}>Pico de Entrada</div>
                    <div style={{ fontSize: 16, fontWeight: 700, color: "var(--navio-700)" }}>{picos.picoEntrada.hora}h <span style={{ fontSize: 12, fontWeight: 400 }}>({picos.picoEntrada.total.toLocaleString("pt-BR")})</span></div>
                  </div>
                  <div style={{ padding: "8px 12px", borderRadius: 10, background: "var(--verde-100)", flex: 1 }}>
                    <div style={{ fontSize: 10.5, color: "var(--tinta-fraca)", textTransform: "uppercase" }}>Pico de Saída</div>
                    <div style={{ fontSize: 16, fontWeight: 700, color: "var(--verde-500)" }}>{picos.picoSaida.hora}h <span style={{ fontSize: 12, fontWeight: 400 }}>({picos.picoSaida.total.toLocaleString("pt-BR")})</span></div>
                  </div>
                </div>
                <ResponsiveContainer width="100%" height={220}>
                  <LineChart data={dadosPicos} margin={{ top: 6, right: 8, left: -18, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="var(--superficie-alt)" />
                    <XAxis dataKey="hora" tick={{ fontSize: 10.5, fill: "var(--tinta-fraca)" }} axisLine={{ stroke: "var(--borda)" }} tickLine={false} />
                    <YAxis tick={{ fontSize: 10.5, fill: "var(--tinta-fraca)" }} axisLine={false} tickLine={false} width={54} tickFormatter={v => v.toLocaleString("pt-BR")} />
                    <Tooltip
                      formatter={(valor, nome) => [valor?.toLocaleString ? valor.toLocaleString("pt-BR") : valor, nome]}
                      labelFormatter={h => `${h}h`}
                      contentStyle={{ fontSize: 12, borderRadius: 8, border: "1px solid var(--borda)" }}
                    />
                    <Legend wrapperStyle={{ fontSize: 11.5 }} />
                    <Line dataKey="entrada" name="Entrada (marcação)" stroke="var(--navio-700)" strokeWidth={2} dot={{ r: 2.5 }} />
                    <Line dataKey="saida" name="Saída (liberação)" stroke="var(--verde-500)" strokeWidth={2} dot={{ r: 2.5 }} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </div>
          </div>
        </div>
      )}

      <RelatorioImprimivel registros={registros} nomeRecorte={empresaId === "todas" ? "Todas as Empresas" : (EMPRESAS.find(e => e.id === empresaId)?.nome || empresaId)} />
    </>
  );
}
