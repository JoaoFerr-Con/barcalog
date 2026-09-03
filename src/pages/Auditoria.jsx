import { obterAutorAtual } from "./sessao.js";

// Log de auditoria — "quem fez o quê e quando", conforme a documentação
// oficial do BarcaLog ("Auditoria e Segurança Total: todos os registros
// possuem logs de alteração e controle de acesso"). Persistido em
// localStorage (mesma limitação de todo o resto do app nesta fase sem
// backend): cada evento fica registrado com autor, ação e detalhes.
//
// Toda leitura/escrita aqui é defensiva de propósito: se o localStorage
// falhar (aba anônima, cota estourada, storage bloqueado) ou algum
// registro vier malformado, isso NUNCA pode derrubar a tela de Auditoria —
// só significa que aquele log específico não fica salvo.

const CHAVE = "barcalog:auditoria:v1";
const EVENTO = "barcalog:auditoria:mudou";
const LIMITE_REGISTROS = 500; // evita o log crescer sem limite no localStorage

function carregar() {
  try {
    const bruto = localStorage.getItem(CHAVE);
    const dados = bruto ? JSON.parse(bruto) : [];
    return Array.isArray(dados) ? dados : [];
  } catch {
    return [];
  }
}

let logs = carregar();

function salvar() {
  if (logs.length > LIMITE_REGISTROS) logs = logs.slice(0, LIMITE_REGISTROS);
  try {
    localStorage.setItem(CHAVE, JSON.stringify(logs));
  } catch {
    // localStorage indisponível ou cota estourada — o log continua
    // valendo em memória nesta sessão, só não persiste entre recargas.
  }
  try {
    window.dispatchEvent(new CustomEvent(EVENTO));
  } catch {
    // ambiente sem CustomEvent (não deveria acontecer em navegador, mas
    // não é motivo pra quebrar a ação que disparou o log).
  }
}

export function assinarAuditoria(callback) {
  window.addEventListener(EVENTO, callback);
  return () => window.removeEventListener(EVENTO, callback);
}

export function registrarLog(acao, detalhes) {
  try {
    logs = [{
      id: `log${Date.now()}${Math.floor(Math.random() * 1000)}`,
      autor: String(obterAutorAtual() ?? "Sistema"),
      acao: String(acao ?? ""),
      detalhes: String(detalhes ?? ""),
      quando: new Date().toISOString()
    }, ...logs];
    salvar();
  } catch {
    // Nunca deixa uma falha ao registrar auditoria quebrar a ação real
    // (registrar ocorrência, mudar status etc) que disparou esse log.
  }
}

// Coage qualquer campo pra string antes de comparar — um registro antigo
// ou malformado (campo undefined/nulo/objeto) não pode derrubar a busca.
function paraTexto(valor) {
  return String(valor ?? "").toLowerCase();
}

export function listarLogs(filtro) {
  const base = Array.isArray(logs) ? logs : [];
  if (!filtro) return base;
  const termo = filtro.toLowerCase();
  return base.filter(l =>
    paraTexto(l?.autor).includes(termo) ||
    paraTexto(l?.acao).includes(termo) ||
    paraTexto(l?.detalhes).includes(termo)
  );
}
