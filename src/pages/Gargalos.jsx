import { useState } from "react";
import { api, mensagemErro } from "../api/cliente.js";
import { useApi } from "../hooks/useApi.js";
import { useAcao } from "../hooks/useAcao.js";
import { Carregando, ErroCarga, Vazio } from "../components/EstadoCarga.jsx";
import { useAmbiente } from "../hooks/useAmbiente.js";
import EstimativaProximasHoras from "../components/EstimativaProximasHoras.jsx";
import { terminalVinculado, terminalInicial, opcoesTerminal } from "../auth/escopo.js";
import { CabecalhoPagina, Cartao, Selo, Icone, Aviso, Abas, Campo, Paginacao } from "../components/ui.jsx";
import { numero, dataHoraLocal, formatarDataPtBR, dataHoraPorto } from "../utils/formatar.js";

// Alertas e gargalos. Cada alerta separa o que é HISTÓRICO (já aconteceu),
// OPERACIONAL (está acontecendo segundo os registros), PREVISÃO (estimativa
// pelo padrão histórico) e DADOS (qualidade/atualização). Regras em
// docs/INDICADORES.md e Application/Metricas/RegrasAlerta.cs.

const TIPOS = {
  Historico: { selo: "info", rotulo: "Histórico", texto: "Já aconteceu: calculado sobre períodos fechados." },
  Operacional: { selo: "critico", rotulo: "Operacional", texto: "Está acontecendo, segundo os registros atuais." },
  Previsao: { selo: "previsao", rotulo: "Previsão", texto: "Estimativa pelo padrão histórico — não é um fato." },
  Dados: { selo: "neutro", rotulo: "Dados", texto: "Qualidade ou atualização dos dados importados." }
};
// Regras que usam o cadastro de exemplo (agenda manual), não as planilhas dos terminais.
const REGRAS_DEMONSTRACAO = new Set(["agendamento-atrasado"]);
const PRIORIDADE = { Alta: ["critico", "Alta"], Media: ["alto", "Média"], Baixa: ["neutro", "Baixa"] };
const ESTADO = { Novo: ["info", "Novo"], EmAnalise: ["atencao", "Em análise"], Resolvido: ["ok", "Resolvido"], Descartado: ["neutro", "Descartado"] };

export default function Gargalos({ papel }) {
  const demo = useAmbiente()?.dadosExemplo;
  const podeAgir = papel === "Operador" || papel === "Gestor";
  const [aba, setAba] = useState("todos");
  const [terminalId, setTerminalId] = useState(terminalInicial);
  const vinculado = terminalVinculado();
  const [pagina, setPagina] = useState(1);
  const [selecionado, setSelecionado] = useState(null);
  const [avaliando, setAvaliando] = useState(false);
  const [resultado, setResultado] = useState(null);

  const terminais = useApi(sinal => api.get("/api/v1/terminais", null, sinal), []);
  const resumo = useApi(sinal => api.get("/api/v1/alertas/resumo", null, sinal), [], { intervaloMs: 60_000 });
  const proximas = useApi(sinal => api.get("/api/v1/alertas/proximas-horas", { terminalId }, sinal), [terminalId]);

  const encerrados = aba === "encerrados";
  const filtro = { tipo: encerrados || aba === "todos" ? undefined : aba, terminalId, pagina, tamanhoPagina: 20, somenteAbertos: !encerrados };
  const lista = useApi(sinal => api.get("/api/v1/alertas", filtro, sinal), [JSON.stringify(filtro)]);
  const itens = lista.dados?.itens || [];

  const porTipo = resumo.dados?.porTipo || {};
  const abas = [
    { id: "todos", rotulo: "Abertos", contagem: resumo.dados?.ativos },
    ...Object.entries(TIPOS).map(([id, t]) => ({ id, rotulo: t.rotulo, contagem: porTipo[id] ?? 0 })),
    { id: "encerrados", rotulo: "Todos, inclusive encerrados" }
  ];

  function mudar(fn) { fn(); setPagina(1); setSelecionado(null); }

  // Reavaliação real no servidor: recalcula alertas e confiabilidade, registra o resultado
  // e devolve o que mudou. Um clique por vez (o botão fica bloqueado e o servidor recusa
  // uma segunda avaliação simultânea).
  async function reavaliar() {
    if (avaliando) return;
    setAvaliando(true);
    try {
      setResultado(await api.post("/api/v1/alertas/avaliar"));
    } catch (e) {
      setResultado({ situacao: "falhou", resumo: mensagemErro(e), motivos: [] });
    } finally {
      setAvaliando(false);
      resumo.recarregar(); lista.recarregar(); proximas.recarregar();
    }
  }

  const tentativa = resumo.dados?.ultimaTentativa;
  return (
    <>
      <CabecalhoPagina titulo="Alertas e gargalos" subtitulo="O que exige atenção, com a evidência de cada alerta e o registro do que foi feito">
        <Campo rotulo="Terminal">
          <select value={terminalId} onChange={e => mudar(() => setTerminalId(e.target.value))} disabled={!!vinculado} title={vinculado ? "Seu usuário está vinculado a este terminal (definido pelo Gestor)." : undefined}>
            {!vinculado && <option value="">Todos os terminais</option>}
            {opcoesTerminal(terminais.dados).map(t => <option key={t.id} value={t.id}>{t.nome}</option>)}
          </select>
        </Campo>
        {podeAgir && (
          <button type="button" className="botao botao--secundario" disabled={avaliando} aria-busy={avaliando} onClick={reavaliar}>
            <Icone nome={avaliando ? "progress_activity" : "refresh"} /> {avaliando ? "Reavaliando…" : "Reavaliar agora"}
          </button>
        )}
      </CabecalhoPagina>

      <p className="fraco" style={{ fontSize: 12.5, margin: "-6px 0 12px" }}>
        {resumo.dados?.ultimaAvaliacao ? <>Última avaliação concluída: {dataHoraLocal(resumo.dados.ultimaAvaliacao)}</> : "Nenhuma avaliação concluída ainda"}
        {" · "}reavaliação automática a cada 10 minutos e após cada importação.
      </p>
      {!resultado && tentativa && tentativa.situacao === "falhou" && (
        <div style={{ marginBottom: 12 }}><Aviso tipo="critico">A última avaliação ({dataHoraLocal(tentativa.inicio)}) falhou: {tentativa.erro} {podeAgir && <button type="button" className="botao botao--link" onClick={reavaliar} disabled={avaliando}>Tentar de novo</button>}</Aviso></div>
      )}
      {resultado && <ResultadoAvaliacao r={resultado} aoFechar={() => setResultado(null)} aoTentar={reavaliar} avaliando={avaliando} />}

      <EstimativaProximasHoras r={proximas} aoEscolherTerminal={id => mudar(() => setTerminalId(id))} />

      <Abas rotulo="Tipo de alerta" abas={abas} ativa={aba} aoMudar={id => mudar(() => setAba(id))} />
      {TIPOS[aba] && <p className="fraco" style={{ fontSize: 13, margin: "8px 0 12px" }}>{TIPOS[aba].texto}</p>}

      <div className="grade-detalhe" style={{ marginTop: 12 }}>
        <Cartao id="lista-alertas" titulo={encerrados ? "Todos os alertas já gerados" : "Alertas abertos"}>
          {lista.erro ? <ErroCarga erro={lista.erro} aoTentarDeNovo={lista.recarregar} />
            : !lista.dados ? <Carregando />
            : !itens.length ? <Vazio icone="task_alt" titulo={encerrados ? "Nenhum alerta gerado" : "Nenhum alerta aberto"} texto={encerrados ? null : "Nenhuma regra disparou na última avaliação."} />
            : (
              <>
                <ul className="lista-alertas">
                  {itens.map(a => (
                    <li key={a.id}>
                      <button type="button" className="item-alerta" onClick={() => setSelecionado(a.id)}
                        aria-pressed={selecionado === a.id}
                        style={{ width: "100%", textAlign: "left", background: selecionado === a.id ? "#F1F5FB" : "transparent", border: "none", borderRadius: 8, padding: "12px 8px", cursor: "pointer", color: "inherit" }}>
                        <span className="ponto-estado" style={{ background: `var(--${PRIORIDADE[a.prioridade][0] === "neutro" ? "serie-neutra" : PRIORIDADE[a.prioridade][0]})` }} aria-hidden="true" />
                        <span style={{ minWidth: 0 }}>
                          <span className="linha-flex" style={{ gap: 6 }}>
                            <Selo tipo={TIPOS[a.tipo].selo}>{TIPOS[a.tipo].rotulo}</Selo>
                            {demo && REGRAS_DEMONSTRACAO.has(a.regra) && <Selo tipo="atencao" icone="science" titulo="Baseado no cadastro fictício de exemplo">dados de demonstração</Selo>}
                            <Selo tipo={PRIORIDADE[a.prioridade][0]}>Prioridade {PRIORIDADE[a.prioridade][1].toLowerCase()}</Selo>
                            <span className="item-alerta__meta">{a.terminal || "Todos os terminais"} · {a.periodo}</span>
                          </span>
                          <span className="item-alerta__titulo" style={{ display: "block", marginTop: 4 }}>{a.titulo}</span>
                          <span className="item-alerta__evidencia" style={{ display: "block" }}>{a.evidencia}</span>
                        </span>
                        <span className="pilha" style={{ gap: 4, alignItems: "flex-end" }}>
                          <Selo tipo={ESTADO[a.estado][0]}>{ESTADO[a.estado][1]}</Selo>
                          {!a.ativo && a.estado !== "Resolvido" && a.estado !== "Descartado" && <span className="fraco" style={{ fontSize: 11.5 }}>deixou de ocorrer</span>}
                        </span>
                      </button>
                    </li>
                  ))}
                </ul>
                <Paginacao pagina={pagina} totalPaginas={lista.dados.totalPaginas} total={lista.dados.total} tamanho={20} aoMudar={setPagina} rotulo="alertas" />
              </>
            )}
        </Cartao>
        <div className="painel-detalhe">
          {selecionado ? <DetalheAlerta id={selecionado} podeAgir={podeAgir} aoMudar={() => { lista.recarregar(); resumo.recarregar(); }} />
            : <Cartao><Vazio icone="left_click" titulo="Selecione um alerta" texto="A evidência, o limite usado e o histórico de ações aparecem aqui." /></Cartao>}
        </div>
      </div>
    </>
  );
}

const ROTULO_CONF = { confiavel: "confiável", limitada: "limitada", insuficiente: "dados insuficientes", desatualizada: "dados desatualizados" };
const COMPARACAO = {
  melhorou: ["ok", "A confiabilidade melhorou"], piorou: ["atencao", "A confiabilidade piorou"],
  igual: ["neutro", "A confiabilidade não mudou"], primeira: ["neutro", "Primeira avaliação registrada"]
};

function ResultadoAvaliacao({ r, aoFechar, aoTentar, avaliando }) {
  if (r.situacao === "falhou")
    return <div style={{ marginBottom: 12 }}><Aviso tipo="critico"><b>A reavaliação falhou.</b> {r.erro || r.resumo} <button type="button" className="botao botao--link" onClick={aoTentar} disabled={avaliando}>Tentar de novo</button></Aviso></div>;
  if (r.situacao === "em-andamento")
    return <div style={{ marginBottom: 12 }}><Aviso tipo="info">{r.resumo}</Aviso></div>;
  const [tipo, frase] = COMPARACAO[r.comparacao] || COMPARACAO.igual;
  return (
    <div className="cartao" role="status" style={{ marginBottom: 16 }}>
      <div className="cartao__corpo pilha" style={{ gap: 8 }}>
        <div className="linha-flex" style={{ justifyContent: "space-between" }}>
          <b>Reavaliação concluída às {dataHoraLocal(r.fim).slice(-5)}</b>
          <button type="button" className="botao-icone" aria-label="Fechar resultado" onClick={aoFechar}><Icone nome="close" /></button>
        </div>
        <span style={{ fontSize: 13.5 }}>{r.resumo}</span>
        <span className="fraco num" style={{ fontSize: 12.5 }}>
          {numero(r.registros)} movimentos no banco · dados até {r.dadosAte ? dataHoraPorto(r.dadosAte).slice(0, 10) : "—"}
          {r.ultimaImportacaoEm ? ` · última importação ${dataHoraLocal(r.ultimaImportacaoEm)}` : " · nenhuma importação de planilha confirmada"} · {numero(r.ativos)} alerta(s) aberto(s)
        </span>
        <div className="linha-flex" style={{ gap: 8 }}>
          <Selo tipo={tipo}>{frase}</Selo>
          <span style={{ fontSize: 13 }}>
            Confiabilidade da estimativa: <b>{ROTULO_CONF[r.confiabilidade] || r.confiabilidade}</b>
            {r.confiabilidadeAnterior && r.comparacao !== "igual" && <> (antes: {ROTULO_CONF[r.confiabilidadeAnterior]})</>}
          </span>
        </div>
        {r.comparacao === "igual" && !r.dadosNovos && r.confiabilidade !== "confiavel" && (
          <span className="fraco" style={{ fontSize: 12.5 }}>Sem dados novos a confiabilidade não muda: reavaliar não melhora a base. O que fazer está em "Por que a confiabilidade não é maior", abaixo.</span>
        )}
      </div>
    </div>
  );
}

function DetalheAlerta({ id, podeAgir, aoMudar }) {
  const demo = useAmbiente()?.dadosExemplo;
  const r = useApi(sinal => api.get(`/api/v1/alertas/${id}`, null, sinal), [id]);
  const [texto, setTexto] = useState("");
  const [executar, enviando] = useAcao();

  if (r.erro) return <Cartao><ErroCarga erro={r.erro} aoTentarDeNovo={r.recarregar} /></Cartao>;
  if (!r.dados) return <Cartao><Carregando /></Cartao>;
  const a = r.dados;
  const aberto = a.estado === "Novo" || a.estado === "EmAnalise";

  function agir(rota, mensagem, exigeTexto) {
    if (exigeTexto && !texto.trim()) return;
    executar(() => api.post(`/api/v1/alertas/${id}/${rota}`, { texto: texto.trim() || null }), { sucesso: mensagem })
      .then(ok => { if (ok) { setTexto(""); r.recarregar(); aoMudar(); } });
  }

  return (
    <Cartao id="detalhe-alerta" titulo={a.titulo} acao={<Selo tipo={ESTADO[a.estado][0]}>{ESTADO[a.estado][1]}</Selo>}>
      <div className="pilha" style={{ gap: 14 }}>
        <div className="linha-flex" style={{ gap: 6 }}>
          <Selo tipo={TIPOS[a.tipo].selo}>{TIPOS[a.tipo].rotulo}</Selo>
                            {demo && REGRAS_DEMONSTRACAO.has(a.regra) && <Selo tipo="atencao" icone="science" titulo="Baseado no cadastro fictício de exemplo">dados de demonstração</Selo>}
          <Selo tipo={PRIORIDADE[a.prioridade][0]}>Prioridade {PRIORIDADE[a.prioridade][1].toLowerCase()}</Selo>
          {!a.ativo && <Selo>Deixou de ocorrer</Selo>}
        </div>
        <p style={{ fontSize: 13.5, margin: 0 }}>{a.evidencia}</p>
        <dl className="definicoes">
          <div><dt>Indicador</dt><dd>{a.indicador}</dd></div>
          <div><dt>Valor observado</dt><dd>{a.valorObservado}</dd></div>
          <div><dt>Limite</dt><dd>{a.limite}</dd></div>
          <div><dt>Período</dt><dd>{a.periodo}</dd></div>
          <div><dt>Terminal</dt><dd>{a.terminal || "Todos"}</dd></div>
          <div><dt>Origem dos dados</dt><dd>{a.origem}</dd></div>
          <div><dt>Identificado em</dt><dd>{dataHoraLocal(a.identificadoEm)}</dd></div>
          <div><dt>Responsável</dt><dd>{a.responsavel || "—"}</dd></div>
        </dl>
        {a.tipo === "Previsao" && <Aviso tipo="info">Estimativa calculada pelo padrão histórico. Confirme com a operação antes de agir.</Aviso>}
        {a.acaoRecomendada && <Aviso tipo="info" icone="lightbulb"><b>Ação sugerida:</b> {a.acaoRecomendada}</Aviso>}

        {podeAgir && (
          <div className="pilha" style={{ gap: 8 }}>
            <Campo rotulo={aberto ? "Ação tomada / justificativa" : "Motivo da reabertura (opcional)"} ajuda={aberto ? "Obrigatório para registrar ação, resolver ou descartar." : null}>
              <textarea rows={3} maxLength={1000} value={texto} onChange={e => setTexto(e.target.value)} />
            </Campo>
            <div className="linha-flex">
              {aberto ? (
                <>
                  {a.estado === "Novo" && <button type="button" className="botao botao--secundario botao--pequeno" disabled={enviando} onClick={() => agir("assumir", "Alerta assumido.")}>Assumir</button>}
                  <button type="button" className="botao botao--secundario botao--pequeno" disabled={enviando || !texto.trim()} onClick={() => agir("acoes", "Ação registrada.", true)}>Registrar ação</button>
                  <button type="button" className="botao botao--primario botao--pequeno" disabled={enviando || !texto.trim()} onClick={() => agir("resolver", "Alerta resolvido.", true)}>Resolver</button>
                  <button type="button" className="botao botao--fantasma botao--pequeno" disabled={enviando || !texto.trim()} onClick={() => agir("descartar", "Alerta descartado.", true)}>Descartar</button>
                </>
              ) : (
                <button type="button" className="botao botao--secundario botao--pequeno" disabled={enviando} onClick={() => agir("reabrir", "Alerta reaberto.")}>Reabrir</button>
              )}
            </div>
          </div>
        )}

        <div>
          <b style={{ fontSize: 13 }}>Histórico</b>
          <ol className="linha-tempo" style={{ marginTop: 6 }}>
            {(a.eventos || []).slice().reverse().map((e, i) => (
              <li key={i}><span className="num">{dataHoraLocal(e.quando)}</span><span><b>{e.acao}</b> · {e.autor}{e.texto && <><br />{e.texto}</>}</span></li>
            ))}
          </ol>
        </div>
      </div>
    </Cartao>
  );
}
