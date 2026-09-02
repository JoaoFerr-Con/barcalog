import { useState } from "react";
import CartaoIndicador from "../components/CartaoIndicador.jsx";
import { useNegativacao } from "../hooks/useNegativacao.js";
import { notificar } from "../components/toast.js";
import {
  listarTransportadoras,
  listarOcorrencias,
  listarContestacoes,
  listarVeiculos,
  registrarOcorrencia,
  responderContestacao,
  reincidenciasN2,
  negativarTransportadora,
  desnegativarTransportadora
} from "../data/negativacaoStore.js";

const NIVEIS = [
  { valor: "N1", rotulo: "N1 — Leve (registro/advertência)", cor: "var(--verde-500)" },
  { valor: "N2", rotulo: "N2 — Moderada (reincidência/monitoramento)", cor: "var(--ambar-600)" },
  { valor: "N3", rotulo: "N3 — Grave (bloqueio automático)", cor: "var(--vermelho-500)" }
];

function FormularioOcorrencia() {
  const veiculos = listarVeiculos();
  const transportadoras = listarTransportadoras();
  const [form, setForm] = useState({
    nivel: "N1", placa: "", cpfMotorista: "", transportadora: "",
    descricao: "", local: "", responsavel: "Admin Teste", evidencias: []
  });

  function atualizar(campo, valor) {
    setForm(f => ({ ...f, [campo]: valor }));
  }

  function enviar(e) {
    e.preventDefault();
    if (!form.placa || !form.transportadora || !form.descricao) return;
    registrarOcorrencia(form);
    if (form.nivel === "N3") {
      notificar(`${form.transportadora} foi negativada automaticamente (ocorrência N3).`, "erro");
    } else {
      notificar(`Ocorrência ${form.nivel} registrada para ${form.placa}.`, "sucesso");
    }
    setForm({ nivel: "N1", placa: "", cpfMotorista: "", transportadora: "", descricao: "", local: "", responsavel: "Admin Teste", evidencias: [] });
  }

  return (
    <form onSubmit={enviar} className="cartao">
      <div className="cartao__cabecalho">
        <h3>Registrar Ocorrência</h3>
        <p>N3 bloqueia a transportadora automaticamente e imediatamente</p>
      </div>
      <div className="cartao__corpo" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
        <label style={{ fontSize: 12, fontWeight: 600 }}>
          Nível de irregularidade
          <select value={form.nivel} onChange={e => atualizar("nivel", e.target.value)} style={estiloInput}>
            {NIVEIS.map(n => <option key={n.valor} value={n.valor}>{n.rotulo}</option>)}
          </select>
        </label>
        <label style={{ fontSize: 12, fontWeight: 600 }}>
          Transportadora
          <select value={form.transportadora} onChange={e => atualizar("transportadora", e.target.value)} style={estiloInput} required>
            <option value="">Selecione</option>
            {transportadoras.map(t => <option key={t.id} value={t.nome}>{t.nome}</option>)}
          </select>
        </label>
        <label style={{ fontSize: 12, fontWeight: 600 }}>
          Placa do veículo
          <input list="placas-frota" value={form.placa} onChange={e => atualizar("placa", e.target.value)} style={estiloInput} placeholder="ABC-1234" required />
          <datalist id="placas-frota">
            {veiculos.map(v => <option key={v.id} value={v.placa} />)}
          </datalist>
        </label>
        <label style={{ fontSize: 12, fontWeight: 600 }}>
          CPF do motorista
          <input value={form.cpfMotorista} onChange={e => atualizar("cpfMotorista", e.target.value)} style={estiloInput} placeholder="000.000.000-00" />
        </label>
        <label style={{ fontSize: 12, fontWeight: 600 }}>
          Local
          <input value={form.local} onChange={e => atualizar("local", e.target.value)} style={estiloInput} placeholder="Pátio de Triagem, Terminal..." />
        </label>
        <label style={{ fontSize: 12, fontWeight: 600 }}>
          Evidências (fotos)
          <input type="file" multiple accept="image/*" onChange={e => atualizar("evidencias", Array.from(e.target.files).map(f => f.name))} style={estiloInput} />
        </label>
        <label style={{ fontSize: 12, fontWeight: 600, gridColumn: "1 / -1" }}>
          Descrição da ocorrência
          <textarea value={form.descricao} onChange={e => atualizar("descricao", e.target.value)} style={{ ...estiloInput, minHeight: 70 }} required />
        </label>
      </div>
      <div style={{ padding: "0 20px 20px", display: "flex", justifyContent: "flex-end" }}>
        <button type="submit" className="botao botao--primario">Registrar ocorrência</button>
      </div>
    </form>
  );
}

const estiloInput = {
  display: "block", width: "100%", marginTop: 6, padding: "8px 10px",
  borderRadius: 8, border: "1px solid var(--borda)", fontSize: 13, fontFamily: "inherit"
};

export default function Negativacao() {
  useNegativacao();
  const transportadoras = listarTransportadoras();
  const ocorrencias = listarOcorrencias();
  const contestacoesPendentes = listarContestacoes().filter(c => c.status === "pendente");
  const negativadas = transportadoras.filter(t => t.status === "negativada");

  return (
    <>
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
          <FormularioOcorrencia />

          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Ocorrências Registradas</h3>
              <p>Histórico completo, mais recentes primeiro</p>
            </div>
            <div className="cartao__corpo" style={{ overflowX: "auto" }}>
              <table>
                <thead>
                  <tr>
                    <th>Nível</th><th>Placa</th><th>Transportadora</th><th>Descrição</th><th>Status</th><th>Data</th>
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
            </div>
          </div>
        </div>

        <div className="pilha">
          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Status das Transportadoras</h3>
              <p>Regular ou negativada, com sinal de reincidência N2</p>
            </div>
            <div className="cartao__corpo">
              {transportadoras.map(t => (
                <div key={t.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "8px 0", borderBottom: "1px solid var(--superficie-alt)", gap: 8, flexWrap: "wrap" }}>
                  <div>
                    <div style={{ fontWeight: 600, fontSize: 13 }}>{t.nome}</div>
                    {reincidenciasN2(t.nome) >= 2 && (
                      <div style={{ fontSize: 11, color: "var(--ambar-600)" }}>{reincidenciasN2(t.nome)} ocorrências N2 nos últimos 30 dias</div>
                    )}
                  </div>
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <span style={{
                      fontSize: 11, fontWeight: 700, padding: "4px 10px", borderRadius: 20,
                      background: t.status === "negativada" ? "var(--vermelho-100)" : "var(--verde-100)",
                      color: t.status === "negativada" ? "var(--vermelho-500)" : "var(--verde-500)"
                    }}>
                      {t.status === "negativada" ? "Negativada" : "Regular"}
                    </span>
                    {t.status === "negativada" ? (
                      <button
                        className="botao botao--fantasma"
                        style={{ fontSize: 12, padding: "5px 10px" }}
                        onClick={() => {
                          desnegativarTransportadora(t.nome, "Desnegativação manual pela equipe do porto.");
                          notificar(`${t.nome} foi desnegativada.`, "sucesso");
                        }}
                      >
                        Desnegativar
                      </button>
                    ) : (
                      <button
                        className="botao botao--fantasma"
                        style={{ fontSize: 12, padding: "5px 10px", color: "var(--vermelho-500)" }}
                        onClick={() => {
                          negativarTransportadora(t.nome, "Negativação manual aplicada pela equipe do porto.");
                          notificar(`${t.nome} foi negativada manualmente.`, "aviso");
                        }}
                      >
                        Negativar
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Contestações — GED</h3>
              <p>Aprovar retira a negativação (se não houver outra N3 ativa)</p>
            </div>
            <div className="cartao__corpo">
              {contestacoesPendentes.length === 0 && (
                <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Nenhuma contestação pendente no momento.</p>
              )}
              {contestacoesPendentes.map(c => (
                <div key={c.id} style={{ padding: "10px 0", borderBottom: "1px solid var(--superficie-alt)" }}>
                  <div style={{ fontWeight: 600, fontSize: 13 }}>{c.transportadora}</div>
                  <div style={{ fontSize: 12.5, color: "var(--tinta-suave)", margin: "4px 0" }}>{c.justificativa}</div>
                  <div style={{ fontSize: 11.5, color: "var(--tinta-fraca)" }}>Documentos: {c.documentos.join(", ") || "nenhum anexado"}</div>
                  <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
                    <button className="botao botao--primario" onClick={() => { responderContestacao(c.id, true, "Documentação conferida, negativação retirada."); notificar(`Contestação de ${c.transportadora} aprovada — negativação retirada.`, "sucesso"); }}>Aprovar</button>
                    <button className="botao" onClick={() => { responderContestacao(c.id, false, "Documentação insuficiente."); notificar(`Contestação de ${c.transportadora} rejeitada.`, "aviso"); }}>Rejeitar</button>
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
