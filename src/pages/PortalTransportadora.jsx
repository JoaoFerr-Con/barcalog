import { useState } from "react";
import { useNegativacao } from "../hooks/useNegativacao.js";
import {
  autenticarTransportadora,
  listarTransportadoras,
  listarOcorrencias,
  listarContestacoes,
  abrirContestacao
} from "../data/negativacaoStore.js";
import { notificar } from "../components/toast.js";

// Portal externo de autoatendimento — pensado como uma "área" separada da
// central de operações interna. Hoje roda dentro do mesmo bundle (ainda não
// há backend/autenticação real), acessível pela URL /portal. Quando a API
// de auth existir, só trocar autenticarTransportadora() por uma chamada real.

function TelaLogin({ aoEntrar, aoVoltarParaInterno }) {
  const [nome, setNome] = useState("");
  const [senha, setSenha] = useState("");
  const [erro, setErro] = useState("");
  const transportadoras = listarTransportadoras();

  function enviar(e) {
    e.preventDefault();
    const t = autenticarTransportadora(nome, senha);
    if (!t) {
      setErro("Transportadora ou senha inválida.");
      notificar("Não foi possível entrar — confira o nome e a senha.", "erro");
      return;
    }
    notificar(`Bem-vindo(a), ${t.nome}.`, "sucesso");
    aoEntrar(t);
  }

  return (
    <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", background: "var(--fundo)", padding: 20 }}>
      <form onSubmit={enviar} className="cartao" style={{ width: 380 }}>
        <div className="cartao__cabecalho" style={{ display: "flex", alignItems: "center", gap: 12 }}>
          <div className="icone-ancora" style={{ width: 40, height: 40 }}>
            <span className="material-symbols-outlined">anchor</span>
          </div>
          <div>
            <h3 style={{ margin: 0 }}>Portal do Transportador</h3>
            <p style={{ margin: "2px 0 0" }}>BarcaLog — Porto de Barcarena</p>
          </div>
        </div>
        <div className="cartao__corpo" style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <label style={{ fontSize: 12, fontWeight: 600 }}>
            Nome da transportadora
            <input list="transportadoras-portal" value={nome} onChange={e => setNome(e.target.value)} style={estiloInput} placeholder="Ex: Rota Amazônia Cargas" />
            <datalist id="transportadoras-portal">
              {transportadoras.map(t => <option key={t.id} value={t.nome} />)}
            </datalist>
          </label>
          <label style={{ fontSize: 12, fontWeight: 600 }}>
            Senha
            <input type="password" value={senha} onChange={e => setSenha(e.target.value)} style={estiloInput} />
          </label>
          {erro && <p style={{ color: "var(--vermelho-500)", fontSize: 12.5 }}>{erro}</p>}
          <button type="submit" className="botao botao--primario">Entrar</button>
          <p style={{ fontSize: 11, color: "var(--tinta-fraca)" }}>Demo: qualquer transportadora cadastrada, senha 1234.</p>
          {aoVoltarParaInterno && (
            <button type="button" className="botao botao--fantasma" onClick={aoVoltarParaInterno} style={{ fontSize: 12 }}>
              ← Voltar para o painel interno
            </button>
          )}
        </div>
      </form>
    </div>
  );
}

const estiloInput = {
  display: "block", width: "100%", marginTop: 6, padding: "8px 10px",
  borderRadius: 8, border: "1px solid var(--borda)", fontSize: 13, fontFamily: "inherit"
};

function FormularioContestacao({ ocorrencia, transportadora, aoFechar }) {
  const [justificativa, setJustificativa] = useState("");
  const [documentos, setDocumentos] = useState([]);

  function enviar(e) {
    e.preventDefault();
    abrirContestacao({ ocorrenciaId: ocorrencia.id, transportadora, justificativa, documentos });
    notificar("Contestação enviada para análise da equipe do porto.", "sucesso");
    aoFechar();
  }

  return (
    <form onSubmit={enviar} style={{ marginTop: 10, padding: 12, background: "var(--superficie-alt)", borderRadius: 10 }}>
      <label style={{ fontSize: 12, fontWeight: 600, display: "block", marginBottom: 8 }}>
        Justificativa da contestação
        <textarea value={justificativa} onChange={e => setJustificativa(e.target.value)} style={{ ...estiloInput, minHeight: 60 }} required />
      </label>
      <label style={{ fontSize: 12, fontWeight: 600, display: "block", marginBottom: 8 }}>
        Documentos comprobatórios (GED)
        <input type="file" multiple onChange={e => setDocumentos(Array.from(e.target.files).map(f => f.name))} style={estiloInput} />
      </label>
      <div style={{ display: "flex", gap: 8 }}>
        <button type="submit" className="botao botao--primario">Enviar contestação</button>
        <button type="button" className="botao botao--fantasma" onClick={aoFechar}>Cancelar</button>
      </div>
    </form>
  );
}

function Dashboard({ transportadora, sair }) {
  useNegativacao();
  const ocorrencias = listarOcorrencias(transportadora.nome);
  const contestacoes = listarContestacoes(transportadora.nome);
  const [contestando, setContestando] = useState(null);

  const ocorrenciasN3Ativas = ocorrencias.filter(o => o.nivel === "N3" && o.status === "ativa");

  return (
    <div style={{ maxWidth: 900, margin: "0 auto", padding: "40px 20px" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
        <div>
          <h2 style={{ margin: 0 }}>{transportadora.nome}</h2>
          <p style={{ color: "var(--tinta-suave)", fontSize: 13, margin: "4px 0 0" }}>CNPJ {transportadora.cnpj}</p>
        </div>
        <button className="botao botao--fantasma" onClick={sair}>Sair</button>
      </div>

      <div className="cartao" style={{ marginBottom: 20 }}>
        <div className="cartao__corpo" style={{ display: "flex", alignItems: "center", gap: 12 }}>
          <span style={{
            fontSize: 13, fontWeight: 700, padding: "6px 14px", borderRadius: 20,
            background: transportadora.status === "negativada" ? "var(--vermelho-100)" : "var(--verde-100)",
            color: transportadora.status === "negativada" ? "var(--vermelho-500)" : "var(--verde-500)"
          }}>
            {transportadora.status === "negativada" ? "Status: Negativada" : "Status: Regular"}
          </span>
          <span style={{ fontSize: 13, color: "var(--tinta-suave)" }}>
            {ocorrencias.length} ocorrência(s) no histórico · {contestacoes.length} contestação(ões) aberta(s)
          </span>
        </div>
      </div>

      <div className="cartao">
        <div className="cartao__cabecalho">
          <h3>Motivos e Datas das Restrições</h3>
          <p>Ocorrências registradas em seu nome</p>
        </div>
        <div className="cartao__corpo">
          {ocorrencias.length === 0 && <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Nenhuma ocorrência registrada.</p>}
          {ocorrencias.map(o => (
            <div key={o.id} style={{ padding: "10px 0", borderBottom: "1px solid var(--superficie-alt)" }}>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <b style={{ fontSize: 13 }}>{o.nivel} — {o.placa}</b>
                <span style={{ fontSize: 12, color: "var(--tinta-fraca)" }}>{new Date(o.criadoEm).toLocaleDateString("pt-BR")}</span>
              </div>
              <p style={{ fontSize: 12.5, color: "var(--tinta-suave)", margin: "4px 0" }}>{o.descricao}</p>
              <span style={{ fontSize: 11.5 }}>Status: {o.status}</span>

              {o.nivel === "N3" && o.status === "ativa" && (
                contestando === o.id
                  ? <FormularioContestacao ocorrencia={o} transportadora={transportadora.nome} aoFechar={() => setContestando(null)} />
                  : <div style={{ marginTop: 6 }}>
                      <button className="botao botao--primario" onClick={() => setContestando(o.id)}>Abrir contestação (GED)</button>
                    </div>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

export default function PortalTransportadora({ aoVoltarParaInterno }) {
  const [logada, setLogada] = useState(null);
  useNegativacao();

  if (!logada) return <TelaLogin aoEntrar={setLogada} aoVoltarParaInterno={aoVoltarParaInterno} />;
  return <Dashboard transportadora={logada} sair={() => setLogada(null)} />;
}
