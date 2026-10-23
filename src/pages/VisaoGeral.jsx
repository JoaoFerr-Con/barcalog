import { useMemo, useState } from "react";
import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, Cell } from "recharts";
import { api, baixarArquivo } from "../api/cliente.js";
import { useApi } from "../hooks/useApi.js";
import { useAcao } from "../hooks/useAcao.js";
import { Carregando, ErroCarga, Vazio } from "../components/EstadoCarga.jsx";
import { CabecalhoPagina, Cartao, Kpi, Selo, Icone, Aviso, Campo, HEX_TERMINAL } from "../components/ui.jsx";
import RelatorioPeriodo from "../components/RelatorioPeriodo.jsx";
import { terminalVinculado, terminalInicial, opcoesTerminal } from "../auth/escopo.js";
import AvisoDemonstracao from "../components/AvisoDemonstracao.jsx";
import {
  duracao, variacaoDuracao, numero, percentual, variacaoPct, mesCurto, mesLongo, limitesDoMes, mesAnterior,
  dataCurta, dataHoraPorto, dataHoraLocal, formatarDataPtBR
} from "../utils/formatar.js";

// Visão Geral — central de operações. Todos os números vêm de
// /api/v1/indicadores/* (regras em docs/INDICADORES.md). O período é o das
// datas de MARCAÇÃO, horário do porto; a comparação é explícita.

const I = "/api/v1/indicadores";
const ROTULO_ORIGEM = { CargaInicial: "Carga inicial", Planilha: "Planilha", Integracao: "Integração" };
const COR_PRIORIDADE = { Alta: "var(--critico)", Media: "var(--alto)", Baixa: "var(--serie-neutra)" };
const SELO_TIPO = { Historico: ["info", "Histórico"], Operacional: ["critico", "Operacional"], Previsao: ["previsao", "Previsão"], Dados: ["neutro", "Dados"] };

/** Mês mais recente em que TODOS os terminais têm dados até o fim do mês. */
function mesPadrao(frescor) {
  const ultimas = (frescor || []).map(f => f.ultimaMarcacao).filter(Boolean).sort();
  if (!ultimas.length) return null;
  const menor = ultimas[0].slice(0, 10);
  const chave = menor.slice(0, 7);
  return menor === limitesDoMes(chave)[1] ? chave : mesAnterior(chave);
}

function sentido(atual, anterior, menorMelhor = true) {
  if (atual == null || anterior == null || atual === anterior) return "neutro";
  return (atual < anterior) === menorMelhor ? "melhor" : "pior";
}

export default function VisaoGeral({ irPara }) {
  const frescor = useApi(sinal => api.get(`${I}/frescor`, null, sinal), []);
  const mensalTodos = useApi(sinal => api.get(`${I}/volume-mensal`, null, sinal), []);
  const [escolha, setEscolha] = useState(null); // { mes } ou { de, ate }
  const [terminalId, setTerminalId] = useState(terminalInicial);
  const vinculado = terminalVinculado();
  const [comparar, setComparar] = useState("anterior");
  const [executar, exportando] = useAcao();
  const [imprimir, setImprimir] = useState(false);

  const meses = useMemo(() => [...new Set((mensalTodos.dados || []).map(m => m.mes))].sort().reverse(), [mensalTodos.dados]);
  const mes = escolha?.mes ?? (escolha ? null : mesPadrao(frescor.dados));
  const [de, ate] = mes ? limitesDoMes(mes) : [escolha?.de, escolha?.ate];
  const [cDe, cAte] = comparar === "anterior" && mes ? limitesDoMes(mesAnterior(mes)) : [null, null];
  const rotuloPeriodo = mes ? mesLongo(mes) : de && ate ? `${formatarDataPtBR(de)} a ${formatarDataPtBR(ate)}` : "todo o período";
  const rotuloComparacao = cDe ? mesLongo(mesAnterior(mes)) : null;

  const pronto = !!de || !frescor.carregando;
  const filtro = { terminalId, de, ate, compararDe: cDe, compararAte: cAte };
  const chave = JSON.stringify(filtro);
  const resumo = useApi(sinal => pronto ? api.get(`${I}/resumo`, filtro, sinal) : Promise.resolve(null), [chave, pronto]);
  const terminais = useApi(sinal => pronto ? api.get(`${I}/terminais`, filtro, sinal) : Promise.resolve(null), [chave, pronto]);
  const fluxo = useApi(sinal => pronto ? api.get(`${I}/fluxo-horario`, { terminalId, de, ate }, sinal) : Promise.resolve(null), [chave, pronto]);
  const alertas = useApi(sinal => api.get("/api/v1/alertas", { tamanhoPagina: 4 }, sinal), []);
  const agenda = useApi(sinal => api.get("/api/v1/agendamentos/resumo", null, sinal), []);
  const portaria = useApi(sinal => api.get("/api/v1/portaria/operacao-agora", null, sinal), []);
  const ultimos = useApi(sinal => api.get("/api/v1/marcacoes", { tamanhoPagina: 5 }, sinal), []);

  const principais = [frescor, resumo, terminais];
  const erro = principais.find(r => r.erro)?.erro;
  if (erro) return <><CabecalhoPagina titulo="Visão Geral" /><ErroCarga erro={erro} aoTentarDeNovo={() => principais.forEach(r => r.recarregar())} /></>;

  const a = resumo.dados?.atual;
  const b = resumo.dados?.anterior;
  const desatualizados = (frescor.dados || []).filter(f => f.desatualizado);
  const limiteLonga = resumo.dados?.limiteEsperaLongaHoras ?? 72;
  const basePrev = rotuloComparacao ? `em ${rotuloComparacao.split(" de ")[0]}` : "";

  return (
    <>
      <CabecalhoPagina titulo="Visão Geral" subtitulo="Fluxo de carretas nos terminais · tempos medidos da marcação à liberação">
        <Campo rotulo="Período">
          <select value={mes ?? "personalizado"} onChange={e => setEscolha(e.target.value === "personalizado" ? { de: de || "", ate: ate || "" } : { mes: e.target.value })}>
            {meses.map(m => <option key={m} value={m}>{mesLongo(m)}</option>)}
            <option value="personalizado">Personalizado…</option>
          </select>
        </Campo>
        {!mes && (
          <>
            <Campo rotulo="De"><input type="date" value={de || ""} onChange={e => setEscolha({ de: e.target.value, ate })} /></Campo>
            <Campo rotulo="Até"><input type="date" value={ate || ""} min={de} onChange={e => setEscolha({ de, ate: e.target.value })} /></Campo>
          </>
        )}
        <Campo rotulo="Terminal">
          <select value={terminalId} onChange={e => setTerminalId(e.target.value)} disabled={!!vinculado} title={vinculado ? "Seu usuário está vinculado a este terminal (definido pelo Gestor)." : undefined}>
            {!vinculado && <option value="">Todos os terminais</option>}
            {opcoesTerminal(frescor.dados, t => t.terminalId).map(t => <option key={t.terminalId} value={t.terminalId}>{t.terminal}</option>)}
          </select>
        </Campo>
        <Campo rotulo="Comparar com">
          <select value={mes ? comparar : "nenhum"} disabled={!mes} onChange={e => setComparar(e.target.value)}>
            <option value="anterior">Mês anterior</option>
            <option value="nenhum">Sem comparação</option>
          </select>
        </Campo>
        <button type="button" className="botao botao--secundario" disabled={exportando || !de}
          onClick={() => executar(() => baixarArquivo("/api/v1/marcacoes/exportar", { terminalId, de, ate }, `marcacoes_${terminalId || "todos"}_${de}_${ate}.csv`), { sucesso: "CSV gerado." })}>
          <Icone nome="download" /> CSV
        </button>
        <button type="button" className="botao botao--secundario" disabled={!a} onClick={() => setImprimir(true)}>
          <Icone nome="print" /> PDF
        </button>
      </CabecalhoPagina>

      {/* Atualização dos dados */}
      {frescor.dados && (
        <div className="aviso aviso--info num" role="status" style={{ flexWrap: "wrap", alignItems: "center" }}>
          <b style={{ color: "var(--tinta)" }}>Dados disponíveis até</b>
          {frescor.dados.map(f => <span key={f.terminalId}>{f.terminal} <b style={{ color: "var(--tinta)" }}>{f.ultimaMarcacao ? dataCurta(f.ultimaMarcacao) : "—"}</b></span>)}
          {desatualizados.length > 0 && <Selo tipo="atencao" icone="warning">{desatualizados.length === frescor.dados.length ? "Todos os terminais sem dados recentes" : `${desatualizados.map(d => d.terminal).join(", ")} sem dados recentes`}</Selo>}
          <span style={{ marginLeft: "auto" }}>
            {(() => { const u = frescor.dados.map(f => f.ultimaImportacaoEm).filter(Boolean).sort().pop(); return u ? <>Última importação {dataHoraLocal(u)} · </> : null; })()}
            <a href="#/importacao" onClick={e => { e.preventDefault(); irPara("importacao"); }}>importar planilhas</a>
          </span>
        </div>
      )}

      {/* Indicadores essenciais */}
      <section aria-label={`Indicadores de ${rotuloPeriodo}`} className="grade-kpi">
        {!a ? <Carregando texto="Calculando indicadores…" /> : <>
          <Kpi rotulo="Movimentações" valor={numero(a.movimentacoes)}
            comparacao={{ texto: variacaoPct(a.movimentacoes, b?.movimentacoes), sentido: "neutro", base: b ? `vs. ${numero(b.movimentacoes)} ${basePrev} · ${numero(a.mediaDiaria)}/dia` : `${numero(a.mediaDiaria)}/dia` }}
            regra={`Marcações com data no período (${rotuloPeriodo}), liberadas ou não. Média/dia = movimentações ÷ ${resumo.dados.periodo?.dias ?? "—"} dias corridos.`} />
          <Kpi rotulo="Espera mediana" valor={duracao(a.esperaMediana)}
            comparacao={{ texto: variacaoDuracao(a.esperaMediana, b?.esperaMediana), sentido: sentido(a.esperaMediana, b?.esperaMediana), base: b ? `vs. ${duracao(b.esperaMediana)} ${basePrev}` : "metade esperou menos que isso" }}
            regra="Mediana de (liberação − marcação) das carretas marcadas no período e já liberadas." />
          <Kpi rotulo="Espera P90" valor={duracao(a.esperaP90)}
            comparacao={{ texto: variacaoDuracao(a.esperaP90, b?.esperaP90), sentido: sentido(a.esperaP90, b?.esperaP90), base: "10% esperaram mais que isso" }}
            regra="90º percentil de (liberação − marcação), por interpolação linear (igual ao PERCENTIL.INC do Excel)." />
          <Kpi rotulo="Liberadas em até 24 h" valor={numero(a.liberadasAte24hPct, 1)} unidade="%"
            comparacao={{ texto: b ? `${a.liberadasAte24hPct >= b.liberadasAte24hPct ? "+" : "−"}${numero(Math.abs(a.liberadasAte24hPct - b.liberadasAte24hPct), 1)} p.p.` : null, sentido: sentido(a.liberadasAte24hPct, b?.liberadasAte24hPct, false), base: b ? `vs. ${percentual(b.liberadasAte24hPct)} ${basePrev}` : "" }}
            regra="Carretas com espera ≤ 24 h ÷ carretas liberadas no período. Meta configurável em Configurações." />
          <Kpi rotulo={`Esperas acima de ${limiteLonga} h`} valor={numero(a.esperasLongas)} unidade="carretas"
            comparacao={{ texto: b ? `${a.esperasLongas - b.esperasLongas > 0 ? "+" : a.esperasLongas < b.esperasLongas ? "−" : ""}${numero(Math.abs(a.esperasLongas - b.esperasLongas))}` : null, sentido: sentido(a.esperasLongas, b?.esperasLongas), base: b ? `vs. ${numero(b.esperasLongas)} ${basePrev}` : "" }}
            regra={`Carretas liberadas com espera > ${limiteLonga} h — casos para investigar um a um. Limite configurável.`} />
        </>}
      </section>
      {a && a.semLiberacao > 0 && <Aviso tipo="atencao">{numero(a.semLiberacao)} carreta(s) marcadas no período ainda sem liberação: contam em movimentações, mas não nas esperas.</Aviso>}

      <div className="grade-2">
        {/* Exigem atenção */}
        <Cartao id="vg-alertas" titulo="Exigem atenção" acao={<a className="link" href="#/gargalos" onClick={e => { e.preventDefault(); irPara("gargalos"); }}>Todos os alertas</a>}>
          {alertas.carregando && !alertas.dados ? <Carregando /> : !alertas.dados?.itens.length ? <Vazio icone="task_alt" titulo="Nenhum alerta aberto" texto="As regras são reavaliadas a cada 10 minutos e após cada importação." /> : (
            <ul className="lista-alertas">
              {alertas.dados.itens.map(al => (
                <li key={al.id} className="item-alerta">
                  <span className="ponto-estado" style={{ background: COR_PRIORIDADE[al.prioridade] }} aria-label={`Prioridade ${al.prioridade}`} />
                  <div>
                    <div className="linha-flex" style={{ gap: 6 }}>
                      <Selo tipo={SELO_TIPO[al.tipo][0]}>{SELO_TIPO[al.tipo][1]}</Selo>
                      <span className="item-alerta__meta">{al.terminal || "Todos"} · {al.periodo}</span>
                    </div>
                    <div className="item-alerta__titulo">{al.titulo}</div>
                    <div className="item-alerta__evidencia">{al.evidencia}</div>
                  </div>
                  <span />
                </li>
              ))}
            </ul>
          )}
        </Cartao>

        {/* Situação agora */}
        <Cartao id="vg-agora" titulo="Situação agora" acao={<span className="fraco" style={{ fontSize: 12 }}>hoje, {new Date().toLocaleDateString("pt-BR")}</span>}>
          <div className="pilha" style={{ gap: 14 }}>
            <div>
              <div className="linha-flex" style={{ justifyContent: "space-between", marginBottom: 8, fontSize: 13 }}>
                <b>Agendamentos do dia</b>
                <a href="#/agendamentos" onClick={e => { e.preventDefault(); irPara("agendamentos"); }}>abrir agenda</a>
              </div>
              <div className="grade-kpi" style={{ gridTemplateColumns: "repeat(4, minmax(0,1fr))", gap: 8 }}>
                {[["Previstos", agenda.dados?.total, null], ["Confirmados", agenda.dados?.confirmados, null], ["Em operação", agenda.dados?.emOperacao, "var(--navio-600)"], ["Atrasados", agenda.dados?.atrasados, "var(--critico-texto)"]].map(([r, n, cor]) => (
                  <div key={r} style={{ border: "1px solid var(--borda)", borderRadius: 10, padding: "8px 12px" }}>
                    <div className="num" style={{ fontFamily: "'Space Grotesk',sans-serif", fontSize: 22, fontWeight: 600, color: cor || "var(--tinta)" }}>{n ?? "—"}</div>
                    <div className="fraco" style={{ fontSize: 12 }}>{r}</div>
                  </div>
                ))}
              </div>
            </div>
            {portaria.dados && (
              <div>
                <b style={{ fontSize: 13 }}>Carretas por situação na portaria</b>
                <BarraSituacao d={portaria.dados} />
              </div>
            )}
            <AvisoDemonstracao compacto />
            <Aviso tipo="info"><b>Origem:</b> agenda e situação de portaria são registradas manualmente no BarcaLog (sem GPS ou sensor), não vêm das planilhas dos terminais.</Aviso>
          </div>
        </Cartao>
      </div>

      {/* Comparação entre terminais */}
      <Cartao id="vg-terminais" titulo={`Comparação entre terminais · ${rotuloPeriodo}`}
        acao={<span className="fraco" style={{ fontSize: 12 }}>{(terminais.dados || []).some(t => t.capacidadeDiaria) ? "" : "Ocupação não exibida: capacidade dos terminais não configurada"}</span>}>
        {!terminais.dados ? <Carregando /> : (
          <div className="tabela-wrap">
            <table style={{ minWidth: 900 }}>
              <thead>
                <tr>
                  <th scope="col">Terminal</th><th scope="col" className="num">Movimentações</th><th scope="col" className="num">Média/dia</th>
                  <th scope="col" className="num">Pico diário</th><th scope="col" className="num">Espera mediana</th>
                  {b && <th scope="col" className="num">vs. {rotuloComparacao?.split(" de ")[0]}</th>}
                  <th scope="col" className="num">Espera P90</th><th scope="col" className="num">Liberadas ≤ 24 h</th>
                  <th scope="col" className="num">Esperas &gt; {limiteLonga} h</th>
                  {terminais.dados.some(t => t.capacidadeDiaria) && <th scope="col" className="num">Ocupação</th>}
                </tr>
              </thead>
              <tbody>
                {terminais.dados.map(t => (
                  <tr key={t.terminalId}>
                    <th scope="row"><span className="linha-flex" style={{ gap: 8 }}><span className="ponto-legenda" style={{ background: HEX_TERMINAL[t.terminalId] }} />{t.terminal}</span></th>
                    <td className="num">{numero(t.atual.movimentacoes)}</td>
                    <td className="num">{numero(t.atual.mediaDiaria)}</td>
                    <td className="num">{t.picoDiario ? <>{numero(t.picoDiario)} <span className="fraco">({dataCurta(t.picoData)})</span></> : "—"}</td>
                    <td className="num" style={{ fontWeight: 600 }}>{duracao(t.atual.esperaMediana)}</td>
                    {b && <td className="num"><b className={`kpi__nota ${""}`} style={{ color: sentido(t.atual.esperaMediana, t.anterior?.esperaMediana) === "pior" ? "var(--critico-texto)" : sentido(t.atual.esperaMediana, t.anterior?.esperaMediana) === "melhor" ? "var(--ok)" : "var(--tinta-suave)" }}>{variacaoDuracao(t.atual.esperaMediana, t.anterior?.esperaMediana) ?? "—"}</b></td>}
                    <td className="num">{duracao(t.atual.esperaP90)}</td>
                    <td className="num">{percentual(t.atual.liberadasAte24hPct)}</td>
                    <td className="num">{numero(t.atual.esperasLongas)}</td>
                    {terminais.dados.some(x => x.capacidadeDiaria) && <td className="num">{t.ocupacaoMediaPct != null ? percentual(t.ocupacaoMediaPct, 0) : <span className="fraco">não configurada</span>}</td>}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Cartao>

      {/* Histórico e fluxo */}
      <div className="grade-2">
        <Cartao id="vg-historico" titulo="Análise de Volume — Histórico" subtitulo={`Movimentações por mês${terminalId ? "" : " e terminal"} · meses parciais marcados com *`}>
          <GraficoMensal dados={(mensalTodos.dados || []).filter(m => !terminalId || m.terminalId === terminalId)} mesDestacado={mes} />
        </Cartao>
        <Cartao id="vg-fluxo" titulo="Chegadas × liberações por hora" subtitulo={`Média por dia · ${rotuloPeriodo}${terminalId ? "" : " · todos os terminais"}`}>
          <GraficoFluxo dados={fluxo.dados} />
        </Cartao>
      </div>

      {/* Últimos registros */}
      <Cartao id="vg-ultimos" titulo="Últimos registros recebidos" acao={<a className="link" href="#/movimentacoes" onClick={e => { e.preventDefault(); irPara("movimentacoes"); }}>Ver todas as movimentações</a>}>
        {!ultimos.dados ? <Carregando /> : (
          <div className="tabela-wrap">
            <table style={{ minWidth: 760 }}>
              <thead><tr><th scope="col">Movimento</th><th scope="col">Senha</th><th scope="col">Terminal</th><th scope="col">Convênio</th><th scope="col">Marcação</th><th scope="col">Liberação</th><th scope="col" className="num">Espera</th><th scope="col">Origem</th></tr></thead>
              <tbody>
                {ultimos.dados.itens.map(m => (
                  <tr key={m.movimentoId}>
                    <td className="id-mono">{m.movimentoId}</td><td className="id-mono">{m.senha}</td><td>{m.terminal}</td><td>{m.convenio}</td>
                    <td className="num">{dataHoraPorto(m.dataMarcacao)}</td><td className="num">{m.dataLiberacao ? dataHoraPorto(m.dataLiberacao) : <Selo tipo="atencao">sem liberação</Selo>}</td>
                    <td className="num">{duracao(m.esperaHoras)}</td><td><Selo>{ROTULO_ORIGEM[m.origem] || m.origem}</Selo></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Cartao>

      {imprimir && a && (
        <RelatorioPeriodo aoFechar={() => setImprimir(false)} periodo={rotuloPeriodo} comparacao={rotuloComparacao}
          terminal={terminalId ? (frescor.dados || []).find(t => t.terminalId === terminalId)?.terminal : null}
          resumo={resumo.dados} terminais={terminais.dados || []} mensal={(mensalTodos.dados || []).filter(m => !terminalId || m.terminalId === terminalId)}
          alertas={alertas.dados?.itens || []} frescor={frescor.dados || []} />
      )}
    </>
  );
}

function BarraSituacao({ d }) {
  const partes = [
    ["No pátio", d.noPatio, "var(--alto)"], ["Aguardando entrada", d.aguardando, "var(--serie-neutra)"],
    ["No porto", d.emOperacao, "var(--navio-600)"], ["Descarga finalizada", d.finalizados, "var(--serie-hidrovias)"]
  ];
  const total = partes.reduce((s, p) => s + p[1], 0);
  if (!total) return <p className="fraco" style={{ fontSize: 13, marginTop: 6 }}>Nenhuma carreta com situação registrada.</p>;
  return (
    <>
      <div role="img" aria-label={partes.map(p => `${p[1]} ${p[0].toLowerCase()}`).join(", ")} style={{ display: "flex", height: 12, borderRadius: 6, overflow: "hidden", gap: 2, marginTop: 8 }}>
        {partes.filter(p => p[1] > 0).map(p => <div key={p[0]} style={{ flex: p[1], background: p[2] }} />)}
      </div>
      <div className="legenda num" style={{ marginTop: 8 }}>
        {partes.map(p => <span key={p[0]}><span className="ponto-legenda" style={{ background: p[2] }} /><b style={{ color: "var(--tinta)" }}>{p[1]}</b> {p[0].toLowerCase()}</span>)}
      </div>
    </>
  );
}

const ROTULO_TERMINAL = { unitapajos: "Unitapajós", tgpm: "TGPM", hidrovias: "Hidrovias" };

function GraficoMensal({ dados, mesDestacado }) {
  const linhas = useMemo(() => {
    const porMes = {};
    dados.forEach(m => {
      const l = (porMes[m.mes] ||= { mes: m.mes, rotulo: mesCurto(m.mes), parcial: false });
      l[m.terminalId] = m.movimentacoes;
      l[`${m.terminalId}_mediana`] = m.esperaMediana;
      if (m.parcial) { l.parcial = true; l[`${m.terminalId}_parcial`] = true; }
    });
    return Object.values(porMes).sort((x, y) => x.mes.localeCompare(y.mes)).map(l => ({ ...l, rotulo: l.parcial ? `${l.rotulo}*` : l.rotulo }));
  }, [dados]);
  const ids = [...new Set(dados.map(d => d.terminalId))].sort((x, y) => ["unitapajos", "tgpm", "hidrovias"].indexOf(x) - ["unitapajos", "tgpm", "hidrovias"].indexOf(y));
  if (!dados.length) return <Vazio icone="bar_chart" titulo="Sem movimentações" />;
  return (
    <div role="img" aria-label={`Movimentações por mês: ${linhas.map(l => `${l.rotulo} ${ids.map(i => `${ROTULO_TERMINAL[i]} ${l[i] ?? 0}`).join(", ")}`).join("; ")}`}>
      <ResponsiveContainer width="100%" height={260}>
        <BarChart data={linhas} margin={{ top: 8, right: 8, left: 0, bottom: 0 }} barCategoryGap="22%">
          <CartesianGrid vertical={false} stroke="#EEF1F5" />
          <XAxis dataKey="rotulo" tick={{ fontSize: 11.5, fill: "#4A5870" }} tickLine={false} axisLine={{ stroke: "#CDD5E1" }} />
          <YAxis tick={{ fontSize: 11, fill: "#6B7890" }} tickLine={false} axisLine={false} width={44} tickFormatter={v => v >= 1000 ? `${numero(v / 1000, 0)} mil` : v} />
          <Tooltip cursor={{ fill: "#F1F5FB" }} formatter={(v, nome, { payload }) => [`${numero(v)} · mediana ${duracao(payload[`${nome}_mediana`])}${payload[`${nome}_parcial`] ? " (mês parcial)" : ""}`, ROTULO_TERMINAL[nome]]}
            labelFormatter={(_, p) => p?.[0] ? mesLongo(p[0].payload.mes) : ""} contentStyle={{ borderRadius: 8, borderColor: "#E3E8EF", fontSize: 12.5 }} />
          <Legend formatter={v => ROTULO_TERMINAL[v]} wrapperStyle={{ fontSize: 12.5 }} />
          {ids.map(id => (
            <Bar key={id} dataKey={id} fill={HEX_TERMINAL[id]} radius={[2, 2, 0, 0]} maxBarSize={14}>
              {linhas.map(l => <Cell key={l.mes} fillOpacity={mesDestacado && l.mes !== mesDestacado ? 0.55 : 1} />)}
            </Bar>
          ))}
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}

function GraficoFluxo({ dados }) {
  if (!dados) return <Carregando />;
  if (!dados.some(d => d.chegadasPorDia || d.liberacoesPorDia)) return <Vazio icone="schedule" titulo="Sem movimentações no período" />;
  const linhas = dados.map(d => ({ ...d, rotulo: `${String(d.hora).padStart(2, "0")}h`, destaque: d.razao != null && d.razao >= 1.5 && d.chegadasPorDia >= 5 }));
  const pico = [...linhas].sort((x, y) => y.chegadasPorDia - x.chegadasPorDia)[0];
  return (
    <div role="img" aria-label={`Maior concentração de chegadas às ${pico.hora}h: ${numero(pico.chegadasPorDia, 1)} por dia contra ${numero(pico.liberacoesPorDia, 1)} liberações.`}>
      <ResponsiveContainer width="100%" height={260}>
        <BarChart data={linhas} margin={{ top: 8, right: 8, left: 0, bottom: 0 }} barGap={1} barCategoryGap="18%">
          <CartesianGrid vertical={false} stroke="#EEF1F5" />
          <XAxis dataKey="rotulo" tick={{ fontSize: 10.5, fill: "#4A5870" }} interval={2} tickLine={false} axisLine={{ stroke: "#CDD5E1" }} />
          <YAxis tick={{ fontSize: 11, fill: "#6B7890" }} tickLine={false} axisLine={false} width={32} />
          <Tooltip cursor={{ fill: "#F1F5FB" }} formatter={(v, nome) => [`${numero(v, 1)} por dia`, nome === "chegadasPorDia" ? "Marcações (chegadas)" : "Liberações"]}
            contentStyle={{ borderRadius: 8, borderColor: "#E3E8EF", fontSize: 12.5 }} />
          <Legend formatter={v => v === "chegadasPorDia" ? "Marcações (chegadas)" : "Liberações"} wrapperStyle={{ fontSize: 12.5 }} />
          <Bar dataKey="chegadasPorDia" fill="#1F4E88" radius={[2, 2, 0, 0]}>
            {linhas.map(l => <Cell key={l.hora} fill={l.destaque ? "#C2362D" : "#1F4E88"} />)}
          </Bar>
          <Bar dataKey="liberacoesPorDia" fill="#C9D3E3" radius={[2, 2, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
      <p className="fraco" style={{ fontSize: 12, marginTop: 4 }}>Em vermelho: horas com chegadas acima de 1,5× as liberações (e ao menos 5 por dia).</p>
    </div>
  );
}
