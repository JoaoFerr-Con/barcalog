import { useState, useMemo } from "react";
import { useNegativacao } from "../hooks/useNegativacao.js";
import {
  autenticarTransportadora,
  listarTransportadoras,
  obterTransportadora,
  listarOcorrencias,
  listarContestacoes,
  listarVeiculos,
  listarCondutores,
  abrirContestacao,
  estaAptaParaOperar,
  SENHA_PADRAO_DEMO
} from "../data/negativacaoStore.js";
import { gerarMetricasMock } from "../data/metricasPortalMock.js";
import { notificar } from "../components/toast.js";
import { definirSessao } from "../data/sessao.js";
import CartaoIndicador from "../components/CartaoIndicador.jsx";

const estiloInput = {
  display: "block", width: "100%", marginTop: 6, padding: "8px 10px",
  borderRadius: 8, border: "1px solid var(--borda)", fontSize: 13, fontFamily: "inherit"
};

function TelaLogin({ aoEntrar, aoVoltarParaInterno }) {
  const [nome, setNome] = useState("");
  const [senha, setSenha] = useState("");
  const [erro, setErro] = useState("");
  const transportadoras = listarTransportadoras();

  function enviar(e) {
    e.preventDefault();
    const t = autenticarTransportadora(nome, senha);
    if (!t) {
      setErro(`Transportadora ou senha inválida. A senha padrão de demonstração é "${SENHA_PADRAO_DEMO}".`);
      notificar("Não foi possível entrar — confira o nome e a senha.", "erro");
      return;
    }
    definirSessao({ tipo: "portal", nome: t.nome });
    notificar(`Bem-vindo(a), ${t.nome}.`, "sucesso");
    aoEntrar(t);
  }

  function entrarComo(t) {
    setNome(t.nome);
    setSenha(SENHA_PADRAO_DEMO);
    definirSessao({ tipo: "portal", nome: t.nome });
    notificar(`Bem-vindo(a), ${t.nome}.`, "sucesso");
    aoEntrar(t);
  }

  return (
    <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", background: "var(--fundo)", padding: 20 }}>
      <form onSubmit={enviar} className="cartao" style={{ width: 400 }}>
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
          <p style={{ fontSize: 11, color: "var(--tinta-fraca)", margin: 0 }}>
            Fase de demonstração: a senha é a mesma pra todas as transportadoras: <b>{SENHA_PADRAO_DEMO}</b>.
          </p>

          <div style={{ borderTop: "1px solid var(--superficie-alt)", paddingTop: 12, marginTop: 4 }}>
            <p style={{ fontSize: 11, fontWeight: 700, color: "var(--tinta-suave)", textTransform: "uppercase", letterSpacing: "0.04em", marginBottom: 8 }}>
              Ou entre direto como (demo)
            </p>
            <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
              {transportadoras.map(t => (
                <button
                  type="button"
                  key={t.id}
                  onClick={() => entrarComo(t)}
                  className="selo"
                  style={{ background: "var(--superficie-alt)", color: "var(--tinta-suave)", cursor: "pointer", border: "none" }}
                >
                  {t.nome}
                </button>
              ))}
            </div>
          </div>

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

// ---------- Navbar do portal (equivalente à barra lateral do painel interno) ----------
function PortalNavbar({ transportadora, apta, sair }) {
  return (
    <header style={{
      background: "var(--navio-900)", color: "#fff", padding: "0 24px", height: 64,
      display: "flex", alignItems: "center", justifyContent: "space-between", position: "sticky", top: 0, zIndex: 30
    }}>
      <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
        <div className="icone-ancora" style={{ width: 32, height: 32 }}>
          <span className="material-symbols-outlined" style={{ fontSize: 18 }}>anchor</span>
        </div>
        <div>
          <div style={{ fontSize: 14, fontWeight: 700 }}>BarcaLog</div>
          <div style={{ fontSize: 10.5, color: "#93A4C2", textTransform: "uppercase", letterSpacing: "0.04em" }}>Portal do Transportador</div>
        </div>
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: 16 }}>
        <div style={{ textAlign: "right" }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>{transportadora.nome}</div>
          <div style={{ fontSize: 11, color: apta ? "#7CE3B0" : "#F3A6A6" }}>{apta ? "Regular" : "Negativada"}</div>
        </div>
        <button className="botao botao--fantasma" onClick={sair} style={{ color: "#fff", borderColor: "rgba(255,255,255,.25)" }}>Sair</button>
      </div>
    </header>
  );
}

function FluxoNegativacao({ transportadora, ocorrencias }) {
  const apta = estaAptaParaOperar(transportadora.nome);
  const temContestada = ocorrencias.some(o => o.status === "contestada");
  const metricas = useMemo(() => gerarMetricasMock(transportadora.nome), [transportadora.nome]);
  const maiorViagem = Math.max(...metricas.tendencia.map(t => t.viagens), 1);
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
          <div style={{
            padding: "16px 18px", borderRadius: 12, marginBottom: 20, display: "flex", alignItems: "center", gap: 12,
            background: apta ? "var(--verde-100)" : "var(--vermelho-100)"
          }}>
            <span className="material-symbols-outlined" style={{ fontSize: 28, color: apta ? "var(--verde-500)" : "var(--vermelho-500)" }}>
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

          <div style={{ display: "flex", overflowX: "auto", gap: 0 }}>
            {etapas.map((e, i) => (
              <div key={e.chave} style={{ display: "flex", alignItems: "center", flex: i === etapas.length - 1 ? "0 0 auto" : 1 }}>
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
                {i < etapas.length - 1 && <div style={{ flex: 1, height: 2, background: e.ativo && etapas[i + 1].ativo ? "var(--navio-800)" : "var(--superficie-alt)", marginBottom: 20 }} />}
              </div>
            ))}
          </div>
        </div>
      </div>

      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 10 }}>
        <span className="material-symbols-outlined" style={{ fontSize: 16, color: "var(--tinta-fraca)" }}>info</span>
        <p style={{ fontSize: 12, color: "var(--tinta-fraca)", margin: 0 }}>
          Métricas de operação abaixo são um exemplo (dados mockados) — quando a integração com as marcações reais estiver pronta, esses números virão do sistema.
        </p>
      </div>

      <div className="grade-kpi" style={{ marginBottom: 20 }}>
        <CartaoIndicador
          rotulo="Viagens no Mês"
          valor={metricas.viagensNoMes}
          icone="local_shipping"
          corIcone="var(--navio-700)"
          corFundoIcone="var(--azul-100)"
          nota="julho/2026 (exemplo)"
        />
        <CartaoIndicador
          rotulo="Volume Transportado"
          valor={`${metricas.volumeToneladas.toLocaleString("pt-BR")} t`}
          icone="scale"
          corIcone="var(--ambar-600)"
          corFundoIcone="#FBEBD1"
          nota="no mês (exemplo)"
        />
        <CartaoIndicador
          rotulo="Tempo Médio de Operação"
          valor={`${metricas.tempoMedioHoras.toFixed(1)}h`}
          icone="schedule"
          corIcone="var(--tinta-suave)"
          corFundoIcone="var(--superficie-alt)"
          nota="marcação → liberação"
        />
        <CartaoIndicador
          rotulo="Pontualidade"
          valor={`${metricas.pontualidade}%`}
          icone="task_alt"
          corIcone="var(--verde-500)"
          corFundoIcone="var(--verde-100)"
          nota="dentro da janela D0–D3"
        />
      </div>

      <div className="cartao">
        <div className="cartao__cabecalho">
          <h3>Viagens por Mês</h3>
          <p>Últimos 6 meses (exemplo)</p>
        </div>
        <div className="cartao__corpo">
          <div className="grafico-fluxo">
            {metricas.tendencia.map(t => (
              <div key={t.mes} className="grafico-fluxo__col">
                <span className="grafico-fluxo__valor">{t.viagens}</span>
                <div className="grafico-fluxo__barra" style={{ height: `${(t.viagens / maiorViagem) * 100}%` }} />
                <span className="grafico-fluxo__rotulo">{t.mes}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </>
  );
}

function FormularioContestacao({ ocorrencia, transportadora, aoFechar }) {
  const [justificativa, setJustificativa] = useState("");
  const [documentos, setDocumentos] = useState([]);

  function enviar(e) {
    e.preventDefault();
    abrirContestacao({ ocorrenciaId: ocorrencia.id, transportadora, justificativa, documentos });
    notificar("Chamado de contestação aberto — nossa equipe vai analisar.", "sucesso");
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
        <button type="submit" className="botao botao--primario">Abrir chamado</button>
        <button type="button" className="botao botao--fantasma" onClick={aoFechar}>Cancelar</button>
      </div>
    </form>
  );
}

function EstadoVazio({ icone, titulo, texto }) {
  return (
    <div style={{ textAlign: "center", color: "var(--tinta-suave)", padding: "28px 12px" }}>
      <span className="material-symbols-outlined" style={{ fontSize: 30, color: "var(--tinta-fraca)" }}>{icone}</span>
      <p style={{ margin: "8px 0 2px", fontWeight: 600, fontSize: 13.5 }}>{titulo}</p>
      <p style={{ margin: 0, fontSize: 12.5 }}>{texto}</p>
    </div>
  );
}

function AbaOcorrenciasContestacoes({ transportadora, ocorrencias, contestacoes }) {
  const [contestando, setContestando] = useState(null);
  const CORES_STATUS_CT = { pendente: "var(--ambar-600)", aprovada: "var(--verde-500)", rejeitada: "var(--vermelho-500)" };
  const contestaveis = ocorrencias.filter(o => o.nivel === "N3" && o.status === "ativa");

  return (
    <>
      <div className="cartao" style={{ marginBottom: 20 }}>
        <div className="cartao__cabecalho">
          <h3>Abrir chamado</h3>
          <p>Ocorrências que ainda podem ser contestadas (bloqueios N3 ativos)</p>
        </div>
        <div className="cartao__corpo">
          {contestaveis.length === 0 && (
            <EstadoVazio icone="task_alt" titulo="Nenhum bloqueio ativo" texto="Quando houver uma ocorrência N3 em aberto, o chamado de contestação aparece aqui." />
          )}
          {contestaveis.map(o => (
            <div key={o.id} style={{ padding: "10px 0", borderBottom: "1px solid var(--superficie-alt)" }}>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <b style={{ fontSize: 13 }}>{o.nivel} — {o.placa}</b>
                <span style={{ fontSize: 12, color: "var(--tinta-fraca)" }}>{new Date(o.criadoEm).toLocaleDateString("pt-BR")}</span>
              </div>
              <p style={{ fontSize: 12.5, color: "var(--tinta-suave)", margin: "4px 0" }}>{o.descricao}</p>
              {contestando === o.id
                ? <FormularioContestacao ocorrencia={o} transportadora={transportadora.nome} aoFechar={() => setContestando(null)} />
                : <button className="botao botao--primario" onClick={() => setContestando(o.id)} style={{ marginTop: 6 }}>Abrir contestação (GED)</button>}
            </div>
          ))}
        </div>
      </div>

      <div className="cartao">
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
                <span style={{ fontSize: 12, color: "var(--tinta-fraca)" }}>{new Date(c.criadoEm).toLocaleDateString("pt-BR")}</span>
                <span style={{ fontSize: 11, fontWeight: 700, color: CORES_STATUS_CT[c.status] }}>{c.status.toUpperCase()}</span>
              </div>
              <p style={{ fontSize: 12.5, color: "var(--tinta-suave)", margin: "4px 0" }}>{c.justificativa}</p>
              {c.documentos.length > 0 && <p style={{ fontSize: 11.5, color: "var(--tinta-fraca)" }}>Documentos: {c.documentos.join(", ")}</p>}
              {c.respostaOperador && <p style={{ fontSize: 11.5, color: "var(--tinta-suave)" }}>Resposta do porto: {c.respostaOperador}</p>}
            </div>
          ))}
        </div>
      </div>
    </>
  );
}

function AbaMinhaFrota({ transportadora }) {
  const veiculos = listarVeiculos(transportadora.nome);
  const condutores = listarCondutores(transportadora.nome);
  return (
    <>
      <div className="cartao" style={{ marginBottom: 20 }}>
        <div className="cartao__cabecalho">
          <h3>Meus veículos</h3>
          <p>Placas cadastradas no BarcaLog vinculadas à {transportadora.nome}</p>
        </div>
        <div className="cartao__corpo" style={{ overflowX: "auto" }}>
          {veiculos.length === 0
            ? <EstadoVazio icone="local_shipping" titulo="Nenhum veículo cadastrado" texto="Cadastros feitos pela equipe do porto em Frotas & Condutores aparecem aqui." />
            : (
              <table>
                <thead><tr><th>Placa</th><th>Modelo</th><th>Status na portaria</th></tr></thead>
                <tbody>
                  {veiculos.map(v => (
                    <tr key={v.id}>
                      <td className="mono">{v.placa}</td>
                      <td>{v.modelo}</td>
                      <td>{v.statusPortaria}</td>
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
            ? <EstadoVazio icone="badge" titulo="Nenhum condutor cadastrado" texto="Cadastros feitos pela equipe do porto em Frotas & Condutores aparecem aqui." />
            : (
              <table>
                <thead><tr><th>Nome</th><th>CPF</th><th>Placa vinculada</th></tr></thead>
                <tbody>
                  {condutores.map(c => (
                    <tr key={c.id}>
                      <td>{c.nome}</td>
                      <td className="mono">{c.cpf}</td>
                      <td className="mono">{c.placaVinculada || "—"}</td>
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

function Dashboard({ transportadora: transportadoraLogada, sair }) {
  useNegativacao();
  const [aba, setAba] = useState("fluxo");
  // Recalcula a cada re-render (via useNegativacao) pra refletir mudanças de
  // status feitas pela equipe interna enquanto a sessão está aberta.
  const transportadora = obterTransportadora(transportadoraLogada.nome) || transportadoraLogada;
  const ocorrencias = listarOcorrencias(transportadora.nome);
  const contestacoes = listarContestacoes(transportadora.nome);
  const apta = estaAptaParaOperar(transportadora.nome);

  return (
    <div style={{ minHeight: "100vh", background: "var(--fundo)" }}>
      <PortalNavbar transportadora={transportadora} apta={apta} sair={sair} />
      <div style={{ maxWidth: 960, margin: "0 auto", padding: "28px 20px 60px" }}>
        <div style={{ marginBottom: 18 }}>
          <p style={{ color: "var(--tinta-suave)", fontSize: 13, margin: 0 }}>CNPJ {transportadora.cnpj}</p>
        </div>

        <div className="abas" style={{ marginBottom: 20, display: "inline-flex" }}>
          {ABAS_PORTAL.map(a => (
            <button key={a.chave} onClick={() => setAba(a.chave)} className={`aba-botao ${aba === a.chave ? "ativo" : ""}`}>
              {a.rotulo}
            </button>
          ))}
        </div>

        <div className="conteudo-pagina" key={aba}>
          {aba === "fluxo" && <FluxoNegativacao transportadora={transportadora} ocorrencias={ocorrencias} />}
          {aba === "chamados" && <AbaOcorrenciasContestacoes transportadora={transportadora} ocorrencias={ocorrencias} contestacoes={contestacoes} />}
          {aba === "frota" && <AbaMinhaFrota transportadora={transportadora} />}
        </div>
      </div>
    </div>
  );
}

export default function PortalTransportadora({ aoVoltarParaInterno }) {
  const [logada, setLogada] = useState(null);
  useNegativacao();

  if (!logada) return <TelaLogin aoEntrar={setLogada} aoVoltarParaInterno={aoVoltarParaInterno} />;
  return <Dashboard transportadora={logada} sair={() => { definirSessao(null); setLogada(null); }} />;
}
