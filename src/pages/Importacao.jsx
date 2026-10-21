import { useRef, useState } from "react";
import { api, baixarArquivo, novaChaveIdempotencia } from "../api/cliente.js";
import { useApi } from "../hooks/useApi.js";
import { useAcao } from "../hooks/useAcao.js";
import { Carregando, ErroCarga, Vazio } from "../components/EstadoCarga.jsx";
import { CabecalhoPagina, Cartao, Selo, Icone, Aviso, Campo, Paginacao, Abas, Kpi } from "../components/ui.jsx";
import { numero, dataHoraPorto, dataHoraLocal, tamanhoArquivo, duracao, dataCurta } from "../utils/formatar.js";

// Importação de planilhas (XLSX/CSV) em três etapas: enviar → revisar → confirmar.
// Nada é gravado em marcações até a confirmação. Regras: docs/IMPORTACAO.md.

const STATUS = { Analisado: ["atencao", "Aguardando confirmação"], Confirmado: ["ok", "Confirmada"], Cancelado: ["neutro", "Cancelada"] };
const CLASSE = { Novo: ["ok", "Novo"], Complementar: ["info", "Completa dados"], Conflito: ["critico", "Conflito"], Rejeitado: ["neutro", "Rejeitada"] };
const DECISAO = { Pendente: ["atencao", "Pendente"], Aplicar: ["info", "Usar a planilha"], Manter: ["neutro", "Manter atual"], NaoSeAplica: ["neutro", "—"] };
const AVISO = { EsperaAcima72h: "Espera acima de 72 h", SemLiberacao: "Sem liberação", PossivelRemarcacao: "Possível remarcação", ConvenioNovo: "Convênio novo" };

export default function Importacao({ papel, irPara }) {
  const gestor = papel === "Gestor";
  const [loteId, setLoteId] = useState(null);
  const historico = useApi(sinal => api.get("/api/v1/importacoes", { tamanhoPagina: 20 }, sinal), []);

  return (
    <>
      <CabecalhoPagina titulo="Importação de dados" subtitulo="Planilhas de marcação e liberação dos terminais (.xlsx ou .csv) — nada é gravado antes da sua confirmação" />
      {!gestor && <Aviso tipo="info">Você pode consultar o histórico. Enviar, decidir conflitos e confirmar importações é restrito ao perfil Gestor.</Aviso>}

      {loteId
        ? <RevisaoLote id={loteId} gestor={gestor} aoVoltar={() => { setLoteId(null); historico.recarregar(); }} irPara={irPara} />
        : gestor && <Envio aoAnalisar={lote => { setLoteId(lote.id); historico.recarregar(); }} />}

      <Cartao id="historico-imp" titulo="Histórico de importações" corpo={false}>
        {historico.erro ? <div className="cartao__corpo"><ErroCarga erro={historico.erro} aoTentarDeNovo={historico.recarregar} /></div>
          : !historico.dados ? <div className="cartao__corpo"><Carregando /></div>
          : !historico.dados.itens.length ? <div className="cartao__corpo"><Vazio icone="upload_file" titulo="Nenhuma importação ainda" /></div>
          : (
            <div className="tabela-wrap">
              <table className="tabela--clicavel" style={{ minWidth: 900 }}>
                <thead>
                  <tr><th scope="col">Nº</th><th scope="col">Arquivo</th><th scope="col">Terminal</th><th scope="col">Período das marcações</th>
                    <th scope="col" className="num">Linhas</th><th scope="col" className="num">Inseridas</th><th scope="col" className="num">Atualizadas</th>
                    <th scope="col">Situação</th><th scope="col">Enviada</th></tr>
                </thead>
                <tbody>
                  {historico.dados.itens.map(l => (
                    <tr key={l.id} className={loteId === l.id ? "selecionada" : ""} tabIndex={0} onClick={() => setLoteId(l.id)}
                      onKeyDown={e => { if (e.key === "Enter") setLoteId(l.id); }}>
                      <td className="num">{l.id}</td>
                      <td style={{ maxWidth: 260, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }} title={l.arquivo}>{l.arquivo}</td>
                      <td>{l.terminal || l.terminalId}</td>
                      <td className="num">{l.marcacaoInicial ? `${dataCurta(l.marcacaoInicial)} a ${dataCurta(l.marcacaoFinal)}/${l.marcacaoFinal.slice(0, 4)}` : "—"}</td>
                      <td className="num">{numero(l.linhas)}</td>
                      <td className="num">{l.status === "Confirmado" ? numero(l.inseridos) : "—"}</td>
                      <td className="num">{l.status === "Confirmado" ? numero(l.atualizados) : "—"}</td>
                      <td><Selo tipo={STATUS[l.status][0]}>{STATUS[l.status][1]}</Selo></td>
                      <td className="num">{dataHoraLocal(l.criadoEm)}<br /><span className="fraco">{l.criadoPor}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
      </Cartao>
    </>
  );
}

function Etapas({ atual }) {
  const etapas = ["Enviar arquivo", "Revisar", "Confirmar"];
  return (
    <ol className="etapas" aria-label="Etapas da importação" style={{ marginBottom: 14 }}>
      {etapas.map((e, i) => (
        <li key={e} className={i < atual ? "feita" : i === atual ? "atual" : ""} aria-current={i === atual ? "step" : undefined}>
          <span className="circulo">{i < atual ? <Icone nome="check" tamanho={15} /> : i + 1}</span>{e}
        </li>
      ))}
    </ol>
  );
}

function Envio({ aoAnalisar }) {
  const terminais = useApi(sinal => api.get("/api/v1/terminais", null, sinal), []);
  const [terminalId, setTerminalId] = useState("");
  const [arquivo, setArquivo] = useState(null);
  const [arrastando, setArrastando] = useState(false);
  const [erro, setErro] = useState(null);
  const [executar, enviando] = useAcao();
  const entrada = useRef(null);

  function escolher(f) {
    setErro(null);
    if (!f) return;
    const ext = f.name.toLowerCase().split(".").pop();
    if (!["xlsx", "csv"].includes(ext)) { setErro("Envie um arquivo .xlsx ou .csv."); return; }
    if (f.size > 50_000_000) { setErro("Arquivo maior que 50 MB."); return; }
    setArquivo(f);
  }

  function enviar() {
    const form = new FormData();
    form.append("terminalId", terminalId);
    form.append("arquivo", arquivo);
    executar(() => api.enviar("/api/v1/importacoes", form, novaChaveIdempotencia()), { sucesso: "Arquivo analisado. Revise antes de confirmar." })
      .then(lote => { if (lote && lote.id) aoAnalisar(lote); });
  }

  return (
    <Cartao id="envio" titulo="Nova importação">
      <Etapas atual={0} />
      <div className="pilha" style={{ gap: 14 }}>
        <Campo rotulo="Terminal da planilha" ajuda="Cada arquivo traz as marcações de um terminal.">
          <select value={terminalId} onChange={e => setTerminalId(e.target.value)} required>
            <option value="">Selecione…</option>
            {(terminais.dados || []).map(t => <option key={t.id} value={t.id}>{t.nome}</option>)}
          </select>
        </Campo>
        <div className={`zona-arquivo ${arrastando ? "arrastando" : ""}`}
          onDragOver={e => { e.preventDefault(); setArrastando(true); }} onDragLeave={() => setArrastando(false)}
          onDrop={e => { e.preventDefault(); setArrastando(false); escolher(e.dataTransfer.files?.[0]); }}>
          <Icone nome="upload_file" tamanho={32} />
          {arquivo
            ? <p style={{ margin: "8px 0" }}><b style={{ color: "var(--tinta)" }}>{arquivo.name}</b> · {tamanhoArquivo(arquivo.size)}</p>
            : <p style={{ margin: "8px 0" }}>Arraste o arquivo aqui ou</p>}
          <button type="button" className="botao botao--secundario botao--pequeno" onClick={() => entrada.current?.click()}>{arquivo ? "Trocar arquivo" : "Escolher arquivo"}</button>
          <input ref={entrada} type="file" accept=".xlsx,.csv" className="sr-only" aria-label="Arquivo da planilha" onChange={e => { escolher(e.target.files?.[0]); e.target.value = ""; }} />
          <p className="fraco" style={{ fontSize: 12, marginTop: 10 }}>
            Colunas obrigatórias: COD_MOVIMENTO, SENHA, CONVENIO, DATA_MARCACAO, HORA_MARCACAO.
            Opcionais: COD_CONVENIO, CICLO, DATA_LIBERACAO, HORA_LIBERACAO. Até 50 MB.
          </p>
        </div>
        {erro && <Aviso tipo="critico">{erro}</Aviso>}
        <div className="linha-flex">
          <button type="button" className="botao botao--primario" disabled={!terminalId || !arquivo || enviando} onClick={enviar}>
            <Icone nome="fact_check" /> {enviando ? "Analisando…" : "Analisar arquivo"}
          </button>
          <span className="fraco" style={{ fontSize: 12.5 }}>A análise compara cada linha com o que já existe. Nenhum dado é alterado nesta etapa.</span>
        </div>
      </div>
    </Cartao>
  );
}

function RevisaoLote({ id, gestor, aoVoltar, irPara }) {
  const lote = useApi(sinal => api.get(`/api/v1/importacoes/${id}`, null, sinal), [id]);
  const [aba, setAba] = useState(null);
  const [pagina, setPagina] = useState(1);
  const [executar, ocupado] = useAcao();
  const abaPadrao = !lote.dados ? null : lote.dados.conflitantes ? "Conflito" : lote.dados.rejeitados ? "Rejeitado" : lote.dados.novos ? "Novo" : "Complementar";
  const abaAtiva = aba ?? abaPadrao;
  const linhas = useApi(sinal => abaAtiva ? api.get(`/api/v1/importacoes/${id}/linhas`, { classificacao: abaAtiva, pagina, tamanhoPagina: 25 }, sinal) : Promise.resolve(null), [id, abaAtiva, pagina]);

  if (lote.erro) return <Cartao><ErroCarga erro={lote.erro} aoTentarDeNovo={lote.recarregar} /></Cartao>;
  if (!lote.dados) return <Cartao><Carregando texto="Carregando análise…" /></Cartao>;
  const l = lote.dados;
  const analisado = l.status === "Analisado";

  function decidir(decisao, linhasIds) {
    executar(() => api.post(`/api/v1/importacoes/${id}/conflitos`, { decisao, linhas: linhasIds || [] }), { sucesso: r => `${numero(r.decididas)} conflito(s) decidido(s).` })
      .then(ok => { if (ok) { lote.recarregar(); linhas.recarregar(); } });
  }
  function confirmar() {
    executar(() => api.post(`/api/v1/importacoes/${id}/confirmar`, null, novaChaveIdempotencia()), { sucesso: r => `Importação confirmada: ${numero(r.inseridos)} inseridas, ${numero(r.atualizados)} atualizadas.` })
      .then(ok => { if (ok) lote.recarregar(); });
  }
  function cancelar() {
    executar(() => api.post(`/api/v1/importacoes/${id}/cancelar`), { sucesso: "Importação cancelada. Nada foi gravado." })
      .then(ok => { if (ok) lote.recarregar(); });
  }

  const abas = [
    { id: "Conflito", rotulo: "Conflitos", contagem: l.conflitantes },
    { id: "Novo", rotulo: "Novos", contagem: l.novos },
    { id: "Complementar", rotulo: "Completam dados", contagem: l.complementares },
    { id: "Rejeitado", rotulo: "Rejeitadas", contagem: l.rejeitados }
  ];

  return (
    <Cartao id="revisao" titulo={`Importação nº ${l.id} · ${l.arquivo}`}
      subtitulo={`${l.terminal || l.terminalId} · ${tamanhoArquivo(l.tamanhoBytes)} · enviada por ${l.criadoPor} em ${dataHoraLocal(l.criadoEm)}${l.marcacaoInicial ? ` · marcações de ${dataHoraPorto(l.marcacaoInicial, { ano: true }).slice(0, 10)} a ${dataHoraPorto(l.marcacaoFinal).slice(0, 10)}` : ""}`}
      acao={<button type="button" className="botao botao--link" onClick={aoVoltar}><Icone nome="arrow_back" /> {gestor && analisado ? "Fechar" : "Voltar"}</button>}>
      <Etapas atual={analisado ? 1 : 3} />
      <div className="pilha" style={{ gap: 14 }}>
        <div className="linha-flex"><Selo tipo={STATUS[l.status][0]}>{STATUS[l.status][1]}</Selo>
          {l.concluidoEm && <span className="fraco" style={{ fontSize: 12.5 }}>{l.status === "Confirmado" ? "Confirmada" : "Cancelada"} por {l.concluidoPor} em {dataHoraLocal(l.concluidoEm)}</span>}
        </div>
        {l.mesmoArquivoConfirmadoEm && analisado && <Aviso tipo="atencao">Um arquivo idêntico já foi confirmado para este terminal em {dataHoraLocal(l.mesmoArquivoConfirmadoEm)}. Confirmar de novo não duplica registros, mas provavelmente não é necessário.</Aviso>}

        <section className="grade-kpi" aria-label="Resultado da análise">
          <Kpi rotulo="Linhas lidas" valor={numero(l.linhas)} nota={`${numero(l.iguais)} já existiam iguais`} />
          <Kpi rotulo="Novas" valor={numero(l.novos)} nota={analisado ? "serão inseridas" : "registros que não existiam"} />
          <Kpi rotulo="Completam dados" valor={numero(l.complementares)} nota="preenchem liberação ou código vazios" />
          <Kpi rotulo="Conflitos" valor={numero(l.conflitantes)} nota={l.conflitosPendentes ? `${numero(l.conflitosPendentes)} sem decisão` : "todos decididos"} />
          <Kpi rotulo="Rejeitadas" valor={numero(l.rejeitados)} nota="erro de leitura; não entram" />
        </section>

        {l.status === "Confirmado" && <Aviso tipo="ok">Gravado: {numero(l.inseridos)} inseridas, {numero(l.atualizados)} atualizadas, {numero(l.ignorados)} ignoradas. Os indicadores e alertas já consideram estes dados. <a href="#/visao-geral" onClick={e => { e.preventDefault(); irPara("visao-geral"); }}>Ver Visão Geral</a></Aviso>}

        {(l.comAvisos > 0 || l.conveniosNovos.length > 0) && (
          <div className="grade-2">
            <div>
              <b style={{ fontSize: 13 }}>Sinais de qualidade nas linhas que entram</b>
              <ul style={{ margin: "6px 0 0", paddingLeft: 18, fontSize: 13.5 }} className="num">
                <li>{numero(l.avisos.esperaAcima72h)} com espera acima de 72 h</li>
                <li>{numero(l.avisos.semLiberacao)} sem liberação</li>
                <li>{numero(l.avisos.possivelRemarcacao)} possíveis remarcações (mesma senha, convênio e liberação, marcada de novo em poucos minutos)</li>
                <li>{numero(l.avisos.convenioNovo)} com convênio ainda não cadastrado</li>
              </ul>
              <p className="fraco" style={{ fontSize: 12, marginTop: 6 }}>São avisos: as linhas entram normalmente e podem ser conferidas no relatório.</p>
            </div>
            {l.conveniosNovos.length > 0 && (
              <div>
                <b style={{ fontSize: 13 }}>Convênios novos ({l.conveniosNovos.length})</b>
                <ul style={{ margin: "6px 0 0", paddingLeft: 18, fontSize: 13.5 }}>
                  {l.conveniosNovos.slice(0, 8).map(c => <li key={c.codigo}><span className="id-mono">{c.codigo}</span> {c.nome} → {c.operador} / {c.carga}{c.deduzido && <span className="fraco"> (deduzido do nome)</span>}</li>)}
                </ul>
                {l.conveniosNovos.length > 8 && <p className="fraco" style={{ fontSize: 12 }}>e mais {l.conveniosNovos.length - 8}. Revise em Configurações › Convênios após confirmar.</p>}
              </div>
            )}
          </div>
        )}

        <Abas rotulo="Linhas por classificação" abas={abas} ativa={abaAtiva} aoMudar={a => { setAba(a); setPagina(1); }} />
        {abaAtiva === "Conflito" && analisado && gestor && l.conflitosPendentes > 0 && (
          <div className="aviso aviso--atencao" style={{ alignItems: "center", flexWrap: "wrap" }}>
            <Icone nome="rule" />
            <span style={{ flex: 1, minWidth: 240 }}>Conflito = o registro já existe com valores diferentes. Decida por linha ou para todos os {numero(l.conflitosPendentes)} pendentes.</span>
            <button type="button" className="botao botao--secundario botao--pequeno" disabled={ocupado} onClick={() => decidir("Manter")}>Manter todos como estão</button>
            <button type="button" className="botao botao--secundario botao--pequeno" disabled={ocupado} onClick={() => decidir("Aplicar")}>Usar a planilha em todos</button>
          </div>
        )}
        <TabelaLinhas r={linhas} aba={abaAtiva} pagina={pagina} setPagina={setPagina} podeDecidir={analisado && gestor} decidir={decidir} ocupado={ocupado} />

        <div className="linha-flex" style={{ borderTop: "1px solid var(--linha)", paddingTop: 14 }}>
          <button type="button" className="botao botao--secundario" onClick={() => executar(() => baixarArquivo(`/api/v1/importacoes/${id}/relatorio`, null, `importacao_${id}.csv`))}>
            <Icone nome="download" /> Relatório por linha (CSV)
          </button>
          {analisado && gestor && <>
            <span style={{ flex: 1 }} />
            <button type="button" className="botao botao--fantasma" disabled={ocupado} onClick={cancelar}>Cancelar importação</button>
            <button type="button" className="botao botao--primario" disabled={ocupado || l.conflitosPendentes > 0} onClick={confirmar}
              title={l.conflitosPendentes > 0 ? "Decida todos os conflitos antes de confirmar" : undefined}>
              <Icone nome="check" /> Confirmar e gravar {numero(l.novos + l.complementares)} linhas
            </button>
          </>}
        </div>
        {analisado && gestor && l.conflitosPendentes > 0 && <p className="fraco" style={{ fontSize: 12.5, textAlign: "right", margin: 0 }}>Decida os {numero(l.conflitosPendentes)} conflitos pendentes para liberar a confirmação.</p>}
      </div>
    </Cartao>
  );
}

function TabelaLinhas({ r, aba, pagina, setPagina, podeDecidir, decidir, ocupado }) {
  if (r.erro) return <ErroCarga erro={r.erro} aoTentarDeNovo={r.recarregar} />;
  if (!r.dados) return <Carregando />;
  if (!r.dados.total) return <Vazio icone="check_circle" titulo="Nenhuma linha nesta classificação" />;
  const conflito = aba === "Conflito";
  return (
    <>
      <div className="tabela-wrap">
        <table style={{ minWidth: 900 }}>
          <thead>
            <tr><th scope="col" className="num">Linha</th><th scope="col">Movimento</th><th scope="col">Senha</th><th scope="col">Convênio</th>
              <th scope="col">Marcação</th><th scope="col">Liberação</th><th scope="col" className="num">Espera</th>
              <th scope="col">{conflito ? "Diferenças" : aba === "Rejeitado" ? "Motivo" : "Avisos"}</th>
              {conflito && <th scope="col">Decisão</th>}</tr>
          </thead>
          <tbody>
            {r.dados.itens.map(x => (
              <tr key={x.id}>
                <td className="num">{x.numeroLinha}</td>
                <td className="id-mono">{x.movimentoId || "—"}</td>
                <td className="id-mono">{x.senha || "—"}</td>
                <td>{x.convenio || "—"}</td>
                <td className="num">{x.dataMarcacao ? dataHoraPorto(x.dataMarcacao) : "—"}</td>
                <td className="num">{x.dataLiberacao ? dataHoraPorto(x.dataLiberacao) : "—"}</td>
                <td className="num">{duracao(x.esperaHoras)}</td>
                <td style={{ fontSize: 12.5, maxWidth: 320 }}>
                  {x.motivo && <span>{x.motivo}</span>}
                  {!conflito && x.avisos.length > 0 && <span className="linha-flex" style={{ gap: 4 }}>{x.avisos.map(a => <Selo key={a} tipo="atencao">{AVISO[a] || a}</Selo>)}</span>}
                  {!x.motivo && !x.avisos.length && <span className="fraco">—</span>}
                </td>
                {conflito && (
                  <td>
                    {podeDecidir ? (
                      <select aria-label={`Decisão da linha ${x.numeroLinha}`} value={x.decisao} disabled={ocupado}
                        onChange={e => e.target.value !== "Pendente" && decidir(e.target.value, [x.id])}>
                        <option value="Pendente" disabled>Pendente</option>
                        <option value="Manter">Manter atual</option>
                        <option value="Aplicar">Usar a planilha</option>
                      </select>
                    ) : <Selo tipo={DECISAO[x.decisao][0]}>{DECISAO[x.decisao][1]}</Selo>}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Paginacao pagina={pagina} totalPaginas={r.dados.totalPaginas} total={r.dados.total} tamanho={25} aoMudar={setPagina} rotulo="linhas" />
    </>
  );
}
