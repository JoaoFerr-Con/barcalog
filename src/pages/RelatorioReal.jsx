import { useState, useMemo } from "react";
import CartaoIndicador from "../components/CartaoIndicador.jsx";
import SeletorEmpresa from "../components/SeletorEmpresa.jsx";
import { useRegistrosReais } from "../hooks/useRegistrosReais.js";
import {
  obterKpisGerais,
  agruparPorMes,
  rankingMaioresEsperas,
  totaisPorOperador,
  totaisPorCarga,
  formatarHoras
} from "../data/relatorio.js";

// Página alimentada 100% pelos dados reais importados das planilhas de
// marcação/liberação (Unitapajós, TGPM, Hidrovias) — nada aqui é sorteado.
export default function RelatorioReal() {
  const [empresaId, setEmpresaId] = useState("todas");
  const { registros, carregando } = useRegistrosReais(empresaId);

  const kpis = useMemo(() => obterKpisGerais(registros), [registros]);
  const porMes = useMemo(() => agruparPorMes(registros), [registros]);
  const top10 = useMemo(() => rankingMaioresEsperas(registros, 10), [registros]);
  const porOperador = useMemo(() => totaisPorOperador(registros), [registros]);
  const porCarga = useMemo(() => totaisPorCarga(registros), [registros]);

  if (carregando) {
    return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Carregando dados reais…</p>;
  }
  if (!kpis) {
    return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Nenhum registro para essa empresa.</p>;
  }

  const maiorMes = Math.max(...porMes.map(m => m.total), 1);
  const maiorOperador = Math.max(...porOperador.map(o => o.total), 1);
  const maiorCarga = Math.max(...porCarga.map(c => c.total), 1);

  return (
    <>
      <div style={{ display: "flex", justifyContent: "flex-end", marginBottom: 12 }}>
        <SeletorEmpresa valor={empresaId} aoMudar={setEmpresaId} />
      </div>

      <div className="grade-kpi">
        <CartaoIndicador
          rotulo="Total de Marcações"
          valor={kpis.total.toLocaleString("pt-BR")}
          icone="fact_check"
          corIcone="var(--navio-700)"
          corFundoIcone="var(--azul-100)"
          nota="Período importado"
        />
        <CartaoIndicador
          rotulo="Média Diária"
          valor={kpis.mediaDiaria.toFixed(1)}
          icone="calendar_month"
          corIcone="var(--ambar-600)"
          corFundoIcone="#FBEBD1"
          nota={`${kpis.diasOperados} dias operados`}
        />
        <CartaoIndicador
          rotulo="Mês Mais Movimentado"
          valor={kpis.mesMaisMovimentado.rotulo}
          icone="trending_up"
          corIcone="var(--verde-500)"
          corFundoIcone="var(--verde-100)"
          nota={`${kpis.mesMaisMovimentado.total.toLocaleString("pt-BR")} marcações`}
        />
        <CartaoIndicador
          rotulo="Tempo Médio de Espera"
          valor={formatarHoras(kpis.tempoMedioEspera)}
          icone="schedule"
          corIcone="var(--tinta-suave)"
          corFundoIcone="var(--superficie-alt)"
          nota="marcação → liberação"
        />
        <CartaoIndicador
          rotulo="Maior Atraso"
          valor={formatarHoras(kpis.maiorAtraso.esperaHoras)}
          icone="report"
          corIcone="var(--vermelho-500)"
          corFundoIcone="var(--vermelho-100)"
          nota={`movimento ${kpis.maiorAtraso.id}`}
        />
      </div>

      <div className="grade-painel">
        <div className="pilha">
          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Volume Total de Marcações por Mês</h3>
              <p>{kpis.total.toLocaleString("pt-BR")} marcações no total, no recorte selecionado</p>
            </div>
            <div className="cartao__corpo">
              <div className="grafico-fluxo">
                {porMes.map(m => (
                  <div key={m.chave} className="grafico-fluxo__col">
                    <span className="grafico-fluxo__valor">{m.total.toLocaleString("pt-BR")}</span>
                    <div className="grafico-fluxo__barra" style={{ height: `${(m.total / maiorMes) * 100}%` }} />
                    <span className="grafico-fluxo__rotulo">{m.rotulo}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>

          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Ranking das Maiores Esperas</h3>
              <p>Movimentos com maior diferença entre marcação e liberação</p>
            </div>
            <div className="cartao__corpo" style={{ overflowX: "auto" }}>
              <table>
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Movimento</th>
                    <th>Senha</th>
                    <th>Convênio</th>
                    {empresaId === "todas" && <th>Empresa</th>}
                    <th>Marcação</th>
                    <th>Liberação</th>
                    <th>Espera</th>
                  </tr>
                </thead>
                <tbody>
                  {top10.map((m, i) => (
                    <tr key={`${m.empresaId}-${m.id}`}>
                      <td>#{i + 1}</td>
                      <td className="mono">{m.id}</td>
                      <td className="mono">{m.senha}</td>
                      <td>{m.convenio}</td>
                      {empresaId === "todas" && <td>{m.empresaNome}</td>}
                      <td className="mono">{new Date(m.marcadoEm).toLocaleString("pt-BR")}</td>
                      <td className="mono">{new Date(m.liberadoEm).toLocaleString("pt-BR")}</td>
                      <td><b>{formatarHoras(m.esperaHoras)}</b></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div className="pilha">
          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Marcações por Operador</h3>
              <p>Bunge, Amaggi, TGPM e Hidrovias no período</p>
            </div>
            <div className="cartao__corpo">
              {porOperador.map(o => (
                <div key={o.operador} style={{ marginBottom: 12 }}>
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
                    <span>{o.operador}</span>
                    <b>{o.total.toLocaleString("pt-BR")}</b>
                  </div>
                  <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}>
                    <div style={{ width: `${(o.total / maiorOperador) * 100}%`, background: "var(--navio-700)", height: "100%", borderRadius: 6 }} />
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Marcações por Tipo de Carga</h3>
              <p>Soja, milho, caçamba e segregado</p>
            </div>
            <div className="cartao__corpo">
              {porCarga.map(c => (
                <div key={c.carga} style={{ marginBottom: 12 }}>
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
                    <span>{c.carga}</span>
                    <b>{c.total.toLocaleString("pt-BR")}</b>
                  </div>
                  <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}>
                    <div style={{ width: `${(c.total / maiorCarga) * 100}%`, background: "var(--ambar-500)", height: "100%", borderRadius: 6 }} />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
