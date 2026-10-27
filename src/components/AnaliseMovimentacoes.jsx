import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend } from "recharts";
import { api } from "../api/cliente.js";
import { useApi } from "../hooks/useApi.js";
import { Carregando, ErroCarga, Vazio } from "./EstadoCarga.jsx";
import { Cartao, Kpi, Selo, Aviso } from "./ui.jsx";
import { duracao, variacaoDuracao, numero, percentual, variacaoPct, formatarDataPtBR, dataHoraPorto } from "../utils/formatar.js";

// Análise 360° das movimentações (GET /api/v1/marcacoes/analise).
// Todos os números são OBSERVADOS nos registros do recorte — nada estimado.
// Cada fatia leva à lista com os mesmos filtros + a fatia (aoDetalhar).
// Regras em docs/INDICADORES.md ("Análise 360°").

const DIAS = ["Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado"];
const DIMENSOES = {
  terminal: { titulo: "Por terminal", filtro: g => ({ terminalId: g.chave }) },
  operador: { titulo: "Por operador", filtro: g => ({ operador: g.chave }) },
  carga: { titulo: "Por carga", filtro: g => ({ carga: g.chave }) },
  convenio: { titulo: "Por convênio", filtro: g => ({ convenioExato: g.chave }) },
  origem: { titulo: "Por origem do registro", filtro: g => ({ origem: g.chave }) }
};

function sentido(atual, anterior, menorMelhor) {
  if (atual == null || anterior == null || atual === anterior) return "neutro";
  return (atual < anterior) === menorMelhor ? "melhor" : "pior";
}

function rotuloPonto(p, agrupamento) {
  if (agrupamento === "mes") { const [a, m] = p.chave.split("-"); return `${m}/${a}`; }
  if (agrupamento === "semana") return `sem. ${formatarDataPtBR(p.de).slice(0, 5)}`;
  return formatarDataPtBR(p.de).slice(0, 5);
}

export default function AnaliseMovimentacoes({ filtros, aoDetalhar }) {
  const consulta = {
    terminalId: filtros.terminalId, de: filtros.de, ate: filtros.ate, operador: filtros.operador, carga: filtros.carga,
    convenio: filtros.convenioExato, origem: filtros.origem, agrupar: filtros.agrupar
  };
  const r = useApi(sinal => api.get("/api/v1/marcacoes/analise", consulta, sinal), [JSON.stringify(consulta)]);

  if (r.erro) {
    if (r.erro.status === 404) return <Cartao><Vazio icone="search_off" titulo="Nenhuma movimentação neste recorte" texto="Ajuste os filtros ou importe planilhas." /></Cartao>;
    return <Cartao><ErroCarga erro={r.erro} aoTentarDeNovo={r.recarregar} /></Cartao>;
  }
  if (!r.dados) return <Cartao><Carregando texto="Calculando a análise…" /></Cartao>;
  const a = r.dados;
  // O drill-down usa o período EFETIVO da análise (o servidor completa quando não há datas).
  const detalhar = extra => aoDetalhar({ de: a.de, ate: a.ate, ...extra });
  const periodo = `${formatarDataPtBR(a.de)} a ${formatarDataPtBR(a.ate)}`;
  const anterior = `${formatarDataPtBR(a.anteriorDe)} a ${formatarDataPtBR(a.anteriorAte)}`;

  return (
    <div className="pilha">
      <Aviso tipo="info">
        <b>{periodo}</b> ({numero(a.dias)} {a.dias === 1 ? "dia" : "dias"}, pela data de marcação, horário do porto)
        {!filtros.de && " — sem datas escolhidas, a análise usa os 30 dias até a última marcação"}.
        {" "}Comparação com o período anterior de mesma duração ({anterior}). Todos os números são observados nos registros; nada é estimado.
      </Aviso>

      <Resumo a={a} anterior={anterior} detalhar={detalhar} />

      <Cartao id="an-temporal" titulo="Evolução no período"
        subtitulo={`Entradas (marcações) e saídas (liberações) por ${a.agrupamento === "dia" ? "dia" : a.agrupamento === "semana" ? "semana (segunda a domingo)" : "mês"}. Clique numa barra para ver os registros marcados nela.`}>
        <Serie a={a} detalhar={detalhar} />
      </Cartao>

      <div className="grade-2">
        <Cartao id="an-hora" titulo="Por hora do dia" subtitulo="Média por dia no período. Clique para ver os registros marcados naquela hora.">
          <PorHora a={a} detalhar={detalhar} />
        </Cartao>
        <Cartao id="an-semana" titulo="Por dia da semana" subtitulo="Entradas por ocorrência do dia no período (ex.: total das segundas ÷ número de segundas).">
          <PorDiaSemana a={a} detalhar={detalhar} />
        </Cartao>
      </div>

      <h3 style={{ fontSize: 15, margin: "8px 0 0" }}>Por operação</h3>
      <p className="fraco" style={{ marginTop: -6, fontSize: 12.5 }}>
        Dimensões que existem nos registros: terminal, operador e carga (do cadastro de convênios), convênio e origem do registro.
        Placa, transportadora e motorista não vêm nas planilhas dos terminais e por isso não aparecem aqui.
      </p>
      <div className="grade-2">
        {a.dimensoes.filter(d => !(d.dimensao === "terminal" && filtros.terminalId)).map(d => (
          <Dimensao key={d.dimensao} d={d} limite={a.limiteEsperaLongaHoras} detalhar={detalhar} />
        ))}
      </div>

      <Qualidade q={a.qualidade} limite={a.limiteEsperaLongaHoras} longas={a.atual.esperasLongas} detalhar={detalhar} />
    </div>
  );
}

function Resumo({ a, anterior, detalhar }) {
  const x = a.atual;
  const b = a.anterior;
  const base = b ? `vs. ${anterior}` : "sem registros no período anterior — sem comparação";
  const comp = (atual, ant, fmt, variacao, menorMelhor) => b && ant != null
    ? { texto: variacao ?? "", sentido: menorMelhor == null ? "neutro" : sentido(atual, ant, menorMelhor), base: `vs. ${fmt(ant)} (${anterior})` }
    : { base };
  return (
    <Cartao id="an-resumo" titulo="Resumo do período" subtitulo="Entradas = carretas marcadas no período. Saídas = liberações com data no período (inclui carretas marcadas antes).">
      <div className="grade-kpi">
        <Kpi rotulo="Entradas" valor={numero(x.entradas)} nota={`${numero(x.entradasPorDia, 1)} por dia`}
          comparacao={comp(x.entradasPorDia, b?.entradasPorDia, v => `${numero(v, 1)}/dia`, variacaoPct(x.entradasPorDia, b?.entradasPorDia), null)}
          regra="Contagem de marcações com data de marcação no período. A comparação usa a média por dia, porque os dois períodos têm a mesma duração mas podem ter dias sem dados." />
        <Kpi rotulo="Saídas" valor={numero(x.saidas)} nota="liberações no período"
          comparacao={comp(x.saidas, b?.saidas, numero, variacaoPct(x.saidas, b?.saidas), null)}
          regra="Contagem de liberações com data de liberação no período, qualquer que seja a data de marcação." />
        <Kpi rotulo="Espera mediana" valor={duracao(x.esperaMediana)}
          comparacao={comp(x.esperaMediana, b?.esperaMediana, duracao, variacaoDuracao(x.esperaMediana, b?.esperaMediana), true)}
          regra="Mediana de (liberação − marcação) das carretas marcadas no período e já liberadas. Metade esperou menos que isso." />
        <Kpi rotulo="Espera P90" valor={duracao(x.esperaP90)}
          comparacao={comp(x.esperaP90, b?.esperaP90, duracao, variacaoDuracao(x.esperaP90, b?.esperaP90), true)}
          regra="90º percentil das mesmas esperas (interpolação linear, igual ao PERCENTIL.INC do Excel): 10% esperaram mais." />
        <Kpi rotulo="Liberadas em até 24 h" valor={percentual(x.liberadasAte24hPct)}
          comparacao={comp(x.liberadasAte24hPct, b?.liberadasAte24hPct, percentual, x.liberadasAte24hPct != null && b?.liberadasAte24hPct != null ? `${x.liberadasAte24hPct - b.liberadasAte24hPct >= 0 ? "+" : "−"}${numero(Math.abs(x.liberadasAte24hPct - b.liberadasAte24hPct), 1)} p.p.` : null, false)}
          regra="Liberadas com espera ≤ 24 h ÷ liberadas (das marcadas no período). Diferença em pontos percentuais." />
        <Kpi rotulo={`Esperas acima de ${numero(a.limiteEsperaLongaHoras)} h`} valor={numero(x.esperasLongas)}
          comparacao={comp(x.esperasLongas, b?.esperasLongas, numero, b && b.esperasLongas !== x.esperasLongas ? `${x.esperasLongas - b.esperasLongas > 0 ? "+" : "−"}${numero(Math.abs(x.esperasLongas - b.esperasLongas))}` : null, true)}
          regra="Liberadas com espera acima do limite de espera longa (Configurações › Limites). Diferença absoluta: com bases pequenas, percentual engana." />
      </div>
      <div className="linha-flex" style={{ marginTop: 12, flexWrap: "wrap" }}>
        <button type="button" className="botao botao--secundario" onClick={() => detalhar({})}>Ver as {numero(x.entradas)} entradas</button>
        {x.pendentes > 0 && <button type="button" className="botao botao--link" onClick={() => detalhar({ qualidade: "sem-liberacao" })}>{numero(x.pendentes)} ainda sem liberação</button>}
        {x.esperasLongas > 0 && <button type="button" className="botao botao--link" onClick={() => detalhar({ qualidade: "espera-longa" })}>{numero(x.esperasLongas)} esperas longas</button>}
      </div>
    </Cartao>
  );
}

function Serie({ a, detalhar }) {
  if (!a.serie.some(p => p.entradas || p.saidas)) return <Vazio icone="timeline" titulo="Sem movimentações no período" />;
  const linhas = a.serie.map(p => ({ ...p, rotulo: rotuloPonto(p, a.agrupamento) + (p.parcial ? "*" : "") }));
  const maior = [...a.serie].sort((x, y) => y.entradas - x.entradas)[0];
  return (
    <>
      <div role="img" aria-label={`Maior volume de entradas em ${formatarDataPtBR(maior.de)}${maior.de !== maior.ate ? ` a ${formatarDataPtBR(maior.ate)}` : ""}: ${numero(maior.entradas)}.`}>
        <ResponsiveContainer width="100%" height={260}>
          <BarChart data={linhas} margin={{ top: 8, right: 8, left: 0, bottom: 0 }} barGap={1} barCategoryGap="18%"
            onClick={e => { const p = e?.activePayload?.[0]?.payload; if (p) detalhar({ de: p.de, ate: p.ate }); }} style={{ cursor: "pointer" }}>
            <CartesianGrid vertical={false} stroke="#EEF1F5" />
            <XAxis dataKey="rotulo" tick={{ fontSize: 10.5, fill: "#4A5870" }} tickLine={false} axisLine={{ stroke: "#CDD5E1" }} minTickGap={8} />
            <YAxis tick={{ fontSize: 11, fill: "#6B7890" }} tickLine={false} axisLine={false} width={40} />
            <Tooltip cursor={{ fill: "#F1F5FB" }} contentStyle={{ borderRadius: 8, borderColor: "#E3E8EF", fontSize: 12.5 }}
              labelFormatter={(_, p) => { const x = p?.[0]?.payload; return x ? `${formatarDataPtBR(x.de)}${x.de !== x.ate ? ` a ${formatarDataPtBR(x.ate)}` : ""}${x.parcial ? " (parcial)" : ""} · mediana ${duracao(x.esperaMediana)}` : ""; }}
              formatter={(v, nome) => [numero(v), nome === "entradas" ? "Entradas (marcações)" : "Saídas (liberações)"]} />
            <Legend formatter={v => v === "entradas" ? "Entradas (marcações)" : "Saídas (liberações)"} wrapperStyle={{ fontSize: 12.5 }} />
            <Bar dataKey="entradas" fill="#1F4E88" radius={[2, 2, 0, 0]} />
            <Bar dataKey="saidas" fill="#C9D3E3" radius={[2, 2, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
      {a.serie.some(p => p.parcial) && <p className="fraco" style={{ fontSize: 12, marginTop: 4 }}>* ponto parcial: o período não cobre a {a.agrupamento === "mes" ? "o mês" : "a semana"} inteira.</p>}
      <details className="estimativa-como" style={{ marginTop: 8 }}>
        <summary>Ver como tabela</summary>
        <div className="tabela-wrap" style={{ marginTop: 8 }}>
          <table style={{ minWidth: 520 }}>
            <thead><tr><th scope="col">Período</th><th scope="col" className="num">Entradas</th><th scope="col" className="num">Saídas</th><th scope="col" className="num">Espera mediana</th><th scope="col"><span className="sr-only">Registros</span></th></tr></thead>
            <tbody>
              {a.serie.map(p => (
                <tr key={p.chave}>
                  <td>{formatarDataPtBR(p.de)}{p.de !== p.ate && ` a ${formatarDataPtBR(p.ate)}`}{p.parcial && " *"}</td>
                  <td className="num">{numero(p.entradas)}</td><td className="num">{numero(p.saidas)}</td><td className="num">{duracao(p.esperaMediana)}</td>
                  <td>{p.entradas > 0 && <button type="button" className="botao botao--link" onClick={() => detalhar({ de: p.de, ate: p.ate })}>Ver registros</button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </details>
    </>
  );
}

function PorHora({ a, detalhar }) {
  if (!a.porHora.some(h => h.entradasPorDia || h.saidasPorDia)) return <Vazio icone="schedule" titulo="Sem movimentações" />;
  const linhas = a.porHora.map(h => ({ ...h, rotulo: `${String(h.hora).padStart(2, "0")}h` }));
  const pico = [...linhas].sort((x, y) => y.entradasPorDia - x.entradasPorDia)[0];
  return (
    <>
      <div role="img" aria-label={`Hora com mais entradas: ${pico.rotulo}, ${numero(pico.entradasPorDia, 1)} por dia.`}>
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={linhas} margin={{ top: 8, right: 8, left: 0, bottom: 0 }} barGap={1} barCategoryGap="14%"
            onClick={e => { const p = e?.activePayload?.[0]?.payload; if (p) detalhar({ hora: String(p.hora) }); }} style={{ cursor: "pointer" }}>
            <CartesianGrid vertical={false} stroke="#EEF1F5" />
            <XAxis dataKey="rotulo" tick={{ fontSize: 10.5, fill: "#4A5870" }} interval={2} tickLine={false} axisLine={{ stroke: "#CDD5E1" }} />
            <YAxis tick={{ fontSize: 11, fill: "#6B7890" }} tickLine={false} axisLine={false} width={32} />
            <Tooltip cursor={{ fill: "#F1F5FB" }} contentStyle={{ borderRadius: 8, borderColor: "#E3E8EF", fontSize: 12.5 }}
              formatter={(v, nome) => [`${numero(v, 1)} por dia`, nome === "entradasPorDia" ? "Entradas" : "Saídas"]} />
            <Legend formatter={v => v === "entradasPorDia" ? "Entradas" : "Saídas"} wrapperStyle={{ fontSize: 12.5 }} />
            <Bar dataKey="entradasPorDia" fill="#1F4E88" radius={[2, 2, 0, 0]} />
            <Bar dataKey="saidasPorDia" fill="#C9D3E3" radius={[2, 2, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
      <p className="fraco" style={{ fontSize: 12, marginTop: 4 }}>
        Pico de entradas: <button type="button" className="botao botao--link" onClick={() => detalhar({ hora: String(pico.hora) })}>{pico.rotulo} ({numero(pico.entradasPorDia, 1)}/dia)</button>
      </p>
    </>
  );
}

function PorDiaSemana({ a, detalhar }) {
  const maior = Math.max(1, ...a.porDiaSemana.map(d => d.entradasPorDia));
  return (
    <table>
      <caption className="sr-only">Entradas médias por dia da semana</caption>
      <thead><tr><th scope="col">Dia</th><th scope="col" className="num">Entradas por dia</th><th scope="col" style={{ width: "45%" }}><span className="sr-only">Proporção</span></th></tr></thead>
      <tbody>
        {a.porDiaSemana.map(d => (
          <tr key={d.dia}>
            <td>{d.entradasPorDia > 0
              ? <button type="button" className="botao botao--link" style={{ padding: 0 }} onClick={() => detalhar({ diaSemana: String(d.dia) })}>{DIAS[d.dia]}</button>
              : DIAS[d.dia]}</td>
            <td className="num">{numero(d.entradasPorDia, 1)}</td>
            <td><div aria-hidden="true" style={{ height: 8, borderRadius: 4, background: "#1F4E88", width: `${(d.entradasPorDia / maior) * 100}%`, minWidth: d.entradasPorDia ? 2 : 0 }} /></td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function Dimensao({ d, limite, detalhar }) {
  const meta = DIMENSOES[d.dimensao];
  if (!d.grupos.length) return null;
  return (
    <Cartao id={`an-${d.dimensao}`} titulo={meta.titulo} corpo={false}>
      <div className="tabela-wrap">
        <table style={{ minWidth: 520 }}>
          <thead>
            <tr><th scope="col">{meta.titulo.replace("Por ", "")}</th><th scope="col" className="num">Entradas</th><th scope="col" className="num">%</th>
              <th scope="col" className="num">Mediana</th><th scope="col" className="num">P90</th><th scope="col" className="num">≤ 24 h</th><th scope="col" className="num">&gt; {numero(limite)} h</th></tr>
          </thead>
          <tbody>
            {[...d.grupos, ...(d.outros ? [d.outros] : [])].map(g => (
              <tr key={g.chave}>
                <td>{g.chave === "outros"
                  ? <span className="fraco">{g.rotulo}</span>
                  : <button type="button" className="botao botao--link" style={{ padding: 0, textAlign: "left" }} onClick={() => detalhar(meta.filtro(g))} title="Ver os registros">{g.rotulo}</button>}</td>
                <td className="num">{numero(g.entradas)}</td>
                <td className="num">{numero(g.pctEntradas, 1)}</td>
                <td className="num">{duracao(g.esperaMediana)}</td>
                <td className="num">{duracao(g.esperaP90)}</td>
                <td className="num">{percentual(g.liberadasAte24hPct)}</td>
                <td className="num">{numero(g.esperasLongas)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {d.grupos.some(g => g.pendentes > 0) && <p className="fraco" style={{ fontSize: 12, padding: "0 16px 12px" }}>Esperas só das carretas já liberadas; as sem liberação ficam fora da mediana e do P90.</p>}
    </Cartao>
  );
}

function Qualidade({ q, limite, longas, detalhar }) {
  const pct = n => q.registros ? ` (${numero((n / q.registros) * 100, 1)}%)` : "";
  const linhas = [
    { rotulo: "Sem liberação", n: q.semLiberacao, impacto: "Não entram em espera, mediana nem P90.", filtro: { qualidade: "sem-liberacao" } },
    { rotulo: "Sem código de convênio", n: q.semCodigoConvenio, impacto: "Típico da carga inicial do sistema. Operador e carga vêm do nome do convênio.", filtro: { qualidade: "sem-codigo" } },
    { rotulo: "Possíveis remarcações", n: q.possiveisRemarcacoes, impacto: "Mesma senha, convênio e liberação, marcada de novo em até 10 min: pode contar a mesma carreta duas vezes. Não há filtro de lista para elas; aparecem no relatório da importação." },
    { rotulo: "Liberadas em lote", n: q.liberadosEmLote, impacto: "Liberadas no mesmo instante que outras do terminal: o horário pode ser o do registro, não o da saída real. O detalhe de cada movimento mostra o lote." },
    { rotulo: "Movimentos duplicados", n: q.duplicidadesMovimento, impacto: "Impossível: o número do movimento é chave única no banco." },
    { rotulo: `Esperas acima de ${numero(limite)} h`, n: longas, impacto: "Não é erro de dado, mas vale conferir: esperas muito longas costumam ser liberação não registrada a tempo.", filtro: { qualidade: "espera-longa" } }
  ];
  return (
    <Cartao id="an-qualidade" titulo="Qualidade dos dados"
      subtitulo={`${numero(q.registros)} entradas analisadas${q.ultimaMarcacao ? ` · última marcação do recorte em ${dataHoraPorto(q.ultimaMarcacao)}` : ""}`} corpo={false}>
      <div className="tabela-wrap">
        <table style={{ minWidth: 600 }}>
          <thead><tr><th scope="col">Verificação</th><th scope="col" className="num">Registros</th><th scope="col">Impacto nos números</th><th scope="col"><span className="sr-only">Registros</span></th></tr></thead>
          <tbody>
            {linhas.map(l => (
              <tr key={l.rotulo}>
                <td>{l.rotulo}</td>
                <td className="num">{l.n > 0 ? <Selo tipo="atencao">{numero(l.n)}{pct(l.n)}</Selo> : <Selo tipo="ok">0</Selo>}</td>
                <td style={{ fontSize: 12.5 }}>{l.impacto}</td>
                <td>{l.filtro && l.n > 0 && <button type="button" className="botao botao--link" onClick={() => detalhar(l.filtro)}>Ver registros</button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Cartao>
  );
}
