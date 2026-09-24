import { useMemo } from "react";
import {
  ResponsiveContainer, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip
} from "recharts";
import { useRegistrosReais } from "../hooks/useRegistrosReais.js";
import { agruparPorMes } from "../data/relatorio.js";
import {
  indiceRiscoGargalo, alertasOperacionais, concentracaoPorTurno, horariosCriticos
} from "../data/metricsEngine.js";

const CORES_NIVEL = {
  normal: { bg: "var(--verde-100)", cor: "var(--verde-500)", emoji: "🟢" },
  atencao: { bg: "#FEF3C7", cor: "var(--ambar-600)", emoji: "🟡" },
  alto: { bg: "#FFE4CC", cor: "#C2650B", emoji: "🟠" },
  critico: { bg: "var(--vermelho-100)", cor: "var(--vermelho-500)", emoji: "🔴" }
};

export default function PrevisaoGargalos() {
  const { registros, carregando } = useRegistrosReais("todas");

  const porMes = useMemo(() => agruparPorMes(registros), [registros]);
  const indice = useMemo(() => indiceRiscoGargalo(registros), [registros]);
  const alertas = useMemo(() => alertasOperacionais(registros), [registros]);
  const turnos = useMemo(() => concentracaoPorTurno(registros), [registros]);
  const criticos = useMemo(() => horariosCriticos(registros), [registros]);

  const horaAgora = new Date().getHours();
  const proximas6h = useMemo(() => {
    const janela = [];
    for (let i = 0; i < 6; i++) {
      const h = (horaAgora + i) % 24;
      janela.push(indice[h]);
    }
    return janela;
  }, [indice, horaAgora]);

  const dadosVolume = useMemo(() => porMes.map(m => ({ mes: m.rotulo, total: m.total })), [porMes]);

  if (carregando) return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Carregando…</p>;

  return (
    <>
      <div className="cartao" style={{ marginBottom: 16 }}>
        <div className="cartao__cabecalho"><h3>Próximas 6 Horas (a partir de agora)</h3><p>Risco = ρ (chegada ÷ liberação) do histórico dessa faixa horária — ρ ≥ 1,4 crítico, ≥ 1,2 alto, ≥ 1,0 atenção</p></div>
        <div className="cartao__corpo">
          <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
            {proximas6h.map((h, i) => {
              const c = CORES_NIVEL[h.nivel];
              return (
                <div key={i} style={{ flex: "1 1 140px", padding: "12px 14px", borderRadius: 12, background: c.bg, textAlign: "center" }}>
                  <div style={{ fontSize: 11, color: "var(--tinta-suave)", fontWeight: 700 }}>{h.hora}h</div>
                  <div style={{ fontSize: 22, margin: "4px 0" }}>{c.emoji}</div>
                  <div style={{ fontSize: 11, color: c.cor, fontWeight: 700, textTransform: "uppercase" }}>{h.nivel}</div>
                  <div style={{ fontSize: 10, color: "var(--tinta-fraca)", marginTop: 2 }}>ρ = {h.rho.toFixed(2)}</div>
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
            <div className="cartao__cabecalho"><h3>Concentração de Tráfego por Turno</h3><p>Percentual de retenção (&gt;24h) por faixa horária — histórico completo</p></div>
            <div className="cartao__corpo">
              <table style={{ fontSize: 12 }}>
                <thead><tr><th>Turno</th><th>Total</th><th>Retidos (&gt;24h)</th><th>% Retenção</th></tr></thead>
                <tbody>
                  {turnos.map(t => (
                    <tr key={t.rotulo} style={{ background: t.pctRetencao > 20 ? "var(--vermelho-100)" : undefined }}>
                      <td><b>{t.rotulo}</b></td>
                      <td>{t.total.toLocaleString("pt-BR")}</td>
                      <td>{t.retidos.toLocaleString("pt-BR")}</td>
                      <td style={{ color: t.pctRetencao > 20 ? "var(--vermelho-500)" : undefined, fontWeight: t.pctRetencao > 20 ? 700 : undefined }}>{t.pctRetencao.toFixed(1)}%</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div className="cartao">
            <div className="cartao__cabecalho"><h3>Horários Mais Críticos</h3><p>Combinações dia da semana × turno com mais retenções (&gt;24h)</p></div>
            <div className="cartao__corpo">
              <table style={{ fontSize: 12 }}>
                <thead><tr><th>Dia da Semana</th><th>Faixa Horária</th><th>Retenções</th></tr></thead>
                <tbody>
                  {criticos.map((c, i) => (
                    <tr key={i}>
                      <td><b>{c.diaSemana}</b></td>
                      <td>{c.turno}</td>
                      <td><b>{c.total}</b></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div className="pilha">
          <div className="cartao">
            <div className="cartao__cabecalho"><h3>Volume Histórico Mensal</h3><p>Só dados reais registrados — sem projeção</p></div>
            <div className="cartao__corpo">
              <ResponsiveContainer width="100%" height={220}>
                <LineChart data={dadosVolume} margin={{ top: 6, right: 8, left: -18, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--superficie-alt)" />
                  <XAxis dataKey="mes" tick={{ fontSize: 10.5, fill: "var(--tinta-fraca)" }} axisLine={{ stroke: "var(--borda)" }} tickLine={false} />
                  <YAxis tick={{ fontSize: 10.5, fill: "var(--tinta-fraca)" }} axisLine={false} tickLine={false} width={54} tickFormatter={v => v.toLocaleString("pt-BR")} />
                  <Tooltip formatter={v => [v.toLocaleString("pt-BR"), "Marcações"]} contentStyle={{ fontSize: 12, borderRadius: 8, border: "1px solid var(--borda)" }} />
                  <Line dataKey="total" stroke="var(--navio-700)" strokeWidth={2} dot={{ r: 2.5 }} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
