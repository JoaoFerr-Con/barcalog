// Cliente HTTP único da aplicação. TODA chamada à API passa por aqui, pra
// que autenticação, erros, sessão expirada e idempotência sejam tratados do
// mesmo jeito em todas as telas.
//
// Segurança:
// - Nenhum segredo aqui: a URL da API é pública (VITE_API_URL) e o único
//   "segredo" é o token do usuário logado.
// - O token fica em memória e em sessionStorage (sobrevive a F5 na mesma aba,
//   some ao fechar a aba). Nunca em localStorage. O risco residual é XSS —
//   mitigado pela CSP do vercel.json e por não usarmos HTML dinâmico.
// - Toda regra de permissão é validada no backend; o frontend só esconde
//   botões por conveniência.

// Em produção, sem VITE_API_URL, a API é chamada na mesma origem do site
// (ex.: rewrite /api/* na Vercel ou proxy reverso) — nunca em localhost.
const URL_API = (import.meta.env.VITE_API_URL || (import.meta.env.DEV ? "http://localhost:5080" : "")).replace(/\/$/, "");
const CHAVE_SESSAO = "barcalog:sessao";
export const EVENTO_SESSAO = "barcalog:sessao-mudou";

let sessao = lerSessaoSalva();
let timerExpiracao = null;
agendarExpiracao();

function lerSessaoSalva() {
  try {
    const bruto = sessionStorage.getItem(CHAVE_SESSAO);
    if (!bruto) return null;
    const s = JSON.parse(bruto);
    if (!s?.token || !s?.expiraEm || new Date(s.expiraEm) <= new Date()) return null;
    return s;
  } catch {
    return null;
  }
}

function agendarExpiracao() {
  clearTimeout(timerExpiracao);
  if (!sessao) return;
  const restante = new Date(sessao.expiraEm).getTime() - Date.now();
  // setTimeout aceita no máximo ~24,8 dias; tokens duram horas.
  timerExpiracao = setTimeout(() => encerrarSessao("expirada"), Math.max(0, Math.min(restante, 2 ** 31 - 1)));
}

function avisar(motivo) {
  window.dispatchEvent(new CustomEvent(EVENTO_SESSAO, { detail: { motivo } }));
}

export function obterSessao() {
  return sessao;
}

export function definirSessao(resposta) {
  sessao = {
    token: resposta.token,
    expiraEm: resposta.expiraEm,
    restricao: resposta.restricao ?? null,
    usuario: resposta.usuario
  };
  try {
    sessionStorage.setItem(CHAVE_SESSAO, JSON.stringify(sessao));
  } catch {
    // sessionStorage bloqueado: a sessão vale só enquanto a página estiver aberta.
  }
  agendarExpiracao();
  avisar("login");
}

/** Limpa a sessão local. motivo: "logout" | "expirada" | "revogada". */
export function encerrarSessao(motivo = "logout") {
  const tinha = !!sessao;
  sessao = null;
  clearTimeout(timerExpiracao);
  try {
    sessionStorage.removeItem(CHAVE_SESSAO);
  } catch {
    // ignorado: nada a remover
  }
  if (tinha) avisar(motivo);
}

export function assinarSessao(callback) {
  const ouvinte = e => callback(e.detail);
  window.addEventListener(EVENTO_SESSAO, ouvinte);
  return () => window.removeEventListener(EVENTO_SESSAO, ouvinte);
}

/** Erro da API já traduzido pra algo exibível. */
export class ErroApi extends Error {
  constructor({ status, titulo, detalhe, codigo, traceId, erros, retryAfter }) {
    super(detalhe || titulo || "Erro inesperado.");
    this.name = "ErroApi";
    this.status = status;
    this.titulo = titulo;
    this.detalhe = detalhe;
    this.codigo = codigo;
    this.traceId = traceId;
    this.erros = erros; // { Campo: ["mensagem"] } em erro de validação (400)
    this.retryAfter = retryAfter;
  }
}

/** Mensagem amigável pra exibir ao usuário (toast / formulário). */
export function mensagemErro(erro) {
  if (!(erro instanceof ErroApi)) return "Algo deu errado. Tente novamente.";
  switch (erro.status) {
    case 0: return "Sem conexão com o servidor. Verifique a internet e tente de novo.";
    case 400: {
      const primeiras = erro.erros ? Object.values(erro.erros).flat().slice(0, 3) : [];
      return primeiras.length ? primeiras.join(" ") : (erro.detalhe || "Dados inválidos.");
    }
    case 401: return erro.detalhe || "Sua sessão expirou. Entre novamente.";
    case 403: return "Você não tem permissão para esta ação.";
    case 404: return erro.detalhe || "Registro não encontrado.";
    case 409: return erro.detalhe || "Outra pessoa alterou este registro ao mesmo tempo. Recarregue e tente de novo.";
    case 413: return "Arquivo ou conteúdo grande demais.";
    case 429: return `Muitas tentativas. Aguarde ${erro.retryAfter || "alguns"} segundos e tente de novo.`;
    case 503: return erro.detalhe || "Serviço temporariamente indisponível. Tente de novo em instantes.";
    case 504: return "O servidor demorou demais para responder. Tente de novo.";
    default:
      if (erro.status >= 500) return `Erro no servidor. Se persistir, informe o código ${erro.traceId || "exibido"} ao suporte.`;
      return erro.detalhe || erro.titulo || "Algo deu errado.";
  }
}

/** Chave de idempotência nova (uma por ação do usuário — reenviar a MESMA em caso de retry). */
export function novaChaveIdempotencia() {
  if (crypto?.randomUUID) return crypto.randomUUID();
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function montarUrl(caminho, parametros) {
  const url = new URL(URL_API + caminho, window.location.origin);
  Object.entries(parametros || {}).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== "" && v !== "todas" && v !== "todos") url.searchParams.set(k, v);
  });
  return url.toString();
}

async function lerErro(resposta) {
  let corpo = null;
  try {
    corpo = await resposta.json();
  } catch {
    // corpo vazio ou não-JSON
  }
  return new ErroApi({
    status: resposta.status,
    titulo: corpo?.title,
    detalhe: corpo?.detail,
    codigo: corpo?.codigo,
    traceId: corpo?.traceId || resposta.headers.get("X-Request-Id"),
    erros: corpo?.errors,
    retryAfter: resposta.headers.get("Retry-After")
  });
}

/**
 * Requisição genérica.
 * opcoes: { parametros, corpo, idempotencia (string), sinal (AbortSignal), resposta: "json" | "blob" | "vazia" }
 */
export async function requisicao(metodo, caminho, opcoes = {}) {
  const { parametros, corpo, idempotencia, sinal, resposta: tipoResposta = "json" } = opcoes;
  const cabecalhos = { Accept: tipoResposta === "blob" ? "*/*" : "application/json" };
  if (corpo !== undefined) cabecalhos["Content-Type"] = "application/json";
  if (sessao?.token) cabecalhos.Authorization = `Bearer ${sessao.token}`;
  if (idempotencia) cabecalhos["Idempotency-Key"] = idempotencia;

  let resposta;
  try {
    resposta = await fetch(montarUrl(caminho, parametros), {
      method: metodo,
      headers: cabecalhos,
      body: corpo !== undefined ? JSON.stringify(corpo) : undefined,
      signal: sinal,
      credentials: "omit" // sem cookies: autenticação só pelo cabeçalho
    });
  } catch (e) {
    if (e?.name === "AbortError") throw e;
    throw new ErroApi({ status: 0, titulo: "Sem conexão" });
  }

  // Resposta que não é da API (ex.: o site devolveu index.html porque a API
  // não está configurada / publicada): erro explícito em vez de JSON inválido.
  const tipo = resposta.headers.get("Content-Type") || "";
  const semJson = !tipo.includes("json");
  if (tipo.includes("text/html") || (semJson && (resposta.status === 404 || resposta.status === 405))) {
    throw new ErroApi({ status: 503, titulo: "API indisponível", detalhe: "Servidor da API não encontrado. Avise o suporte." });
  }

  if (!resposta.ok) {
    const erro = await lerErro(resposta);
    // Token recusado (expirado, revogado por logout/troca de senha/desativação).
    if (resposta.status === 401 && sessao?.token && caminho !== "/api/v1/auth/login") encerrarSessao("revogada");
    throw erro;
  }
  if (tipoResposta === "blob") return resposta.blob();
  if (resposta.status === 204 || tipoResposta === "vazia") return null;
  return resposta.json();
}

export const api = {
  get: (caminho, parametros, sinal) => requisicao("GET", caminho, { parametros, sinal }),
  post: (caminho, corpo, idempotencia) => requisicao("POST", caminho, { corpo, idempotencia }),
  put: (caminho, corpo) => requisicao("PUT", caminho, { corpo }),
  patch: (caminho, corpo) => requisicao("PATCH", caminho, { corpo }),
  del: caminho => requisicao("DELETE", caminho, { resposta: "vazia" })
};

/** Busca todas as páginas de uma listagem paginada (até `limitePaginas`). */
export async function buscarTodos(caminho, parametros = {}, limitePaginas = 20) {
  const itens = [];
  let total = 0;
  for (let pagina = 1; pagina <= limitePaginas; pagina++) {
    const r = await api.get(caminho, { ...parametros, pagina, tamanhoPagina: 500 });
    itens.push(...r.itens);
    total = r.total;
    if (itens.length >= total || r.itens.length === 0) break;
  }
  return { itens, total, truncado: itens.length < total };
}

/** Baixa um arquivo autenticado (ex.: CSV) e dispara o download no navegador. */
export async function baixarArquivo(caminho, parametros, nomeArquivo) {
  const blob = await requisicao("GET", caminho, { parametros, resposta: "blob" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = nomeArquivo;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
