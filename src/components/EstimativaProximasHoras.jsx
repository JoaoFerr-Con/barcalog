import { Cartao, Icone, Aviso } from "./ui.jsx";
import { Carregando } from "./EstadoCarga.jsx";
import { numero, formatarDataPtBR, dataHoraPorto, dataCurta } from "../utils/formatar.js";

// Estimativa das próximas horas em linguagem operacional. Os números e as
// conclusões vêm prontos de /api/v1/alertas/proximas-horas
// (Application/Metricas/EstimativaJanelas.cs) — aqui só se apresenta.

export const SITUACAO = {
  dentro: { rotulo: "Dentro da capacidade", icone: "check_circle", cor: "var(--ok)", fundo: "var(--ok-fundo)", texto: "var(--ok)" },
  atencao: { rotulo: "Atenção", icone: "warning", cor: "#D9A400", fundo: "var(--atencao-fundo)", texto: "var(--atencao-texto)" },
  risco: { rotulo: "Risco de fila", icone: "error", cor: "var(--critico)", fundo: "var(--critico-fundo)", texto: "var(--critico-texto)" },
  insuficiente: { rotulo: "Dados insuficientes", icone: "help", cor: "var(--borda-forte)", fundo: "var(--neutro-fundo)", texto: "var(--tinta-suave)" }
};
// Classes de confiabilidade (Application/Metricas/EstimativaJanelas.cs → Confiabilidade).
export const CONFIABILIDADE = {
  confiavel: { rotulo: "Histórico confiável", icone: "verified", aviso: "ok" },
  limitada: { rotulo: "Confiabilidade limitada", icone: "info", aviso: "atencao" },
  insuficiente: { rotulo: "Dados insuficientes", icone: "help", aviso: "info" },
  desatualizada: { rotulo: "Dados desatualizados", icone: "warning", aviso: "critico" }
};
const FONTE = {
  cadastrada: "capacidade por hora cadastrada em Configurações",
  observada: "média das liberações registradas nessas mesmas horas e datas (não há capacidade cadastrada)",
  mista: "capacidade cadastrada onde existe; nos demais terminais, média das liberações registradas"
};

const faixa = h => `${String(h).padStart(2, "0")}h–${String((h + 1) % 24).padStart(2, "0")}h`;
const qtd = v => numero(Math.round(v ?? 0));
const plural = (v, um, varios) => (Math.round(v ?? 0) === 1 ? um : varios);

function SeloSituacao({ situacao, compacto }) {
  const s = SITUACAO[situacao] || SITUACAO.insuficiente;
  return (
    <span className="selo" style={{ background: s.fundo, color: s.texto, fontWeight: 600 }}>
      <Icone nome={s.icone} />{compacto && situacao === "dentro" ? "Dentro" : s.rotulo}
    </span>
  );
}

export default function EstimativaProximasHoras({ r, aoEscolherTerminal }) {
  if (r.erro) return null;
  if (!r.dados) return r.carregando ? <Cartao><Carregando texto="Calculando as próximas horas…" /></Cartao> : null;
  const { geral, terminais, horas, geradoEm } = r.dados;
  const res = geral.resumo;
  const janelas = geral.janelas;
  const semDados = geral.confiabilidade === "insuficiente";
  const inicio = janelas[0]?.hora;
  const fim = janelas.length ? (janelas[janelas.length - 1].hora + 1) % 24 : null;
  const horizonte = inicio != null ? `entre ${String(inicio).padStart(2, "0")}h e ${String(fim).padStart(2, "0")}h` : "";
  const escala = Math.max(1, ...janelas.map(j => Math.max(j.demandaPrevista, j.capacidadeAtendimento)));
  const conf = CONFIABILIDADE[geral.confiabilidade];
  const fontes = [...new Set(janelas.filter(j => j.situacao !== "insuficiente").map(j => j.fonteCapacidade))];
  const fonte = fontes.length === 1 ? fontes[0] : "mista";
  const critica = janelas.find(j => j.hora === res.horaMaisCritica);
  // Saldo que importa: o da janela mais pressionada (em "todos", no terminal onde ela ocorre).
  const gravidade = { risco: 3, atencao: 2, dentro: 1, insuficiente: 0 };
  const pressao = (geral.terminalId ? [geral] : terminais)
    .flatMap(t => t.janelas.filter(j => j.situacao === "risco" || j.situacao === "atencao").map(j => ({ terminal: t.terminal, j })))
    .sort((a, b) => gravidade[b.j.situacao] - gravidade[a.j.situacao] || a.j.saldo - b.j.saldo)[0];

  return (
    <Cartao id="proximas-horas" titulo={`Próximas ${horas} horas — estimativa pelo histórico`}
      acao={<span className="linha-flex fraco" style={{ fontSize: 12.5, gap: 4 }}><Icone nome={conf.icone} tamanho={16} />{conf.rotulo}</span>}
      subtitulo={semDados ? null : `${geral.terminal} · baseada no que costuma acontecer nessas horas (${formatarDataPtBR(geral.baseDe)} a ${formatarDataPtBR(geral.baseAte)}). São estimativas, não chegadas confirmadas. Calculada às ${dataHoraPorto(geradoEm).slice(11)}.`}>
      <div className="pilha" style={{ gap: 16 }}>
        <Confiabilidade geral={geral} terminais={terminais} />

        {!semDados && (
          <>
            {/* Resumo do horizonte */}
            <div className="estimativa-resumo">
              <div>
                <span className="estimativa-resumo__rotulo">Demanda prevista</span>
                <b className="num">{qtd(res.demandaPrevista)} <small>{plural(res.demandaPrevista, "carreta", "carretas")}</small></b>
                <span className="fraco">{horizonte} · média das marcações registradas nessas horas nas planilhas dos terminais, de {dataCurta(geral.baseDe)} a {dataCurta(geral.baseAte)}</span>
              </div>
              <div>
                <span className="estimativa-resumo__rotulo">Capacidade de atendimento</span>
                <b className="num">{qtd(res.capacidadeAtendimento)} <small>{plural(res.capacidadeAtendimento, "carreta", "carretas")}</small></b>
                <span className="fraco">{FONTE[fonte]}</span>
              </div>
              <div>
                <span className="estimativa-resumo__rotulo">Saldo de capacidade</span>
                {pressao
                  ? <>
                      <b className="num" style={{ color: pressao.j.situacao === "risco" ? "var(--critico-texto)" : "var(--atencao-texto)" }}>{qtd(-pressao.j.saldo)} <small>acima</small></b>
                      <span className="fraco">{geral.terminalId ? "" : `${pressao.terminal}, `}{faixa(pressao.j.hora)} · nas {horas} h, {res.saldo >= 0 ? `${qtd(res.saldo)} de folga no total` : `${qtd(-res.saldo)} acima no total`}</span>
                    </>
                  : <>
                      <b className="num" style={{ color: "var(--ok)" }}>{qtd(res.saldo)} <small>de folga</small></b>
                      <span className="fraco">atendimento estimado além da demanda nas {horas} h</span>
                    </>}
              </div>
              <div>
                <span className="estimativa-resumo__rotulo">Situação</span>
                <span style={{ marginTop: 4 }}><SeloSituacao situacao={res.piorSituacao} /></span>
                <span className="fraco">{critica ? `Janela que exige atenção: ${faixa(critica.hora)}` : "Nenhuma janela com pressão"}</span>
              </div>
            </div>
            <p className="estimativa-tendencia">
              <Icone nome={res.piorSituacao === "dentro" ? "trending_flat" : /diminu/.test(res.tendencia) ? "trending_down" : "trending_up"} />
              {res.tendencia}
            </p>

            {/* Janelas */}
            <div className="estimativa-janelas" role="list" aria-label="Janelas de uma hora">
              {janelas.map(j => {
                const s = SITUACAO[j.situacao] || SITUACAO.insuficiente;
                const destaque = j.hora === res.horaMaisCritica;
                return (
                  <article key={j.hora} role="listitem" className={`estimativa-janela ${destaque ? "destaque" : ""}`} style={{ borderTopColor: s.cor }}
                    aria-label={`${faixa(j.hora)}: ${s.rotulo}`}>
                    <div className="linha-flex" style={{ justifyContent: "space-between" }}>
                      <b>{faixa(j.hora)}</b>
                      {j.hora === res.horaMaiorMovimento && j.situacao !== "insuficiente" && <span className="fraco" style={{ fontSize: 11.5 }} title="Janela com maior movimento previsto">maior movimento</span>}
                    </div>
                    <SeloSituacao situacao={j.situacao} compacto />
                    {j.situacao !== "insuficiente" && (
                      <>
                        <div className="estimativa-janela__numeros num">
                          <span><b>{qtd(j.demandaPrevista)}</b> {plural(j.demandaPrevista, "prevista", "previstas")}</span>
                          <span className="fraco">capacidade {qtd(j.capacidadeAtendimento)}</span>
                        </div>
                        <div className="estimativa-barra" aria-hidden="true">
                          <span className="estimativa-barra__capacidade" style={{ width: `${(j.capacidadeAtendimento / escala) * 100}%` }} />
                          <span className="estimativa-barra__demanda" style={{ width: `${(j.demandaPrevista / escala) * 100}%`, background: s.cor }} />
                          <span className="estimativa-barra__marca" style={{ left: `${(j.capacidadeAtendimento / escala) * 100}%` }} />
                        </div>
                      </>
                    )}
                    <p className="estimativa-janela__mensagem">{j.mensagem}</p>
                    {j.confiabilidade === "limitada" && geral.confiabilidade === "confiavel" && <span className="fraco" style={{ fontSize: 11.5 }}>Poucas chegadas registradas nesta hora — menor confiabilidade.</span>}
                  </article>
                );
              })}
            </div>
            <div className="legenda" style={{ fontSize: 12 }}>
              <span><span className="estimativa-legenda" style={{ background: "var(--ok)" }} /> barra cheia = demanda prevista</span>
              <span><span className="estimativa-legenda estimativa-legenda--contorno" /> faixa cinza e traço = capacidade de atendimento</span>
              <span>mesma escala em todas as janelas</span>
            </div>
          </>
        )}

        {/* Por terminal */}
        {!geral.terminalId && terminais.length > 1 && (
          <div className="tabela-wrap">
            <table className={aoEscolherTerminal ? "tabela--clicavel" : ""} style={{ minWidth: 720 }}>
              <caption className="sr-only">Estimativa por terminal nas próximas {horas} horas</caption>
              <thead>
                <tr><th scope="col">Terminal</th><th scope="col">Situação</th><th scope="col">Janela que exige atenção</th>
                  <th scope="col" className="num">Demanda em {horas} h</th><th scope="col" className="num">Capacidade em {horas} h</th><th scope="col">Confiabilidade</th><th scope="col">Dados até</th></tr>
              </thead>
              <tbody>
                {terminais.map(t => {
                  const tr = t.resumo;
                  const sem = t.confiabilidade === "insuficiente";
                  return (
                    <tr key={t.terminalId} tabIndex={aoEscolherTerminal ? 0 : undefined} onClick={() => aoEscolherTerminal?.(t.terminalId)}
                      onKeyDown={e => { if (e.key === "Enter") aoEscolherTerminal?.(t.terminalId); }}>
                      <th scope="row">{t.terminal}</th>
                      <td><SeloSituacao situacao={tr.piorSituacao} /></td>
                      <td>{sem ? <span className="fraco">{t.motivos[0]?.descricao}</span> : tr.horaMaisCritica != null
                        ? (() => { const jc = t.janelas.find(x => x.hora === tr.horaMaisCritica); return <>{faixa(jc.hora)} <span className="fraco num">· {qtd(-jc.saldo)} acima</span></>; })()
                        : <span className="fraco">nenhuma · {qtd(tr.saldo)} de folga</span>}</td>
                      <td className="num">{sem ? "—" : `${qtd(tr.demandaPrevista)}`}</td>
                      <td className="num">{sem ? "—" : `${qtd(tr.capacidadeAtendimento)}`}</td>
                      <td>{CONFIABILIDADE[t.confiabilidade]?.rotulo}{t.erro && <span className="fraco num" style={{ display: "block", fontSize: 12 }}>erro medido {numero(t.erro.erroPct, 0)}% (dia anterior: {numero(t.erro.erroIngenuoPct, 0)}%)</span>}</td>
                      <td className="num">{t.ultimoRegistro ? formatarDataPtBR(t.ultimoRegistro) : "—"}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        <details className="estimativa-como">
          <summary>Como esta estimativa é feita</summary>
          <p>
            Para cada hora, o BarcaLog olha os últimos {30} dias com dados de cada terminal: quantas carretas costumam
            chegar (demanda prevista) e quantas costumam ser liberadas (atendimento habitual). Se a capacidade por hora
            estiver cadastrada em Configurações, ela é usada no lugar do atendimento habitual.
          </p>
          <p>
            <b>Dentro da capacidade</b>: a demanda não passa do atendimento (ou passa por menos de 1 carreta), ou há
            menos de 5 carretas previstas na hora. <b>Atenção</b>: a demanda passa do atendimento. <b>Risco de fila</b>:
            a demanda chega a {numero(r.dados.limiteRisco, 1)}× o atendimento ou mais (limite ajustável em Configurações).
            Em "Todos os terminais", vale a situação do terminal mais pressionado, porque as carretas não mudam de terminal.
          </p>
          <p>
            É uma média do que já aconteceu, não um modelo de previsão: feriados, safra e navios atracados não entram.
            Confira com a fila real antes de agir.
          </p>
          <p>
            <b>Confiabilidade.</b> <i>Confiável</i>: base completa (30 dias com dados), dados em dia e erro medido não
            maior que o de simplesmente repetir o mesmo horário do dia anterior. <i>Limitada</i>: utilizável, com uma
            restrição explicada acima. <i>Dados desatualizados</i>: o terminal parou de enviar dados há mais dias que o
            limite de Configurações — importe as planilhas recentes. <i>Dados insuficientes</i>: menos de 7 dias com dados;
            não há estimativa. O erro medido é calculado refazendo a estimativa de cada um dos últimos 30 dias com dados,
            como se fosse a véspera, e comparando com o que chegou de fato.
          </p>
        </details>
      </div>
    </Cartao>
  );
}

/** Classe de confiabilidade com motivo, impacto e ação; erro medido por terminal. */
function Confiabilidade({ geral, terminais }) {
  const c = CONFIABILIDADE[geral.confiabilidade] || CONFIABILIDADE.limitada;
  const erros = (geral.terminalId ? [geral] : terminais).filter(t => t.erro);
  if (geral.confiabilidade === "confiavel")
    return erros.length ? (
      <p className="fraco num" style={{ fontSize: 12.5, margin: 0 }}>
        Erro medido nos últimos 30 dias com dados: {erros.map(t => `${geral.terminalId ? "" : `${t.terminal} `}${numero(t.erro.erroPct, 0)}%`).join(" · ")} das chegadas por hora
        {" "}(repetir o dia anterior erraria {erros.map(t => `${numero(t.erro.erroIngenuoPct, 0)}%`).join(" · ")}).
      </p>
    ) : null;
  // Agrupa motivos iguais de vários terminais numa linha só.
  const grupos = [];
  for (const m of geral.motivos) {
    const g = grupos.find(x => x.codigo === m.codigo && x.impacto === m.impacto.replace(/\d{2}\/\d{2} a \d{2}\/\d{2}/, "") && x.acao === m.acao);
    if (g) g.itens.push(m); else grupos.push({ codigo: m.codigo, impacto: m.impacto.replace(/\d{2}\/\d{2} a \d{2}\/\d{2}/, ""), acao: m.acao, itens: [m] });
  }
  return (
    <Aviso tipo={c.aviso} icone={c.icone}>
      <b>{c.rotulo}.</b>{" "}
      {geral.confiabilidade === "desatualizada" && "Os dados pararam de chegar: a estimativa descreve um período passado."}
      {geral.confiabilidade === "limitada" && "A estimativa pode ser usada como referência, com as restrições abaixo."}
      {geral.confiabilidade === "insuficiente" && "Não há base para estimar estas horas."}
      <ul style={{ margin: "6px 0 0", paddingLeft: 18 }}>
        {grupos.map(g => (
          <li key={g.codigo + g.acao} style={{ marginBottom: 4 }}>
            {g.itens.map(m => <span key={(m.terminal || "") + m.descricao} style={{ display: "block" }}>{m.terminal && !geral.terminalId ? <b>{m.terminal}: </b> : null}{m.descricao}</span>)}
            <span style={{ display: "block" }}><i>Impacto:</i> {g.itens[0].impacto} <i>O que fazer:</i> {g.acao}</span>
          </li>
        ))}
      </ul>
    </Aviso>
  );
}

/**
 * Versão compacta (Portaria): só a conclusão das próximas horas, com o terminal
 * e a janela que exigem atenção. Mesmos dados e regras da tela Gargalos e alertas.
 */
export function ResumoEstimativa({ r }) {
  if (r.erro || !r.dados) return null;
  const { geral, terminais, horas } = r.dados;
  const res = geral.resumo;
  const gravidade = { risco: 3, atencao: 2, dentro: 1, insuficiente: 0 };
  const pressionadas = terminais
    .flatMap(t => t.janelas.filter(j => j.situacao === "risco" || j.situacao === "atencao").map(j => ({ terminal: t.terminal, j })))
    .sort((a, b) => gravidade[b.j.situacao] - gravidade[a.j.situacao] || a.j.hora - b.j.hora)
    .slice(0, 3);
  return (
    <Cartao id="portaria-proximas" titulo={`Próximas ${horas} horas — estimativa pelo histórico`}
      acao={<a className="link" href="#/gargalos" style={{ fontSize: 13 }}>ver detalhes</a>}>
      <div className="pilha" style={{ gap: 10 }}>
        <div className="linha-flex">
          <SeloSituacao situacao={res.piorSituacao} />
          <span style={{ fontSize: 14, fontWeight: 500 }}>{res.tendencia}</span>
        </div>
        {pressionadas.length > 0 && (
          <ul style={{ margin: 0, paddingLeft: 18, fontSize: 13.5 }}>
            {pressionadas.map(({ terminal, j }) => (
              <li key={`${terminal}-${j.hora}`}><b>{terminal}, {faixa(j.hora)}</b>: {qtd(j.demandaPrevista)} {plural(j.demandaPrevista, "carreta prevista", "carretas previstas")} para capacidade de {qtd(j.capacidadeAtendimento)}.</li>
            ))}
          </ul>
        )}
        {geral.confiabilidade !== "confiavel" && <span className="fraco" style={{ fontSize: 12.5 }}>{CONFIABILIDADE[geral.confiabilidade]?.rotulo}: veja o motivo em "ver detalhes".</span>}
      </div>
    </Cartao>
  );
}
