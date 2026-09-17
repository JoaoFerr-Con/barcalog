import { useState, useMemo } from "react";
import CartaoIndicador from "../components/CartaoIndicador.jsx";
import SeletorEmpresa from "../components/SeletorEmpresa.jsx";
import Modal from "../components/Modal.jsx";
import { useRegistrosReais } from "../hooks/useRegistrosReais.js";
import {
  obterKpisGerais, agruparPorMes, agruparPorMesDetalhado, totaisPorEmpresa,
  totaisPorOperador, totaisPorCarga, rankingMaioresEsperas, porHoraDoDia,
  porDiaDaSemana, tendenciaSLA, scoreEficienciaPorOperador, atrasosRecorrentes,
  distribuicaoPorCiclo, formatarHoras
} from "../data/relatorio.js";
import {
  analisePreditiva, tmaPorTerminal, distribuicaoJanelas, JANELAS, formatarBRL,
  custoTotalDemurrage, CAPACIDADE_DIARIA
} from "../data/metricsEngine.js";
import { exportarCSV, exportarPDF } from "../utils/exportar.js";
import { EMPRESAS } from "../data/registry.js";
import RelatorioImprimivel from "../components/RelatorioImprimivel.jsx";

const ABAS = [
  { chave: "resumo", rotulo: "Resumo" },
  { chave: "detalhes", rotulo: "Ranking & Detalhes" },
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
  const horaDoDia = useMemo(() => porHoraDoDia(registros), [registros]);
  const diaDaSemana = useMemo(() => porDiaDaSemana(registros), [registros]);
  const sla = useMemo(() => tendenciaSLA(registros), [registros]);
  const scoreOp = useMemo(() => scoreEficienciaPorOperador(registros), [registros]);
  const recorrentes = useMemo(() => atrasosRecorrentes(registros, 24, 3), [registros]);
  const porCiclo = useMemo(() => distribuicaoPorCiclo(registros), [registros]);
  const janelas = useMemo(() => distribuicaoJanelas(registros), [registros]);
  const demurrage = useMemo(() => custoTotalDemurrage(registros), [registros]);

  if (carregando) return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Carregando dados reais…</p>;
  if (!kpis) return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Nenhum registro para essa empresa.</p>;

  const maiorMes = Math.max(...porMes.map(m => m.total), 1);
  const maiorEmpresa = Math.max(...porEmpresa.map(e => e.total), 1);
  const maiorOperador = Math.max(...porOperador.map(o => o.total), 1);
  const maiorCarga = Math.max(...porCarga.map(c => c.total), 1);
  const maiorHora = Math.max(...horaDoDia.map(h => h.total), 1);
  const maiorDiaSemana = Math.max(...diaDaSemana.map(d => d.total), 1);
  const maiorCiclo = Math.max(...porCiclo.map(c => c.total), 1);
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
          <p style={{ fontSize: 13, color: "var(--tinta-suave)", marginBottom: 12 }}>
            O tempo médio de <b>{formatarHoras(kpis.tempoMedioEspera)}</b> é o intervalo entre a marcação (entrada) e a liberação (saída) de cada veículo.
            {sla && sla.piorou && ` O SLA piorou ${sla.variacaoPct.toFixed(1)}% no último mês comparado aos 3 anteriores.`}
          </p>
          <h4 style={{ fontSize: 13, marginBottom: 8 }}>TMA por Terminal</h4>
          {tma.map(t => (
            <div key={t.terminal} style={{ display: "flex", justifyContent: "space-between", padding: "6px 0", borderBottom: "1px solid var(--superficie-alt)", fontSize: 13 }}>
              <span>{t.terminal}</span>
              <span><b>{formatarHoras(t.tmaGeral)}</b> {t.variacao !== null && <span style={{ color: t.variacao > 0 ? "var(--vermelho-500)" : "var(--verde-500)", fontSize: 11 }}>({t.variacao > 0 ? "+" : ""}{t.variacao.toFixed(1)}%)</span>}</span>
            </div>
          ))}
          <h4 style={{ fontSize: 13, marginTop: 14, marginBottom: 8 }}>Distribuição por Janelas</h4>
          {janelas.map(j => (
            <div key={j.chave} style={{ display: "flex", justifyContent: "space-between", padding: "4px 0", fontSize: 12 }}>
              <span><span style={{ display: "inline-block", width: 8, height: 8, borderRadius: 2, background: j.cor, marginRight: 6 }} />{j.rotulo}</span>
              <span>{j.total.toLocaleString("pt-BR")} ({j.pct.toFixed(1)}%)</span>
            </div>
          ))}
        </Modal>
      )}
      {modal === "mesMov" && (
        <Modal titulo={`Mês Mais Movimentado: ${kpis.mesMaisMovimentado.rotulo}`} subtitulo="Detalhamento do mês de pico" aoFechar={() => setModal(null)}>
          <p style={{ fontSize: 13, color: "var(--tinta-suave)", marginBottom: 12 }}>
            O mês de <b>{kpis.mesMaisMovimentado.rotulo}</b> teve <b>{kpis.mesMaisMovimentado.total.toLocaleString("pt-BR")}</b> marcações,
            representando o maior volume do período. Custo estimado de retenção no período todo: <b>{formatarBRL(demurrage.custoTotal)}</b> ({demurrage.carretasRetidas} carretas retidas além de 144h).
          </p>
          <div className="grafico-fluxo" style={{ height: 140 }}>
            {porMes.map(m => (
              <div key={m.chave} className="grafico-fluxo__col">
                <span className="grafico-fluxo__valor">{m.total.toLocaleString("pt-BR")}</span>
                <div className="grafico-fluxo__barra" style={{ height: `${(m.total / maiorMes) * 100}%`, background: m.chave === kpis.mesMaisMovimentado.chave ? "var(--ambar-500)" : undefined }} />
                <span className="grafico-fluxo__rotulo">{m.rotulo}</span>
              </div>
            ))}
          </div>
        </Modal>
      )}

      {/* ===== ABA RESUMO ===== */}
      {aba === "resumo" && (
        <div className="grade-painel conteudo-pagina">
          <div className="pilha">
            <div className="cartao" style={{ cursor: "pointer" }} onClick={() => setModal("mesMov")}>
              <div className="cartao__cabecalho">
                <h3>Volume de Marcações por Mês <span className="material-symbols-outlined" style={{ fontSize: 14, verticalAlign: "middle", color: "var(--tinta-fraca)" }}>open_in_new</span></h3>
                <p>Clique pra ver o detalhamento do mês de pico</p>
              </div>
              <div className="cartao__corpo">
                <div className="grafico-fluxo">
                  {porMes.map(m => (
                    <div key={m.chave} className="grafico-fluxo__col">
                      <span className="grafico-fluxo__valor">{m.total.toLocaleString("pt-BR")}</span>
                      <div className="grafico-fluxo__barra" style={{ height: `${(m.total / maiorMes) * 100}%` }} />
                      <span className="grafico-fluxo__rotulo">{m.rotulo}</span>
                    </div>
                  ))}
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
            {empresaId === "todas" && (
              <div className="cartao">
                <div className="cartao__cabecalho"><h3>Marcações por Empresa</h3><p>Distribuição entre as fontes de dados importadas</p></div>
                <div className="cartao__corpo">
                  {porEmpresa.map(e => (
                    <div key={e.empresa} style={{ marginBottom: 12 }}>
                      <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
                        <span>{e.empresa}</span><b>{e.total.toLocaleString("pt-BR")} · espera média {formatarHoras(e.esperaMedia)}</b>
                      </div>
                      <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}>
                        <div style={{ width: `${(e.total / maiorEmpresa) * 100}%`, background: "var(--navio-700)", height: "100%", borderRadius: 6 }} />
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
            <div className="cartao">
              <div className="cartao__cabecalho"><h3>TMA por Terminal</h3><p>Tempo Médio de Atendimento e variação mês a mês</p></div>
              <div className="cartao__corpo">
                {tma.map(t => (
                  <div key={t.terminal} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "8px 0", borderBottom: "1px solid var(--superficie-alt)", fontSize: 13 }}>
                    <span>{t.terminal}</span>
                    <div style={{ textAlign: "right" }}>
                      <b>{formatarHoras(t.tmaGeral)}</b>
                      {t.variacao !== null && (
                        <span style={{ marginLeft: 8, fontSize: 11, fontWeight: 700, color: t.variacao > 5 ? "var(--vermelho-500)" : t.variacao < -5 ? "var(--verde-500)" : "var(--tinta-suave)" }}>
                          {t.variacao > 0 ? "▲" : "▼"} {Math.abs(t.variacao).toFixed(1)}%
                        </span>
                      )}
                    </div>
                  </div>
                ))}
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
              <div className="cartao__cabecalho"><h3>Ranking das Maiores Esperas</h3><p>Movimentos com maior diferença entre marcação e liberação</p></div>
              <div className="cartao__corpo" style={{ overflowX: "auto" }}>
                {top10.map((m, i) => (
                  <div key={`${m.empresaId}-${m.id}`} style={{
                    display: "flex", justifyContent: "space-between", alignItems: "center",
                    padding: "10px 0", borderBottom: "1px solid var(--superficie-alt)",
                    borderLeft: `3px solid ${m.esperaHoras > 144 ? "var(--vermelho-500)" : m.esperaHoras > 48 ? "var(--ambar-500)" : "var(--verde-500)"}`,
                    paddingLeft: 12
                  }}>
                    <div>
                      <div style={{ fontSize: 12, fontWeight: 700, color: "var(--tinta-fraca)" }}>#{i + 1}</div>
                      <div style={{ fontSize: 13, fontWeight: 600 }}>{m.convenio}</div>
                      <div style={{ fontSize: 11.5, color: "var(--tinta-suave)" }}>
                        Movimento {m.id} · Senha {m.senha}{empresaId === "todas" ? ` · ${m.empresaNome}` : ""}
                      </div>
                      <div style={{ fontSize: 11, color: "var(--tinta-fraca)" }}>
                        {new Date(m.marcadoEm).toLocaleString("pt-BR")} → {new Date(m.liberadoEm).toLocaleString("pt-BR")}
                      </div>
                    </div>
                    <div style={{ textAlign: "right", flexShrink: 0 }}>
                      <div style={{ fontSize: 18, fontWeight: 700, fontFamily: "'Space Grotesk', sans-serif", color: m.esperaHoras > 144 ? "var(--vermelho-500)" : "var(--tinta)" }}>
                        {formatarHoras(m.esperaHoras)}
                      </div>
                      <div style={{ fontSize: 10, color: "var(--tinta-fraca)" }}>{m.esperaHoras.toFixed(1)}h</div>
                    </div>
                  </div>
                ))}
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

      {/* ===== ABA ANÁLISES ===== */}
      {aba === "analises" && (
        <div className="grade-painel conteudo-pagina">
          <div className="pilha">
            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Análise Preditiva de Volume</h3>
                <p>Projeção com base em sazonalidade histórica e tendência de demanda — {preditiva.tendencia === "alta" ? "tendência de alta" : preditiva.tendencia === "queda" ? "tendência de queda" : "tendência estável"}</p>
              </div>
              <div className="cartao__corpo">
                <div className="grafico-fluxo">
                  {todosComProjecao.map(m => (
                    <div key={m.chave} className="grafico-fluxo__col">
                      <span className="grafico-fluxo__valor">{m.total.toLocaleString("pt-BR")}{m.projetado && m.confianca ? ` (${m.confianca}%)` : ""}</span>
                      <div className="grafico-fluxo__barra" style={{
                        height: `${(m.total / maiorComProj) * 100}%`,
                        background: m.projetado ? "var(--ambar-500)" : undefined,
                        backgroundImage: m.projetado ? "repeating-linear-gradient(45deg, rgba(255,255,255,.35) 0 4px, transparent 4px 8px)" : undefined
                      }} />
                      <span className="grafico-fluxo__rotulo">{m.rotulo}</span>
                    </div>
                  ))}
                </div>
                <p style={{ fontSize: 11, color: "var(--tinta-fraca)", marginTop: 8 }}>Barras listradas = meses projetados com intervalo de confiança</p>
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
              <div className="cartao__cabecalho"><h3>Horário de Pico</h3><p>Marcações por hora do dia</p></div>
              <div className="cartao__corpo">
                <div className="grafico-fluxo" style={{ gap: 2 }}>
                  {horaDoDia.map(h => (<div key={h.hora} className="grafico-fluxo__col"><div className="grafico-fluxo__barra" style={{ height: `${(h.total / maiorHora) * 100}%` }} /><span className="grafico-fluxo__rotulo" style={{ fontSize: 9.5 }}>{h.hora}h</span></div>))}
                </div>
              </div>
            </div>
            <div className="cartao">
              <div className="cartao__cabecalho"><h3>Distribuição por Ciclo</h3><p>Quantas vezes a mesma senha circulou pelo terminal</p></div>
              <div className="cartao__corpo">
                {porCiclo.map(c => (
                  <div key={c.balde} style={{ marginBottom: 12 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}><span>Ciclo {c.balde}</span><b>{c.total.toLocaleString("pt-BR")} · espera média {c.total > 0 ? formatarHoras(c.esperaMedia) : "—"}</b></div>
                    <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}><div style={{ width: `${(c.total / maiorCiclo) * 100}%`, background: "var(--navio-700)", height: "100%", borderRadius: 6 }} /></div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      <RelatorioImprimivel registros={registros} nomeRecorte={empresaId === "todas" ? "Todas as Empresas" : (EMPRESAS.find(e => e.id === empresaId)?.nome || empresaId)} />
    </>
  );
}
