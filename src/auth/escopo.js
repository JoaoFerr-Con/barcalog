import { obterSessao } from "../api/cliente.js";

// Escopo e preferências do usuário logado, só para a INTERFACE refletir o que o
// servidor já aplica: quem decide o acesso é o backend (vínculo de terminal no
// token). Preferência nunca amplia acesso — o servidor recusa (403).

/** Terminal ao qual o usuário está vinculado (null = todos). */
export function terminalVinculado() {
  return obterSessao()?.usuario?.terminalId || null;
}

/** Terminal já selecionado ao abrir uma tela: o vínculo, senão a preferência, senão "todos". */
export function terminalInicial() {
  const u = obterSessao()?.usuario;
  return u?.terminalId || u?.preferencias?.terminalPreferido || "";
}

/** Opções do seletor de terminal: com vínculo, só o próprio terminal. */
export function opcoesTerminal(lista, id = t => t.id) {
  const v = terminalVinculado();
  return v ? (lista || []).filter(t => id(t) === v) : lista || [];
}
