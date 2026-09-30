import { useEffect, useState } from "react";
import {
  api, requisicao, obterSessao, definirSessao, encerrarSessao, assinarSessao, ErroApi
} from "../api/cliente.js";
import { limparCache } from "../data/negativacaoStore.js";

// Fluxo de autenticação contra /api/v1/auth. Quem decide se a senha, o código
// MFA ou o papel valem é SEMPRE o backend; aqui só guardamos o token recebido
// e reagimos às respostas.

/**
 * Tenta o login. Devolve:
 *   { ok: true, sessao }          — logado (pode vir com sessao.restricao)
 *   { mfa: true }                 — senha certa, falta o código do autenticador
 *   { erro: ErroApi }             — credenciais inválidas, bloqueio, 429 etc.
 */
export async function entrar(email, senha, codigoMfa) {
  try {
    const resposta = await api.post("/api/v1/auth/login", {
      email: email.trim(),
      senha,
      codigoMfa: codigoMfa?.trim() || undefined
    });
    limparCache(); // nada do usuário anterior pode sobrar na memória
    definirSessao(resposta);
    return { ok: true, sessao: obterSessao() };
  } catch (e) {
    if (e instanceof ErroApi && e.status === 401 && e.codigo === "mfa_requerido") return { mfa: true };
    return { erro: e };
  }
}

/** Encerra a sessão no servidor (revoga todos os tokens do usuário) e localmente. */
export async function sair() {
  try {
    if (obterSessao()) await requisicao("POST", "/api/v1/auth/logout", { resposta: "vazia" });
  } catch {
    // Mesmo se o servidor estiver fora, a sessão local é encerrada. O token
    // antigo expira sozinho (duração curta) — não há o que mais fazer aqui.
  } finally {
    limparCache();
    encerrarSessao("logout");
  }
}

/** Troca a senha. O servidor encerra todas as sessões: o usuário entra de novo. */
export async function trocarSenha(senhaAtual, novaSenha) {
  await api.post("/api/v1/auth/trocar-senha", { senhaAtual, novaSenha });
  limparCache();
  encerrarSessao("senha-trocada");
}

/** Gera o segredo TOTP (mostrado uma única vez) — exige a senha atual. */
export function configurarMfa(senhaAtual) {
  return api.post("/api/v1/auth/mfa/configurar", { senhaAtual });
}

/** Confirma o MFA com um código do app. O servidor encerra as sessões. */
export async function ativarMfa(codigo) {
  await api.post("/api/v1/auth/mfa/ativar", { codigo: codigo.trim() });
  limparCache();
  encerrarSessao("mfa-ativado");
}

const MENSAGENS_SAIDA = {
  expirada: "Sua sessão expirou. Entre novamente.",
  revogada: "Sua sessão foi encerrada (logout em outro lugar, troca de senha ou acesso revogado). Entre novamente.",
  "senha-trocada": "Senha alterada. Entre com a nova senha.",
  "mfa-ativado": "Verificação em duas etapas ativada. Entre novamente usando o código do app."
};

/**
 * Sessão atual + motivo da última saída (pra explicar ao usuário por que
 * voltou pra tela de login). Re-renderiza em login/logout/expiração.
 */
export function useSessao() {
  const [estado, setEstado] = useState(() => ({ sessao: obterSessao(), aviso: null }));
  useEffect(() => assinarSessao(({ motivo }) => {
    if (motivo !== "login") limparCache();
    setEstado({ sessao: obterSessao(), aviso: MENSAGENS_SAIDA[motivo] || null });
  }), []);
  return estado;
}

export function iniciais(nome) {
  const partes = (nome || "").trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return "?";
  return (partes[0][0] + (partes.length > 1 ? partes[partes.length - 1][0] : partes[0][1] || "")).toUpperCase();
}
