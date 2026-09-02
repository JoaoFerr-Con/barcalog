import React, {
  useState,
  useEffect,
  useMemo,
  createContext,
  useContext
} from "react";
import {
  TRANSPORTADORAS,
  CARGAS,
  JANELAS,
  LISTA_STATUS,
  TERMINAIS,
  gerarDadosIniciais
} from "./data/mock.js";
import { lazy, Suspense } from "react";
import CartaoIndicador from "./components/CartaoIndicador.jsx";
import Negativacao from "./pages/Negativacao.jsx";
import GestaoFrotas from "./pages/GestaoFrotas.jsx";
const RelatorioReal = lazy(() => import("./pages/RelatorioReal.jsx"));
const VisaoGeral = lazy(() => import("./pages/VisaoGeral.jsx"));
const MapaOperacional = lazy(() => import("./pages/MapaOperacional.jsx"));

/* =========================================================
   DADOS SIMULADOS (mock) — usados só na fila operacional ao vivo.
   Ver src/data/mock.js. Dados reais (Unitapajós) estão em
   src/data/marcacoesReal.json + src/data/relatorio.js, servidos
   pela página "Relatório Real" (RelatorioReal.jsx).
   ========================================================= */

/* =========================================================
   CONTEXTO — Autenticação
   ========================================================= */
const ContextoAutenticacao = createContext(null);
function ProvedorAutenticacao({
  children
}) {
  const [usuario, setUsuario] = useState(() => {
    const salvo = localStorage.getItem("barcalog_usuario");
    return salvo ? JSON.parse(salvo) : null;
  });
  useEffect(() => {
    if (usuario) localStorage.setItem("barcalog_usuario", JSON.stringify(usuario));else localStorage.removeItem("barcalog_usuario");
  }, [usuario]);
  function entrar(email, senha) {
    if (!email || !senha) return {
      ok: false,
      erro: "Informe e-mail e senha."
    };
    const nome = email.split("@")[0];
    setUsuario({
      nome: nome.charAt(0).toUpperCase() + nome.slice(1),
      email,
      iniciais: nome.slice(0, 2).toUpperCase()
    });
    return {
      ok: true
    };
  }
  function sair() {
    setUsuario(null);
  }
  return /*#__PURE__*/React.createElement(ContextoAutenticacao.Provider, {
    value: {
      usuario,
      entrar,
      sair
    }
  }, children);
}
function useAutenticacao() {
  return useContext(ContextoAutenticacao);
}

/* =========================================================
   CONTEXTO — Dados operacionais
   ========================================================= */
const ContextoDados = createContext(null);
const CHAVE_ARMAZENAMENTO = "barcalog_carretas_v2";
function ProvedorDados({
  children
}) {
  const [carretas, setCarretas] = useState(() => {
    const salvo = localStorage.getItem(CHAVE_ARMAZENAMENTO);
    if (salvo) {
      try {
        return JSON.parse(salvo);
      } catch {
        return gerarDadosIniciais();
      }
    }
    return gerarDadosIniciais();
  });
  useEffect(() => {
    localStorage.setItem(CHAVE_ARMAZENAMENTO, JSON.stringify(carretas));
  }, [carretas]);
  function adicionarCarga(dados) {
    const novaCarreta = {
      id: `carreta-${Date.now()}`,
      placa: dados.placa.toUpperCase(),
      transportadora: dados.transportadora,
      carga: dados.tipoGrao,
      volume: Number(dados.volume) || 0,
      origem: dados.origem,
      terminal: dados.terminal,
      janela: dados.janela.toUpperCase(),
      status: "Aguardando",
      criadoEm: new Date().toISOString()
    };
    setCarretas(prev => [novaCarreta, ...prev]);
    return novaCarreta;
  }
  function buscarPorPlaca(placa) {
    const consulta = placa.trim().toUpperCase();
    if (!consulta) return null;
    return carretas.find(c => c.placa.toUpperCase() === consulta) || null;
  }
  function atualizarStatus(id, status) {
    setCarretas(prev => prev.map(c => c.id === id ? {
      ...c,
      status
    } : c));
  }

  /* -------- Métricas e indicadores derivados -------- */
  const stats = useMemo(() => {
    const total = carretas.length;
    const noPatio = carretas.filter(c => c.status === "No Pátio").length;
    const noPorto = carretas.filter(c => c.status === "No Porto").length;
    const aguardando = carretas.filter(c => c.status === "Aguardando").length;
    const finalizada = carretas.filter(c => c.status === "Descarga Finalizada").length;
    const volumeTotal = carretas.filter(c => c.status === "Descarga Finalizada").reduce((s, c) => s + c.volume, 0);
    const fluxoPorHorario = [6, 8, 10, 12, 14, 16, 18, 20].map(hora => ({
      hora: `${String(hora).padStart(2, "0")}:00`,
      valor: carretas.filter(c => (c.placa.charCodeAt(1) + hora) % 8 === 0).length * 5 + 20
    }));
    const porTerminal = TERMINAIS.map(nome => ({
      nome,
      quantidade: carretas.filter(c => c.terminal === nome).length
    }));
    const porJanela = JANELAS.map(nome => ({
      nome,
      quantidade: carretas.filter(c => c.janela === nome).length
    }));
    const somaPorTransportadora = {};
    carretas.forEach(c => {
      somaPorTransportadora[c.transportadora] = (somaPorTransportadora[c.transportadora] || 0) + c.volume;
    });
    const rankingTransportadoras = Object.entries(somaPorTransportadora).map(([nome, volume]) => ({
      nome,
      volume
    })).sort((a, b) => b.volume - a.volume).slice(0, 5);
    const ativas = carretas.filter(c => c.status === "No Pátio" || c.status === "No Porto");
    const tempoMedioPermanenciaHoras = ativas.length ? ativas.reduce((s, c) => s + (Date.now() - new Date(c.criadoEm).getTime()) / 3600000, 0) / ativas.length : 0;
    const recentes = [...carretas].sort((a, b) => new Date(b.criadoEm) - new Date(a.criadoEm)).slice(0, 6);
    return {
      total,
      noPatio,
      noPorto,
      aguardando,
      finalizada,
      volumeTotal,
      fluxoPorHorario,
      porTerminal,
      porJanela,
      rankingTransportadoras,
      tempoMedioPermanenciaHoras,
      recentes
    };
  }, [carretas]);
  return /*#__PURE__*/React.createElement(ContextoDados.Provider, {
    value: {
      carretas,
      adicionarCarga,
      buscarPorPlaca,
      atualizarStatus,
      stats
    }
  }, children);
}
function useDados() {
  return useContext(ContextoDados);
}

/* =========================================================
   NAVEGAÇÃO
   ========================================================= */
const ITENS_NAV = [{
  chave: "visao-geral",
  rotulo: "Visão Geral",
  icone: "dashboard"
}, {
  chave: "carretas",
  rotulo: "Carretas",
  icone: "local_shipping"
}, {
  chave: "mapa",
  rotulo: "Mapa",
  icone: "map"
}, {
  chave: "frotas",
  rotulo: "Frotas & Condutores",
  icone: "badge"
}, {
  chave: "portaria",
  rotulo: "Portaria",
  icone: "door_front"
}, {
  chave: "pesagem",
  rotulo: "Pesagem",
  icone: "scale"
}, {
  chave: "negativacao",
  rotulo: "Negativação",
  icone: "gpp_maybe"
}, {
  chave: "relatorio-real",
  rotulo: "Relatório Real",
  icone: "insights"
}];
function relogioFormatado(data) {
  const hora = data.toLocaleTimeString("pt-BR", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit"
  });
  const dia = data.toLocaleDateString("pt-BR", {
    weekday: "long",
    day: "2-digit",
    month: "long"
  });
  return {
    hora,
    dia
  };
}
const TITULOS_PAGINA = {
  "visao-geral": {
    titulo: "Visão Geral",
    sub: "Central de Operações — Porto de Barcarena"
  },
  "carretas": {
    titulo: "Gestão de Carretas",
    sub: "Consulta e acompanhamento da frota em operação"
  },
  "mapa": {
    titulo: "Mapa Operacional",
    sub: "Volume e tempo de espera reais por terminal"
  },
  "frotas": {
    titulo: "Frotas & Condutores",
    sub: "Placas e motoristas monitorados pelo Sistema de Negativação"
  },
  "portaria": {
    titulo: "Controle de Portaria",
    sub: "Operação rápida — Pátio principal"
  },
  "pesagem": {
    titulo: "Pesagem",
    sub: "Registro de peso bruto, tara e líquido"
  },
  "negativacao": {
    titulo: "Sistema de Negativação",
    sub: "Ocorrências, bloqueios automáticos N3 e contestações (GED)"
  },
  "relatorio-real": {
    titulo: "Relatório Real",
    sub: "Marcações reais importadas — Unitapajós, TGPM e Hidrovias"
  }
};
function EstruturaBase({
  pagina,
  definirPagina,
  children
}) {
  const {
    usuario,
    sair
  } = useAutenticacao();
  const [agora, setAgora] = useState(new Date());
  useEffect(() => {
    const t = setInterval(() => setAgora(new Date()), 1000);
    return () => clearInterval(t);
  }, []);
  const {
    hora,
    dia
  } = relogioFormatado(agora);
  const infoPagina = TITULOS_PAGINA[pagina];
  return /*#__PURE__*/React.createElement("div", {
    className: "app-shell"
  }, /*#__PURE__*/React.createElement("aside", {
    className: "barra-lateral"
  }, /*#__PURE__*/React.createElement("div", {
    className: "barra-lateral__marca"
  }, /*#__PURE__*/React.createElement("div", {
    className: "icone-ancora"
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined"
  }, "anchor")), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("h1", null, "BarcaLog"), /*#__PURE__*/React.createElement("span", {
    className: "sub"
  }, "Porto de Barcarena"))), /*#__PURE__*/React.createElement("nav", {
    className: "barra-lateral__nav"
  }, ITENS_NAV.map(item => /*#__PURE__*/React.createElement("button", {
    key: item.chave,
    onClick: () => definirPagina(item.chave),
    className: `nav-item ${pagina === item.chave ? "ativo" : ""}`
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined"
  }, item.icone), /*#__PURE__*/React.createElement("span", null, item.rotulo)))), /*#__PURE__*/React.createElement("div", {
    className: "barra-lateral__rodape"
  }, /*#__PURE__*/React.createElement("button", {
    className: "cartao-usuario",
    onClick: sair
  }, /*#__PURE__*/React.createElement("div", {
    className: "cartao-usuario__avatar"
  }, usuario?.iniciais || "OP"), /*#__PURE__*/React.createElement("div", {
    className: "cartao-usuario__texto"
  }, /*#__PURE__*/React.createElement("p", null, usuario?.nome || "Operador"), /*#__PURE__*/React.createElement("span", null, "Sair"))))), /*#__PURE__*/React.createElement("div", {
    className: "conteudo"
  }, /*#__PURE__*/React.createElement("header", {
    className: "topo"
  }, /*#__PURE__*/React.createElement("div", {
    className: "topo__titulo"
  }, /*#__PURE__*/React.createElement("h2", null, infoPagina.titulo), /*#__PURE__*/React.createElement("p", null, infoPagina.sub)), /*#__PURE__*/React.createElement("div", {
    className: "topo__direita"
  }, /*#__PURE__*/React.createElement("span", {
    className: "pill-turno"
  }, /*#__PURE__*/React.createElement("span", {
    className: "ponto"
  }), "Turno em andamento"), /*#__PURE__*/React.createElement("div", {
    className: "relogio"
  }, /*#__PURE__*/React.createElement("div", {
    className: "hora mono"
  }, hora), /*#__PURE__*/React.createElement("div", {
    className: "data"
  }, dia)))), /*#__PURE__*/React.createElement("main", {
    className: "area-principal"
  }, /*#__PURE__*/React.createElement("div", {
    className: "pagina"
  }, children))), /*#__PURE__*/React.createElement("nav", {
    className: "nav-mobile"
  }, ITENS_NAV.map(item => /*#__PURE__*/React.createElement("button", {
    key: item.chave,
    onClick: () => definirPagina(item.chave),
    className: pagina === item.chave ? "ativo" : ""
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined",
    style: {
      fontSize: "20px"
    }
  }, item.icone), item.rotulo.split(" ")[0]))));
}

/* =========================================================
   TELA — Login
   ========================================================= */
function TelaLogin() {
  const {
    entrar
  } = useAutenticacao();
  const [email, setEmail] = useState("");
  const [senha, setSenha] = useState("");
  const [erro, setErro] = useState("");
  function aoEnviar(e) {
    e.preventDefault();
    const resultado = entrar(email, senha);
    if (!resultado.ok) setErro(resultado.erro);
  }
  return /*#__PURE__*/React.createElement("div", {
    className: "tela-login"
  }, /*#__PURE__*/React.createElement("div", {
    className: "tela-login__imagem"
  }, /*#__PURE__*/React.createElement("img", {
    src: "https://images.unsplash.com/photo-1779583074717-e60fa13131ce?fm=jpg&q=80&w=1600&auto=format&fit=crop",
    alt: "Carreta transportando carga em rodovia"
  }), /*#__PURE__*/React.createElement("span", {
    className: "tela-login__selo"
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined"
  }, "local_shipping"), " Operação em tempo real"), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    className: "tela-login__texto"
  }, /*#__PURE__*/React.createElement("h2", null, "Precisão Logística"), /*#__PURE__*/React.createElement("p", null, "Plataforma de gestão logística do Porto de Barcarena. Controle de carretas, janelas de descarga e checkpoints em um só lugar.")), /*#__PURE__*/React.createElement("div", {
    className: "tela-login__rodape"
  }, /*#__PURE__*/React.createElement("div", {
    className: "tela-login__metrica"
  }, /*#__PURE__*/React.createElement("b", null, "48"), /*#__PURE__*/React.createElement("span", null, "Carretas ativas")), /*#__PURE__*/React.createElement("div", {
    className: "tela-login__metrica"
  }, /*#__PURE__*/React.createElement("b", null, "3"), /*#__PURE__*/React.createElement("span", null, "Terminais integrados")), /*#__PURE__*/React.createElement("div", {
    className: "tela-login__metrica"
  }, /*#__PURE__*/React.createElement("b", null, "24/7"), /*#__PURE__*/React.createElement("span", null, "Monitoramento"))))), /*#__PURE__*/React.createElement("div", {
    className: "tela-login__form"
  }, /*#__PURE__*/React.createElement("div", {
    className: "cartao-login"
  }, /*#__PURE__*/React.createElement("div", {
    className: "cartao-login__marca"
  }, /*#__PURE__*/React.createElement("div", {
    className: "icone-ancora"
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined"
  }, "anchor")), /*#__PURE__*/React.createElement("h2", null, "BarcaLog")), /*#__PURE__*/React.createElement("p", {
    className: "cartao-login__sub"
  }, "Autenticação de operadores — Porto de Barcarena"), /*#__PURE__*/React.createElement("form", {
    onSubmit: aoEnviar,
    style: {
      display: "flex",
      flexDirection: "column",
      gap: 18
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "campo"
  }, /*#__PURE__*/React.createElement("label", null, "E-mail institucional"), /*#__PURE__*/React.createElement("div", {
    className: "campo-icone"
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined"
  }, "mail"), /*#__PURE__*/React.createElement("input", {
    type: "email",
    value: email,
    onChange: e => setEmail(e.target.value),
    placeholder: "operador@barcalog.com",
    required: true
  }))), /*#__PURE__*/React.createElement("div", {
    className: "campo"
  }, /*#__PURE__*/React.createElement("label", null, "Senha"), /*#__PURE__*/React.createElement("div", {
    className: "campo-icone"
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined"
  }, "lock"), /*#__PURE__*/React.createElement("input", {
    type: "password",
    value: senha,
    onChange: e => setSenha(e.target.value),
    placeholder: "••••••••",
    required: true
  }))), erro && /*#__PURE__*/React.createElement("p", {
    style: {
      color: "var(--vermelho-500)",
      fontSize: 13
    }
  }, erro), /*#__PURE__*/React.createElement("div", {
    style: {
      display: "flex",
      justifyContent: "flex-end"
    }
  }, /*#__PURE__*/React.createElement("a", {
    href: "#",
    style: {
      fontSize: 12.5,
      color: "var(--navio-600)",
      fontWeight: 600
    }
  }, "Esqueci minha senha")), /*#__PURE__*/React.createElement("button", {
    type: "submit",
    className: "botao botao--primario",
    style: {
      background: "var(--navio-900)",
      color: "#fff",
      padding: "13px 18px"
    }
  }, "Acessar sistema"), /*#__PURE__*/React.createElement("p", {
    style: {
      textAlign: "center",
      fontSize: 12,
      color: "var(--tinta-suave)"
    }
  }, "Demonstração: use qualquer e-mail e senha para entrar.")))));
}

/* =========================================================
   COMPONENTES DE PAINEL
   ========================================================= */
const CORES_STATUS = {
  "No Porto": "#1E9E6B",
  "No Pátio": "#F2A93B",
  "Aguardando": "#3568D4",
  "Descarga Finalizada": "#9AA6BC"
};

/* =========================================================
   PÁGINA — Gestão de Carretas
   ========================================================= */
const TAMANHO_PAGINA = 8;
const ESTILO_STATUS = {
  "No Porto": {
    icone: "anchor"
  },
  "No Pátio": {
    icone: "warehouse"
  },
  "Aguardando": {
    icone: "schedule"
  },
  "Descarga Finalizada": {
    icone: "task_alt"
  }
};
function PaginaCarretas() {
  const {
    carretas
  } = useDados();
  const [busca, setBusca] = useState("");
  const [filtroJanela, setFiltroJanela] = useState("Todas");
  const [pagina, setPagina] = useState(1);
  const filtradas = useMemo(() => {
    return carretas.filter(c => {
      const combinaBusca = !busca || c.placa.toLowerCase().includes(busca.toLowerCase()) || c.terminal.toLowerCase().includes(busca.toLowerCase());
      const combinaJanela = filtroJanela === "Todas" || c.janela === filtroJanela;
      return combinaBusca && combinaJanela;
    });
  }, [carretas, busca, filtroJanela]);
  const totalPaginas = Math.max(1, Math.ceil(filtradas.length / TAMANHO_PAGINA));
  const paginaAtual = Math.min(pagina, totalPaginas);
  const inicio = (paginaAtual - 1) * TAMANHO_PAGINA;
  const visiveis = filtradas.slice(inicio, inicio + TAMANHO_PAGINA);
  return /*#__PURE__*/React.createElement("div", {
    className: "cartao"
  }, /*#__PURE__*/React.createElement("div", {
    className: "cartao__corpo",
    style: {
      paddingTop: 20,
      display: "flex",
      flexDirection: "column",
      gap: 16
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "barra-filtros"
  }, /*#__PURE__*/React.createElement("div", {
    className: "busca"
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined"
  }, "search"), /*#__PURE__*/React.createElement("input", {
    placeholder: "Buscar por placa ou porto de Barcarena...",
    value: busca,
    onChange: e => {
      setBusca(e.target.value);
      setPagina(1);
    }
  })), /*#__PURE__*/React.createElement("div", {
    className: "chips"
  }, ["Todas", ...JANELAS].map(j => /*#__PURE__*/React.createElement("button", {
    key: j,
    className: `chip ${filtroJanela === j ? "ativo" : ""}`,
    onClick: () => {
      setFiltroJanela(j);
      setPagina(1);
    }
  }, j === "Todas" ? "Todas" : `Janela ${j}`)))), /*#__PURE__*/React.createElement("table", null, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, /*#__PURE__*/React.createElement("th", null, "Placa"), /*#__PURE__*/React.createElement("th", null, "Porto de Barcarena"), /*#__PURE__*/React.createElement("th", null, "Janela"), /*#__PURE__*/React.createElement("th", null, "Status atual"))), /*#__PURE__*/React.createElement("tbody", null, visiveis.map(c => /*#__PURE__*/React.createElement("tr", {
    key: c.id
  }, /*#__PURE__*/React.createElement("td", null, /*#__PURE__*/React.createElement("span", {
    className: "placa-chip"
  }, c.placa)), /*#__PURE__*/React.createElement("td", null, c.terminal), /*#__PURE__*/React.createElement("td", {
    className: "mono"
  }, c.janela), /*#__PURE__*/React.createElement("td", null, /*#__PURE__*/React.createElement("span", {
    className: "selo",
    style: {
      background: `${CORES_STATUS[c.status]}22`,
      color: CORES_STATUS[c.status]
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined"
  }, ESTILO_STATUS[c.status].icone), c.status)))))), /*#__PURE__*/React.createElement("div", {
    className: "paginacao"
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 12.5,
      color: "var(--tinta-suave)"
    }
  }, "Mostrando ", filtradas.length === 0 ? 0 : inicio + 1, "–", Math.min(inicio + TAMANHO_PAGINA, filtradas.length), " de ", filtradas.length), /*#__PURE__*/React.createElement("div", {
    className: "paginacao__paginas"
  }, /*#__PURE__*/React.createElement("button", {
    className: "pagina-btn",
    disabled: paginaAtual === 1,
    onClick: () => setPagina(p => p - 1)
  }, "‹"), Array.from({
    length: totalPaginas
  }, (_, i) => i + 1).slice(0, 5).map(n => /*#__PURE__*/React.createElement("button", {
    key: n,
    className: `pagina-btn ${paginaAtual === n ? "ativo" : ""}`,
    onClick: () => setPagina(n)
  }, n)), /*#__PURE__*/React.createElement("button", {
    className: "pagina-btn",
    disabled: paginaAtual === totalPaginas,
    onClick: () => setPagina(p => p + 1)
  }, "›")))));
}

/* =========================================================
   PÁGINA — Portaria
   ========================================================= */
const ACOES_PORTARIA = [{
  chave: "No Pátio",
  rotulo: "Registrar entrada no pátio",
  icone: "login",
  cor: "var(--ambar-600)",
  fundo: "#FBEBD1",
  borda: "#F2D8A5"
}, {
  chave: "Aguardando",
  rotulo: "Registrar saída do pátio",
  icone: "logout",
  cor: "var(--tinta-suave)",
  fundo: "var(--superficie-alt)",
  borda: "var(--borda)"
}, {
  chave: "No Porto",
  rotulo: "Registrar entrada no porto",
  icone: "directions_boat",
  cor: "var(--azul-500)",
  fundo: "var(--azul-100)",
  borda: "#C3D6F5"
}, {
  chave: "Descarga Finalizada",
  rotulo: "Finalizar descarga",
  icone: "download_done",
  cor: "var(--verde-500)",
  fundo: "var(--verde-100)",
  borda: "#BEE7D2"
}];
function Portaria() {
  const {
    buscarPorPlaca,
    atualizarStatus
  } = useDados();
  const [consulta, setConsulta] = useState("");
  const [resultado, setResultado] = useState(null);
  const [naoEncontrado, setNaoEncontrado] = useState(false);
  const [mensagem, setMensagem] = useState("");
  function aoBuscar(e) {
    e.preventDefault();
    const encontrada = buscarPorPlaca(consulta);
    setResultado(encontrada);
    setNaoEncontrado(!encontrada);
    setMensagem("");
  }
  function aoAcionar(status) {
    if (!resultado) return;
    atualizarStatus(resultado.id, status);
    setResultado(r => ({
      ...r,
      status
    }));
    setMensagem(`Status atualizado para "${status}".`);
  }
  return /*#__PURE__*/React.createElement("div", {
    className: "cartao"
  }, /*#__PURE__*/React.createElement("div", {
    className: "cartao__corpo",
    style: {
      maxWidth: 720,
      margin: "0 auto",
      paddingTop: 24
    }
  }, /*#__PURE__*/React.createElement("form", {
    onSubmit: aoBuscar,
    style: {
      marginBottom: 24
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "campo-icone"
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined",
    style: {
      fontSize: 22
    }
  }, "search"), /*#__PURE__*/React.createElement("input", {
    value: consulta,
    onChange: e => setConsulta(e.target.value),
    placeholder: "Digite a placa do veículo",
    style: {
      width: "100%",
      padding: "16px 16px 16px 42px",
      fontSize: 18,
      textAlign: "center",
      textTransform: "uppercase",
      border: "1px solid var(--borda)",
      borderRadius: 12
    }
  }))), naoEncontrado && /*#__PURE__*/React.createElement("p", {
    style: {
      textAlign: "center",
      color: "var(--tinta-suave)",
      marginBottom: 20
    }
  }, "Nenhuma carreta encontrada com essa placa. Verifique se o cadastro foi feito em \"Cadastrar Carga\"."), resultado && /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("div", {
    style: {
      background: "var(--superficie-alt)",
      border: "1px solid var(--borda)",
      borderRadius: 12,
      padding: 16,
      display: "flex",
      justifyContent: "space-between",
      alignItems: "center",
      flexWrap: "wrap",
      gap: 12,
      marginBottom: 16
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("h3", {
    style: {
      fontSize: 17
    }
  }, resultado.transportadora), /*#__PURE__*/React.createElement("div", {
    style: {
      display: "flex",
      gap: 8,
      marginTop: 8,
      flexWrap: "wrap"
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "selo",
    style: {
      background: "#fff",
      color: "var(--tinta-suave)"
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined"
  }, "inventory_2"), " ", resultado.carga, " (", resultado.volume, "t)"), /*#__PURE__*/React.createElement("span", {
    className: "selo",
    style: {
      background: "var(--azul-100)",
      color: "var(--azul-500)"
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined"
  }, "schedule"), " Janela: ", resultado.janela), /*#__PURE__*/React.createElement("span", {
    className: "selo",
    style: {
      background: "#fff",
      color: "var(--tinta-suave)"
    }
  }, "Status atual: ", resultado.status))), /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: "right"
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "placa-chip",
    style: {
      fontSize: 16,
      padding: "6px 12px"
    }
  }, resultado.placa))), mensagem && /*#__PURE__*/React.createElement("p", {
    style: {
      color: "var(--verde-500)",
      marginBottom: 14,
      fontSize: 13.5
    }
  }, mensagem), /*#__PURE__*/React.createElement("div", {
    className: "grade-acoes"
  }, ACOES_PORTARIA.map(acao => /*#__PURE__*/React.createElement("button", {
    key: acao.chave,
    onClick: () => aoAcionar(acao.chave),
    className: "acao-portaria",
    style: {
      background: acao.fundo,
      color: acao.cor,
      borderColor: acao.borda
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined"
  }, acao.icone), /*#__PURE__*/React.createElement("span", {
    className: "rotulo"
  }, acao.rotulo)))))));
}

/* =========================================================
   PÁGINA — Pesagem
   ========================================================= */
function Pesagem() {
  const {
    buscarPorPlaca,
    carretas
  } = useDados();
  const [consulta, setConsulta] = useState("");
  const [resultado, setResultado] = useState(null);
  const [buscou, setBuscou] = useState(false);
  const [pesoBruto, setPesoBruto] = useState("");
  const [pesoTara, setPesoTara] = useState("");
  function aoBuscar(e) {
    e.preventDefault();
    setBuscou(true);
    setResultado(buscarPorPlaca(consulta));
  }
  const pesoLiquido = pesoBruto && pesoTara ? (Number(pesoBruto) - Number(pesoTara)).toFixed(2) : null;
  const semNenhumaCarreta = carretas.length === 0;
  return /*#__PURE__*/React.createElement("div", {
    className: "cartao",
    style: {
      maxWidth: 640,
      margin: "0 auto"
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "cartao__corpo",
    style: {
      paddingTop: 24,
      display: "flex",
      flexDirection: "column",
      gap: 20
    }
  }, /*#__PURE__*/React.createElement("form", {
    onSubmit: aoBuscar
  }, /*#__PURE__*/React.createElement("div", {
    className: "campo-icone"
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined",
    style: {
      fontSize: 22
    }
  }, "search"), /*#__PURE__*/React.createElement("input", {
    value: consulta,
    onChange: e => setConsulta(e.target.value),
    placeholder: "Digite a placa do veículo",
    disabled: semNenhumaCarreta,
    style: {
      width: "100%",
      padding: "14px 14px 14px 42px",
      fontSize: 17,
      textAlign: "center",
      textTransform: "uppercase",
      border: "1px solid var(--borda)",
      borderRadius: 12,
      opacity: semNenhumaCarreta ? 0.6 : 1
    }
  }))), semNenhumaCarreta && /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: "center",
      color: "var(--tinta-suave)",
      padding: "24px 12px"
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined",
    style: { fontSize: 32, color: "var(--tinta-fraca)" }
  }, "scale"), /*#__PURE__*/React.createElement("p", {
    style: { margin: "10px 0 4px", fontWeight: 600 }
  }, "Nenhum veículo cadastrado ainda"), /*#__PURE__*/React.createElement("p", {
    style: { margin: 0, fontSize: 13 }
  }, "Cadastre uma placa em \"Frotas & Condutores\" pra poder pesá-la aqui.")),
  !semNenhumaCarreta && !buscou && /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: "center",
      color: "var(--tinta-suave)",
      padding: "24px 12px"
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined",
    style: { fontSize: 32, color: "var(--tinta-fraca)" }
  }, "search"), /*#__PURE__*/React.createElement("p", {
    style: { margin: "10px 0 0", fontSize: 13 }
  }, "Digite uma placa acima e busque pra iniciar a pesagem.")),
  !semNenhumaCarreta && buscou && resultado === null && /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: "center",
      color: "var(--tinta-suave)",
      padding: "24px 12px"
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "material-symbols-outlined",
    style: { fontSize: 32, color: "var(--tinta-fraca)" }
  }, "search_off"), /*#__PURE__*/React.createElement("p", {
    style: { margin: "10px 0 4px", fontWeight: 600 }
  }, "Nenhuma carreta encontrada com essa placa"), /*#__PURE__*/React.createElement("p", {
    style: { margin: 0, fontSize: 13 }
  }, "Verifique se o veículo está cadastrado em \"Frotas & Condutores\".")), resultado && /*#__PURE__*/React.createElement("div", {
    style: {
      background: "var(--superficie-alt)",
      border: "1px solid var(--borda)",
      borderRadius: 12,
      padding: 18,
      display: "flex",
      flexDirection: "column",
      gap: 16
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: "flex",
      justifyContent: "space-between",
      alignItems: "center"
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "placa-chip",
    style: {
      fontSize: 15
    }
  }, resultado.placa), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 12.5,
      color: "var(--tinta-suave)"
    }
  }, resultado.transportadora)), /*#__PURE__*/React.createElement("div", {
    style: {
      display: "grid",
      gridTemplateColumns: "1fr 1fr",
      gap: 16
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "campo"
  }, /*#__PURE__*/React.createElement("label", null, "Peso bruto (kg)"), /*#__PURE__*/React.createElement("input", {
    type: "number",
    value: pesoBruto,
    onChange: e => setPesoBruto(e.target.value),
    placeholder: "0.00"
  })), /*#__PURE__*/React.createElement("div", {
    className: "campo"
  }, /*#__PURE__*/React.createElement("label", null, "Tara (kg)"), /*#__PURE__*/React.createElement("input", {
    type: "number",
    value: pesoTara,
    onChange: e => setPesoTara(e.target.value),
    placeholder: "0.00"
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      background: "#fff",
      borderRadius: 10,
      padding: 14,
      display: "flex",
      justifyContent: "space-between",
      alignItems: "center"
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 13.5,
      color: "var(--tinta-suave)"
    }
  }, "Peso líquido"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 22,
      fontWeight: 700,
      color: "var(--navio-800)"
    }
  }, pesoLiquido !== null ? `${pesoLiquido} kg` : "—")))));
}

/* =========================================================
   RAIZ DO APLICATIVO
   ========================================================= */
function EstruturaApp() {
  const {
    usuario
  } = useAutenticacao();
  const [pagina, setPagina] = useState("visao-geral");
  if (!usuario) return /*#__PURE__*/React.createElement(TelaLogin, null);
  const carregando = /*#__PURE__*/React.createElement("p", {
    style: { color: "var(--tinta-suave)", fontSize: 13 }
  }, "Carregando…");
  const paginas = {
    "visao-geral": /*#__PURE__*/React.createElement(Suspense, { fallback: carregando }, /*#__PURE__*/React.createElement(VisaoGeral, null)),
    "carretas": /*#__PURE__*/React.createElement(PaginaCarretas, null),
    "mapa": /*#__PURE__*/React.createElement(Suspense, { fallback: carregando }, /*#__PURE__*/React.createElement(MapaOperacional, null)),
    "frotas": /*#__PURE__*/React.createElement(GestaoFrotas, null),
    "portaria": /*#__PURE__*/React.createElement(Portaria, null),
    "pesagem": /*#__PURE__*/React.createElement(Pesagem, null),
    "negativacao": /*#__PURE__*/React.createElement(Negativacao, null),
    "relatorio-real": /*#__PURE__*/React.createElement(Suspense, { fallback: carregando }, /*#__PURE__*/React.createElement(RelatorioReal, null))
  };
  return /*#__PURE__*/React.createElement(ProvedorDados, null, /*#__PURE__*/React.createElement(EstruturaBase, {
    pagina: pagina,
    definirPagina: setPagina
  }, paginas[pagina]));
}
function Aplicativo() {
  return /*#__PURE__*/React.createElement(ProvedorAutenticacao, null, /*#__PURE__*/React.createElement(EstruturaApp, null));
}

export default Aplicativo;
