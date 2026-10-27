import { useEffect, useMemo, useState } from "react";
import { ResponsiveContainer, BarChart, Bar, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, Cell } from "recharts";
import { api, baixarArquivoPost, mensagemErro } from "../api/cliente.js";
import { useApi } from "../hooks/useApi.js";
import { useAcao } from "../hooks/useAcao.js";
import { CabecalhoPagina, Cartao, Campo, Icone, Selo, Segmentos, Aviso, Paginacao } from "../components/ui.jsx";

// Central de relatórios: escolher → filtrar → prévia → exportar.
// A prévia é o MESMO documento que o servidor transforma em PDF e Excel
// (GET /api/v1/relatorios/previa), por isso os números não mudam na exportação.
// Nenhum cálculo aqui: a tela só desenha o que a API devolve.

const ICONES = { gerencial: "summarize", operacional: "fact_check", analise360: "query_stats" };
const CORES = {
  marca: "#1F4E88", ambar: "#F2A93B", neutra: "#B8C4D6", estimado: "#9AA9BE",
  unitapajos: "#1F4E88", tgpm: "#E0861A", hidrovias: "#2A9D8F"
};

function lerEndereco() {
  const qs = new URLSearchParams(window.location.hash.split("?")[1] || "");
  return { tipo: qs.get("tipo"), de: qs.get("de"), ate: qs.get("ate"), terminalId: qs.get("terminalId") };
}

const dataHoraBr = iso => iso ? new Date(iso).toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "short" }) : "";
const tamanho = b => b > 1024 * 1024 ? `${(b / 1024 / 1024).toFixed(1).replace(".", ",")} MB` : `${Math.max(1, Math.round(b / 1024))} KB`;

export default function Relatorios() {
  const opcoes = useApi(sinal => api.get("/api/v1/relatorios/opcoes", null, sinal), []);
  const [paginaHist, setPaginaHist] = useState(1);
  const historico = useApi(sinal => api.get("/api/v1/relatorios/historico", { pagina: paginaHist, tamanho: 10 }, sinal), [paginaHist]);

  const inicial = useMemo(lerEndereco, []);
  const [tipo, setTipo] = useState(inicial.tipo || "gerencial");
  const [mes, setMes] = useState(null);
  const [de, setDe] = useState(inicial.de || "");
  const [ate, setAte] = useState(inicial.ate || "");
  const [terminalId, setTerminalId] = useState(inicial.terminalId || "");
  const [operador, setOperador] = useState("");
  const [carga, setCarga] = useState("");
  const [detalhamento, setDetalhamento] = useState("completo");

  const [previa, setPrevia] = useState(null);           // { doc, filtro }
  const [erroPrevia, setErroPrevia] = useState(null);
  const [gerarPrevia, gerando] = useAcao();
  const [exportar, exportando] = useAcao();
  const [formatoEmAndamento, setFormatoEmAndamento] = useState(null);

  const o = opcoes.dados;
  const vinculado = o?.terminalVinculado;

  // Período padrão: o pedido pelo endereço, senão o último mês completo.
  useEffect(() => {
    if (!o || mes !== null) return;
    const doEndereco = inicial.de && inicial.ate ? o.meses.find(m => m.de === inicial.de && m.ate === inicial.ate) : null;
    if (inicial.de && inicial.ate && !doEndereco) setMes("personalizado");
    else {
      const m = doEndereco || o.meses.find(x => x.de === o.padraoDe) || o.meses[o.meses.length - 1];
      setMes(m?.chave ?? "personalizado");
      if (m) { setDe(m.de); setAte(m.ate); }
    }
    if (vinculado) setTerminalId(vinculado);
  }, [o]); // eslint-disable-line react-hooks/exhaustive-deps

  const filtro = useMemo(() => ({
    tipo, de: de || null, ate: ate || null, terminalId: terminalId || null,
    operador: operador || null, carga: carga || null, detalhamento
  }), [tipo, de, ate, terminalId, operador, carga, detalhamento]);
  const previaDesatualizada = previa && JSON.stringify(previa.filtro) !== JSON.stringify(filtro);
  const periodoValido = de && ate && de <= ate;

  function escolherMes(chave) {
    setMes(chave);
    if (chave === "personalizado") return;
    if (chave === "tudo") {
      // Todo o histórico: da primeira à última marcação entre os terminais disponíveis.
      setDe(o.terminais.map(t => t.primeiraMarcacao).sort()[0]);
      setAte(o.terminais.map(t => t.ultimaMarcacao).sort().slice(-1)[0]);
      return;
    }
    const m = o.meses.find(x => x.chave === chave);
    if (m) { setDe(m.de); setAte(m.ate); }
  }

  async function montarPrevia() {
    setErroPrevia(null);
    const f = filtro;
    const doc = await gerarPrevia(async () => {
      try {
        return await api.get("/api/v1/relatorios/previa", f);
      } catch (e) {
        setErroPrevia(mensagemErro(e));
        throw e;
      }
    });
    if (doc) setPrevia({ doc, filtro: f });
  }

  async function emitir(formato) {
    setFormatoEmAndamento(formato);
    const r = await exportar(
      () => baixarArquivoPost("/api/v1/relatorios/exportar", { formato }, previa.filtro, `relatorio.${formato}`),
      { sucesso: x => `Relatório ${x.codigo} gerado (${formato.toUpperCase()}).`, erro: "Não foi possível gerar o relatório" });
    setFormatoEmAndamento(null);
    if (r) historico.recarregar();
  }

  async function reemitir(item, formato) {
    setFormatoEmAndamento(`h${item.id}`);
    const r = await exportar(
      () => baixarArquivoPost(`/api/v1/relatorios/historico/${item.id}/reemitir`, { formato }, undefined, `relatorio.${formato}`),
      { sucesso: x => `Relatório emitido de novo: ${x.codigo}.`, erro: "Não foi possível emitir de novo" });
    setFormatoEmAndamento(null);
    if (r) historico.recarregar();
  }

  return (
    <>
      <CabecalhoPagina titulo="Relatórios" subtitulo="Escolha o relatório, o período e o recorte · confira a prévia · exporte em PDF ou Excel" />

      {opcoes.erro && <Aviso tipo="critico">Não foi possível carregar as opções de relatório: {mensagemErro(opcoes.erro)}</Aviso>}

      <ol className="rel-etapas" aria-label="Etapas">
        <li className="feita">1. Relatório</li>
        <li className={periodoValido ? "feita" : ""}>2. Período e filtros</li>
        <li className={previa && !previaDesatualizada ? "feita" : ""}>3. Prévia</li>
        <li>4. Exportar</li>
      </ol>

      {/* 1. Tipo */}
      <div className="rel-tipos" role="radiogroup" aria-label="Tipo de relatório">
        {(o?.tipos || []).map(t => (
          <button key={t.id} type="button" role="radio" aria-checked={tipo === t.id} className={`rel-tipo ${tipo === t.id ? "ativo" : ""}`} onClick={() => setTipo(t.id)}>
            <Icone nome={ICONES[t.id] || "description"} />
            <b>{t.titulo}</b>
            <span>{t.descricao}</span>
          </button>
        ))}
      </div>

      {/* 2. Filtros */}
      <Cartao titulo="Período e filtros" subtitulo="Padrão: o último mês completo com dados de todos os terminais. Mês marcado como parcial tem terminal sem dados no mês inteiro.">
        <div className="barra-filtros rel-filtros">
          <Campo rotulo="Período">
            <select value={mes ?? ""} onChange={e => escolherMes(e.target.value)} disabled={!o}>
              {(o?.meses || []).slice().reverse().map(m => <option key={m.chave} value={m.chave}>{m.rotulo}{m.completo ? "" : " (parcial)"}</option>)}
              <option value="tudo">Todo o histórico disponível</option>
              <option value="personalizado">Personalizado…</option>
            </select>
          </Campo>
          {mes === "personalizado" && (
            <>
              <Campo rotulo="De"><input type="date" value={de} onChange={e => setDe(e.target.value)} /></Campo>
              <Campo rotulo="Até"><input type="date" value={ate} min={de} onChange={e => setAte(e.target.value)} /></Campo>
            </>
          )}
          <Campo rotulo="Terminal">
            <select value={terminalId} onChange={e => setTerminalId(e.target.value)} disabled={!!vinculado}
              title={vinculado ? "Seu usuário está vinculado a este terminal (definido pelo Gestor)." : undefined}>
              {!vinculado && <option value="">Todos os terminais</option>}
              {(o?.terminais || []).map(t => <option key={t.id} value={t.id}>{t.nome}</option>)}
            </select>
          </Campo>
          <Campo rotulo="Operador">
            <select value={operador} onChange={e => setOperador(e.target.value)}>
              <option value="">Todos</option>
              {(o?.operadores || []).map(x => <option key={x} value={x}>{x}</option>)}
            </select>
          </Campo>
          <Campo rotulo="Carga">
            <select value={carga} onChange={e => setCarga(e.target.value)}>
              <option value="">Todas</option>
              {(o?.cargas || []).map(x => <option key={x} value={x}>{x}</option>)}
            </select>
          </Campo>
          <Campo rotulo="Detalhamento">
            <Segmentos rotulo="Detalhamento" valor={detalhamento} aoMudar={setDetalhamento}
              opcoes={[{ id: "resumido", rotulo: "Resumido" }, { id: "completo", rotulo: "Completo" }]} />
          </Campo>
          <button type="button" className="botao botao--primario" disabled={!o || !periodoValido || gerando} onClick={montarPrevia}>
            <Icone nome={gerando ? "progress_activity" : "preview"} /> {gerando ? "Gerando prévia…" : "Gerar prévia"}
          </button>
        </div>
        {de && ate && de > ate && <p className="fraco" role="alert" style={{ color: "var(--critico-texto)" }}>A data inicial não pode ser maior que a final.</p>}
        {o && <p className="fraco rel-ajuda">Dias acima da referência usam {o.referenciaDiariaCarretas.toLocaleString("pt-BR")} carretas por dia, por terminal — referência provisória, não capacidade oficial.</p>}
      </Cartao>

      {erroPrevia && <Aviso tipo="critico">{erroPrevia}</Aviso>}

      {/* 3 e 4. Prévia e exportação */}
      {previa && (
        <section className="rel-previa" aria-label="Prévia do relatório">
          <div className="rel-previa__barra">
            <div>
              <b>Prévia</b>
              {previaDesatualizada
                ? <Selo tipo="atencao" icone="sync_problem">Os filtros mudaram — gere a prévia de novo</Selo>
                : <span className="fraco"> · é este o conteúdo que vai para o arquivo</span>}
            </div>
            <div className="rel-previa__acoes">
              <button type="button" className="botao botao--primario" disabled={exportando || previa.doc.semDados} onClick={() => emitir("pdf")}>
                <Icone nome={formatoEmAndamento === "pdf" ? "progress_activity" : "picture_as_pdf"} /> {formatoEmAndamento === "pdf" ? "Gerando PDF…" : "Exportar PDF"}
              </button>
              <button type="button" className="botao botao--secundario" disabled={exportando || previa.doc.semDados} onClick={() => emitir("xlsx")}>
                <Icone nome={formatoEmAndamento === "xlsx" ? "progress_activity" : "table_view"} /> {formatoEmAndamento === "xlsx" ? "Gerando Excel…" : "Exportar Excel"}
              </button>
            </div>
          </div>
          {previa.doc.semDados && <Aviso tipo="atencao">Não há movimentações no período e recorte escolhidos — nada a exportar.</Aviso>}
          <Documento doc={previa.doc} />
        </section>
      )}

      {/* Histórico */}
      <Cartao titulo="Relatórios emitidos" subtitulo="O arquivo não fica guardado: “Emitir de novo” gera outro com os mesmos filtros e os dados de hoje (código novo).">
        {historico.carregando && !historico.dados ? <p className="fraco">Carregando…</p>
          : historico.erro ? <Aviso tipo="critico">{mensagemErro(historico.erro)}</Aviso>
          : historico.dados?.total === 0 ? <p className="fraco">Nenhum relatório emitido ainda.</p>
          : (
            <>
              <div className="tabela-wrap">
                <table className="tabela">
                  <thead><tr><th>Código</th><th>Relatório</th><th>Período</th><th>Abrangência</th><th>Formato</th><th>Emitido por</th><th>Em</th><th>Tamanho</th><th></th></tr></thead>
                  <tbody>
                    {historico.dados?.itens.map(h => (
                      <tr key={h.id}>
                        <td className="id-mono" title={`SHA-256: ${h.hashSha256}`}>{h.codigo}</td>
                        <td>{h.titulo}</td>
                        <td>{h.periodo}</td>
                        <td>{h.abrangencia}</td>
                        <td><Selo tipo={h.formato === "pdf" ? "info" : "ok"}>{h.formato.toUpperCase()}</Selo></td>
                        <td>{h.geradoPor}</td>
                        <td>{dataHoraBr(h.geradoEm)}</td>
                        <td className="num">{tamanho(h.tamanhoBytes)}</td>
                        <td className="direita">
                          <button type="button" className="botao botao--fantasma botao--pequeno" disabled={exportando} onClick={() => reemitir(h, h.formato)}>
                            <Icone nome={formatoEmAndamento === `h${h.id}` ? "progress_activity" : "replay"} /> Emitir de novo
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {historico.dados && historico.dados.total > 10 && (
                <Paginacao pagina={paginaHist} totalPaginas={Math.ceil(historico.dados.total / 10)} total={historico.dados.total} tamanho={10} aoMudar={setPaginaHist} rotulo="relatórios" />
              )}
            </>
          )}
      </Cartao>
    </>
  );
}

/* =========================================================
   Documento (prévia): desenha seções e blocos como vieram da API.
   ========================================================= */

function Documento({ doc }) {
  return (
    <article className="rel-papel">
      <header className="rel-papel__timbre">
        <div className="rel-papel__marca">
          <span className="icone-ancora"><Icone nome="anchor" /></span>
          <div><b>BarcaLog</b><span>Inteligência logística · Barcarena (PA)</span></div>
        </div>
        <div className="rel-papel__codigo">
          <span>{doc.titulo.toUpperCase()}</span>
          <span>Prévia — o código é criado na exportação</span>
        </div>
      </header>
      <div className="rel-papel__corpo">
        <h1>{doc.titulo}</h1>
        <p className="rel-papel__sub">{doc.subtitulo}</p>
        <dl className="rel-ficha">
          <div><dt>Período</dt><dd>{doc.periodoRotulo}</dd></div>
          <div><dt>Abrangência</dt><dd>{doc.abrangencia}</dd></div>
          <div><dt>Emissão</dt><dd>{dataHoraBr(doc.geradoEm)}</dd></div>
          <div><dt>Responsável pelos dados</dt><dd>{doc.organizacao || "Consolidação: sistema BarcaLog"}</dd></div>
        </dl>
        {doc.secoes.map((s, i) => (
          <section key={s.id} className="rel-secao">
            <h2><span>{String(i + 1).padStart(2, "0")}</span>{s.titulo}</h2>
            {s.blocos.map((b, k) => <Bloco key={k} b={b} />)}
          </section>
        ))}
      </div>
      <footer className="rel-papel__rodape">BarcaLog · consolidação de dados de movimentação logística · Fonte: {doc.fonteDados}</footer>
    </article>
  );
}

function Bloco({ b }) {
  switch (b.tipo) {
    case "paragrafo":
      return <p className={`rel-p rel-p--${b.estilo}`}>{b.texto}</p>;
    case "indicadores":
      return (
        <div className="rel-kpis">
          {b.itens.map(i => (
            <div key={i.rotulo} className="rel-kpi">
              <span className="rel-kpi__rotulo">{i.rotulo}</span>
              <b className="rel-kpi__valor">{i.valor}</b>
              {i.comparacao && <span className={`rel-kpi__comp rel-kpi__comp--${i.sentido || "neutro"}`}>{i.comparacao}</span>}
            </div>
          ))}
        </div>
      );
    case "tabela":
      return (
        <figure className="rel-tabela">
          <figcaption>{b.titulo}</figcaption>
          <div className="tabela-wrap">
            <table>
              <thead><tr>{b.colunas.map(c => <th key={c.titulo} className={c.alinhamento === "direita" ? "direita" : ""}>{c.titulo}</th>)}</tr></thead>
              <tbody>
                {b.linhas.map((l, r) => (
                  <tr key={r} className={b.linhasDestacadas.includes(r) ? "destaque" : ""}>
                    {l.map((c, j) => <td key={j} className={b.colunas[j].alinhamento === "direita" ? "num" : ""}>{c}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {b.nota && <p className="rel-nota">{b.nota}</p>}
        </figure>
      );
    case "grafico":
      return <Grafico b={b} />;
    case "lista":
      return <ul className="rel-lista">{b.itens.map((t, i) => <li key={i}>{t}</li>)}</ul>;
    case "quadro":
      return (
        <div className={`rel-quadro rel-quadro--${b.tom}`}>
          <b>{b.titulo}</b>
          <dl>{b.itens.map((i, k) => <div key={k}><dt>{i.rotulo}</dt><dd>{i.valor}</dd></div>)}</dl>
        </div>
      );
    default:
      return null;
  }
}

function Grafico({ b }) {
  const dados = b.categorias.map((c, i) => {
    const linha = { categoria: c };
    b.series.forEach(s => { linha[s.nome] = s.valores[i]; });
    return linha;
  });
  const emHoras = b.unidade.startsWith("horas");
  const fmt = v => (v == null ? "—" : emHoras
    ? `${Math.floor(Math.round(v * 60) / 60)}h${String(Math.round(v * 60) % 60).padStart(2, "0")}`
    : Number(v).toLocaleString("pt-BR", { maximumFractionDigits: 1 }));
  const empilhado = b.tipoGrafico === "barrasEmpilhadas";
  const comum = (
    <>
      <CartesianGrid vertical={false} stroke="#E3E8EF" />
      <XAxis dataKey="categoria" tick={{ fontSize: 11 }} interval="preserveStartEnd" minTickGap={4} />
      <YAxis tick={{ fontSize: 11 }} tickFormatter={fmt} width={48} />
      <Tooltip formatter={fmt} />
      <Legend wrapperStyle={{ fontSize: 12 }} />
    </>
  );
  return (
    <figure className="rel-grafico">
      <figcaption>{b.titulo} <span className="fraco">· {b.unidade}</span></figcaption>
      <div style={{ width: "100%", height: 240 }}>
        <ResponsiveContainer>
          {b.tipoGrafico === "linhas" ? (
            <LineChart data={dados}>
              {comum}
              {b.series.map(s => <Line key={s.nome} dataKey={s.nome} stroke={CORES[s.estimado ? "estimado" : s.cor] || CORES.neutra} strokeDasharray={s.estimado ? "5 4" : undefined} strokeWidth={2} dot={false} connectNulls isAnimationActive={false} />)}
            </LineChart>
          ) : (
            <BarChart data={dados}>
              {comum}
              {b.series.map(s => (
                <Bar key={s.nome} dataKey={s.nome} stackId={empilhado ? "a" : undefined} fill={CORES[s.estimado ? "estimado" : s.cor] || CORES.neutra} isAnimationActive={false}>
                  {!empilhado && b.series.length === 1 && dados.map((_, i) => (
                    <Cell key={i} stroke={b.categoriasDestacadas.includes(i) ? "#D98D1C" : undefined} strokeWidth={b.categoriasDestacadas.includes(i) ? 2 : 0} />
                  ))}
                </Bar>
              ))}
            </BarChart>
          )}
        </ResponsiveContainer>
      </div>
      {b.nota && <p className="rel-nota">{b.nota}</p>}
    </figure>
  );
}
