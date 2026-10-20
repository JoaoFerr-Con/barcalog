import { useState, useEffect, useMemo, lazy, Suspense } from "react";
import Tour, { tourJaVisto } from "./components/Tour.jsx";
import Negativacao from "./pages/Negativacao.jsx";
import GestaoFrotas from "./pages/GestaoFrotas.jsx";
import Portaria from "./pages/Portaria.jsx";
import { FormularioLogin, TelaRestricao } from "./components/Acesso.jsx";
import { Carregando, ErroCarga, Vazio } from "./components/EstadoCarga.jsx";
import { CabecalhoPagina, Icone, Selo } from "./components/ui.jsx";
import { useSessao, sair, iniciais } from "./auth/autenticacao.js";
import { useNegativacao } from "./hooks/useNegativacao.js";
import { useApi } from "./hooks/useApi.js";
import { api } from "./api/cliente.js";
import { listarVeiculos } from "./data/negativacaoStore.js";
import { STATUS_PORTARIA } from "./api/rotulos.js";
const VisaoGeral = lazy(() => import("./pages/VisaoGeral.jsx"));
const Auditoria = lazy(() => import("./pages/Auditoria.jsx"));
const Gargalos = lazy(() => import("./pages/Gargalos.jsx"));
const Agendamentos = lazy(() => import("./pages/Agendamentos.jsx"));
const Configuracoes = lazy(() => import("./pages/Configuracoes.jsx"));
const Movimentacoes = lazy(() => import("./pages/Movimentacoes.jsx"));
const Importacao = lazy(() => import("./pages/Importacao.jsx"));

// Painel interno (equipe do porto). A autenticação é feita na API
// (/api/v1/auth/login, JWT); aqui só guardamos o token e mostramos as telas.
// O menu esconde o que o papel não pode usar, mas quem garante isso é o
// backend (políticas Leitura/Escrita/Gestao em cada endpoint).

const PAPEIS_INTERNOS = ["Operador", "Gestor", "Auditor"];

/* =========================================================
   NAVEGAÇÃO — agrupada por contexto operacional
   ========================================================= */
const GRUPOS_NAV = [
  { nome: "Operação", itens: [
    { chave: "visao-geral", rotulo: "Visão Geral", icone: "dashboard", cabecalhoProprio: true,
      descricao: "Volume, esperas e alertas do período, comparação entre terminais e situação de hoje." },
    { chave: "portaria", rotulo: "Portaria", icone: "door_front", titulo: "Portaria", sub: "Busca por placa, situação da carreta e fila no pátio (status informado pela portaria)",
      descricao: "Busca um veículo pela placa e registra sua situação no pátio/porto." },
    { chave: "agendamentos", rotulo: "Agendamentos", icone: "calendar_month", titulo: "Agendamentos", sub: "Agenda do dia por hora × capacidade cadastrada; status registrado pela operação",
      descricao: "Agenda do dia por hora × capacidade cadastrada, com status e QR de cada agendamento." },
    { chave: "gargalos", rotulo: "Gargalos e alertas", icone: "warning", cabecalhoProprio: true, alertas: true,
      descricao: "Alertas com origem, evidência, limite e histórico de tratamento; previsão das próximas horas." }
  ]},
  { nome: "Dados", itens: [
    { chave: "movimentacoes", rotulo: "Movimentações", icone: "swap_horiz", cabecalhoProprio: true,
      descricao: "Cada marcação e liberação, com filtros, sinais de qualidade e origem do registro." },
    { chave: "importacao", rotulo: "Importação", icone: "upload_file", cabecalhoProprio: true,
      descricao: "Planilhas dos terminais: análise, conflitos, confirmação e histórico." }
  ]},
  { nome: "Controle", itens: [
    { chave: "negativacao", rotulo: "Negativação", icone: "gpp_maybe", titulo: "Negativação", sub: "Ocorrências N1/N2/N3, bloqueio automático N3 e contestações",
      descricao: "Registra ocorrências (N1/N2/N3), aplica bloqueio automático em infrações graves e analisa contestações." },
    { chave: "carretas", rotulo: "Frota", icone: "local_shipping", titulo: "Frota", sub: "Carretas cadastradas e situação na portaria",
      descricao: "Lista a frota cadastrada, com busca e o status de portaria de cada carreta." },
    { chave: "frotas", rotulo: "Cadastros", icone: "badge", titulo: "Cadastros", sub: "Placas, transportadoras e condutores",
      descricao: "Cadastro de placas, transportadoras e condutores usados pela Negativação e pela Portaria." },
    { chave: "auditoria", rotulo: "Auditoria", icone: "history", titulo: "Auditoria", sub: "Quem fez o quê e quando — registrado automaticamente pelo servidor",
      descricao: "Log de quem fez o quê e quando — toda gravação fica registrada automaticamente no servidor." }
  ]},
  { nome: "Administração", itens: [
    { chave: "configuracoes", rotulo: "Configurações", icone: "tune", titulo: "Configurações", sub: "Capacidade dos terminais, limites dos indicadores e alertas, revisão de convênios",
      descricao: "Capacidade dos terminais, limites dos alertas e cadastro de convênios." }
  ]}
];
const ITENS_NAV = GRUPOS_NAV.flatMap(g => g.itens);
const CHAVES = new Set(ITENS_NAV.map(i => i.chave));

const PASSOS_TOUR = ITENS_NAV.map(item => ({ alvo: item.chave, titulo: item.rotulo, texto: item.descricao })).concat([
  { alvo: "recolher", titulo: "Recolher o menu", texto: "Recolhe a barra lateral para ganhar espaço — ficam só os ícones." },
  { alvo: "portal", titulo: "Portal do Transportador", texto: "Abre a área externa (em /portal) onde a transportadora, com login próprio, consulta status, motivos de negativação e abre contestações." }
]);

/** Página atual pelo endereço (#/movimentacoes): atualizar a página não perde a tela. */
function paginaDoEndereco() {
  const chave = window.location.hash.replace(/^#\/?/, "").split("?")[0];
  return CHAVES.has(chave) ? chave : "visao-geral";
}

function EstruturaBase({ usuario, pagina, definirPagina, aoAbrirPortal, children }) {
  const [recolhida, setRecolhida] = useState(false);
  const [menuAberto, setMenuAberto] = useState(false);
  const [tourAtivo, setTourAtivo] = useState(false);
  // Contagem real de alertas abertos (reconsultada a cada minuto).
  const { dados: resumoAlertas } = useApi(sinal => api.get("/api/v1/alertas/resumo", null, sinal), [pagina], { intervaloMs: 60000 });
  useEffect(() => {
    if (usuario && !tourJaVisto(usuario.email)) {
      const t = setTimeout(() => setTourAtivo(true), 500);
      return () => clearTimeout(t);
    }
  }, [usuario?.email]);
  const item = ITENS_NAV.find(i => i.chave === pagina);

  function ir(chave) {
    definirPagina(chave);
    setMenuAberto(false);
  }

  return (
    <>
      {tourAtivo && <Tour passos={PASSOS_TOUR} chaveUsuario={usuario?.email} aoTerminar={() => setTourAtivo(false)} />}
      <div className={`app-shell ${recolhida ? "recolhida" : ""} ${menuAberto ? "menu-aberto" : ""}`}>
        <aside className="barra-lateral" onClick={e => { if (e.target === e.currentTarget) setMenuAberto(false); }}>
          <div className="barra-lateral__marca">
            <div className="icone-ancora"><Icone nome="anchor" /></div>
            {!recolhida && (
              <div>
                <h1>BarcaLog</h1>
                <span className="sub">Porto de Barcarena</span>
              </div>
            )}
            <button type="button" data-tour="recolher" className="botao-recolher" onClick={() => setRecolhida(r => !r)}
              title={recolhida ? "Expandir menu" : "Recolher menu"} aria-label={recolhida ? "Expandir menu" : "Recolher menu"}>
              <Icone nome={recolhida ? "chevron_right" : "chevron_left"} tamanho={18} />
            </button>
          </div>
          <nav className="barra-lateral__nav" aria-label="Menu principal">
            {GRUPOS_NAV.map(g => (
              <div key={g.nome} role="group" aria-label={g.nome}>
                <div className="nav-grupo" aria-hidden={recolhida}>{g.nome}</div>
                {g.itens.map(it => {
                  const n = it.alertas ? resumoAlertas?.ativos : 0;
                  return (
                    <a key={it.chave} href={`#/${it.chave}`} data-tour={it.chave}
                      onClick={e => { e.preventDefault(); ir(it.chave); }}
                      className={`nav-item ${pagina === it.chave ? "ativo" : ""}`} title={recolhida ? it.rotulo : it.descricao}
                      aria-current={pagina === it.chave ? "page" : undefined}>
                      <Icone nome={it.icone} />
                      {!recolhida && <span className="nav-item__texto">{it.rotulo}</span>}
                      {n > 0 && <span className="badge-alerta num" aria-label={`${n} alertas abertos`}>{n}</span>}
                    </a>
                  );
                })}
              </div>
            ))}
          </nav>
          <div className="barra-lateral__rodape">
            <button type="button" data-tour="portal" className="botao-portal" onClick={aoAbrirPortal} title="Portal do Transportador">
              <Icone nome="open_in_new" tamanho={18} />
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
          <button type="button" className="botao-menu-mobile" aria-expanded={menuAberto} onClick={() => setMenuAberto(true)}>
            <Icone nome="menu" /> BarcaLog · {item?.rotulo}
          </button>
          <main className="area-principal" id="conteudo">
            <div className="pagina" key={pagina}>
              {!item?.cabecalhoProprio && <CabecalhoPagina titulo={item?.titulo || item?.rotulo} subtitulo={item?.sub} />}
              {children}
            </div>
          </main>
        </div>
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
const TAMANHO_PAGINA = 10;
const ESTILO_STATUS = {
  "No Porto": { icone: "anchor", tipo: "operacao" },
  "No Pátio": { icone: "warehouse", tipo: "atencao" },
  "Aguardando": { icone: "schedule", tipo: "info" },
  "Descarga Finalizada": { icone: "task_alt", tipo: "ok" }
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
          <div className="tabela-wrap">
            <table>
              <thead><tr><th scope="col">Placa</th><th scope="col">Transportadora</th><th scope="col">Terminal</th><th scope="col">Status atual</th></tr></thead>
              <tbody>
                {visiveis.map(c => {
                  const estilo = ESTILO_STATUS[c.statusPortaria] || { icone: "help", tipo: "neutro" };
                  return (
                    <tr key={c.id}>
                      <td><span className="placa-chip">{c.placa}</span></td>
                      <td>{c.transportadora}</td>
                      <td>{c.terminalNome || "—"}</td>
                      <td>
                        <span className="linha-flex">
                          <Selo tipo={estilo.tipo} icone={estilo.icone}>{c.statusPortaria}</Selo>
                          {c.statusNegativacao === "negativado" && <Selo tipo="critico" icone="block">Negativada</Selo>}
                        </span>
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
            {filtradas.length === 0 ? 0 : inicio + 1}–{Math.min(inicio + TAMANHO_PAGINA, filtradas.length)} de {filtradas.length} carretas · status informado manualmente na Portaria
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
  const [pagina, setPagina] = useState(paginaDoEndereco);
  useEffect(() => {
    const aoMudar = () => setPagina(paginaDoEndereco());
    window.addEventListener("hashchange", aoMudar);
    return () => window.removeEventListener("hashchange", aoMudar);
  }, []);
  function irPara(chave) {
    if (window.location.hash !== `#/${chave}`) window.location.hash = `/${chave}`;
    setPagina(chave);
    window.scrollTo(0, 0);
  }

  if (!sessao) return <TelaLogin aviso={aviso} aoAbrirPortal={aoAbrirPortal} />;
  if (sessao.restricao) return <TelaRestricao sessao={sessao} />;
  if (!PAPEIS_INTERNOS.includes(sessao.usuario?.papel)) return <AcessoNegado aoAbrirPortal={aoAbrirPortal} />;

  const carregando = <Carregando />;
  const papel = sessao.usuario.papel;
  const paginas = {
    "visao-geral": <VisaoGeral irPara={irPara} />,
    "carretas": <PaginaCarretas />,
    "frotas": <GestaoFrotas />,
    "portaria": <Portaria />,
    "negativacao": <Negativacao />,
    "auditoria": <Auditoria />,
    "gargalos": <Gargalos papel={papel} />,
    "agendamentos": <Agendamentos papel={papel} />,
    "movimentacoes": <Movimentacoes />,
    "importacao": <Importacao papel={papel} irPara={irPara} />,
    "configuracoes": <Configuracoes papel={papel} />
  };
  return (
    <EstruturaBase usuario={sessao.usuario} pagina={pagina} definirPagina={irPara} aoAbrirPortal={aoAbrirPortal}>
      <Suspense fallback={carregando}>{paginas[pagina]}</Suspense>
    </EstruturaBase>
  );
}
