import { useState, useMemo } from "react";
import CartaoIndicador from "../components/CartaoIndicador.jsx";
import SeletorEmpresa from "../components/SeletorEmpresa.jsx";
import { useRegistrosReais } from "../hooks/useRegistrosReais.js";
import {
  obterKpisGerais,
  agruparPorMes,
  totaisPorEmpresa,
  totaisPorOperador,
  totaisPorCarga,
  rankingMaioresEsperas,
  projetarVolume,
  porHoraDoDia,
  porDiaDaSemana,
  tendenciaSLA,
  scoreEficienciaPorOperador,
  atrasosRecorrentes,
  distribuicaoPorCiclo,
  formatarHoras
} from "../data/relatorio.js";
import { exportarCSV, exportarPDF } from "../utils/exportar.js";

const ABAS = [
  { chave: "resumo", rotulo: "Resumo" },
  { chave: "detalhes", rotulo: "Detalhes" },
  { chave: "analises", rotulo: "Análises" }
];

// Visão Geral = landing page da operação, 100% em cima dos dados reais
// importados (Unitapajós, TGPM, Hidrovias). Concentra tudo que antes estava
// espalhado entre "Visão Geral" e "Relatório Real" — essa última foi
// descontinuada como página própria.
//
// Pra somar uma nova empresa/fonte de dados aqui, não precisa mexer neste
// arquivo — só registrar o novo dataset em src/data/registry.js.
export default function VisaoGeral() {
  const [empresaId, setEmpresaId] = useState("todas");
  const [aba, setAba] = useState("resumo");
  const { registros, carregando } = useRegistrosReais(empresaId);

  const kpis = useMemo(() => obterKpisGerais(registros), [registros]);
  const porMes = useMemo(() => agruparPorMes(registros), [registros]);
  const porEmpresa = useMemo(() => totaisPorEmpresa(registros), [registros]);
  const porOperador = useMemo(() => totaisPorOperador(registros), [registros]);
  const porCarga = useMemo(() => totaisPorCarga(registros), [registros]);
  const top10 = useMemo(() => rankingMaioresEsperas(registros, 10), [registros]);
  const recentes = useMemo(
    () => [...registros].sort((a, b) => new Date(b.marcadoEm) - new Date(a.marcadoEm)).slice(0, 8),
    [registros]
  );

  // ---- Análises (projeção, picos, SLA, score, atrasos recorrentes) ----
  const projecao = useMemo(() => projetarVolume(porMes, 2), [porMes]);
  const horaDoDia = useMemo(() => porHoraDoDia(registros), [registros]);
  const diaDaSemana = useMemo(() => porDiaDaSemana(registros), [registros]);
  const sla = useMemo(() => tendenciaSLA(registros), [registros]);
  const scoreOperadores = useMemo(() => scoreEficienciaPorOperador(registros), [registros]);
  const recorrentes = useMemo(() => atrasosRecorrentes(registros, 24, 3), [registros]);
  const porCiclo = useMemo(() => distribuicaoPorCiclo(registros), [registros]);

  if (carregando) {
    return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Carregando dados reais…</p>;
  }
  if (!kpis) {
    return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Nenhum registro para essa empresa.</p>;
  }

  const maiorMes = Math.max(...porMes.map(m => m.total), 1);
  const maiorEmpresa = Math.max(...porEmpresa.map(e => e.total), 1);
  const maiorOperador = Math.max(...porOperador.map(o => o.total), 1);
  const maiorCarga = Math.max(...porCarga.map(c => c.total), 1);
  const maiorHora = Math.max(...horaDoDia.map(h => h.total), 1);
  const maiorDiaSemana = Math.max(...diaDaSemana.map(d => d.total), 1);
  const pontosProjecao = projecao?.pontos || [];
  const maiorComProjecao = Math.max(maiorMes, ...pontosProjecao.map(p => p.total), 1);
  const maiorCiclo = Math.max(...porCiclo.map(c => c.total), 1);

  function exportarRegistrosCSV() {
    exportarCSV(
      registros,
      [
        { rotulo: "Movimento", chave: "id" },
        { rotulo: "Senha", chave: "senha" },
        { rotulo: "Convênio", chave: "convenio" },
        { rotulo: "Empresa", chave: "empresaNome" },
        { rotulo: "Operador", chave: "operador" },
        { rotulo: "Carga", chave: "carga" },
        { rotulo: "Ciclo", chave: "ciclo" },
        { rotulo: "Marcação", valor: r => new Date(r.marcadoEm).toLocaleString("pt-BR") },
        { rotulo: "Liberação", valor: r => new Date(r.liberadoEm).toLocaleString("pt-BR") },
        { rotulo: "Espera (h)", chave: "esperaHoras" }
      ],
      `marcacoes_${empresaId}`
    );
  }

  return (
    <>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16, flexWrap: "wrap", gap: 12 }}>
        <div className="abas">
          {ABAS.map(a => (
            <button
              key={a.chave}
              onClick={() => setAba(a.chave)}
              className={`aba-botao ${aba === a.chave ? "ativo" : ""}`}
            >
              {a.rotulo}
            </button>
          ))}
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <SeletorEmpresa valor={empresaId} aoMudar={setEmpresaId} />
          <button className="botao botao--fantasma botao-exportar" onClick={exportarRegistrosCSV} title="Exportar os registros do recorte atual em CSV">
            <span className="material-symbols-outlined" style={{ fontSize: 16 }}>download</span> CSV
          </button>
          <button className="botao botao--fantasma botao-exportar" onClick={exportarPDF} title="Abrir a caixa de impressão do navegador (permite salvar como PDF)">
            <span className="material-symbols-outlined" style={{ fontSize: 16 }}>picture_as_pdf</span> PDF
          </button>
        </div>
      </div>

      <div className="grade-kpi">
        <CartaoIndicador
          rotulo="Total de Marcações"
          valor={kpis.total.toLocaleString("pt-BR")}
          icone="fact_check"
          corIcone="var(--navio-700)"
          corFundoIcone="var(--azul-100)"
          nota="Período importado"
        />
        <CartaoIndicador
          rotulo="Média Diária"
          valor={kpis.mediaDiaria.toFixed(1)}
          icone="calendar_month"
          corIcone="var(--ambar-600)"
          corFundoIcone="#FBEBD1"
          nota={`${kpis.diasOperados} dias operados`}
        />
        <CartaoIndicador
          rotulo="Tempo Médio de Espera"
          valor={formatarHoras(kpis.tempoMedioEspera)}
          icone="schedule"
          corIcone="var(--tinta-suave)"
          corFundoIcone="var(--superficie-alt)"
          nota="marcação → liberação"
        />
        <CartaoIndicador
          rotulo="Mês Mais Movimentado"
          valor={kpis.mesMaisMovimentado.rotulo}
          icone="trending_up"
          corIcone="var(--verde-500)"
          corFundoIcone="var(--verde-100)"
          nota={`${kpis.mesMaisMovimentado.total.toLocaleString("pt-BR")} marcações`}
        />
      </div>

      {aba === "resumo" && (
        <div className="grade-painel conteudo-pagina">
          <div className="pilha">
            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Volume de Marcações por Mês</h3>
                <p>Recorte atual: {empresaId === "todas" ? "todas as empresas" : registros[0]?.empresaNome}</p>
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
                  <thead>
                    <tr>
                      <th>Movimento</th>
                      <th>Convênio</th>
                      {empresaId === "todas" && <th>Empresa</th>}
                      <th>Marcação</th>
                      <th>Espera</th>
                    </tr>
                  </thead>
                  <tbody>
                    {recentes.map(m => (
                      <tr key={`${m.empresaId}-${m.id}`}>
                        <td className="mono">{m.id}</td>
                        <td>{m.convenio}</td>
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
                <div className="cartao__cabecalho">
                  <h3>Marcações por Empresa</h3>
                  <p>Distribuição entre as fontes de dados importadas</p>
                </div>
                <div className="cartao__corpo">
                  {porEmpresa.map(e => (
                    <div key={e.empresa} style={{ marginBottom: 12 }}>
                      <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
                        <span>{e.empresa}</span>
                        <b>{e.total.toLocaleString("pt-BR")} · espera média {formatarHoras(e.esperaMedia)}</b>
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
              <div className="cartao__cabecalho">
                <h3>Marcações por Operador</h3>
                <p>Bunge, Amaggi, TGPM e Hidrovias no recorte selecionado</p>
              </div>
              <div className="cartao__corpo">
                {porOperador.map(o => (
                  <div key={o.operador} style={{ marginBottom: 12 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
                      <span>{o.operador}</span>
                      <b>{o.total.toLocaleString("pt-BR")}</b>
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

      {aba === "detalhes" && (
        <div className="grade-painel conteudo-pagina">
          <div className="pilha">
            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Ranking das Maiores Esperas</h3>
                <p>Movimentos com maior diferença entre marcação e liberação</p>
              </div>
              <div className="cartao__corpo" style={{ overflowX: "auto" }}>
                <table>
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>Movimento</th>
                      <th>Senha</th>
                      <th>Convênio</th>
                      {empresaId === "todas" && <th>Empresa</th>}
                      <th>Marcação</th>
                      <th>Liberação</th>
                      <th>Espera</th>
                    </tr>
                  </thead>
                  <tbody>
                    {top10.map((m, i) => (
                      <tr key={`${m.empresaId}-${m.id}`}>
                        <td>#{i + 1}</td>
                        <td className="mono">{m.id}</td>
                        <td className="mono">{m.senha}</td>
                        <td>{m.convenio}</td>
                        {empresaId === "todas" && <td>{m.empresaNome}</td>}
                        <td className="mono">{new Date(m.marcadoEm).toLocaleString("pt-BR")}</td>
                        <td className="mono">{new Date(m.liberadoEm).toLocaleString("pt-BR")}</td>
                        <td><b>{formatarHoras(m.esperaHoras)}</b></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>

          <div className="pilha">
            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Marcações por Tipo de Carga</h3>
                <p>Soja, milho, caçamba e segregado</p>
              </div>
              <div className="cartao__corpo">
                {porCarga.map(c => (
                  <div key={c.carga} style={{ marginBottom: 12 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
                      <span>{c.carga}</span>
                      <b>{c.total.toLocaleString("pt-BR")}</b>
                    </div>
                    <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}>
                      <div style={{ width: `${(c.total / maiorCarga) * 100}%`, background: "var(--ambar-500)", height: "100%", borderRadius: 6 }} />
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      {aba === "analises" && (
        <div className="grade-painel conteudo-pagina">
          <div className="pilha">
            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Projeção de Volume</h3>
                <p>Tendência estatística simples (regressão linear) sobre o histórico — não é IA, é projeção de tendência.</p>
              </div>
              <div className="cartao__corpo">
                {!projecao ? (
                  <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Histórico curto demais pra projetar com confiança.</p>
                ) : (
                  <>
                    <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 14 }}>
                      <span className="material-symbols-outlined" style={{ color: projecao.tendencia === "alta" ? "var(--verde-500)" : "var(--vermelho-500)" }}>
                        {projecao.tendencia === "alta" ? "trending_up" : "trending_down"}
                      </span>
                      <span style={{ fontSize: 13 }}>
                        Tendência de <b>{projecao.tendencia}</b> — próximo mês estimado em <b>{pontosProjecao[0]?.total.toLocaleString("pt-BR")}</b> marcações.
                      </span>
                    </div>
                    <div className="grafico-fluxo">
                      {[...porMes, ...pontosProjecao].map(m => (
                        <div key={m.chave} className="grafico-fluxo__col">
                          <span className="grafico-fluxo__valor">{m.total.toLocaleString("pt-BR")}</span>
                          <div
                            className="grafico-fluxo__barra"
                            style={{
                              height: `${(m.total / maiorComProjecao) * 100}%`,
                              background: m.projetado ? "var(--ambar-500)" : undefined,
                              opacity: m.projetado ? 0.85 : 1,
                              backgroundImage: m.projetado ? "repeating-linear-gradient(45deg, rgba(255,255,255,.35) 0 4px, transparent 4px 8px)" : undefined
                            }}
                          />
                          <span className="grafico-fluxo__rotulo">{m.rotulo}{m.projetado ? " *" : ""}</span>
                        </div>
                      ))}
                    </div>
                    <p style={{ fontSize: 11, color: "var(--tinta-fraca)", marginTop: 8 }}>* meses projetados (listrado, em âmbar)</p>
                  </>
                )}
              </div>
            </div>

            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Detecção de Atraso Recorrente</h3>
                <p>Convênios com {"≥"}3 marcações acima de 24h de espera — padrão, não outlier isolado</p>
              </div>
              <div className="cartao__corpo" style={{ overflowX: "auto" }}>
                {recorrentes.length === 0 ? (
                  <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Nenhum convênio com atraso recorrente acima de 24h no recorte atual.</p>
                ) : (
                  <table>
                    <thead>
                      <tr>
                        <th>Convênio</th>
                        {empresaId === "todas" && <th>Empresa</th>}
                        <th>Ocorrências {'>'}24h</th>
                        <th>Espera Média</th>
                      </tr>
                    </thead>
                    <tbody>
                      {recorrentes.map(r => (
                        <tr key={r.convenio}>
                          <td>{r.convenio}</td>
                          {empresaId === "todas" && <td>{r.empresaNome}</td>}
                          <td><b>{r.ocorrencias}</b></td>
                          <td>{formatarHoras(r.esperaMedia)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>
          </div>

          <div className="pilha">
            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Alerta de Tendência de SLA</h3>
                <p>Espera média do último mês vs média dos 3 meses anteriores</p>
              </div>
              <div className="cartao__corpo">
                {!sla ? (
                  <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Histórico curto demais pra comparar.</p>
                ) : (
                  <div style={{
                    display: "flex", alignItems: "center", gap: 12, padding: "14px 16px", borderRadius: 12,
                    background: sla.piorou ? "var(--vermelho-100)" : sla.melhorou ? "var(--verde-100)" : "var(--superficie-alt)"
                  }}>
                    <span className="material-symbols-outlined" style={{
                      fontSize: 26,
                      color: sla.piorou ? "var(--vermelho-500)" : sla.melhorou ? "var(--verde-500)" : "var(--tinta-suave)"
                    }}>
                      {sla.piorou ? "trending_up" : sla.melhorou ? "trending_down" : "trending_flat"}
                    </span>
                    <div>
                      <div style={{ fontWeight: 700, fontSize: 14 }}>
                        {sla.piorou ? "SLA piorando" : sla.melhorou ? "SLA melhorando" : "SLA estável"} ({sla.variacaoPct > 0 ? "+" : ""}{sla.variacaoPct.toFixed(1)}%)
                      </div>
                      <div style={{ fontSize: 12.5, color: "var(--tinta-suave)" }}>
                        {sla.ultimoMesRotulo}: espera média {formatarHoras(sla.esperaUltimoMes)} — meses anteriores: {formatarHoras(sla.esperaMediaAnterior)}
                      </div>
                    </div>
                  </div>
                )}
              </div>
            </div>

            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Score de Eficiência por Operador</h3>
                <p>Combina volume (40%) + espera média (40%) + consistência (20%) — comparativo entre os operadores do recorte</p>
              </div>
              <div className="cartao__corpo">
                {scoreOperadores.map(o => (
                  <div key={o.operador} style={{ marginBottom: 14 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
                      <span>{o.operador}</span>
                      <b>{o.score}/100</b>
                    </div>
                    <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}>
                      <div style={{
                        width: `${o.score}%`, height: "100%", borderRadius: 6,
                        background: o.score >= 70 ? "var(--verde-500)" : o.score >= 40 ? "var(--ambar-500)" : "var(--vermelho-500)"
                      }} />
                    </div>
                    <div style={{ fontSize: 11, color: "var(--tinta-fraca)", marginTop: 3 }}>
                      {o.total.toLocaleString("pt-BR")} marcações · espera média {formatarHoras(o.esperaMedia)}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Horário de Pico</h3>
                <p>Marcações por hora do dia</p>
              </div>
              <div className="cartao__corpo">
                <div className="grafico-fluxo" style={{ gap: 2 }}>
                  {horaDoDia.map(h => (
                    <div key={h.hora} className="grafico-fluxo__col">
                      <div className="grafico-fluxo__barra" style={{ height: `${(h.total / maiorHora) * 100}%` }} />
                      <span className="grafico-fluxo__rotulo" style={{ fontSize: 9.5 }}>{h.hora}h</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Volume por Dia da Semana</h3>
              </div>
              <div className="cartao__corpo">
                <div className="grafico-fluxo">
                  {diaDaSemana.map(d => (
                    <div key={d.rotulo} className="grafico-fluxo__col">
                      <span className="grafico-fluxo__valor">{d.total.toLocaleString("pt-BR")}</span>
                      <div className="grafico-fluxo__barra" style={{ height: `${(d.total / maiorDiaSemana) * 100}%` }} />
                      <span className="grafico-fluxo__rotulo">{d.rotulo}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Distribuição por Ciclo</h3>
                <p>Quantas vezes a mesma senha/ticket circulou pelo terminal, e a espera média em cada faixa</p>
              </div>
              <div className="cartao__corpo">
                {porCiclo.map(c => (
                  <div key={c.balde} style={{ marginBottom: 12 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
                      <span>Ciclo {c.balde}</span>
                      <b>{c.total.toLocaleString("pt-BR")} · espera média {c.total > 0 ? formatarHoras(c.esperaMedia) : "—"}</b>
                    </div>
                    <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}>
                      <div style={{ width: `${(c.total / maiorCiclo) * 100}%`, background: "var(--navio-700)", height: "100%", borderRadius: 6 }} />
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
