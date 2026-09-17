import { useMemo } from "react";
import CartaoIndicador from "../components/CartaoIndicador.jsx";
import { useRegistrosReais } from "../hooks/useRegistrosReais.js";
import { totaisPorEmpresa, agruparPorMes, formatarHoras } from "../data/relatorio.js";
import { EMPRESAS } from "../data/registry.js";
import { tmaPorTerminal, distribuicaoJanelas, CAPACIDADE_DIARIA } from "../data/metricsEngine.js";

export default function MapaOperacional() {
  const { registros, carregando } = useRegistrosReais("todas");
  const porEmpresa = useMemo(() => totaisPorEmpresa(registros), [registros]);
  const tma = useMemo(() => tmaPorTerminal(registros), [registros]);
  const janelas = useMemo(() => distribuicaoJanelas(registros), [registros]);

  const porEmpresaDetalhado = useMemo(() => {
    return EMPRESAS.map(e => {
      const doTerminal = registros.filter(r => r.empresaId === e.id);
      const meses = agruparPorMes(doTerminal);
      const ultimoMes = meses[meses.length - 1];
      const agregado = porEmpresa.find(x => x.empresa === e.nome);
      const tmaTerminal = tma.find(t => t.terminal === e.nome);
      const janelasDo = distribuicaoJanelas(doTerminal);
      const pctD0 = janelasDo.find(j => j.chave === "D0")?.pct || 0;
      return { ...e, total: agregado?.total || 0, esperaMedia: agregado?.esperaMedia || 0, ultimoMes, tmaTerminal, pctD0, janelasDo };
    });
  }, [registros, porEmpresa, tma]);

  const maiorMes = useMemo(() => {
    const todos = agruparPorMes(registros);
    return Math.max(...todos.map(m => m.total), 1);
  }, [registros]);

  const porMesGeral = useMemo(() => agruparPorMes(registros), [registros]);

  if (carregando) return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Carregando…</p>;

  return (
    <>
      <div className="grade-kpi">
        {porEmpresaDetalhado.map(e => (
          <CartaoIndicador
            key={e.id}
            rotulo={e.nome}
            valor={e.total.toLocaleString("pt-BR")}
            icone="anchor"
            corIcone="var(--navio-700)"
            corFundoIcone="var(--azul-100)"
            nota={`espera média ${formatarHoras(e.esperaMedia)}`}
          />
        ))}
      </div>

      <div className="grade-painel conteudo-pagina">
        <div className="pilha">
          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Eficiência Operacional por Terminal</h3>
              <p>TMA, variação mês a mês, taxa D0 (≤24h) e SLA</p>
            </div>
            <div className="cartao__corpo">
              {porEmpresaDetalhado.map(e => {
                const t = e.tmaTerminal;
                return (
                  <div key={e.id} style={{ marginBottom: 16, padding: "12px 14px", borderRadius: 12, background: "var(--superficie-alt)" }}>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
                      <h4 style={{ margin: 0, fontSize: 14 }}>{e.nome}</h4>
                      {t?.variacao !== null && t?.variacao !== undefined && (
                        <span style={{ fontSize: 12, fontWeight: 700, color: t.variacao > 5 ? "var(--vermelho-500)" : t.variacao < -5 ? "var(--verde-500)" : "var(--tinta-suave)" }}>
                          TMA {t.variacao > 0 ? "▲" : "▼"} {Math.abs(t.variacao).toFixed(1)}% vs mês anterior
                        </span>
                      )}
                    </div>
                    <div style={{ display: "flex", gap: 20, flexWrap: "wrap" }}>
                      <div>
                        <div style={{ fontSize: 11, color: "var(--tinta-fraca)", textTransform: "uppercase", letterSpacing: "0.03em" }}>TMA Geral</div>
                        <div style={{ fontSize: 18, fontWeight: 700, fontFamily: "'Space Grotesk', sans-serif" }}>{formatarHoras(e.esperaMedia)}</div>
                      </div>
                      <div>
                        <div style={{ fontSize: 11, color: "var(--tinta-fraca)", textTransform: "uppercase", letterSpacing: "0.03em" }}>Taxa D0 (≤24h)</div>
                        <div style={{ fontSize: 18, fontWeight: 700, color: e.pctD0 > 80 ? "var(--verde-500)" : e.pctD0 > 60 ? "var(--ambar-600)" : "var(--vermelho-500)" }}>{e.pctD0.toFixed(1)}%</div>
                      </div>
                      <div>
                        <div style={{ fontSize: 11, color: "var(--tinta-fraca)", textTransform: "uppercase", letterSpacing: "0.03em" }}>% Capacidade</div>
                        <div style={{ fontSize: 18, fontWeight: 700 }}>{((e.total / (e.ultimoMes ? porEmpresaDetalhado.length : 1) / CAPACIDADE_DIARIA) * 100 / 214 * porEmpresaDetalhado.length).toFixed(1)}%</div>
                      </div>
                      <div>
                        <div style={{ fontSize: 11, color: "var(--tinta-fraca)", textTransform: "uppercase", letterSpacing: "0.03em" }}>Volume Total</div>
                        <div style={{ fontSize: 18, fontWeight: 700 }}>{e.total.toLocaleString("pt-BR")}</div>
                      </div>
                    </div>
                    <div style={{ display: "flex", gap: 3, marginTop: 10 }}>
                      {e.janelasDo.map(j => (
                        <div key={j.chave} style={{ flex: Math.max(j.pct, 1), background: j.cor, borderRadius: 3, height: 6, opacity: 0.8 }} title={`${j.rotulo}: ${j.pct.toFixed(1)}%`} />
                      ))}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        <div className="pilha">
          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Volume Consolidado por Mês</h3>
              <p>Soma dos três terminais</p>
            </div>
            <div className="cartao__corpo">
              <div className="grafico-fluxo">
                {porMesGeral.map(m => (
                  <div key={m.chave} className="grafico-fluxo__col">
                    <span className="grafico-fluxo__valor">{m.total.toLocaleString("pt-BR")}</span>
                    <div className="grafico-fluxo__barra" style={{ height: `${(m.total / maiorMes) * 100}%` }} />
                    <span className="grafico-fluxo__rotulo">{m.rotulo}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>

          {porEmpresaDetalhado.map(e => (
            <div key={e.id} className="cartao">
              <div className="cartao__cabecalho">
                <h3>{e.nome}</h3>
                <p>Último mês com dados: {e.ultimoMes?.rotulo || "—"} — {e.ultimoMes?.total.toLocaleString("pt-BR") || 0} marcações</p>
              </div>
              <div className="cartao__corpo">
                <p style={{ fontSize: 13 }}>Tempo médio de atendimento (marcação → liberação): <b>{formatarHoras(e.esperaMedia)}</b></p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </>
  );
}
