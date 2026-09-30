import { useState, useMemo } from "react";
import { QRCodeSVG } from "qrcode.react";
import { useApi } from "../hooks/useApi.js";
import { useAcao } from "../hooks/useAcao.js";
import { useNegativacao } from "../hooks/useNegativacao.js";
import { api, novaChaveIdempotencia, obterSessao } from "../api/cliente.js";
import { STATUS_AGENDAMENTO, TERMINAIS, nomeTerminal } from "../api/rotulos.js";
import { hojeISO } from "../utils/formatar.js";
import { Carregando, ErroCarga, Vazio } from "../components/EstadoCarga.jsx";
import { listarTransportadoras, listarVeiculos } from "../data/negativacaoStore.js";

// Agendamentos persistidos na API. A API recusa: carreta negativada (N3),
// placa de outra transportadora, data passada e dois agendamentos ativos da
// mesma carreta no mesmo horário.
const CORES_STATUS = {
  Agendado: { bg: "var(--superficie-alt)", cor: "var(--tinta-suave)" },
  Confirmado: { bg: "var(--azul-100)", cor: "var(--navio-700)" },
  ACaminho: { bg: "#FEF3C7", cor: "var(--ambar-600)" },
  AguardandoEntrada: { bg: "#FEF3C7", cor: "var(--ambar-600)" },
  EmOperacao: { bg: "var(--verde-100)", cor: "var(--verde-500)" },
  Finalizado: { bg: "var(--verde-100)", cor: "var(--verde-500)" },
  Atrasado: { bg: "var(--vermelho-100)", cor: "var(--vermelho-500)" },
  Cancelado: { bg: "var(--superficie-alt)", cor: "var(--tinta-fraca)" }
};
const CARGAS = ["Soja", "Milho", "Soja Segregado", "Caçamba", "Outro"];
const JANELAS_CONFORMIDADE = [
  { valor: "D0", rotulo: "D0 — até 24h", cor: "var(--verde-500)", bg: "var(--verde-100)" },
  { valor: "D1", rotulo: "D1 — 24 a 48h", cor: "var(--ambar-600)", bg: "#FEF3C7" },
  { valor: "D2", rotulo: "D2 — 48 a 72h", cor: "#C2650B", bg: "#FFE4CC" },
  { valor: "D3", rotulo: "D3 — 72 a 96h", cor: "var(--vermelho-500)", bg: "var(--vermelho-100)" }
];
function janelaInfo(valor) {
  return JANELAS_CONFORMIDADE.find(j => j.valor === valor) || JANELAS_CONFORMIDADE[0];
}

const estiloCampo = { display: "block", width: "100%", marginTop: 4, padding: 8, borderRadius: 8, border: "1px solid var(--borda)" };
const FORM_VAZIO = { hora: "", placa: "", transportadoraId: "", terminalId: "unitapajos", carga: "Soja", motorista: "", janelaConformidade: "D0" };

export default function Agendamentos() {
  useNegativacao(); // transportadoras e placas pro formulário
  const papel = obterSessao()?.usuario?.papel;
  const podeEscrever = papel === "Operador" || papel === "Gestor";
  const [data, setData] = useState(hojeISO());
  const [filtroTerminal, setFiltroTerminal] = useState("todos");
  const [filtroStatus, setFiltroStatus] = useState("todos");
  const [formAberto, setFormAberto] = useState(false);
  const [qrAberto, setQrAberto] = useState(null);
  const [form, setForm] = useState(FORM_VAZIO);
  const [executar, salvando] = useAcao();
  const [executarStatus, mudandoStatus] = useAcao();
  const transportadoras = listarTransportadoras();
  const veiculos = listarVeiculos();

  const lista = useApi(
    sinal => api.get("/api/v1/agendamentos", { data, terminalId: filtroTerminal, status: filtroStatus, tamanhoPagina: 500 }, sinal),
    [data, filtroTerminal, filtroStatus]
  );
  const resumo = useApi(sinal => api.get("/api/v1/agendamentos/resumo", { data }, sinal), [data]);
  const agendamentos = lista.dados?.itens ?? [];

  function recarregarTudo() {
    lista.recarregar();
    resumo.recarregar();
  }

  function escolherPlaca(valor) {
    const placa = valor.toUpperCase();
    const v = veiculos.find(x => x.placa === placa);
    setForm(f => ({ ...f, placa, transportadoraId: v ? String(v.transportadoraId) : f.transportadoraId }));
  }

  async function criarAgendamento(e) {
    e.preventDefault();
    const chave = novaChaveIdempotencia();
    const novo = await executar(
      () => api.post("/api/v1/agendamentos", { ...form, data, transportadoraId: Number(form.transportadoraId), motorista: form.motorista || null }, chave),
      { sucesso: a => `Agendamento ${a.codigo} criado pra ${a.placa} às ${a.hora}.`, erro: "Agendamento não criado" }
    );
    if (novo) {
      setForm(FORM_VAZIO);
      setFormAberto(false);
      recarregarTudo();
    }
  }

  async function mudarStatus(id, status) {
    const ok = await executarStatus(() => api.patch(`/api/v1/agendamentos/${id}/status`, { status }), { erro: "Status não alterado" });
    if (ok) recarregarTudo();
  }

  const porHora = resumo.dados?.porHora ?? [];
  const agendamentoQr = useMemo(() => agendamentos.find(x => x.id === qrAberto), [agendamentos, qrAberto]);

  return (
    <>
      {(lista.erro || resumo.erro) && <ErroCarga erro={lista.erro || resumo.erro} aoTentarDeNovo={recarregarTudo} />}

      <div className="grade-kpi" style={{ marginBottom: 16 }}>
        {[
          { rotulo: "Agendados no dia", valor: resumo.dados?.total, cor: undefined },
          { rotulo: "Confirmados", valor: resumo.dados?.confirmados, cor: "var(--navio-700)" },
          { rotulo: "Em Operação", valor: resumo.dados?.emOperacao, cor: "var(--verde-500)" },
          { rotulo: "Atrasados", valor: resumo.dados?.atrasados, cor: "var(--vermelho-500)" }
        ].map(k => (
          <div key={k.rotulo} className="cartao"><div className="cartao__corpo">
            <div style={{ fontSize: 11, color: "var(--tinta-fraca)", textTransform: "uppercase" }}>{k.rotulo}</div>
            <div style={{ fontSize: 28, fontWeight: 700, fontFamily: "'Space Grotesk', sans-serif", color: k.cor }}>{k.valor ?? "—"}</div>
          </div></div>
        ))}
      </div>

      <div className="cartao" style={{ marginBottom: 16 }}>
        <div className="cartao__cabecalho"><h3>Concentração por Horário</h3><p>Quantidade de agendamentos por horário — ajuda a ver sobreposição antes de confirmar</p></div>
        <div className="cartao__corpo">
          {porHora.length === 0 ? <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Nenhum agendamento nesse dia.</p> : (
            <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
              {porHora.map(h => (
                <div key={h.hora} style={{
                  padding: "8px 14px", borderRadius: 10,
                  background: h.nivel === "critico" ? "var(--vermelho-100)" : h.nivel === "atencao" ? "#FEF3C7" : "var(--superficie-alt)"
                }}>
                  <div style={{ fontSize: 11, color: "var(--tinta-fraca)" }}>{h.hora}</div>
                  <div style={{ fontSize: 16, fontWeight: 700, color: h.nivel === "critico" ? "var(--vermelho-500)" : h.nivel === "atencao" ? "var(--ambar-600)" : "var(--tinta)" }}>
                    {h.total} agend. {h.nivel === "critico" ? "⚠️" : ""}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="cartao">
        <div className="cartao__cabecalho" style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 10 }}>
          <div>
            <h3>Agendamentos</h3>
            <p>Filtre por dia, terminal ou status</p>
          </div>
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            <input type="date" value={data} onChange={e => setData(e.target.value || hojeISO())} aria-label="Data" style={{ padding: "6px 10px", borderRadius: 8, border: "1px solid var(--borda)", fontSize: 12.5 }} />
            <select value={filtroTerminal} onChange={e => setFiltroTerminal(e.target.value)} aria-label="Terminal" style={{ padding: "6px 10px", borderRadius: 8, border: "1px solid var(--borda)", fontSize: 12.5 }}>
              <option value="todos">Todos os terminais</option>
              {TERMINAIS.map(t => <option key={t.id} value={t.id}>{t.nome}</option>)}
            </select>
            <select value={filtroStatus} onChange={e => setFiltroStatus(e.target.value)} aria-label="Status" style={{ padding: "6px 10px", borderRadius: 8, border: "1px solid var(--borda)", fontSize: 12.5 }}>
              <option value="todos">Todos os status</option>
              {Object.entries(STATUS_AGENDAMENTO).map(([k, r]) => <option key={k} value={k}>{r}</option>)}
            </select>
            {podeEscrever && (
              <button type="button" className="botao" onClick={() => setFormAberto(f => !f)} aria-expanded={formAberto} style={{ fontSize: 12.5, padding: "6px 12px" }}>
                <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 16, verticalAlign: "middle" }}>add</span> Novo Agendamento
              </button>
            )}
          </div>
        </div>

        {formAberto && (
          <div className="cartao__corpo" style={{ borderBottom: "1px solid var(--superficie-alt)" }}>
            <p style={{ fontSize: 11.5, color: "var(--tinta-suave)", marginBottom: 12 }}>
              <b>Janela de Conformidade</b> é o compromisso de SLA assumido no agendamento — em quanto tempo, depois da entrada, a carreta deve ser liberada (D0 até 24h, D1 até 48h, e assim por diante). Carretas negativadas (N3) não podem ser agendadas.
            </p>
            <form onSubmit={criarAgendamento} style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 12 }}>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Horário ({data.split("-").reverse().join("/")})
                <input type="time" value={form.hora} onChange={e => setForm(f => ({ ...f, hora: e.target.value }))} style={estiloCampo} required />
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Terminal
                <select value={form.terminalId} onChange={e => setForm(f => ({ ...f, terminalId: e.target.value }))} style={estiloCampo}>
                  {TERMINAIS.map(t => <option key={t.id} value={t.id}>{t.nome}</option>)}
                </select>
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Tipo de Carga
                <select value={form.carga} onChange={e => setForm(f => ({ ...f, carga: e.target.value }))} style={estiloCampo}>
                  {CARGAS.map(c => <option key={c} value={c}>{c}</option>)}
                </select>
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Janela de Conformidade
                <select value={form.janelaConformidade} onChange={e => setForm(f => ({ ...f, janelaConformidade: e.target.value }))} style={estiloCampo}>
                  {JANELAS_CONFORMIDADE.map(j => <option key={j.valor} value={j.valor}>{j.rotulo}</option>)}
                </select>
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Placa
                <input list="placas-agendamento" value={form.placa} onChange={e => escolherPlaca(e.target.value)} placeholder="ABC-1234" style={estiloCampo} required maxLength={8} />
                <datalist id="placas-agendamento">{veiculos.map(v => <option key={v.id} value={v.placa} />)}</datalist>
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Transportadora
                <select value={form.transportadoraId} onChange={e => setForm(f => ({ ...f, transportadoraId: e.target.value }))} style={estiloCampo} required>
                  <option value="">Selecione</option>
                  {transportadoras.map(t => <option key={t.id} value={t.id}>{t.nome}</option>)}
                </select>
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Motorista (nome)
                <input value={form.motorista} onChange={e => setForm(f => ({ ...f, motorista: e.target.value }))} placeholder="Nome do motorista" style={estiloCampo} maxLength={150} />
              </label>
              <div style={{ gridColumn: "1 / -1" }}>
                <button type="submit" className="botao" disabled={salvando} aria-busy={salvando}>{salvando ? "Criando…" : "Criar agendamento"}</button>
              </div>
            </form>
          </div>
        )}

        <div className="cartao__corpo" style={{ overflowX: "auto" }}>
          {lista.carregando && !lista.dados ? <Carregando /> : agendamentos.length === 0 ? (
            <Vazio icone="event_busy" titulo="Nenhum agendamento" texto="Não há agendamentos para esse dia e filtros." />
          ) : (
            <table>
              <thead><tr><th scope="col">Horário</th><th scope="col">Código</th><th scope="col">Placa</th><th scope="col">Transportadora</th><th scope="col">Terminal</th><th scope="col">Carga</th><th scope="col">Janela</th><th scope="col">Status</th><th scope="col"><span className="sr-only">QR</span></th></tr></thead>
              <tbody>
                {agendamentos.map(a => {
                  const c = CORES_STATUS[a.status] || CORES_STATUS.Agendado;
                  const j = janelaInfo(a.janelaConformidade);
                  return (
                    <tr key={a.id}>
                      <td className="mono">{a.hora}</td>
                      <td className="mono">{a.codigo}</td>
                      <td className="mono">{a.placa}</td>
                      <td>{a.transportadora}</td>
                      <td>{a.terminal || nomeTerminal(a.terminalId)}</td>
                      <td>{a.carga}</td>
                      <td>
                        <span style={{ fontSize: 11, fontWeight: 700, padding: "3px 8px", borderRadius: 20, background: j.bg, color: j.cor }} title={j.rotulo}>{j.valor}</span>
                      </td>
                      <td>
                        {podeEscrever ? (
                          <select
                            value={a.status}
                            disabled={mudandoStatus}
                            aria-label={`Status do agendamento ${a.codigo}`}
                            onChange={e => mudarStatus(a.id, e.target.value)}
                            style={{ fontSize: 11, fontWeight: 700, padding: "3px 6px", borderRadius: 20, background: c.bg, color: c.cor, border: "none" }}
                          >
                            {Object.entries(STATUS_AGENDAMENTO).map(([k, r]) => <option key={k} value={k}>{r}</option>)}
                          </select>
                        ) : (
                          <span style={{ fontSize: 11, fontWeight: 700, padding: "3px 8px", borderRadius: 20, background: c.bg, color: c.cor }}>{STATUS_AGENDAMENTO[a.status]}</span>
                        )}
                      </td>
                      <td>
                        <button type="button" className="botao botao--fantasma" style={{ fontSize: 11, padding: "3px 8px" }} onClick={() => setQrAberto(qrAberto === a.id ? null : a.id)} aria-expanded={qrAberto === a.id}>
                          <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 13, verticalAlign: "middle" }}>qr_code_2</span> QR
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
          {agendamentoQr && (
            <div style={{ display: "flex", alignItems: "center", gap: 16, padding: 16, marginTop: 12, border: "1px dashed var(--borda)", borderRadius: 12, flexWrap: "wrap" }}>
              <QRCodeSVG value={agendamentoQr.codigo} size={110} title={`QR do agendamento ${agendamentoQr.codigo}`} />
              <div style={{ fontSize: 12.5, color: "var(--tinta-suave)" }}>
                QR do agendamento <b>{agendamentoQr.codigo}</b> ({agendamentoQr.placa}, {agendamentoQr.hora}). Esse é o código que a transportadora apresenta na portaria —
                a leitura recupera o agendamento inteiro, sem digitar nada.
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  );
}
