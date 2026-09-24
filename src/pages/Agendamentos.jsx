import { useState, useMemo } from "react";
import { QRCodeSVG } from "qrcode.react";
import AvisoDadosSimulados from "../components/AvisoDadosSimulados.jsx";
import { EMPRESAS } from "../data/registry.js";
import { listarTransportadoras } from "../data/negativacaoStore.js";
import { notificar } from "../components/toast.js";

// Simulação completa do módulo de Agendamentos — em memória (useState),
// não persiste entre recarregamentos. É a demonstração de como o módulo
// funcionaria assim que existir um backend de verdade por trás: hoje,
// criar um agendamento aqui não avisa ninguém em outro terminal/navegador.
const STATUS = ["Agendado", "Confirmado", "A caminho", "Aguardando entrada", "Em operação", "Finalizado", "Atrasado", "Cancelado"];
const CORES_STATUS = {
  "Agendado": { bg: "var(--superficie-alt)", cor: "var(--tinta-suave)" },
  "Confirmado": { bg: "var(--azul-100)", cor: "var(--navio-700)" },
  "A caminho": { bg: "#FEF3C7", cor: "var(--ambar-600)" },
  "Aguardando entrada": { bg: "#FEF3C7", cor: "var(--ambar-600)" },
  "Em operação": { bg: "var(--verde-100)", cor: "var(--verde-500)" },
  "Finalizado": { bg: "var(--verde-100)", cor: "var(--verde-500)" },
  "Atrasado": { bg: "var(--vermelho-100)", cor: "var(--vermelho-500)" },
  "Cancelado": { bg: "var(--superficie-alt)", cor: "var(--tinta-fraca)" }
};
const CARGAS = ["Soja", "Milho", "Caçamba", "Soja Segregado", "Outro"];

const SEED_AGENDAMENTOS = [
  { id: "AGD-004821", hora: "08:00", placa: "ENM-1001", transportadora: "Rota Amazônia Cargas", terminal: "unitapajos", carga: "Milho", motorista: "Elias Farias", status: "Finalizado" },
  { id: "AGD-004822", hora: "09:00", placa: "NGL-3021", transportadora: "Norte Grãos Logística", terminal: "unitapajos", carga: "Soja", motorista: "José Ribeiro", status: "Em operação" },
  { id: "AGD-004823", hora: "10:00", placa: "ATS-4410", transportadora: "AgroTransportes Sul", terminal: "tgpm", carga: "Soja", motorista: "Marcos Andrade", status: "Aguardando entrada" },
  { id: "AGD-004824", hora: "10:00", placa: "RAC-5502", transportadora: "Rota Amazônia Cargas", terminal: "tgpm", carga: "Milho", motorista: "Paulo Souza", status: "Confirmado" },
  { id: "AGD-004825", hora: "11:00", placa: "BCT-7715", transportadora: "Barcarena Transportes", terminal: "hidrovias", carga: "Soja Segregado", motorista: "Carla Lima", status: "A caminho" },
  { id: "AGD-004826", hora: "11:00", placa: "TNC-2290", transportadora: "TransNorte Cargas", terminal: "hidrovias", carga: "Caçamba", motorista: "Rui Nogueira", status: "Atrasado" },
  { id: "AGD-004827", hora: "12:00", placa: "ATS-9091", transportadora: "AgroTransportes Sul", terminal: "unitapajos", carga: "Soja", motorista: "Fábio Melo", status: "Agendado" }
];

export default function Agendamentos() {
  const [agendamentos, setAgendamentos] = useState(SEED_AGENDAMENTOS);
  const [filtroTerminal, setFiltroTerminal] = useState("todos");
  const [filtroStatus, setFiltroStatus] = useState("todos");
  const [formAberto, setFormAberto] = useState(false);
  const [qrAberto, setQrAberto] = useState(null);
  const transportadoras = listarTransportadoras();

  const [form, setForm] = useState({ hora: "", placa: "", transportadora: "", terminal: "unitapajos", carga: "Soja", motorista: "" });

  const filtrados = useMemo(() => agendamentos.filter(a =>
    (filtroTerminal === "todos" || a.terminal === filtroTerminal) &&
    (filtroStatus === "todos" || a.status === filtroStatus)
  ).sort((a, b) => a.hora.localeCompare(b.hora)), [agendamentos, filtroTerminal, filtroStatus]);

  const porHora = useMemo(() => {
    const mapa = {};
    agendamentos.forEach(a => { mapa[a.hora] = (mapa[a.hora] || 0) + 1; });
    return Object.entries(mapa).sort(([a], [b]) => a.localeCompare(b));
  }, [agendamentos]);

  const resumo = useMemo(() => ({
    total: agendamentos.length,
    confirmados: agendamentos.filter(a => ["Confirmado", "A caminho", "Aguardando entrada", "Em operação", "Finalizado"].includes(a.status)).length,
    emAndamento: agendamentos.filter(a => a.status === "Em operação").length,
    atrasados: agendamentos.filter(a => a.status === "Atrasado").length
  }), [agendamentos]);

  function nomeTerminal(id) {
    return EMPRESAS.find(e => e.id === id)?.nome || id;
  }

  function criarAgendamento(e) {
    e.preventDefault();
    if (!form.placa || !form.transportadora || !form.hora) {
      notificar("Preencha placa, transportadora e horário.", "erro");
      return;
    }
    const novo = { id: `AGD-${Date.now().toString().slice(-6)}`, ...form, status: "Agendado" };
    setAgendamentos(lista => [...lista, novo]);
    setForm({ hora: "", placa: "", transportadora: "", terminal: "unitapajos", carga: "Soja", motorista: "" });
    setFormAberto(false);
    notificar(`Agendamento ${novo.id} criado pra ${novo.placa} às ${novo.hora}.`, "sucesso");
  }

  function mudarStatus(id, status) {
    setAgendamentos(lista => lista.map(a => a.id === id ? { ...a, status } : a));
  }

  return (
    <>
      <AvisoDadosSimulados>
        Esta é uma simulação completa de como o módulo de Agendamentos funcionaria. Os agendamentos criados aqui ficam só na
        memória desta aba — não são salvos, não aparecem pra outro operador, e não persistem se você recarregar a página.
      </AvisoDadosSimulados>

      <div className="grade-kpi" style={{ marginBottom: 16 }}>
        <div className="cartao"><div className="cartao__corpo">
          <div style={{ fontSize: 11, color: "var(--tinta-fraca)", textTransform: "uppercase" }}>Agendados Hoje</div>
          <div style={{ fontSize: 28, fontWeight: 700, fontFamily: "'Space Grotesk', sans-serif" }}>{resumo.total}</div>
        </div></div>
        <div className="cartao"><div className="cartao__corpo">
          <div style={{ fontSize: 11, color: "var(--tinta-fraca)", textTransform: "uppercase" }}>Confirmados</div>
          <div style={{ fontSize: 28, fontWeight: 700, fontFamily: "'Space Grotesk', sans-serif", color: "var(--navio-700)" }}>{resumo.confirmados}</div>
        </div></div>
        <div className="cartao"><div className="cartao__corpo">
          <div style={{ fontSize: 11, color: "var(--tinta-fraca)", textTransform: "uppercase" }}>Em Operação</div>
          <div style={{ fontSize: 28, fontWeight: 700, fontFamily: "'Space Grotesk', sans-serif", color: "var(--verde-500)" }}>{resumo.emAndamento}</div>
        </div></div>
        <div className="cartao"><div className="cartao__corpo">
          <div style={{ fontSize: 11, color: "var(--tinta-fraca)", textTransform: "uppercase" }}>Atrasados</div>
          <div style={{ fontSize: 28, fontWeight: 700, fontFamily: "'Space Grotesk', sans-serif", color: "var(--vermelho-500)" }}>{resumo.atrasados}</div>
        </div></div>
      </div>

      <div className="cartao" style={{ marginBottom: 16 }}>
        <div className="cartao__cabecalho"><h3>Concentração por Horário</h3><p>Quantidade de agendamentos por janela — ajuda a ver sobreposição antes de confirmar</p></div>
        <div className="cartao__corpo">
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            {porHora.map(([hora, qtd]) => (
              <div key={hora} style={{
                padding: "8px 14px", borderRadius: 10,
                background: qtd >= 3 ? "var(--vermelho-100)" : qtd >= 2 ? "#FEF3C7" : "var(--superficie-alt)"
              }}>
                <div style={{ fontSize: 11, color: "var(--tinta-fraca)" }}>{hora}</div>
                <div style={{ fontSize: 16, fontWeight: 700, color: qtd >= 3 ? "var(--vermelho-500)" : qtd >= 2 ? "var(--ambar-600)" : "var(--tinta)" }}>
                  {qtd} agend. {qtd >= 3 ? "⚠️" : ""}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="cartao">
        <div className="cartao__cabecalho" style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 10 }}>
          <div>
            <h3>Agendamentos</h3>
            <p>Lista completa — filtre por terminal ou status</p>
          </div>
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            <select value={filtroTerminal} onChange={e => setFiltroTerminal(e.target.value)} style={{ padding: "6px 10px", borderRadius: 8, border: "1px solid var(--borda)", fontSize: 12.5 }}>
              <option value="todos">Todos os terminais</option>
              {EMPRESAS.map(e => <option key={e.id} value={e.id}>{e.nome}</option>)}
            </select>
            <select value={filtroStatus} onChange={e => setFiltroStatus(e.target.value)} style={{ padding: "6px 10px", borderRadius: 8, border: "1px solid var(--borda)", fontSize: 12.5 }}>
              <option value="todos">Todos os status</option>
              {STATUS.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
            <button className="botao" onClick={() => setFormAberto(f => !f)} style={{ fontSize: 12.5, padding: "6px 12px" }}>
              <span className="material-symbols-outlined" style={{ fontSize: 16, verticalAlign: "middle" }}>add</span> Novo Agendamento
            </button>
          </div>
        </div>

        {formAberto && (
          <div className="cartao__corpo" style={{ borderBottom: "1px solid var(--superficie-alt)" }}>
            <form onSubmit={criarAgendamento} style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12 }}>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Horário
                <input type="time" value={form.hora} onChange={e => setForm(f => ({ ...f, hora: e.target.value }))} style={{ display: "block", width: "100%", marginTop: 4, padding: 8, borderRadius: 8, border: "1px solid var(--borda)" }} required />
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Terminal
                <select value={form.terminal} onChange={e => setForm(f => ({ ...f, terminal: e.target.value }))} style={{ display: "block", width: "100%", marginTop: 4, padding: 8, borderRadius: 8, border: "1px solid var(--borda)" }}>
                  {EMPRESAS.map(e => <option key={e.id} value={e.id}>{e.nome}</option>)}
                </select>
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Tipo de Carga
                <select value={form.carga} onChange={e => setForm(f => ({ ...f, carga: e.target.value }))} style={{ display: "block", width: "100%", marginTop: 4, padding: 8, borderRadius: 8, border: "1px solid var(--borda)" }}>
                  {CARGAS.map(c => <option key={c} value={c}>{c}</option>)}
                </select>
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Placa
                <input value={form.placa} onChange={e => setForm(f => ({ ...f, placa: e.target.value.toUpperCase() }))} placeholder="ABC-1234" style={{ display: "block", width: "100%", marginTop: 4, padding: 8, borderRadius: 8, border: "1px solid var(--borda)" }} required />
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Transportadora
                <select value={form.transportadora} onChange={e => setForm(f => ({ ...f, transportadora: e.target.value }))} style={{ display: "block", width: "100%", marginTop: 4, padding: 8, borderRadius: 8, border: "1px solid var(--borda)" }} required>
                  <option value="">Selecione</option>
                  {transportadoras.map(t => <option key={t.id} value={t.nome}>{t.nome}</option>)}
                </select>
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Motorista (nome)
                <input value={form.motorista} onChange={e => setForm(f => ({ ...f, motorista: e.target.value }))} placeholder="Nome do motorista" style={{ display: "block", width: "100%", marginTop: 4, padding: 8, borderRadius: 8, border: "1px solid var(--borda)" }} />
              </label>
              <div style={{ gridColumn: "1 / -1" }}>
                <button type="submit" className="botao">Criar agendamento</button>
              </div>
            </form>
          </div>
        )}

        <div className="cartao__corpo" style={{ overflowX: "auto" }}>
          <table>
            <thead><tr><th>Horário</th><th>Placa</th><th>Transportadora</th><th>Terminal</th><th>Carga</th><th>Status</th><th></th></tr></thead>
            <tbody>
              {filtrados.map(a => {
                const c = CORES_STATUS[a.status];
                return (
                  <tr key={a.id}>
                    <td className="mono">{a.hora}</td>
                    <td className="mono">{a.placa}</td>
                    <td>{a.transportadora}</td>
                    <td>{nomeTerminal(a.terminal)}</td>
                    <td>{a.carga}</td>
                    <td>
                      <select
                        value={a.status}
                        onChange={e => mudarStatus(a.id, e.target.value)}
                        style={{ fontSize: 11, fontWeight: 700, padding: "3px 6px", borderRadius: 20, background: c.bg, color: c.cor, border: "none" }}
                      >
                        {STATUS.map(s => <option key={s} value={s}>{s}</option>)}
                      </select>
                    </td>
                    <td>
                      <button className="botao botao--fantasma" style={{ fontSize: 11, padding: "3px 8px" }} onClick={() => setQrAberto(qrAberto === a.id ? null : a.id)}>
                        <span className="material-symbols-outlined" style={{ fontSize: 13, verticalAlign: "middle" }}>qr_code_2</span> QR
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          {qrAberto && (() => {
            const a = agendamentos.find(x => x.id === qrAberto);
            if (!a) return null;
            return (
              <div style={{ display: "flex", alignItems: "center", gap: 16, padding: 16, marginTop: 12, border: "1px dashed var(--borda)", borderRadius: 12 }}>
                <QRCodeSVG value={a.id} size={110} />
                <div style={{ fontSize: 12.5, color: "var(--tinta-suave)" }}>
                  QR do agendamento <b>{a.id}</b> ({a.placa}, {a.hora}). Esse é o código que a transportadora recebe e apresenta na portaria —
                  a leitura recupera o agendamento inteiro, sem digitar nada.
                </div>
              </div>
            );
          })()}
        </div>
      </div>
    </>
  );
}
