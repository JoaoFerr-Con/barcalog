// Sistema de notificação simples (pub-sub), sem precisar envolver a árvore
// inteira num provider. notificar() pode ser chamado de qualquer lugar
// (dentro ou fora de componentes React); <ToastHost/> (montado uma vez na
// raiz do app) escuta e renderiza.
const EVENTO = "barcalog:toast";

export function notificar(mensagem, tipo = "sucesso") {
  window.dispatchEvent(new CustomEvent(EVENTO, { detail: { mensagem, tipo, id: Date.now() + Math.random() } }));
}

export function assinarToasts(callback) {
  const ouvinte = e => callback(e.detail);
  window.addEventListener(EVENTO, ouvinte);
  return () => window.removeEventListener(EVENTO, ouvinte);
}
