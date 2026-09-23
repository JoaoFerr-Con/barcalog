import { useState, useMemo } from "react";
import {
  ResponsiveContainer, ComposedChart, Line, Area, XAxis, YAxis,
  CartesianGrid, Tooltip, Legend
} from "recharts";
import { useRegistrosReais } from "../hooks/useRegistrosReais.js";
import { agruparPorMes, formatarHoras } from "../data/relatorio.js";
import { analisePreditiva, indiceRiscoGargalo, alertasOperacionais } from "../data/metricsEngine.js";

const CORES_NIVEL = {
  normal: { bg: "var(--verde-100)", cor: "var(--verde-500)", emoji: "🟢" },
  atencao: { bg: "#FEF3C7", cor: "var(--ambar-600)", emoji: "🟡" },
  alto: { bg: "#FFE4CC", cor: "#C2650B", emoji: "🟠" },
  critico: { bg: "var(--vermelho-100)", cor: "var(--vermelho-500)", emoji: "🔴" }
};

export default function PrevisaoGargalos() {
  const { registros, carregando } = useRegistrosReais("todas");
  const [empresaFiltro] = useState("todas");

  const porMes = useMemo(() => agruparPorMes(registros), [registros]);
  const preditiva = useMemo(() => analisePreditiva(registros, 3), [registros]);
  const indice = useMemo(() => indiceRiscoGargalo(registros), [registros]);
  const alertas = useMemo(() => alertasOperacionais(registros), [registros]);

  const dadosPreditiva = useMemo(() => {
    const hist = porMes.map((m, i) => ({
      mes: m.rotulo, historico: m.total,
      projecao: i === porMes.length - 1 ? m.total : null, banda: null
    }));
    const proj = preditiva.projecoes.map(p => ({
      mes: p.rotulo, historico: null, projecao: p.total, banda: [p.margemInferior, p.margemSuperior]
    }));
    return [...hist, ...proj];
  }, [porMes, preditiva]);

  const horaAgora = new Date().getHours();
  const proximas6h = useMemo(() => {
    const janela = [];
    for (let i = 0; i < 6; i++) {
      const h = (horaAgora + i) % 24;
      janela.push(indice[h]);
    }
    return janela;
  }, [indice, horaAgora]);

  if (carregando) return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Carregando…</p>;

  return (
    <>
      <div className="cartao" style={{ marginBottom: 16 }}>
        <div className="cartao__corpo" style={{ display: "flex", alignItems: "flex-start", gap: 10, background: "var(--azul-100)", borderRadius: 10 }}>
          <span className="material-symbols-outlined" style={{ color: "var(--navio-700)", fontSize: 20, flexShrink: 0 }}>info</span>
          <div style={{ fontSize: 12.5, color: "var(--tinta-suave)" }}>
            Este módulo estima risco de gargalo com base no <b>padrão histórico real</b> de chegada e liberação de carretas (103.325 marcações).
            Ainda não usa dados de agendamento futuro — quando o módulo de Agendamentos existir com backend próprio,
            a previsão passa a considerar também a demanda já confirmada pra frente, não só o padrão do passado.
          </div>
        </div>
      </div>

      <div className="cartao" style={{ marginBottom: 16 }}>
        <div className="cartao__cabecalho"><h3>Próximas 6 Horas (a partir de agora)</h3><p>Risco estimado por horário, com base no ritmo histórico dessa faixa</p></div>
        <div className="cartao__corpo">
          <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
            {proximas6h.map((h, i) => {
              const c = CORES_NIVEL[h.nivel];
              return (
                <div key={i} style={{ flex: "1 1 140px", padding: "12px 14px", borderRadius: 12, background: c.bg, textAlign: "center" }}>
                  <div style={{ fontSize: 11, color: "var(--tinta-suave)", fontWeight: 700 }}>{h.hora}h</div>
                  <div style={{ fontSize: 22, margin: "4px 0" }}>{c.emoji}</div>
                  <div style={{ fontSize: 11, color: c.cor, fontWeight: 700, textTransform: "uppercase" }}>{h.nivel}</div>
                  <div style={{ fontSize: 10, color: "var(--tinta-fraca)", marginTop: 2 }}>índice {h.indice}/100</div>
                </div>
              );
            })}
          </div>
        </div>
      </div>

      <div className="grade-painel conteudo-pagina">
        <div className="pilha">
          <div className="cartao">
            <div className="cartao__cabecalho"><h3>Alertas Operacionais</h3><p>Gerados a partir dos indicadores e do padrão histórico</p></div>
            <div className="cartao__corpo">
              {alertas.map((a, i) => {
                const c = CORES_NIVEL[a.nivel] || CORES_NIVEL.normal;
                return (
                  <div key={i} style={{ display: "flex", gap: 10, padding: "10px 12px", marginBottom: 8, borderRadius: 10, background: c.bg }}>
                    <span style={{ fontSize: 16 }}>{c.emoji}</span>
                    <div>
                      <div style={{ fontSize: 12.5, fontWeight: 700, color: c.cor }}>{a.titulo}</div>
                      <div style={{ fontSize: 11.5, color: "var(--tinta-suave)" }}>{a.detalhe}</div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          <div className="cartao">
            <div className="cartao__cabecalho"><h3>Índice de Gargalo por Hora do Dia</h3><p>0–100, combinando fator de utilização (λ/μ) e volume relativo — histórico completo</p></div>
            <div className="cartao__corpo" style={{ overflowX: "auto" }}>
              <table style={{ fontSize: 11.5 }}>
                <thead><tr><th>Hora</th><th>Chegadas/h</th><th>Liberações/h</th><th>ρ (λ/μ)</th><th>Índice</th><th>Nível</th></tr></thead>
                <tbody>
                  {indice.filter(h => h.nivel !== "normal").sort((a, b) => b.indice - a.indice).map(h => {
                    const c = CORES_NIVEL[h.nivel];
                    return (
                      <tr key={h.hora}>
                        <td><b>{h.hora}h</b></td>
                        <td>{h.lambda.toFixed(1)}</td>
                        <td>{h.mu.toFixed(1)}</td>
                        <td style={{ color: h.rho >= 1 ? "var(--vermelho-500)" : undefined, fontWeight: h.rho >= 1 ? 700 : undefined }}>{h.rho.toFixed(2)}</td>
                        <td><b>{h.indice}</b></td>
                        <td style={{ color: c.cor, fontWeight: 700 }}>{c.emoji} {h.nivel}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
              {indice.every(h => h.nivel === "normal") && <p style={{ fontSize: 12.5, color: "var(--tinta-suave)" }}>Nenhuma hora do dia apresenta risco acima do normal no período analisado.</p>}
            </div>
          </div>
        </div>

        <div className="pilha">
          <div className="cartao">
            <div className="cartao__cabecalho"><h3>Análise Preditiva de Volume</h3><p>Projeção mensal com faixa de confiança — movida pra cá, separada do histórico puro da Visão Geral</p></div>
            <div className="cartao__corpo">
              <ResponsiveContainer width="100%" height={200}>
                <ComposedChart data={dadosPreditiva} margin={{ top: 6, right: 8, left: -18, bottom: 0 }}>
                  <defs>
                    <linearGradient id="bandaConfianca2" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor="var(--ambar-500)" stopOpacity={0.28} />
                      <stop offset="100%" stopColor="var(--ambar-500)" stopOpacity={0.04} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--superficie-alt)" />
                  <XAxis dataKey="mes" tick={{ fontSize: 10, fill: "var(--tinta-fraca)" }} axisLine={{ stroke: "var(--borda)" }} tickLine={false} />
                  <YAxis tick={{ fontSize: 10, fill: "var(--tinta-fraca)" }} axisLine={false} tickLine={false} width={52} tickFormatter={v => v.toLocaleString("pt-BR")} />
                  <Tooltip formatter={(v, n) => [v?.toLocaleString ? v.toLocaleString("pt-BR") : v, n]} contentStyle={{ fontSize: 12, borderRadius: 8, border: "1px solid var(--borda)" }} />
                  <Legend wrapperStyle={{ fontSize: 11 }} />
                  <Area dataKey="banda" name="Faixa de confiança" stroke="none" fill="url(#bandaConfianca2)" connectNulls />
                  <Line dataKey="historico" name="Histórico" stroke="var(--navio-700)" strokeWidth={2} dot={{ r: 2.5 }} connectNulls={false} />
                  <Line dataKey="projecao" name="Projeção" stroke="var(--ambar-500)" strokeWidth={2} strokeDasharray="5 3" dot={{ r: 2.5 }} connectNulls />
                </ComposedChart>
              </ResponsiveContainer>
              <p style={{ fontSize: 10.5, color: "var(--tinta-fraca)", marginTop: 4 }}>
                Tendência dos últimos meses (regressão linear). Com menos de um ano de histórico, ainda não dá pra detectar sazonalidade de calendário real.
              </p>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
