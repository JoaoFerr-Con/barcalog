import { useState } from "react";
import CartaoIndicador from "../components/CartaoIndicador.jsx";
import { useNegativacao } from "../hooks/useNegativacao.js";
import { useAcao } from "../hooks/useAcao.js";
import { Carregando, ErroCarga, Vazio } from "../components/EstadoCarga.jsx";
import { obterSessao } from "../api/cliente.js";
import {
  listarTransportadoras,
  listarOcorrencias,
  listarContestacoes,
  listarVeiculos,
  registrarOcorrencia,
  responderContestacao,
  reincidenciasN2,
  negativarVeiculo,
  desnegativarVeiculo
} from "../data/negativacaoStore.js";

const NIVEIS = [
  { valor: "N1", rotulo: "N1 — Leve (registro/advertência)", cor: "var(--verde-500)" },
  { valor: "N2", rotulo: "N2 — Moderada (reincidência/monitoramento)", cor: "var(--ambar-600)" },
  { valor: "N3", rotulo: "N3 — Grave (bloqueio automático)", cor: "var(--vermelho-500)" }
];

const FORM_VAZIO = { nivel: "N1", placa: "", transportadora: "", descricao: "", local: "" };

// Auditor só lê: o backend recusa gravação (403); aqui só escondemos os botões.
function podeEscrever() {
  const papel = obterSessao()?.usuario?.papel;
  return papel === "Operador" || papel === "Gestor";
}

function FormularioOcorrencia() {
  const veiculos = listarVeiculos();
  const transportadoras = listarTransportadoras();
  const [form, setForm] = useState(FORM_VAZIO);
  const [executar, enviando] = useAcao();

  function atualizar(campo, valor) {
    setForm(f => ({ ...f, [campo]: valor }));
  }

  // Ao escolher uma placa cadastrada, preenche a transportadora dela.
  function escolherPlaca(valor) {
    const v = veiculos.find(x => x.placa === valor.toUpperCase());
    setForm(f => ({ ...f, placa: valor.toUpperCase(), transportadora: v ? v.transportadora : f.transportadora }));
  }

  async function enviar(e) {
    e.preventDefault();
    const r = await executar(() => registrarOcorrencia(form), {
      sucesso: res => res.veiculoBloqueado
        ? `Carreta ${res.ocorrencia.placa} foi negativada automaticamente (ocorrência N3).`
        : `Ocorrência ${res.ocorrencia.nivel} registrada para ${res.ocorrencia.placa}${res.veiculoCadastrado ? "" : " (placa não cadastrada na frota)"}.`,
      erro: "Ocorrência não registrada"
    });
    if (r) setForm(FORM_VAZIO);
  }

  return (
    <form onSubmit={enviar} className="cartao">
      <div className="cartao__cabecalho">
        <h3>Registrar Ocorrência</h3>
        <p>N3 bloqueia a carreta (e o condutor, se identificado) automaticamente e imediatamente</p>
      </div>
      <div className="cartao__corpo" style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12 }}>
        <label style={{ fontSize: 12, fontWeight: 600 }}>
          Nível de irregularidade
          <select value={form.nivel} onChange={e => atualizar("nivel", e.target.value)} style={estiloInput}>
            {NIVEIS.map(n => <option key={n.valor} value={n.valor}>{n.rotulo}</option>)}
          </select>
        </label>
        <label style={{ fontSize: 12, fontWeight: 600 }}>
          Placa do veículo
          <input list="placas-frota" value={form.placa} onChange={e => escolherPlaca(e.target.value)} style={estiloInput} placeholder="ABC-1234 ou ABC1D23" required maxLength={8} />
          <datalist id="placas-frota">
            {veiculos.map(v => <option key={v.id} value={v.placa} />)}
          </datalist>
        </label>
        <label style={{ fontSize: 12, fontWeight: 600 }}>
          Transportadora
          <select value={form.transportadora} onChange={e => atualizar("transportadora", e.target.value)} style={estiloInput} required>
            <option value="">Selecione</option>
            {transportadoras.map(t => <option key={t.id} value={t.nome}>{t.nome}</option>)}
          </select>
        </label>
        <label style={{ fontSize: 12, fontWeight: 600 }}>
          Local
          <input value={form.local} onChange={e => atualizar("local", e.target.value)} style={estiloInput} placeholder="Pátio de Triagem, Terminal..." maxLength={150} />
        </label>
        <label style={{ fontSize: 12, fontWeight: 600, gridColumn: "1 / -1" }}>
          Descrição da ocorrência
          <textarea value={form.descricao} onChange={e => atualizar("descricao", e.target.value)} style={{ ...estiloInput, minHeight: 70 }} required minLength={3} maxLength={2000} />
        </label>
      </div>
      <div style={{ padding: "0 20px 20px", display: "flex", justifyContent: "flex-end" }}>
        <button type="submit" className="botao botao--primario" disabled={enviando} aria-busy={enviando}>
          {enviando ? "Registrando…" : "Registrar ocorrência"}
        </button>
      </div>
    </form>
  );
}

const estiloInput = {
  display: "block", width: "100%", marginTop: 6, padding: "8px 10px",
  borderRadius: 8, border: "1px solid var(--borda)", fontSize: 13, fontFamily: "inherit"
};

export default function Negativacao() {
  const { carregado, erro, recarregar } = useNegativacao();
  const transportadoras = listarTransportadoras();
  const ocorrencias = listarOcorrencias();
  const contestacoesPendentes = listarContestacoes().filter(c => c.status === "pendente");
  const negativadas = transportadoras.filter(t => t.status === "negativada");
  const [expandidas, setExpandidas] = useState({});
  const [executar, ocupado] = useAcao();
  const escrita = podeEscrever();

  function alternarExpandida(id) {
    setExpandidas(e => ({ ...e, [id]: !e[id] }));
  }

  if (erro && !carregado) return <ErroCarga erro={erro} aoTentarDeNovo={recarregar} />;
  if (!carregado) return <Carregando />;

  return (
    <>
      {erro && <ErroCarga erro={erro} aoTentarDeNovo={recarregar} />}
      <div className="grade-kpi">
        <CartaoIndicador
          rotulo="Transportadoras Negativadas"
          valor={negativadas.length}
          icone="block"
          corIcone="var(--vermelho-500)"
          corFundoIcone="var(--vermelho-100)"
          nota={`de ${transportadoras.length} cadastradas`}
        />
        <CartaoIndicador
          rotulo="Ocorrências Ativas"
          valor={ocorrencias.filter(o => o.status === "ativa").length}
          icone="report"
          corIcone="var(--ambar-600)"
          corFundoIcone="#FBEBD1"
          nota="aguardando ação"
        />
        <CartaoIndicador
          rotulo="Contestações Pendentes"
          valor={contestacoesPendentes.length}
          icone="gavel"
          corIcone="var(--navio-700)"
          corFundoIcone="var(--azul-100)"
          nota="aguardando análise do GED"
        />
      </div>

      <div className="grade-painel">
        <div className="pilha">
          {escrita && <FormularioOcorrencia />}

          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Ocorrências Registradas</h3>
              <p>Mais recentes primeiro (até 500)</p>
            </div>
            <div className="cartao__corpo" style={{ overflowX: "auto" }}>
              {ocorrencias.length === 0 ? <Vazio icone="task_alt" titulo="Nenhuma ocorrência registrada" /> : (
                <table>
                  <thead>
                    <tr>
                      <th scope="col">Nível</th><th scope="col">Placa</th><th scope="col">Transportadora</th><th scope="col">Descrição</th><th scope="col">Status</th><th scope="col">Data</th>
                    </tr>
                  </thead>
                  <tbody>
                    {ocorrencias.map(o => (
                      <tr key={o.id}>
                        <td><b style={{ color: NIVEIS.find(n => n.valor === o.nivel)?.cor }}>{o.nivel}</b></td>
                        <td className="mono">{o.placa}</td>
                        <td>{o.transportadora}</td>
                        <td style={{ maxWidth: 260 }}>{o.descricao}</td>
                        <td>{o.status}</td>
                        <td className="mono">{new Date(o.criadoEm).toLocaleDateString("pt-BR")}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>

        <div className="pilha">
          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Status das Transportadoras</h3>
              <p>Não é a empresa que é negativada — são as carretas dela. O status aqui é calculado a partir das carretas de cada uma.</p>
            </div>
            <div className="cartao__corpo">
              {transportadoras.map(t => {
                const frota = listarVeiculos(t.nome);
                const aberta = !!expandidas[t.id];
                const n2 = reincidenciasN2(t.nome);
                return (
                  <div key={t.id} style={{ borderBottom: "1px solid var(--superficie-alt)" }}>
                    <button
                      type="button"
                      onClick={() => alternarExpandida(t.id)}
                      aria-expanded={aberta}
                      style={{
                        width: "100%", display: "flex", justifyContent: "space-between", alignItems: "center",
                        padding: "10px 0", gap: 8, flexWrap: "wrap", background: "none", border: "none", cursor: "pointer", textAlign: "left"
                      }}
                    >
                      <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                        <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 18, color: "var(--tinta-fraca)", transform: aberta ? "rotate(90deg)" : "none", transition: "transform .15s" }}>
                          chevron_right
                        </span>
                        <div>
                          <div style={{ fontWeight: 600, fontSize: 13 }}>{t.nome}</div>
                          {n2 >= 2 && (
                            <div style={{ fontSize: 11, color: "var(--ambar-600)" }}>{n2} ocorrências N2 nos últimos 30 dias</div>
                          )}
                        </div>
                      </div>
                      <span style={{
                        fontSize: 11, fontWeight: 700, padding: "4px 10px", borderRadius: 20,
                        background: t.carretasNegativadas > 0 ? "var(--vermelho-100)" : "var(--verde-100)",
                        color: t.carretasNegativadas > 0 ? "var(--vermelho-500)" : "var(--verde-500)"
                      }}>
                        {t.carretasNegativadas} de {t.carretasTotal} carretas negativadas
                      </span>
                    </button>

                    {aberta && (
                      <div style={{ paddingBottom: 10 }}>
                        {frota.length === 0 && <p style={{ fontSize: 12.5, color: "var(--tinta-suave)", margin: "0 0 8px 26px" }}>Nenhuma carreta cadastrada pra essa transportadora.</p>}
                        {frota.map(v => (
                          <div key={v.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "6px 0 6px 26px", gap: 8, flexWrap: "wrap" }}>
                            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                              <span className="placa-chip" style={{ fontSize: 12 }}>{v.placa}</span>
                              <span style={{ fontSize: 12, color: "var(--tinta-suave)" }}>{v.modelo}</span>
                            </div>
                            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                              <span style={{
                                fontSize: 10.5, fontWeight: 700, padding: "3px 8px", borderRadius: 20,
                                background: v.statusNegativacao === "negativada" ? "var(--vermelho-100)" : "var(--verde-100)",
                                color: v.statusNegativacao === "negativada" ? "var(--vermelho-500)" : "var(--verde-500)"
                              }}>
                                {v.statusNegativacao === "negativada" ? "Negativada" : "Regular"}
                              </span>
                              {escrita && (v.statusNegativacao === "negativada" ? (
                                <button
                                  type="button"
                                  className="botao botao--fantasma"
                                  style={{ fontSize: 11.5, padding: "4px 8px" }}
                                  disabled={ocupado}
                                  onClick={() => executar(() => desnegativarVeiculo(v.id), { sucesso: `Carreta ${v.placa} foi desnegativada.` })}
                                >
                                  Desnegativar
                                </button>
                              ) : (
                                <button
                                  type="button"
                                  className="botao botao--fantasma"
                                  style={{ fontSize: 11.5, padding: "4px 8px", color: "var(--vermelho-500)" }}
                                  disabled={ocupado}
                                  onClick={() => {
                                    if (!window.confirm(`Negativar a carreta ${v.placa}? Isso registra uma ocorrência N3 e bloqueia novos carregamentos.`)) return;
                                    executar(() => negativarVeiculo(v.id, "Negativação manual aplicada pela equipe do porto."), { sucesso: `Carreta ${v.placa} foi negativada manualmente.` });
                                  }}
                                >
                                  Negativar
                                </button>
                              ))}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>

          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Contestações — GED</h3>
              <p>Aprovar retira a negativação da carreta daquela ocorrência</p>
            </div>
            <div className="cartao__corpo">
              {contestacoesPendentes.length === 0 && (
                <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Nenhuma contestação pendente no momento.</p>
              )}
              {contestacoesPendentes.map(c => (
                <div key={c.id} style={{ padding: "10px 0", borderBottom: "1px solid var(--superficie-alt)" }}>
                  <div style={{ fontWeight: 600, fontSize: 13 }}>{c.transportadora} {c.placa && <span className="mono" style={{ fontWeight: 400 }}>— {c.nivelOcorrencia} {c.placa}</span>}</div>
                  <div style={{ fontSize: 12.5, color: "var(--tinta-suave)", margin: "4px 0" }}>{c.justificativa}</div>
                  {escrita && (
                    <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
                      <button type="button" className="botao botao--primario" disabled={ocupado}
                        onClick={() => executar(() => responderContestacao(c.id, true, "Documentação conferida, negativação retirada."), { sucesso: `Contestação de ${c.transportadora} aprovada — negativação retirada.` })}>
                        Aprovar
                      </button>
                      <button type="button" className="botao" disabled={ocupado}
                        onClick={() => executar(() => responderContestacao(c.id, false, "Documentação insuficiente."), { sucesso: `Contestação de ${c.transportadora} rejeitada.` })}>
                        Rejeitar
                      </button>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
