import { useState, useMemo } from "react";
import CartaoIndicador from "../components/CartaoIndicador.jsx";
import SeletorEmpresa from "../components/SeletorEmpresa.jsx";
import { useRegistrosReais } from "../hooks/useRegistrosReais.js";
import {
  obterKpisGerais,
  agruparPorMes,
  totaisPorEmpresa,
  totaisPorOperador,
  formatarHoras
} from "../data/relatorio.js";

// Visão Geral = landing page da operação, 100% em cima dos dados reais
// importados (Unitapajós, TGPM, Hidrovias). Substitui a versão anterior,
// que usava carretas geradas aleatoriamente e um "Ranking de
// Transportadoras" que não correspondia a nenhum dado real disponível.
//
// Pra somar uma nova empresa/fonte de dados aqui, não precisa mexer neste
// arquivo — só registrar o novo dataset em src/data/registry.js.
export default function VisaoGeral() {
  const [empresaId, setEmpresaId] = useState("todas");
  const { registros, carregando } = useRegistrosReais(empresaId);

  const kpis = useMemo(() => obterKpisGerais(registros), [registros]);
  const porMes = useMemo(() => agruparPorMes(registros), [registros]);
  const porEmpresa = useMemo(() => totaisPorEmpresa(registros), [registros]);
  const porOperador = useMemo(() => totaisPorOperador(registros), [registros]);
  const recentes = useMemo(
    () => [...registros].sort((a, b) => new Date(b.marcadoEm) - new Date(a.marcadoEm)).slice(0, 8),
    [registros]
  );

  if (carregando) {
    return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Carregando dados reais…</p>;
  }
  if (!kpis) {
    return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Nenhum registro para essa empresa.</p>;
  }

  const maiorMes = Math.max(...porMes.map(m => m.total), 1);
  const maiorEmpresa = Math.max(...porEmpresa.map(e => e.total), 1);
  const maiorOperador = Math.max(...porOperador.map(o => o.total), 1);

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
          rotulo="Tempo Médio de Espera"
          valor={formatarHoras(kpis.tempoMedioEspera)}
          icone="schedule"
          corIcone="var(--tinta-suave)"
          corFundoIcone="var(--superficie-alt)"
          nota="marcação → liberação"
        />
        <CartaoIndicador
          rotulo="Mês Mais Movimentado"
          valor={kpis.mesMaisMovimentado.rotulo}
          icone="trending_up"
          corIcone="var(--verde-500)"
          corFundoIcone="var(--verde-100)"
          nota={`${kpis.mesMaisMovimentado.total.toLocaleString("pt-BR")} marcações`}
        />
      </div>

      <div className="grade-painel">
        <div className="pilha">
          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Volume de Marcações por Mês</h3>
              <p>Recorte atual: {empresaId === "todas" ? "todas as empresas" : registros[0]?.empresaNome}</p>
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
              <h3>Últimas Marcações</h3>
              <p>Registros mais recentes no recorte selecionado</p>
            </div>
            <div className="cartao__corpo" style={{ overflowX: "auto" }}>
              <table>
                <thead>
                  <tr>
                    <th>Movimento</th>
                    <th>Convênio</th>
                    {empresaId === "todas" && <th>Empresa</th>}
                    <th>Marcação</th>
                    <th>Espera</th>
                  </tr>
                </thead>
                <tbody>
                  {recentes.map(m => (
                    <tr key={`${m.empresaId}-${m.id}`}>
                      <td className="mono">{m.id}</td>
                      <td>{m.convenio}</td>
                      {empresaId === "todas" && <td>{m.empresaNome}</td>}
                      <td className="mono">{new Date(m.marcadoEm).toLocaleString("pt-BR")}</td>
                      <td>{formatarHoras(m.esperaHoras)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div className="pilha">
          {empresaId === "todas" && (
            <div className="cartao">
              <div className="cartao__cabecalho">
                <h3>Marcações por Empresa</h3>
                <p>Distribuição entre as fontes de dados importadas</p>
              </div>
              <div className="cartao__corpo">
                {porEmpresa.map(e => (
                  <div key={e.empresa} style={{ marginBottom: 12 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
                      <span>{e.empresa}</span>
                      <b>{e.total.toLocaleString("pt-BR")} · espera média {formatarHoras(e.esperaMedia)}</b>
                    </div>
                    <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}>
                      <div style={{ width: `${(e.total / maiorEmpresa) * 100}%`, background: "var(--navio-700)", height: "100%", borderRadius: 6 }} />
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Marcações por Operador</h3>
              <p>Bunge, Amaggi, TGPM e Hidrovias no recorte selecionado</p>
            </div>
            <div className="cartao__corpo">
              {porOperador.map(o => (
                <div key={o.operador} style={{ marginBottom: 12 }}>
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
                    <span>{o.operador}</span>
                    <b>{o.total.toLocaleString("pt-BR")}</b>
                  </div>
                  <div style={{ background: "var(--superficie-alt)", borderRadius: 6, height: 8 }}>
                    <div style={{ width: `${(o.total / maiorOperador) * 100}%`, background: "var(--ambar-500)", height: "100%", borderRadius: 6 }} />
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
