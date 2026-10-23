import { useEffect, useMemo, useState } from "react";
import { api, baixarArquivo } from "../api/cliente.js";
import { useApi } from "../hooks/useApi.js";
import { useAcao } from "../hooks/useAcao.js";
import { Carregando, ErroCarga, Vazio } from "../components/EstadoCarga.jsx";
import { CabecalhoPagina, Cartao, Selo, Icone, Aviso, Campo, Paginacao, Abas } from "../components/ui.jsx";
import AnaliseMovimentacoes from "../components/AnaliseMovimentacoes.jsx";
import { terminalVinculado, terminalInicial, opcoesTerminal } from "../auth/escopo.js";
import { duracao, dataHoraPorto, dataHoraLocal, numero, formatarDataPtBR } from "../utils/formatar.js";

// Movimentações: a lista de registros (marcação/liberação, com filtros,
// paginação no servidor e origem de cada linha) e a Análise 360° do mesmo
// recorte. Filtros e aba ficam no endereço (#/movimentacoes?aba=analise&...):
// atualizar a página, voltar do detalhamento ou compartilhar o link mantém o recorte.

const ORIGEM = { CargaInicial: "Carga inicial", Planilha: "Planilha", Integracao: "Integração" };
const DIAS = ["domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado"];
const TAMANHO = 50;
const CAMPOS = ["busca", "terminalId", "de", "ate", "convenio", "convenioExato", "operador", "carga", "qualidade", "origem", "hora", "diaSemana", "agrupar"];
const VAZIO = Object.fromEntries(CAMPOS.map(c => [c, ""]));
/** Filtros que só chegam pelo detalhamento da análise: aparecem como etiquetas removíveis. */
const ETIQUETAS = {
  convenioExato: v => `Convênio: ${v}`,
  hora: v => `Marcadas às ${String(v).padStart(2, "0")}h`,
  diaSemana: v => `Marcadas ${["no", "na", "na", "na", "na", "na", "no"][v] || "no"} ${DIAS[v] || v}`
};

function lerEndereco() {
  const qs = new URLSearchParams(window.location.hash.split("?")[1] || "");
  const filtros = { ...VAZIO };
  for (const c of CAMPOS) filtros[c] = qs.get(c) || "";
  if (!qs.has("terminalId")) filtros.terminalId = terminalInicial();
  const v = terminalVinculado();
  if (v) filtros.terminalId = v;
  return { aba: qs.get("aba") === "analise" ? "analise" : "lista", filtros };
}

function escreverEndereco(aba, filtros) {
  const qs = new URLSearchParams();
  if (aba === "analise") qs.set("aba", "analise");
  for (const c of CAMPOS) if (filtros[c] !== "" && filtros[c] != null) qs.set(c, filtros[c]);
  // Terminal "todos" explícito (para não cair de novo no terminal preferido).
  if (!filtros.terminalId) qs.set("terminalId", "");
  window.location.hash = `/movimentacoes?${qs.toString()}`;
}

export default function Movimentacoes() {
  const [estado, setEstado] = useState(lerEndereco);
  useEffect(() => {
    const aoMudar = () => setEstado(lerEndereco());
    window.addEventListener("hashchange", aoMudar);
    return () => window.removeEventListener("hashchange", aoMudar);
  }, []);
  const { aba, filtros } = estado;
  const [rascunho, setRascunho] = useState(filtros);
  useEffect(() => setRascunho(filtros), [JSON.stringify(filtros)]); // eslint-disable-line react-hooks/exhaustive-deps

  const terminais = useApi(sinal => api.get("/api/v1/terminais", null, sinal), []);
  const convenios = useApi(sinal => api.get("/api/v1/convenios", null, sinal).catch(() => []), []);
  const limites = useApi(sinal => api.get("/api/v1/configuracoes", null, sinal).catch(() => null), []);
  const limiteLonga = limites.dados?.parametros?.limiteEsperaLongaHoras ?? 72;
  const operadores = useMemo(() => [...new Set((convenios.dados || []).map(c => c.operador))].sort(), [convenios.dados]);
  const cargas = useMemo(() => [...new Set((convenios.dados || []).map(c => c.carga))].sort(), [convenios.dados]);
  const vinculado = terminalVinculado();

  function navegar(novaAba, novos) { escreverEndereco(novaAba, novos); window.scrollTo(0, 0); }
  function aplicar(e) { e?.preventDefault(); navegar(aba, rascunho); }
  function limpar() { navegar(aba, { ...VAZIO, terminalId: vinculado || "" }); }
  function remover(campo) { navegar(aba, { ...filtros, [campo]: "" }); }
  /** Da análise para a lista: mesmos filtros + a fatia clicada (e o período efetivo da análise). */
  function detalhar(extra) {
    navegar("lista", { ...filtros, busca: "", convenio: "", qualidade: "", hora: "", diaSemana: "", agrupar: "", ...extra });
  }
  const set = campo => e => setRascunho(r => ({ ...r, [campo]: e.target.value }));
  const ativos = CAMPOS.filter(c => c !== "agrupar" && filtros[c] && !(c === "terminalId" && vinculado)).length;
  const lista = aba === "lista";

  return (
    <>
      <CabecalhoPagina titulo="Movimentações" subtitulo="Cada carreta marcada nos terminais: quando chegou, quando foi liberada e de onde veio o registro">
        {lista && <BotaoCsv filtros={filtros} />}
      </CabecalhoPagina>

      <Abas rotulo="Visões de movimentações" ativa={aba} aoMudar={nova => navegar(nova, filtros)}
        abas={[{ id: "lista", rotulo: "Lista" }, { id: "analise", rotulo: "Análise 360°" }]} />

      <form className="cartao barra-filtros" onSubmit={aplicar} style={{ padding: "14px 16px", margin: "12px 0 16px" }} aria-label="Filtros">
        {lista && <Campo rotulo="Movimento ou senha"><input type="search" value={rascunho.busca} onChange={set("busca")} placeholder="ex.: 1234567" maxLength={32} /></Campo>}
        <Campo rotulo="Terminal">
          <select value={rascunho.terminalId} onChange={set("terminalId")} disabled={!!vinculado} title={vinculado ? "Seu usuário está vinculado a este terminal (definido pelo Gestor)." : undefined}>
            {!vinculado && <option value="">Todos</option>}
            {opcoesTerminal(terminais.dados).map(t => <option key={t.id} value={t.id}>{t.nome}</option>)}
          </select>
        </Campo>
        <Campo rotulo="Marcação de"><input type="date" value={rascunho.de} onChange={set("de")} /></Campo>
        <Campo rotulo="até"><input type="date" value={rascunho.ate} min={rascunho.de || undefined} onChange={set("ate")} /></Campo>
        <Campo rotulo="Operador">
          <select value={rascunho.operador} onChange={set("operador")}>
            <option value="">Todos</option>
            {operadores.map(o => <option key={o} value={o}>{o}</option>)}
          </select>
        </Campo>
        <Campo rotulo="Carga">
          <select value={rascunho.carga} onChange={set("carga")}>
            <option value="">Todas</option>
            {cargas.map(c => <option key={c} value={c}>{c}</option>)}
          </select>
        </Campo>
        {lista && <Campo rotulo="Convênio"><input value={rascunho.convenio} onChange={set("convenio")} placeholder="contém…" maxLength={100} /></Campo>}
        {lista && (
          <Campo rotulo="Situação">
            <select value={rascunho.qualidade} onChange={set("qualidade")}>
              <option value="">Todas</option>
              <option value="sem-liberacao">Sem liberação</option>
              <option value="espera-longa">Espera acima de {limiteLonga} h</option>
              <option value="sem-codigo">Sem código de convênio</option>
            </select>
          </Campo>
        )}
        <Campo rotulo="Origem">
          <select value={rascunho.origem} onChange={set("origem")}>
            <option value="">Todas</option>
            {Object.entries(ORIGEM).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </Campo>
        {!lista && (
          <Campo rotulo="Agrupar por">
            <select value={rascunho.agrupar} onChange={set("agrupar")}>
              <option value="">Automático</option>
              <option value="dia">Dia</option>
              <option value="semana">Semana</option>
              <option value="mes">Mês</option>
            </select>
          </Campo>
        )}
        <div className="linha-flex" style={{ alignSelf: "flex-end" }}>
          <button type="submit" className="botao botao--primario"><Icone nome="filter_alt" /> {lista ? "Filtrar" : "Analisar"}</button>
          {ativos > 0 && <button type="button" className="botao botao--link" onClick={limpar}>Limpar ({ativos})</button>}
        </div>
      </form>

      {Object.keys(ETIQUETAS).some(c => filtros[c]) && (
        <div className="linha-flex" style={{ flexWrap: "wrap", marginTop: -8, marginBottom: 12 }} aria-label="Filtros do detalhamento">
          {Object.entries(ETIQUETAS).filter(([c]) => filtros[c]).map(([c, rotulo]) => (
            <button key={c} type="button" className="selo" onClick={() => remover(c)} title="Remover este filtro" style={{ cursor: "pointer", border: "none" }}>
              {rotulo(filtros[c])} <Icone nome="close" tamanho={14} />
            </button>
          ))}
        </div>
      )}
      {vinculado && <div style={{ marginBottom: 12 }}><Aviso tipo="info">Seu usuário está vinculado ao terminal {opcoesTerminal(terminais.dados)[0]?.nome || vinculado}: todas as consultas mostram só ele.</Aviso></div>}

      {lista
        ? <Lista filtros={filtros} limiteLonga={limiteLonga} ativos={ativos} />
        : <AnaliseMovimentacoes filtros={filtros} aoDetalhar={detalhar} />}
    </>
  );
}

function consultaLista(f, limiteLonga) {
  return {
    busca: f.busca.trim() || undefined, terminalId: f.terminalId, de: f.de, ate: f.ate,
    convenio: f.convenio.trim() || undefined, convenioExato: f.convenioExato, operador: f.operador, carga: f.carga, origem: f.origem,
    hora: f.hora, diaSemana: f.diaSemana,
    semLiberacao: f.qualidade === "sem-liberacao" ? true : undefined,
    esperaAcimaHoras: f.qualidade === "espera-longa" ? limiteLonga : undefined,
    semCodigoConvenio: f.qualidade === "sem-codigo" ? true : undefined
  };
}

function BotaoCsv({ filtros }) {
  const [executar, exportando] = useAcao();
  return (
    <button type="button" className="botao botao--secundario" disabled={exportando}
      title="Exporta as carretas já liberadas no terminal, período e convênio filtrados (os demais filtros não se aplicam ao CSV)"
      onClick={() => executar(() => baixarArquivo("/api/v1/marcacoes/exportar", { terminalId: filtros.terminalId, de: filtros.de, ate: filtros.ate, convenio: filtros.convenio.trim() || undefined }, `movimentacoes_${filtros.terminalId || "todos"}.csv`), { sucesso: "CSV gerado." })}>
      <Icone nome="download" /> CSV das liberadas
    </button>
  );
}

function Lista({ filtros, limiteLonga, ativos }) {
  const [ordenacao, setOrdenacao] = useState("marcacao_desc");
  const [pagina, setPagina] = useState(1);
  const [selecionado, setSelecionado] = useState(null);
  const consulta = { ...consultaLista(filtros, limiteLonga), ordenacao };
  const chave = JSON.stringify(consulta);
  useEffect(() => { setPagina(1); setSelecionado(null); }, [chave]);
  const lista = useApi(sinal => api.get("/api/v1/marcacoes", { ...consulta, pagina, tamanhoPagina: TAMANHO }, sinal), [chave, pagina]);

  function ordenar(campo) {
    setOrdenacao(o => o === `${campo}_desc` ? `${campo}_asc` : `${campo}_desc`);
  }
  const seta = campo => ordenacao.startsWith(campo) ? (ordenacao.endsWith("asc") ? "arrow_upward" : "arrow_downward") : "unfold_more";
  const ariaOrd = campo => ordenacao.startsWith(campo) ? (ordenacao.endsWith("asc") ? "ascending" : "descending") : "none";
  const recorte = filtros.de && filtros.ate ? `${formatarDataPtBR(filtros.de)} a ${formatarDataPtBR(filtros.ate)}` : null;

  return (
    <div className={selecionado ? "grade-detalhe" : undefined}>
      <Cartao id="tabela-mov" corpo={false}>
        {lista.erro ? <div className="cartao__corpo"><ErroCarga erro={lista.erro} aoTentarDeNovo={lista.recarregar} /></div>
          : !lista.dados ? <div className="cartao__corpo"><Carregando /></div>
          : !lista.dados.total ? <div className="cartao__corpo"><Vazio icone="search_off" titulo="Nenhuma movimentação" texto={ativos ? "Nenhum registro com esses filtros." : "Importe planilhas para ver os registros."} /></div>
          : (
            <>
              {recorte && <p className="fraco" style={{ fontSize: 12.5, padding: "12px 16px 0", margin: 0 }}>{numero(lista.dados.total)} registros marcados de {recorte}</p>}
              <div className="tabela-wrap">
                <table className="tabela--clicavel" style={{ minWidth: 820 }}>
                  <caption className="sr-only">Movimentações — clique numa linha para ver o detalhe</caption>
                  <thead>
                    <tr>
                      <th scope="col">Movimento</th><th scope="col">Senha</th><th scope="col">Terminal</th><th scope="col">Convênio</th>
                      <th scope="col" aria-sort={ariaOrd("marcacao")}><button type="button" className="th-ordenavel" onClick={() => ordenar("marcacao")}>Marcação <Icone nome={seta("marcacao")} tamanho={15} /></button></th>
                      <th scope="col">Liberação</th>
                      <th scope="col" className="num" aria-sort={ariaOrd("espera")}><button type="button" className="th-ordenavel" onClick={() => ordenar("espera")}>Espera <Icone nome={seta("espera")} tamanho={15} /></button></th>
                      <th scope="col">Origem</th>
                    </tr>
                  </thead>
                  <tbody>
                    {lista.dados.itens.map(m => (
                      <tr key={m.movimentoId} className={selecionado === m.movimentoId ? "selecionada" : ""} onClick={() => setSelecionado(m.movimentoId)}
                        tabIndex={0} onKeyDown={e => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); setSelecionado(m.movimentoId); } }} aria-selected={selecionado === m.movimentoId}>
                        <td className="id-mono">{m.movimentoId}</td>
                        <td className="id-mono">{m.senha}</td>
                        <td>{m.terminal}</td>
                        <td>{m.convenio}{!m.codConvenio && <> <Selo tipo="atencao" titulo="Sem código de convênio">sem código</Selo></>}</td>
                        <td className="num">{dataHoraPorto(m.dataMarcacao)}</td>
                        <td className="num">{m.dataLiberacao ? dataHoraPorto(m.dataLiberacao) : <Selo tipo="atencao">sem liberação</Selo>}</td>
                        <td className="num" style={{ fontWeight: m.esperaHoras > limiteLonga ? 600 : undefined, color: m.esperaHoras > limiteLonga ? "var(--critico-texto)" : undefined }}>{duracao(m.esperaHoras)}</td>
                        <td><Selo>{ORIGEM[m.origem] || m.origem}</Selo></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div style={{ padding: "0 16px 12px" }}>
                <Paginacao pagina={pagina} totalPaginas={lista.dados.totalPaginas} total={lista.dados.total} tamanho={TAMANHO} aoMudar={p => { setPagina(p); setSelecionado(null); }} rotulo="movimentações" />
              </div>
            </>
          )}
      </Cartao>
      {selecionado && (
        <div className="painel-detalhe">
          <DetalheMovimento id={selecionado} limiteLonga={limiteLonga} aoFechar={() => setSelecionado(null)} />
        </div>
      )}
    </div>
  );
}

function DetalheMovimento({ id, limiteLonga, aoFechar }) {
  const r = useApi(sinal => api.get(`/api/v1/marcacoes/${encodeURIComponent(id)}/detalhe`, null, sinal), [id]);
  if (r.erro) return <Cartao><ErroCarga erro={r.erro} aoTentarDeNovo={r.recarregar} /></Cartao>;
  if (!r.dados) return <Cartao><Carregando /></Cartao>;
  const d = r.dados;
  const m = d.movimento;
  return (
    <Cartao id="detalhe-mov" titulo={`Movimento ${m.movimentoId}`}
      acao={<button type="button" className="botao-icone" aria-label="Fechar detalhe" onClick={aoFechar}><Icone nome="close" /></button>}>
      <div className="pilha" style={{ gap: 14 }}>
        <dl className="definicoes">
          <div><dt>Senha</dt><dd className="id-mono">{m.senha}</dd></div>
          <div><dt>Terminal</dt><dd>{m.terminal}</dd></div>
          <div><dt>Convênio</dt><dd>{m.convenio}</dd></div>
          <div><dt>Código do convênio</dt><dd>{m.codConvenio || <Selo tipo="atencao">não informado</Selo>}</dd></div>
          <div><dt>Operador</dt><dd>{m.operador}</dd></div>
          <div><dt>Carga</dt><dd>{m.carga}</dd></div>
          <div><dt>Ciclo</dt><dd>{m.ciclo}</dd></div>
          <div><dt>Espera</dt><dd style={{ color: m.esperaHoras > limiteLonga ? "var(--critico-texto)" : undefined }}>{duracao(m.esperaHoras)}</dd></div>
        </dl>

        <div>
          <b style={{ fontSize: 13 }}>Linha do tempo (horário do porto)</b>
          <ol className="linha-tempo" style={{ marginTop: 6 }}>
            <li><span className="num">{dataHoraPorto(m.dataMarcacao, { segundos: true })}</span><span><b>Marcação</b> · carreta registrada no terminal</span></li>
            {m.dataLiberacao
              ? <li><span className="num">{dataHoraPorto(m.dataLiberacao, { segundos: true })}</span><span><b>Liberação</b> · espera de {duracao(m.esperaHoras)}{d.liberadosNoMesmoInstante > 1 && <><br />Liberada em lote com outras {numero(d.liberadosNoMesmoInstante - 1)} carretas no mesmo instante</>}</span></li>
              : <li><span className="num">—</span><span><b>Sem liberação</b> registrada até a última importação</span></li>}
          </ol>
        </div>

        <div>
          <b style={{ fontSize: 13 }}>Origem do registro</b>
          <dl className="definicoes" style={{ marginTop: 6 }}>
            <div><dt>Origem</dt><dd>{ORIGEM[m.origem] || m.origem}</dd></div>
            {m.origem === "Planilha" && <>
              <div><dt>Arquivo</dt><dd style={{ wordBreak: "break-all" }}>{d.arquivoOrigem || "—"}</dd></div>
              <div><dt>Linha da planilha</dt><dd>{m.linhaOrigem ?? "—"}</dd></div>
              <div><dt>Importação nº</dt><dd>{m.loteImportacaoId ?? "—"}</dd></div>
              <div><dt>Importado em</dt><dd>{d.importadoEm ? dataHoraLocal(d.importadoEm) : "—"}</dd></div>
              <div><dt>Importado por</dt><dd>{d.importadoPor || "—"}</dd></div>
            </>}
          </dl>
          {m.origem === "CargaInicial" && <Aviso tipo="info">Registro da carga inicial do sistema (arquivo de dados entregue com o projeto). Não há linha de planilha associada.</Aviso>}
        </div>
      </div>
    </Cartao>
  );
}

