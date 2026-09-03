// Sessão "atual" bem simples — quem está logado agora, interno ou pelo
// Portal do Transportador. Existe só pra outros stores (auditoria) saberem
// quem fez uma ação, sem precisar passar usuário como parâmetro em toda
// função. Quando existir autenticação real, isso vira o token/claims da API.
let sessaoAtual = null;

export function definirSessao(sessao) {
  sessaoAtual = sessao;
}

export function obterAutorAtual() {
  if (!sessaoAtual) return "Sistema";
  return sessaoAtual.tipo === "portal" ? `${sessaoAtual.nome} (transportadora)` : sessaoAtual.nome;
}
