import { useState } from "react";
import { api, baixarArquivo } from "../api/cliente.js";
import { useApi } from "../hooks/useApi.js";
import { useAcao } from "../hooks/useAcao.js";
import { Carregando, ErroCarga, Vazio } from "../components/EstadoCarga.jsx";
import { CabecalhoPagina, Cartao, Selo, Icone, Aviso, Campo, Paginacao } from "../components/ui.jsx";
import { duracao, dataHoraPorto, dataHoraLocal, numero } from "../utils/formatar.js";

// Movimentações: todos os registros de marcação/liberação, com filtros,
// paginação no servidor e a origem de cada linha (carga inicial, planilha
// com número da linha, ou integração).

const ORIGEM = { CargaInicial: "Carga inicial", Planilha: "Planilha", Integracao: "Integração" };
const TAMANHO = 50;
const VAZIO = { busca: "", terminalId: "", de: "", ate: "", convenio: "", qualidade: "", origem: "" };

export default function Movimentacoes() {
  const [rascunho, setRascunho] = useState(VAZIO);
  const [filtros, setFiltros] = useState(VAZIO);
  const [ordenacao, setOrdenacao] = useState("marcacao_desc");
  const [pagina, setPagina] = useState(1);
  const [selecionado, setSelecionado] = useState(null);
  const [executar, exportando] = useAcao();

  const terminais = useApi(sinal => api.get("/api/v1/terminais", null, sinal), []);
  const limites = useApi(sinal => api.get("/api/v1/configuracoes", null, sinal).catch(() => null), []);
  const limiteLonga = limites.dados?.parametros?.limiteEsperaLongaHoras ?? 72;

  const consulta = {
    busca: filtros.busca.trim() || undefined, terminalId: filtros.terminalId, de: filtros.de, ate: filtros.ate,
    convenio: filtros.convenio.trim() || undefined, origem: filtros.origem,
    semLiberacao: filtros.qualidade === "sem-liberacao" ? true : undefined,
    esperaAcimaHoras: filtros.qualidade === "espera-longa" ? limiteLonga : undefined,
    ordenacao
  };
  const lista = useApi(sinal => api.get("/api/v1/marcacoes", { ...consulta, pagina, tamanhoPagina: TAMANHO }, sinal), [JSON.stringify(consulta), pagina]);

  function aplicar(e) { e?.preventDefault(); setFiltros(rascunho); setPagina(1); setSelecionado(null); }
  function limpar() { setRascunho(VAZIO); setFiltros(VAZIO); setPagina(1); setSelecionado(null); }
  function ordenar(campo) {
    setOrdenacao(o => o === `${campo}_desc` ? `${campo}_asc` : `${campo}_desc`);
    setPagina(1);
  }
  const set = campo => e => setRascunho(r => ({ ...r, [campo]: e.target.value }));
  const seta = campo => ordenacao.startsWith(campo) ? (ordenacao.endsWith("asc") ? "arrow_upward" : "arrow_downward") : "unfold_more";
  const ariaOrd = campo => ordenacao.startsWith(campo) ? (ordenacao.endsWith("asc") ? "ascending" : "descending") : "none";
  const ativos = Object.entries(filtros).filter(([, v]) => v).length;

  return (
    <>
      <CabecalhoPagina titulo="Movimentações" subtitulo="Cada carreta marcada nos terminais: quando chegou, quando foi liberada e de onde veio o registro">
        <button type="button" className="botao botao--secundario" disabled={exportando || !lista.dados?.total}
          title="Exporta as carretas já liberadas no terminal, período e convênio filtrados (os demais filtros não se aplicam ao CSV)"
          onClick={() => executar(() => baixarArquivo("/api/v1/marcacoes/exportar", { terminalId: filtros.terminalId, de: filtros.de, ate: filtros.ate, convenio: consulta.convenio }, `movimentacoes_${filtros.terminalId || "todos"}.csv`), { sucesso: "CSV gerado." })}>
          <Icone nome="download" /> CSV das liberadas
        </button>
      </CabecalhoPagina>

      <form className="cartao barra-filtros" onSubmit={aplicar} style={{ padding: "14px 16px", marginBottom: 16 }} aria-label="Filtros">
        <Campo rotulo="Movimento ou senha"><input type="search" value={rascunho.busca} onChange={set("busca")} placeholder="ex.: 1234567" maxLength={32} /></Campo>
        <Campo rotulo="Terminal">
          <select value={rascunho.terminalId} onChange={set("terminalId")}>
            <option value="">Todos</option>
            {(terminais.dados || []).map(t => <option key={t.id} value={t.id}>{t.nome}</option>)}
          </select>
        </Campo>
        <Campo rotulo="Marcação de"><input type="date" value={rascunho.de} onChange={set("de")} /></Campo>
        <Campo rotulo="até"><input type="date" value={rascunho.ate} min={rascunho.de || undefined} onChange={set("ate")} /></Campo>
        <Campo rotulo="Convênio"><input value={rascunho.convenio} onChange={set("convenio")} placeholder="contém…" maxLength={100} /></Campo>
        <Campo rotulo="Situação">
          <select value={rascunho.qualidade} onChange={set("qualidade")}>
            <option value="">Todas</option>
            <option value="sem-liberacao">Sem liberação</option>
            <option value="espera-longa">Espera acima de {limiteLonga} h</option>
          </select>
        </Campo>
        <Campo rotulo="Origem">
          <select value={rascunho.origem} onChange={set("origem")}>
            <option value="">Todas</option>
            {Object.entries(ORIGEM).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </Campo>
        <div className="linha-flex" style={{ alignSelf: "flex-end" }}>
          <button type="submit" className="botao botao--primario"><Icone nome="filter_alt" /> Filtrar</button>
          {ativos > 0 && <button type="button" className="botao botao--link" onClick={limpar}>Limpar ({ativos})</button>}
        </div>
      </form>

      <div className={selecionado ? "grade-detalhe" : undefined}>
        <Cartao id="tabela-mov" corpo={false}>
          {lista.erro ? <div className="cartao__corpo"><ErroCarga erro={lista.erro} aoTentarDeNovo={lista.recarregar} /></div>
            : !lista.dados ? <div className="cartao__corpo"><Carregando /></div>
            : !lista.dados.total ? <div className="cartao__corpo"><Vazio icone="search_off" titulo="Nenhuma movimentação" texto={ativos ? "Nenhum registro com esses filtros." : "Importe planilhas para ver os registros."} /></div>
            : (
              <>
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
    </>
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

