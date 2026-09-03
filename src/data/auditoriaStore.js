import { obterAutorAtual } from "./sessao.js";

// Log de auditoria — "quem fez o quê e quando", conforme a documentação
// oficial do BarcaLog ("Auditoria e Segurança Total: todos os registros
// possuem logs de alteração e controle de acesso"). Persistido em
// localStorage (mesma limitação de todo o resto do app nesta fase sem
// backend): cada evento fica registrado com autor, ação e detalhes.

const CHAVE = "barcalog:auditoria:v1";
const EVENTO = "barcalog:auditoria:mudou";
const LIMITE_REGISTROS = 500; // evita o log crescer sem limite no localStorage

function carregar() {
  try {
    const bruto = localStorage.getItem(CHAVE);
    return bruto ? JSON.parse(bruto) : [];
  } catch {
    return [];
  }
}

let logs = carregar();

function salvar() {
  if (logs.length > LIMITE_REGISTROS) logs = logs.slice(0, LIMITE_REGISTROS);
  localStorage.setItem(CHAVE, JSON.stringify(logs));
  window.dispatchEvent(new CustomEvent(EVENTO));
}

export function assinarAuditoria(callback) {
  window.addEventListener(EVENTO, callback);
  return () => window.removeEventListener(EVENTO, callback);
}

export function registrarLog(acao, detalhes) {
  logs = [{
    id: `log${Date.now()}${Math.floor(Math.random() * 1000)}`,
    autor: obterAutorAtual(),
    acao,
    detalhes: detalhes || "",
    quando: new Date().toISOString()
  }, ...logs];
  salvar();
}

export function listarLogs(filtro) {
  if (!filtro) return logs;
  const termo = filtro.toLowerCase();
  return logs.filter(l =>
    l.autor.toLowerCase().includes(termo) ||
    l.acao.toLowerCase().includes(termo) ||
    l.detalhes.toLowerCase().includes(termo)
  );
}
