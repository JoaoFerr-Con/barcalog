import { createPortal } from "react-dom";
import {
  obterKpisGerais,
  agruparPorMesDetalhado,
  rankingMaioresEsperas,
  agruparPorDiaEmpresa,
  diasQueExcederamCapacidade,
  detalhamentoDiarioPorMes,
  formatarDataPtBR,
  formatarHoras,
  CAPACIDADE_DIARIA_CARRETAS
} from "../data/relatorio.js";

// Relatório dedicado só pra impressão/PDF — não é a tela do dashboard
// "printada". É um documento HTML próprio, montado fora da árvore visual
// do app via portal em document.body, invisível na tela, e só aparece
// quando o navegador entra em modo de impressão (@media print em styles.css).
//
// A estrutura segue o padrão do PDF "Dashboard de Operação - Unitapajós":
//   Pág. 1: Banner, KPIs, gráfico mensal, tabela mensal detalhada
//   Pág. 2: Ranking das maiores esperas (com Senha e Tempo Total)
//   Pág. 3+: Detalhamento diário por mês (gráfico de barras por dia)
//   Última pág: Dia de maior fluxo, capacidade operacional, top 30 dias
export default function RelatorioImprimivel({ registros, nomeRecorte }) {
  if (!registros || registros.length === 0) return null;

  const kpis = obterKpisGerais(registros);
  const porMes = agruparPorMesDetalhado(registros);
  const top10Esperas = rankingMaioresEsperas(registros, 10);
  const porDiaEmpresa = agruparPorDiaEmpresa(registros);
  const diaMaiorFluxo = porDiaEmpresa[0];
  const top30Dias = porDiaEmpresa.slice(0, 30);
  const excederamCapacidade = diasQueExcederamCapacidade(registros);
  const maiorVolumeMes = Math.max(...porMes.map(m => m.total), 1);
  const detalhesDiarios = detalhamentoDiarioPorMes(registros);

  // Agrupa detalhamentos em pares de 2 meses por página (como no PDF de
  // referência, que coloca Jan+Fev numa página, Mar+Abr noutra, etc).
  const paginasDiarias = [];
  for (let i = 0; i < detalhesDiarios.length; i += 2) {
    paginasDiarias.push(detalhesDiarios.slice(i, i + 2));
  }

  const conteudo = (
    <div className="relatorio-imprimir">
      {/* ====== PÁGINA 1: Visão Geral ====== */}
      <div className="ri-pagina">
        <div className="ri-banner">
          <h1>Dashboard de Operação — {nomeRecorte}</h1>
          <p>Análise de Produtividade, Movimentações Mensais/Diárias e Tempo de Permanência</p>
        </div>

        <div className="ri-kpis">
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Total Marcações</span>
            <span className="ri-kpi__valor">{kpis.total.toLocaleString("pt-BR")}</span>
            <span className="ri-kpi__nota">Jan a Jul/2026</span>
          </div>
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Média Diária</span>
            <span className="ri-kpi__valor">{kpis.mediaDiaria.toFixed(1)}</span>
            <span className="ri-kpi__nota">itens por dia</span>
          </div>
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Mês Mais Movimentado</span>
            <span className="ri-kpi__valor">{kpis.mesMaisMovimentado.rotulo}</span>
            <span className="ri-kpi__nota">{kpis.mesMaisMovimentado.total.toLocaleString("pt-BR")} itens</span>
          </div>
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Tempo Médio Espera</span>
            <span className="ri-kpi__valor">{formatarHoras(kpis.tempoMedioEspera)}</span>
            <span className="ri-kpi__nota">marcação → liberação</span>
          </div>
          <div className="ri-kpi ri-kpi--alerta">
            <span className="ri-kpi__rotulo">Maior Atraso</span>
            <span className="ri-kpi__valor">{formatarHoras(kpis.maiorAtraso.esperaHoras)}</span>
            <span className="ri-kpi__nota">outlier histórico</span>
          </div>
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />1. Visão Geral: Volume Mensal de Marcações</h2>
          <div className="ri-grafico" style={{ height: 130 }}>
            {porMes.map(m => (
              <div key={m.chave} className="ri-grafico__col">
                <span className="ri-grafico__valor">{m.total.toLocaleString("pt-BR")}</span>
                <div className="ri-grafico__barra" style={{ height: `${(m.total / maiorVolumeMes) * 100}%` }} />
                <span className="ri-grafico__rotulo">{m.rotulo}</span>
              </div>
            ))}
          </div>
          <table className="ri-tabela">
            <thead>
              <tr><th>Mês de Referência</th><th>Total de Marcações</th><th>Representatividade (%)</th><th>Dias Operados</th><th>Média Diária</th></tr>
            </thead>
            <tbody>
              {porMes.map(m => (
                <tr key={m.chave}>
                  <td><b>{m.rotulo}</b></td>
                  <td>{m.total.toLocaleString("pt-BR")}</td>
                  <td>{m.representatividade.toFixed(1)}%</td>
                  <td>{m.diasOperados} dias</td>
                  <td>{m.mediaDiaria.toFixed(1)} itens/dia</td>
                </tr>
              ))}
              <tr className="ri-tabela__total">
                <td>TOTAL CONSOLIDADO</td>
                <td>{kpis.total.toLocaleString("pt-BR")}</td>
                <td>100,0%</td>
                <td>{kpis.diasOperados} dias</td>
                <td>{kpis.mediaDiaria.toFixed(1)} itens/dia</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      {/* ====== PÁGINA 2: Ranking das Maiores Esperas ====== */}
      <div className="ri-pagina">
        <div className="ri-secao">
          <h2><span className="ri-barra" />2. Ranking das Maiores Diferenças entre Hora Marcada e Hora Liberação</h2>
          <p className="ri-texto">
            A tabela abaixo apresenta os veículos/movimentos que enfrentaram os maiores tempos de permanência
            entre o registro da marcação e a respectiva liberação no sistema.
          </p>
          <table className="ri-tabela">
            <thead>
              <tr>
                <th>Rank</th>
                <th>Movimento</th>
                <th>Senha</th>
                <th>Convênio</th>
                <th>Data/Hora Marcação</th>
                <th>Data/Hora Liberação</th>
                <th>Diferença (h)</th>
                <th>Tempo Total</th>
              </tr>
            </thead>
            <tbody>
              {top10Esperas.map((m, i) => (
                <tr key={`${m.empresaId}-${m.id}`}>
                  <td>#{i + 1}</td>
                  <td>{m.id}</td>
                  <td>{m.senha}</td>
                  <td>{m.convenio}</td>
                  <td>{new Date(m.marcadoEm).toLocaleString("pt-BR")}</td>
                  <td>{new Date(m.liberadoEm).toLocaleString("pt-BR")}</td>
                  <td><b>{m.esperaHoras.toFixed(2)} h</b></td>
                  <td>{formatarHoras(m.esperaHoras)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* ====== PÁGINAS 3+: Detalhamento Diário por Mês ====== */}
      {paginasDiarias.map((par, idx) => {
        const tituloSecao = par.length === 2
          ? `${par[0].rotulo} e ${par[1].rotulo}`
          : par[0].rotulo;
        return (
          <div key={idx} className="ri-pagina">
            <div className="ri-secao">
              <h2><span className="ri-barra" />Detalhamento Diário: {tituloSecao}</h2>
              <p className="ri-texto">
                Marcações por dia do mês — cada barra é uma carreta registrada naquele dia.
                A distribuição diária revela picos operacionais, finais de semana ociosos e
                padrões de sazonalidade intra-mensal.
              </p>
              {par.map(mes => {
                const maiorDia = Math.max(...mes.dias.map(d => d.total), 1);
                return (
                  <div key={mes.chave} style={{ marginBottom: 18 }}>
                    <p className="ri-subtitulo">
                      Marcações por Dia — {mes.rotulo} (Total: {mes.total.toLocaleString("pt-BR")} itens)
                    </p>
                    <div className="ri-grafico" style={{ height: 100 }}>
                      {mes.dias.map(d => (
                        <div key={d.dia} className="ri-grafico__col">
                          <span className="ri-grafico__valor">{d.total > 0 ? d.total : ""}</span>
                          <div className="ri-grafico__barra" style={{ height: `${(d.total / maiorDia) * 100}%` }} />
                          <span className="ri-grafico__rotulo">{d.dia}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        );
      })}

      {/* ====== ÚLTIMA PÁGINA: Capacidade e Top 30 ====== */}
      <div className="ri-pagina">
        <div className="ri-secao">
          <h2><span className="ri-barra" />Dia de Maior Fluxo no Período</h2>
          <p className="ri-texto">
            Identifica o dia individual com maior concentração de carretas em um único terminal,
            indicando potenciais gargalos de capacidade.
          </p>
          {diaMaiorFluxo && (
            <div className="ri-destaque">
              <div>
                <div className="ri-destaque__valor">{formatarDataPtBR(diaMaiorFluxo.data)}</div>
                <div className="ri-destaque__legenda">{diaMaiorFluxo.empresa}</div>
              </div>
              <div className="ri-destaque__numero">{diaMaiorFluxo.total.toLocaleString("pt-BR")} carretas</div>
            </div>
          )}
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />Capacidade Operacional</h2>
          <p className="ri-texto">
            Capacidade de referência: <b>{CAPACIDADE_DIARIA_CARRETAS.toLocaleString("pt-BR")} carretas/dia</b> por terminal.
            A tabela abaixo lista os dias que ultrapassaram esse limite — se nenhum ultrapassou,
            o terminal operou dentro da capacidade planejada em todo o período.
          </p>
          {excederamCapacidade.length === 0 ? (
            <p className="ri-texto" style={{ fontStyle: "italic" }}>
              Nenhum dia, em nenhum terminal, excedeu a capacidade de referência no período analisado.
            </p>
          ) : (
            <table className="ri-tabela">
              <thead><tr><th>Dia</th><th>Empresa/Terminal</th><th>Carretas</th><th>Excedente</th></tr></thead>
              <tbody>
                {excederamCapacidade.map(d => (
                  <tr key={`${d.data}-${d.empresa}`} className="ri-tabela__alerta">
                    <td>{formatarDataPtBR(d.data)}</td>
                    <td>{d.empresa}</td>
                    <td><b>{d.total.toLocaleString("pt-BR")}</b></td>
                    <td>+{(d.total - CAPACIDADE_DIARIA_CARRETAS).toLocaleString("pt-BR")}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />Top 30 Dias com Maior Volume (por período e empresa)</h2>
          <p className="ri-texto">
            Ranking dos 30 dias mais movimentados no período, cruzando data e terminal.
            Dias que excederam a capacidade de {CAPACIDADE_DIARIA_CARRETAS.toLocaleString("pt-BR")} carretas/dia aparecem sinalizados.
          </p>
          <table className="ri-tabela">
            <thead><tr><th>#</th><th>Dia</th><th>Empresa/Terminal</th><th>Carretas</th><th>Status</th></tr></thead>
            <tbody>
              {top30Dias.map((d, i) => {
                const excedeu = d.total > CAPACIDADE_DIARIA_CARRETAS;
                return (
                  <tr key={`${d.data}-${d.empresa}`} className={excedeu ? "ri-tabela__alerta" : ""}>
                    <td>#{i + 1}</td>
                    <td>{formatarDataPtBR(d.data)}</td>
                    <td>{d.empresa}</td>
                    <td><b>{d.total.toLocaleString("pt-BR")}</b></td>
                    <td>{excedeu ? "Excedeu capacidade" : "Dentro da capacidade"}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );

  return createPortal(conteudo, document.body);
}
