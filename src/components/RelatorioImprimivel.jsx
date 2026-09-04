import { createPortal } from "react-dom";
import {
  obterKpisGerais,
  agruparPorMesDetalhado,
  rankingMaioresEsperas,
  agruparPorDiaEmpresa,
  diasQueExcederamCapacidade,
  formatarDataPtBR,
  formatarHoras,
  CAPACIDADE_DIARIA_CARRETAS
} from "../data/relatorio.js";

// Relatório dedicado só pra impressão/PDF — não é a tela do dashboard
// "printada" (era isso que gerava páginas em branco/cortadas antes). É um
// documento HTML próprio, sempre montado (fora da árvore visual do app via
// portal em document.body) e invisível na tela — só aparece quando o
// navegador entra em modo de impressão (ver @media print em styles.css).
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

  const conteudo = (
    <div className="relatorio-imprimir">
      <div className="ri-pagina">
        <div className="ri-banner">
          <h1>Dashboard de Operação — {nomeRecorte}</h1>
          <p>Análise de Produtividade, Movimentações Mensais/Diárias e Capacidade Operacional</p>
        </div>

        <div className="ri-kpis">
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Total Marcações</span>
            <span className="ri-kpi__valor">{kpis.total.toLocaleString("pt-BR")}</span>
          </div>
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Média Diária</span>
            <span className="ri-kpi__valor">{kpis.mediaDiaria.toFixed(1)}</span>
          </div>
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Mês Mais Movimentado</span>
            <span className="ri-kpi__valor">{kpis.mesMaisMovimentado.rotulo}</span>
          </div>
          <div className="ri-kpi">
            <span className="ri-kpi__rotulo">Tempo Médio Espera</span>
            <span className="ri-kpi__valor">{formatarHoras(kpis.tempoMedioEspera)}</span>
          </div>
          <div className="ri-kpi ri-kpi--alerta">
            <span className="ri-kpi__rotulo">Maior Atraso</span>
            <span className="ri-kpi__valor">{formatarHoras(kpis.maiorAtraso.esperaHoras)}</span>
          </div>
        </div>

        <div className="ri-secao">
          <h2><span className="ri-barra" />1. Volume Mensal de Marcações</h2>
          <div className="ri-grafico">
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
              <tr><th>Mês de Referência</th><th>Total de Marcações</th><th>Representatividade</th><th>Dias Operados</th><th>Média Diária</th></tr>
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

      <div className="ri-pagina">
        <div className="ri-secao">
          <h2><span className="ri-barra" />2. Dia de Maior Fluxo</h2>
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
          <h2><span className="ri-barra" />3. Capacidade Operacional</h2>
          <p className="ri-texto">
            Capacidade de referência: <b>{CAPACIDADE_DIARIA_CARRETAS.toLocaleString("pt-BR")} carretas/dia</b> por terminal.
          </p>
          {excederamCapacidade.length === 0 ? (
            <p className="ri-texto">Nenhum dia, em nenhum terminal, excedeu a capacidade de referência no período analisado.</p>
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
          <h2><span className="ri-barra" />4. Top 30 Dias com Maior Volume (por período e empresa)</h2>
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

      <div className="ri-pagina">
        <div className="ri-secao">
          <h2><span className="ri-barra" />5. Ranking das Maiores Esperas</h2>
          <p className="ri-texto">Movimentos com maior diferença entre marcação e liberação, no período analisado.</p>
          <table className="ri-tabela">
            <thead>
              <tr><th>Rank</th><th>Movimento</th><th>Convênio</th><th>Empresa</th><th>Marcação</th><th>Liberação</th><th>Espera</th></tr>
            </thead>
            <tbody>
              {top10Esperas.map((m, i) => (
                <tr key={`${m.empresaId}-${m.id}`}>
                  <td>#{i + 1}</td>
                  <td>{m.id}</td>
                  <td>{m.convenio}</td>
                  <td>{m.empresaNome}</td>
                  <td>{new Date(m.marcadoEm).toLocaleString("pt-BR")}</td>
                  <td>{new Date(m.liberadoEm).toLocaleString("pt-BR")}</td>
                  <td><b>{formatarHoras(m.esperaHoras)}</b></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );

  return createPortal(conteudo, document.body);
}
