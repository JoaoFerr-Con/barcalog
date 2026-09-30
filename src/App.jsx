import { useState, useEffect, useMemo, lazy, Suspense } from "react";
import Tour, { tourJaVisto } from "./components/Tour.jsx";
import Negativacao from "./pages/Negativacao.jsx";
import GestaoFrotas from "./pages/GestaoFrotas.jsx";
import Portaria from "./pages/Portaria.jsx";
import { FormularioLogin, TelaRestricao } from "./components/Acesso.jsx";
import { Carregando, ErroCarga, Vazio } from "./components/EstadoCarga.jsx";
import { useSessao, sair, iniciais } from "./auth/autenticacao.js";
import { useNegativacao } from "./hooks/useNegativacao.js";
import { listarVeiculos } from "./data/negativacaoStore.js";
import { STATUS_PORTARIA } from "./api/rotulos.js";
const VisaoGeral = lazy(() => import("./pages/VisaoGeral.jsx"));
const Auditoria = lazy(() => import("./pages/Auditoria.jsx"));
const PrevisaoGargalos = lazy(() => import("./pages/PrevisaoGargalos.jsx"));
const Agendamentos = lazy(() => import("./pages/Agendamentos.jsx"));
const Configuracoes = lazy(() => import("./pages/Configuracoes.jsx"));

// Painel interno (equipe do porto). A autenticação é feita na API
// (/api/v1/auth/login, JWT); aqui só guardamos o token e mostramos as telas.
// O menu esconde o que o papel não pode usar, mas quem garante isso é o
// backend (políticas Leitura/Escrita/Gestao em cada endpoint).

const PAPEIS_INTERNOS = ["Operador", "Gestor", "Auditor"];

/* =========================================================
   NAVEGAÇÃO
   ========================================================= */
const ITENS_NAV = [{
  chave: "visao-geral",
  rotulo: "Visão Geral",
  icone: "dashboard",
  descricao: "Central de operações: KPIs, visão por terminal, alertas e os gráficos reais de Unitapajós, TGPM e Hidrovias."
}, {
  chave: "agendamentos",
  rotulo: "Agendamentos",
  icone: "calendar_month",
  descricao: "Criação e acompanhamento das janelas de agendamento por terminal, com QR code de acesso."
}, {
  chave: "portaria",
  rotulo: "Portaria",
  icone: "door_front",
  descricao: "Busca um veículo pela placa e registra sua movimentação no pátio/porto."
}, {
  chave: "gargalos",
  rotulo: "Previsão de Gargalos",
  icone: "warning",
  descricao: "Índice de risco por horário, alertas operacionais e a análise preditiva de volume — baseado em padrão histórico real."
}, {
  chave: "negativacao",
  rotulo: "Negativação",
  icone: "gpp_maybe",
  descricao: "Núcleo do BarcaLog: registra ocorrências (N1/N2/N3), aplica bloqueio automático em infrações graves e analisa contestações no GED."
}, {
  chave: "carretas",
  rotulo: "Carretas",
  icone: "local_shipping",
  descricao: "Lista a frota cadastrada, com busca e o status de portaria de cada carreta."
}, {
  chave: "frotas",
  rotulo: "Cadastros",
  icone: "badge",
  descricao: "Cadastro de placas, transportadoras e condutores — a base usada pelo Sistema de Negativação e pela Portaria."
}, {
  chave: "auditoria",
  rotulo: "Auditoria",
  icone: "history",
  descricao: "Log de quem fez o quê e quando — toda gravação fica registrada automaticamente no servidor."
}, {
  chave: "configuracoes",
  rotulo: "Configurações",
  icone: "settings",
  descricao: "Parâmetros operacionais (capacidade, limites de congestionamento) — módulo em desenvolvimento."
}];

const TITULOS_PAGINA = {
  "visao-geral": { titulo: "Visão Geral", sub: "Central de Operações — Porto de Barcarena" },
  "carretas": { titulo: "Gestão de Carretas", sub: "Frota cadastrada e status de portaria" },
  "frotas": { titulo: "Cadastros", sub: "Placas, transportadoras e condutores monitorados pelo Sistema de Negativação" },
  "portaria": { titulo: "Controle de Portaria", sub: "Operação rápida — Pátio principal" },
  "negativacao": { titulo: "Sistema de Negativação", sub: "Ocorrências, bloqueios automáticos N3 e contestações (GED)" },
  "auditoria": { titulo: "Auditoria", sub: "Log de alterações — quem fez o quê e quando" },
  "gargalos": { titulo: "Previsão de Gargalos", sub: "Índice de risco por horário e análise preditiva — baseado em padrão histórico real" },
  "agendamentos": { titulo: "Agendamentos", sub: "Janelas de agendamento por terminal" },
  "configuracoes": { titulo: "Configurações", sub: "Parâmetros operacionais — módulo em desenvolvimento" }
};

const PASSOS_TOUR = ITENS_NAV.map(item => ({
  alvo: item.chave,
  titulo: item.rotulo,
  texto: item.descricao
})).concat([
  { alvo: "recolher", titulo: "Recolher o menu", texto: "Clique aqui pra recolher a barra lateral e ganhar espaço de tela — só os ícones ficam visíveis." },
  { alvo: "portal", titulo: "Portal do Transportador", texto: "Abre a área externa (em /portal) onde a própria transportadora, com login próprio, consulta status, motivos de negativação e abre contestações." }
]);

function relogioFormatado(data) {
  return {
    hora: data.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit", second: "2-digit" }),
    dia: data.toLocaleDateString("pt-BR", { weekday: "long", day: "2-digit", month: "long" })
  };
}

function EstruturaBase({ usuario, pagina, definirPagina, aoAbrirPortal, children }) {
  const [agora, setAgora] = useState(new Date());
  const [recolhida, setRecolhida] = useState(false);
  const [tourAtivo, setTourAtivo] = useState(false);
  useEffect(() => {
    const t = setInterval(() => setAgora(new Date()), 1000);
    return () => clearInterval(t);
  }, []);
  useEffect(() => {
    if (usuario && !tourJaVisto(usuario.email)) {
      const t = setTimeout(() => setTourAtivo(true), 500);
      return () => clearTimeout(t);
    }
  }, [usuario?.email]);
  const { hora, dia } = relogioFormatado(agora);
  const infoPagina = TITULOS_PAGINA[pagina];

  return (
    <>
      {tourAtivo && <Tour passos={PASSOS_TOUR} chaveUsuario={usuario?.email} aoTerminar={() => setTourAtivo(false)} />}
      <div className={`app-shell ${recolhida ? "recolhida" : ""}`}>
        <aside className="barra-lateral">
          <div className="barra-lateral__marca">
            <div className="icone-ancora"><span className="material-symbols-outlined" aria-hidden="true">anchor</span></div>
            {!recolhida && (
              <div>
                <h1>BarcaLog</h1>
                <span className="sub">Porto de Barcarena</span>
              </div>
            )}
            <button type="button" data-tour="recolher" className="botao-recolher" onClick={() => setRecolhida(r => !r)}
              title={recolhida ? "Expandir menu" : "Recolher menu"} aria-label={recolhida ? "Expandir menu" : "Recolher menu"}>
              <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 18 }}>{recolhida ? "chevron_right" : "chevron_left"}</span>
            </button>
          </div>
          <nav className="barra-lateral__nav" aria-label="Menu principal">
            {ITENS_NAV.map(item => (
              <button type="button" key={item.chave} data-tour={item.chave} onClick={() => definirPagina(item.chave)}
                className={`nav-item ${pagina === item.chave ? "ativo" : ""}`} title={item.descricao}
                aria-current={pagina === item.chave ? "page" : undefined}>
                <span className="material-symbols-outlined" aria-hidden="true">{item.icone}</span>
                {!recolhida && <span>{item.rotulo}</span>}
              </button>
            ))}
          </nav>
          <div className="barra-lateral__rodape">
            <button type="button" data-tour="portal" className="botao botao--fantasma"
              style={{ width: "100%", justifyContent: recolhida ? "center" : "flex-start", gap: 8, marginBottom: 12, borderStyle: "dashed" }}
              onClick={aoAbrirPortal} title="Portal do Transportador">
              <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 18 }}>open_in_new</span>
              {!recolhida && "Portal do Transportador"}
            </button>
            <button type="button" className="cartao-usuario" onClick={sair} title={`Sair (${usuario.email})`}>
              <div className="cartao-usuario__avatar" aria-hidden="true">{iniciais(usuario.nome)}</div>
              {!recolhida && (
                <div className="cartao-usuario__texto">
                  <p>{usuario.nome}</p>
                  <span>{usuario.papel} · Sair</span>
                </div>
              )}
            </button>
          </div>
        </aside>
        <div className="conteudo">
          <header className="topo">
            <div className="topo__titulo">
              <h2>{infoPagina.titulo}</h2>
              <p>{infoPagina.sub}</p>
            </div>
            <div className="topo__direita">
              <span className="pill-turno"><span className="ponto" />Turno em andamento</span>
              <div className="relogio">
                <div className="hora mono">{hora}</div>
                <div className="data">{dia}</div>
              </div>
            </div>
          </header>
          <main className="area-principal">
            <div className="pagina conteudo-pagina" key={pagina}>{children}</div>
          </main>
        </div>
        <nav className="nav-mobile" aria-label="Menu">
          {ITENS_NAV.map(item => (
            <button type="button" key={item.chave} onClick={() => definirPagina(item.chave)} className={pagina === item.chave ? "ativo" : ""}
              aria-current={pagina === item.chave ? "page" : undefined}>
              <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: "20px" }}>{item.icone}</span>
              {item.rotulo.split(" ")[0]}
            </button>
          ))}
        </nav>
      </div>
    </>
  );
}

/* =========================================================
   TELA — Login
   ========================================================= */
function TelaLogin({ aviso, aoAbrirPortal }) {
  return (
    <div className="tela-login">
      <div className="tela-login__imagem">
        <img src="/porto.webp" alt="Complexo portuário de Barcarena" />
        <span className="tela-login__selo"><span className="material-symbols-outlined" aria-hidden="true">local_shipping</span> Operação em tempo real</span>
        <div>
          <div className="tela-login__texto">
            <h2>Precisão Logística</h2>
            <p>Plataforma de gestão logística do Porto de Barcarena. Controle de carretas, janelas de descarga e checkpoints em um só lugar.</p>
          </div>
          <div className="tela-login__rodape">
            <div className="tela-login__metrica"><b>3</b><span>Terminais integrados</span></div>
            <div className="tela-login__metrica"><b>24/7</b><span>Monitoramento</span></div>
          </div>
        </div>
      </div>
      <div className="tela-login__form">
        <div className="cartao-login">
          <div className="cartao-login__marca">
            <div className="icone-ancora"><span className="material-symbols-outlined" aria-hidden="true">anchor</span></div>
            <h2>BarcaLog</h2>
          </div>
          <p className="cartao-login__sub">Autenticação de operadores — Porto de Barcarena</p>
          <FormularioLogin placeholderEmail="nome@barcalog.com.br" aviso={aviso} />
          <p style={{ textAlign: "center", fontSize: 12, marginTop: 14 }}>
            É transportadora? <a href="/portal" onClick={e => { e.preventDefault(); aoAbrirPortal(); }} style={{ color: "var(--navio-600)", fontWeight: 600 }}>Acesse o Portal do Transportador</a>
          </p>
        </div>
      </div>
    </div>
  );
}

/** Usuário do Portal tentou entrar no painel interno: a API recusaria tudo mesmo. */
function AcessoNegado({ aoAbrirPortal }) {
  return (
    <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", padding: 16 }}>
      <div className="cartao" style={{ maxWidth: 420 }}>
        <div className="cartao__corpo" style={{ textAlign: "center", padding: 28 }}>
          <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 36, color: "var(--vermelho-500)" }}>block</span>
          <h3 style={{ margin: "8px 0" }}>Acesso restrito à equipe do porto</h3>
          <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Sua conta é de transportadora. Use o Portal do Transportador.</p>
          <div style={{ display: "flex", gap: 8, justifyContent: "center", marginTop: 12 }}>
            <button type="button" className="botao botao--primario" onClick={aoAbrirPortal}>Ir para o Portal</button>
            <button type="button" className="botao botao--fantasma" onClick={sair}>Sair</button>
          </div>
        </div>
      </div>
    </div>
  );
}

/* =========================================================
   PÁGINA — Gestão de Carretas (frota cadastrada, via API)
   ========================================================= */
const TAMANHO_PAGINA = 8;
const ESTILO_STATUS = {
  "No Porto": { icone: "anchor", cor: "#1E9E6B" },
  "No Pátio": { icone: "warehouse", cor: "#F2A93B" },
  "Aguardando": { icone: "schedule", cor: "#3568D4" },
  "Descarga Finalizada": { icone: "task_alt", cor: "#9AA6BC" }
};

function PaginaCarretas() {
  const { carregado, erro, recarregar } = useNegativacao();
  const carretas = listarVeiculos();
  const [busca, setBusca] = useState("");
  const [filtroStatus, setFiltroStatus] = useState("Todos");
  const [pagina, setPagina] = useState(1);
  const filtradas = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    return carretas.filter(c => {
      const combinaBusca = !termo || c.placa.toLowerCase().includes(termo) || (c.transportadora || "").toLowerCase().includes(termo) || (c.terminalNome || "").toLowerCase().includes(termo);
      const combinaStatus = filtroStatus === "Todos" || c.statusPortaria === filtroStatus;
      return combinaBusca && combinaStatus;
    });
  }, [carretas, busca, filtroStatus]);

  if (erro && !carregado) return <ErroCarga erro={erro} aoTentarDeNovo={recarregar} />;
  if (!carregado) return <Carregando texto="Carregando frota…" />;

  const totalPaginas = Math.max(1, Math.ceil(filtradas.length / TAMANHO_PAGINA));
  const paginaAtual = Math.min(pagina, totalPaginas);
  const inicio = (paginaAtual - 1) * TAMANHO_PAGINA;
  const visiveis = filtradas.slice(inicio, inicio + TAMANHO_PAGINA);

  return (
    <div className="cartao">
      <div className="cartao__corpo" style={{ paddingTop: 20, display: "flex", flexDirection: "column", gap: 16 }}>
        <div className="barra-filtros">
          <div className="busca">
            <span className="material-symbols-outlined" aria-hidden="true">search</span>
            <input placeholder="Buscar por placa, transportadora ou terminal…" aria-label="Buscar carretas" value={busca} maxLength={60}
              onChange={e => { setBusca(e.target.value); setPagina(1); }} />
          </div>
          <div className="chips">
            {["Todos", ...Object.values(STATUS_PORTARIA)].map(s => (
              <button type="button" key={s} className={`chip ${filtroStatus === s ? "ativo" : ""}`} aria-pressed={filtroStatus === s}
                onClick={() => { setFiltroStatus(s); setPagina(1); }}>{s}</button>
            ))}
          </div>
        </div>
        {filtradas.length === 0 ? <Vazio icone="local_shipping" titulo="Nenhuma carreta encontrada" /> : (
          <div style={{ overflowX: "auto" }}>
            <table>
              <thead><tr><th scope="col">Placa</th><th scope="col">Transportadora</th><th scope="col">Terminal</th><th scope="col">Status atual</th></tr></thead>
              <tbody>
                {visiveis.map(c => {
                  const estilo = ESTILO_STATUS[c.statusPortaria] || { icone: "help", cor: "#9AA6BC" };
                  return (
                    <tr key={c.id}>
                      <td><span className="placa-chip">{c.placa}</span></td>
                      <td>{c.transportadora}</td>
                      <td>{c.terminalNome || "—"}</td>
                      <td>
                        <span className="selo" style={{ background: `${estilo.cor}22`, color: estilo.cor }}>
                          <span className="material-symbols-outlined" aria-hidden="true">{estilo.icone}</span>{c.statusPortaria}
                        </span>
                        {c.statusNegativacao === "negativado" && <span className="selo" style={{ marginLeft: 6, background: "var(--vermelho-100)", color: "var(--vermelho-500)" }}>Negativada</span>}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
        <div className="paginacao">
          <span style={{ fontSize: 12.5, color: "var(--tinta-suave)" }}>
            Mostrando {filtradas.length === 0 ? 0 : inicio + 1}–{Math.min(inicio + TAMANHO_PAGINA, filtradas.length)} de {filtradas.length}
          </span>
          <div className="paginacao__paginas">
            <button type="button" className="pagina-btn" aria-label="Página anterior" disabled={paginaAtual === 1} onClick={() => setPagina(paginaAtual - 1)}>‹</button>
            <span style={{ fontSize: 12.5, padding: "0 8px" }}>{paginaAtual} / {totalPaginas}</span>
            <button type="button" className="pagina-btn" aria-label="Próxima página" disabled={paginaAtual === totalPaginas} onClick={() => setPagina(paginaAtual + 1)}>›</button>
          </div>
        </div>
      </div>
    </div>
  );
}

/* =========================================================
   RAIZ DO APLICATIVO
   ========================================================= */
export default function Aplicativo({ aoAbrirPortal }) {
  const { sessao, aviso } = useSessao();
  const [pagina, setPagina] = useState("visao-geral");

  if (!sessao) return <TelaLogin aviso={aviso} aoAbrirPortal={aoAbrirPortal} />;
  if (sessao.restricao) return <TelaRestricao sessao={sessao} />;
  if (!PAPEIS_INTERNOS.includes(sessao.usuario?.papel)) return <AcessoNegado aoAbrirPortal={aoAbrirPortal} />;

  const carregando = <Carregando />;
  const paginas = {
    "visao-geral": <VisaoGeral />,
    "carretas": <PaginaCarretas />,
    "frotas": <GestaoFrotas />,
    "portaria": <Portaria />,
    "negativacao": <Negativacao />,
    "auditoria": <Auditoria />,
    "gargalos": <PrevisaoGargalos />,
    "agendamentos": <Agendamentos />,
    "configuracoes": <Configuracoes />
  };
  return (
    <EstruturaBase usuario={sessao.usuario} pagina={pagina} definirPagina={setPagina} aoAbrirPortal={aoAbrirPortal}>
      <Suspense fallback={carregando}>{paginas[pagina]}</Suspense>
    </EstruturaBase>
  );
}
