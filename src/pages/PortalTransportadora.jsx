import { useState } from "react";
import { useApi } from "../hooks/useApi.js";
import { useAcao } from "../hooks/useAcao.js";
import { api, novaChaveIdempotencia } from "../api/cliente.js";
import { adaptarTransportadora, adaptarOcorrencia, adaptarContestacao, adaptarVeiculo, adaptarCondutor } from "../api/rotulos.js";
import { useSessao, sair } from "../auth/autenticacao.js";
import { FormularioLogin, TelaRestricao } from "../components/Acesso.jsx";
import { Carregando, ErroCarga } from "../components/EstadoCarga.jsx";
import CartaoIndicador from "../components/CartaoIndicador.jsx";

// Portal do Transportador (área externa). Login próprio (papel Transportadora
// na API) e todos os dados vêm de /api/v1/portal/*: o backend força o escopo
// pela transportadora do token — trocar um id na URL não mostra dados de outra
// empresa (a API devolve 404).

const P = "/api/v1/portal";

const estiloInput = {
  display: "block", width: "100%", marginTop: 6, padding: "8px 10px",
  borderRadius: 8, border: "1px solid var(--borda)", fontSize: 13, fontFamily: "inherit"
};

function TelaLogin({ aviso, aoVoltarParaInterno }) {
  return (
    <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", background: "var(--bg)", padding: 20 }}>
      <div className="cartao" style={{ width: "100%", maxWidth: 400 }}>
        <div className="cartao__cabecalho" style={{ display: "flex", alignItems: "center", gap: 12 }}>
          <div className="icone-ancora" style={{ width: 40, height: 40 }}>
            <span className="material-symbols-outlined" aria-hidden="true">anchor</span>
          </div>
          <div>
            <h3 style={{ margin: 0 }}>Portal do Transportador</h3>
            <p style={{ margin: "2px 0 0" }}>BarcaLog — Porto de Barcarena</p>
          </div>
        </div>
        <div className="cartao__corpo" style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <FormularioLogin placeholderEmail="contato@suatransportadora.com.br" aviso={aviso} textoBotao="Entrar" />
          <p style={{ fontSize: 11.5, color: "var(--tinta-fraca)", margin: 0 }}>
            O acesso é criado pela equipe do porto para cada transportadora. Não tem login? Fale com a administração do terminal.
          </p>
          {aoVoltarParaInterno && (
            <button type="button" className="botao botao--fantasma" onClick={aoVoltarParaInterno} style={{ fontSize: 12 }}>
              ← Voltar para o painel interno
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

// ---------- Navbar do portal (equivalente à barra lateral do painel interno) ----------
function PortalNavbar({ transportadora, usuario }) {
  const apta = transportadora.aptaParaOperar;
  return (
    <header style={{
      background: "var(--navio-900)", color: "#fff", padding: "0 24px", minHeight: 64, flexWrap: "wrap", gap: 12,
      display: "flex", alignItems: "center", justifyContent: "space-between", position: "sticky", top: 0, zIndex: 30
    }}>
      <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
        <div className="icone-ancora" style={{ width: 32, height: 32 }}>
          <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 18 }}>anchor</span>
        </div>
        <div>
          <div style={{ fontSize: 14, fontWeight: 700 }}>BarcaLog</div>
          <div style={{ fontSize: 10.5, color: "#93A4C2", textTransform: "uppercase", letterSpacing: "0.04em" }}>Portal do Transportador</div>
        </div>
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: 16 }}>
        <div style={{ textAlign: "right" }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>{transportadora.nome}</div>
          <div style={{ fontSize: 11, color: apta ? "#7CE3B0" : "#F3A6A6" }}>{apta ? "Regular" : "Negativada"} · {usuario.email}</div>
        </div>
        <button type="button" className="botao botao--fantasma" onClick={sair} style={{ color: "#fff", borderColor: "rgba(255,255,255,.25)" }}>Sair</button>
      </div>
    </header>
  );
}

function FluxoNegativacao({ transportadora, ocorrencias }) {
  const apta = transportadora.aptaParaOperar;
  const temContestada = ocorrencias.some(o => o.status === "contestada");
  const ativas = ocorrencias.filter(o => o.status === "ativa").length;
  const etapas = [
    { chave: "regular", rotulo: "Regular", ativo: true },
    { chave: "ocorrencia", rotulo: "Ocorrência registrada", ativo: ocorrencias.length > 0 },
    { chave: "bloqueio", rotulo: "Bloqueio N3", ativo: transportadora.status === "negativada" || temContestada },
    { chave: "contestacao", rotulo: "Contestação em análise", ativo: temContestada },
    { chave: "resolucao", rotulo: apta ? "Regularizada" : "Aguardando análise", ativo: apta && ocorrencias.length > 0 }
  ];

  return (
    <>
      <div className="cartao" style={{ marginBottom: 20 }}>
        <div className="cartao__cabecalho">
          <h3>Fluxo de negativação da empresa</h3>
          <p>Etapa atual do processo, de acordo com o histórico de ocorrências</p>
        </div>
        <div className="cartao__corpo">
          <div role="status" style={{
            padding: "16px 18px", borderRadius: 12, marginBottom: 20, display: "flex", alignItems: "center", gap: 12,
            background: apta ? "var(--verde-100)" : "var(--vermelho-100)"
          }}>
            <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 28, color: apta ? "var(--verde-500)" : "var(--vermelho-500)" }}>
              {apta ? "verified" : "block"}
            </span>
            <div>
              <div style={{ fontWeight: 700, fontSize: 15, color: apta ? "var(--verde-500)" : "var(--vermelho-500)" }}>
                {apta ? "Apta para operar no município" : "NÃO apta para operar no município"}
              </div>
              <div style={{ fontSize: 12.5, color: "var(--tinta-suave)" }}>
                {apta
                  ? "Nenhuma restrição ativa impede a circulação desta transportadora em Barcarena."
                  : "Há uma negativação ativa — a transportadora está bloqueada para novos carregamentos até resolução."}
              </div>
            </div>
          </div>

          <ol style={{ display: "flex", overflowX: "auto", gap: 0, listStyle: "none", padding: 0, margin: 0 }}>
            {etapas.map((e, i) => (
              <li key={e.chave} style={{ display: "flex", alignItems: "center", flex: i === etapas.length - 1 ? "0 0 auto" : 1 }}>
                <div style={{ textAlign: "center", minWidth: 90 }}>
                  <div style={{
                    width: 28, height: 28, borderRadius: "50%", margin: "0 auto 6px", display: "flex", alignItems: "center", justifyContent: "center",
                    background: e.ativo ? "var(--navio-800)" : "var(--superficie-alt)",
                    color: e.ativo ? "#fff" : "var(--tinta-fraca)", fontSize: 13, fontWeight: 700
                  }}>
                    {i + 1}
                  </div>
                  <div style={{ fontSize: 11, color: e.ativo ? "var(--tinta)" : "var(--tinta-fraca)", fontWeight: e.ativo ? 600 : 400 }}>{e.rotulo}</div>
                </div>
                {i < etapas.length - 1 && <div aria-hidden="true" style={{ flex: 1, height: 2, background: e.ativo && etapas[i + 1].ativo ? "var(--navio-800)" : "var(--superficie-alt)", marginBottom: 20 }} />}
              </li>
            ))}
          </ol>
        </div>
      </div>

      <div className="grade-kpi" style={{ marginBottom: 20 }}>
        <CartaoIndicador rotulo="Carretas cadastradas" valor={transportadora.carretasTotal} icone="local_shipping"
          corIcone="var(--navio-700)" corFundoIcone="var(--azul-100)" nota="na base do BarcaLog" />
        <CartaoIndicador rotulo="Carretas negativadas" valor={transportadora.carretasNegativadas} icone="block"
          corIcone="var(--vermelho-500)" corFundoIcone="var(--vermelho-100)" nota="bloqueadas para operar" />
        <CartaoIndicador rotulo="Ocorrências ativas" valor={ativas} icone="report"
          corIcone="var(--ambar-600)" corFundoIcone="#FBEBD1" nota="sem contestação ou resolução" />
        <CartaoIndicador rotulo="Reincidências N2" valor={transportadora.reincidenciasN2} icone="repeat"
          corIcone="var(--tinta-suave)" corFundoIcone="var(--superficie-alt)" nota="últimos 30 dias" />
      </div>
    </>
  );
}

function FormularioContestacao({ ocorrencia, aoFechar, aoEnviar }) {
  const [justificativa, setJustificativa] = useState("");
  const [executar, enviando] = useAcao();
  // Uma chave por formulário: se a rede falhar e o usuário reenviar, a API
  // reconhece a mesma ação e não abre dois chamados.
  const [chave] = useState(novaChaveIdempotencia);

  function enviar(e) {
    e.preventDefault();
    executar(
      () => api.post(`${P}/contestacoes`, { ocorrenciaId: ocorrencia.id, justificativa: justificativa.trim() }, chave),
      { sucesso: "Chamado de contestação aberto — nossa equipe vai analisar.", erro: "Não foi possível abrir o chamado" }
    ).then(ok => { if (ok !== false) { aoEnviar(); aoFechar(); } });
  }

  return (
    <form onSubmit={enviar} style={{ marginTop: 10, padding: 12, background: "var(--superficie-alt)", borderRadius: 10 }}>
      <label style={{ fontSize: 12, fontWeight: 600, display: "block", marginBottom: 8 }}>
        Justificativa da contestação
        <textarea value={justificativa} onChange={e => setJustificativa(e.target.value)} style={{ ...estiloInput, minHeight: 80 }}
          required minLength={10} maxLength={4000} placeholder="Explique o que aconteceu (mínimo 10 caracteres)." />
      </label>
      <p style={{ fontSize: 11.5, color: "var(--tinta-fraca)", margin: "0 0 8px" }}>
        Envio de documentos (GED) ainda não disponível: descreva as evidências na justificativa e envie os arquivos pelo canal oficial do terminal.
      </p>
      <div style={{ display: "flex", gap: 8 }}>
        <button type="submit" className="botao botao--primario" disabled={enviando}>{enviando ? "Enviando…" : "Abrir chamado"}</button>
        <button type="button" className="botao botao--fantasma" onClick={aoFechar} disabled={enviando}>Cancelar</button>
      </div>
    </form>
  );
}

function EstadoVazio({ icone, titulo, texto }) {
  return (
    <div style={{ textAlign: "center", color: "var(--tinta-suave)", padding: "28px 12px" }}>
      <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 30, color: "var(--tinta-fraca)" }}>{icone}</span>
      <p style={{ margin: "8px 0 2px", fontWeight: 600, fontSize: 13.5 }}>{titulo}</p>
      <p style={{ margin: 0, fontSize: 12.5 }}>{texto}</p>
    </div>
  );
}

const ROTULO_STATUS_CT = { pendente: "Em análise", aprovada: "Aprovada", rejeitada: "Rejeitada" };
const CORES_STATUS_CT = { pendente: "var(--ambar-600)", aprovada: "var(--verde-500)", rejeitada: "var(--vermelho-500)" };

function AbaOcorrenciasContestacoes({ ocorrencias, contestacoes, aoMudar }) {
  const [contestando, setContestando] = useState(null);
  const pendentes = new Set(contestacoes.filter(c => c.status === "pendente").map(c => c.ocorrenciaId));
  const contestaveis = ocorrencias.filter(o => o.nivel === "N3" && o.status === "ativa" && !pendentes.has(o.id));

  return (
    <>
      <div className="cartao" style={{ marginBottom: 20 }}>
        <div className="cartao__cabecalho">
          <h3>Abrir chamado</h3>
          <p>Ocorrências que ainda podem ser contestadas (bloqueios N3 ativos)</p>
        </div>
        <div className="cartao__corpo">
          {contestaveis.length === 0 && (
            <EstadoVazio icone="task_alt" titulo="Nenhum bloqueio contestável" texto="Quando houver uma ocorrência N3 em aberto, o chamado de contestação aparece aqui." />
          )}
          {contestaveis.map(o => (
            <div key={o.id} style={{ padding: "10px 0", borderBottom: "1px solid var(--superficie-alt)" }}>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <b style={{ fontSize: 13 }}>{o.nivel} — {o.placa}</b>
                <span style={{ fontSize: 12, color: "var(--tinta-fraca)" }}>{new Date(o.criadoEm).toLocaleDateString("pt-BR")}</span>
              </div>
              <p style={{ fontSize: 12.5, color: "var(--tinta-suave)", margin: "4px 0" }}>{o.descricao}</p>
              {contestando === o.id
                ? <FormularioContestacao ocorrencia={o} aoFechar={() => setContestando(null)} aoEnviar={aoMudar} />
                : <button type="button" className="botao botao--primario" onClick={() => setContestando(o.id)} style={{ marginTop: 6 }}>Abrir contestação</button>}
            </div>
          ))}
        </div>
      </div>

      <div className="cartao" style={{ marginBottom: 20 }}>
        <div className="cartao__cabecalho">
          <h3>Meus chamados</h3>
          <p>Contestações já abertas e o andamento de cada uma</p>
        </div>
        <div className="cartao__corpo">
          {contestacoes.length === 0 && (
            <EstadoVazio icone="folder_open" titulo="Nenhum chamado aberto" texto="Os chamados que você abrir aparecem aqui, com o status de análise da equipe do porto." />
          )}
          {contestacoes.map(c => (
            <div key={c.id} style={{ padding: "10px 0", borderBottom: "1px solid var(--superficie-alt)" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <span style={{ fontSize: 12, color: "var(--tinta-fraca)" }}>
                  {new Date(c.criadoEm).toLocaleDateString("pt-BR")}{c.placa ? ` · ${c.nivelOcorrencia || ""} ${c.placa}` : ""}
                </span>
                <span style={{ fontSize: 11, fontWeight: 700, color: CORES_STATUS_CT[c.status] }}>{(ROTULO_STATUS_CT[c.status] || c.status).toUpperCase()}</span>
              </div>
              <p style={{ fontSize: 12.5, color: "var(--tinta-suave)", margin: "4px 0", whiteSpace: "pre-line" }}>{c.justificativa}</p>
              {c.respostaOperador && <p style={{ fontSize: 11.5, color: "var(--tinta-suave)" }}>Resposta do porto: {c.respostaOperador}</p>}
            </div>
          ))}
        </div>
      </div>

      <div className="cartao">
        <div className="cartao__cabecalho">
          <h3>Histórico de ocorrências</h3>
          <p>Todas as ocorrências registradas para a sua empresa</p>
        </div>
        <div className="cartao__corpo" style={{ overflowX: "auto" }}>
          {ocorrencias.length === 0
            ? <EstadoVazio icone="verified" titulo="Nenhuma ocorrência" texto="Sua empresa não tem ocorrências registradas." />
            : (
              <table>
                <thead><tr><th scope="col">Data</th><th scope="col">Nível</th><th scope="col">Placa</th><th scope="col">Descrição</th><th scope="col">Status</th></tr></thead>
                <tbody>
                  {ocorrencias.map(o => (
                    <tr key={o.id}>
                      <td className="mono">{new Date(o.criadoEm).toLocaleDateString("pt-BR")}</td>
                      <td><b>{o.nivel}</b></td>
                      <td className="mono">{o.placa}</td>
                      <td style={{ maxWidth: 320 }}>{o.descricao}</td>
                      <td>{o.status}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
        </div>
      </div>
    </>
  );
}

function AbaMinhaFrota({ transportadora, veiculos, condutores }) {
  return (
    <>
      <div className="cartao" style={{ marginBottom: 20 }}>
        <div className="cartao__cabecalho">
          <h3>Meus veículos</h3>
          <p>Placas cadastradas no BarcaLog vinculadas à {transportadora.nome}</p>
        </div>
        <div className="cartao__corpo" style={{ overflowX: "auto" }}>
          {veiculos.length === 0
            ? <EstadoVazio icone="local_shipping" titulo="Nenhum veículo cadastrado" texto="Cadastros feitos pela equipe do porto aparecem aqui." />
            : (
              <table>
                <thead><tr><th scope="col">Placa</th><th scope="col">Modelo</th><th scope="col">Status na portaria</th><th scope="col">Situação</th></tr></thead>
                <tbody>
                  {veiculos.map(v => (
                    <tr key={v.id}>
                      <td className="mono">{v.placa}</td>
                      <td>{v.modelo || "—"}</td>
                      <td>{v.statusPortaria}</td>
                      <td style={{ color: v.statusNegativacao === "negativado" ? "var(--vermelho-500)" : "var(--verde-500)", fontWeight: 600 }}>
                        {v.statusNegativacao === "negativado" ? "Negativada" : "Regular"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
        </div>
      </div>

      <div className="cartao">
        <div className="cartao__cabecalho">
          <h3>Meus condutores</h3>
          <p>Motoristas vinculados à {transportadora.nome}</p>
        </div>
        <div className="cartao__corpo" style={{ overflowX: "auto" }}>
          {condutores.length === 0
            ? <EstadoVazio icone="badge" titulo="Nenhum condutor cadastrado" texto="Cadastros feitos pela equipe do porto aparecem aqui." />
            : (
              <table>
                <thead><tr><th scope="col">Nome</th><th scope="col">Placa vinculada</th><th scope="col">Situação</th></tr></thead>
                <tbody>
                  {condutores.map(c => (
                    <tr key={c.id}>
                      <td>{c.nome}</td>
                      <td className="mono">{c.placaVinculada || "—"}</td>
                      <td>{c.statusNegativacao === "negativado" ? "Negativado" : "Regular"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
        </div>
      </div>
    </>
  );
}

const ABAS_PORTAL = [
  { chave: "fluxo", rotulo: "Visão Geral" },
  { chave: "chamados", rotulo: "Ocorrências & Chamados" },
  { chave: "frota", rotulo: "Minha Frota" }
];

async function buscarPortal(sinal) {
  const [resumo, ocorrencias, contestacoes, veiculos, condutores] = await Promise.all([
    api.get(`${P}/resumo`, null, sinal),
    api.get(`${P}/ocorrencias`, { tamanhoPagina: 200 }, sinal),
    api.get(`${P}/contestacoes`, { tamanhoPagina: 200 }, sinal),
    api.get(`${P}/veiculos`, { tamanhoPagina: 500 }, sinal),
    api.get(`${P}/condutores`, { tamanhoPagina: 500 }, sinal)
  ]);
  return {
    transportadora: adaptarTransportadora(resumo),
    ocorrencias: ocorrencias.itens.map(adaptarOcorrencia),
    contestacoes: contestacoes.itens.map(adaptarContestacao),
    veiculos: veiculos.itens.map(adaptarVeiculo),
    condutores: condutores.itens.map(adaptarCondutor)
  };
}

function Dashboard({ usuario }) {
  const [aba, setAba] = useState("fluxo");
  // Atualiza a cada 2 min pra refletir análises feitas pela equipe do porto.
  const { dados, erro, recarregar } = useApi(buscarPortal, [], { intervaloMs: 120000 });

  if (erro && !dados) return <div style={{ padding: 24 }}><ErroCarga erro={erro} aoTentarDeNovo={recarregar} /></div>;
  if (!dados) return <Carregando texto="Carregando dados da sua empresa…" />;
  const { transportadora, ocorrencias, contestacoes, veiculos, condutores } = dados;

  return (
    <div style={{ minHeight: "100vh", background: "var(--bg)" }}>
      <PortalNavbar transportadora={transportadora} usuario={usuario} />
      <div style={{ maxWidth: 960, margin: "0 auto", padding: "28px 16px 60px" }}>
        <div style={{ marginBottom: 18 }}>
          <p style={{ color: "var(--tinta-suave)", fontSize: 13, margin: 0 }}>CNPJ {transportadora.cnpj}</p>
        </div>

        <div className="abas" role="tablist" style={{ marginBottom: 20, display: "inline-flex", flexWrap: "wrap" }}>
          {ABAS_PORTAL.map(a => (
            <button type="button" role="tab" aria-selected={aba === a.chave} key={a.chave} onClick={() => setAba(a.chave)} className={`aba-botao ${aba === a.chave ? "ativo" : ""}`}>
              {a.rotulo}
            </button>
          ))}
        </div>

        <div className="conteudo-pagina" key={aba}>
          {aba === "fluxo" && <FluxoNegativacao transportadora={transportadora} ocorrencias={ocorrencias} />}
          {aba === "chamados" && <AbaOcorrenciasContestacoes ocorrencias={ocorrencias} contestacoes={contestacoes} aoMudar={recarregar} />}
          {aba === "frota" && <AbaMinhaFrota transportadora={transportadora} veiculos={veiculos} condutores={condutores} />}
        </div>
      </div>
    </div>
  );
}

/** Conta interna logada abrindo /portal: o portal é só para transportadoras. */
function ContaInterna({ aoVoltarParaInterno }) {
  return (
    <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", padding: 16 }}>
      <div className="cartao" style={{ maxWidth: 420 }}>
        <div className="cartao__corpo" style={{ textAlign: "center", padding: 28 }}>
          <h3 style={{ margin: "0 0 8px" }}>Você está logado com uma conta da equipe do porto</h3>
          <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>O portal é exclusivo para transportadoras. Para ver o portal, saia e entre com uma conta de transportadora.</p>
          <div style={{ display: "flex", gap: 8, justifyContent: "center", marginTop: 12 }}>
            {aoVoltarParaInterno && <button type="button" className="botao botao--primario" onClick={aoVoltarParaInterno}>Voltar ao painel</button>}
            <button type="button" className="botao botao--fantasma" onClick={sair}>Sair</button>
          </div>
        </div>
      </div>
    </div>
  );
}

export default function PortalTransportadora({ aoVoltarParaInterno }) {
  const { sessao, aviso } = useSessao();
  if (!sessao) return <TelaLogin aviso={aviso} aoVoltarParaInterno={aoVoltarParaInterno} />;
  if (sessao.restricao) return <TelaRestricao sessao={sessao} />;
  if (sessao.usuario?.papel !== "Transportadora") return <ContaInterna aoVoltarParaInterno={aoVoltarParaInterno} />;
  return <Dashboard usuario={sessao.usuario} />;
}
