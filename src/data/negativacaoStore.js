// Sistema de Negativação — agora apoiado na API (antes: localStorage).
//
// Por que manter este módulo em vez de chamar fetch direto nas telas:
// as telas já consomem este contrato (listarVeiculos(), listarOcorrencias()…
// + o evento de mudança via useNegativacao). Mantendo o contrato, trocamos a
// fonte de dados sem reescrever layout e com menos risco de regressão.
//
// Como funciona agora:
// - listar*() são síncronos e leem um CACHE em memória carregado da API
//   (carregar()). As regras (N3 bloqueia, status da transportadora calculado,
//   contestação aprovada regulariza só a carreta) rodam no BACKEND.
// - Toda gravação chama a API (com Idempotency-Key), depois recarrega o
//   cache e avisa as telas. Erros sobem como ErroApi pra tela mostrar.

import { api, buscarTodos, novaChaveIdempotencia } from "../api/cliente.js";
import {
  adaptarTransportadora, adaptarVeiculo, adaptarCondutor, adaptarOcorrencia, adaptarContestacao,
  STATUS_PORTARIA, chavePorRotulo
} from "../api/rotulos.js";

const EVENTO = "barcalog:negativacao:mudou";

let estado = {
  transportadoras: [], veiculos: [], condutores: [], ocorrencias: [], contestacoes: [],
  carregado: false, carregando: false, erro: null
};
let cargaEmAndamento = null;
// Incrementa a cada limparCache(): uma carga iniciada antes do logout não pode
// gravar os dados do usuário anterior no cache ao terminar.
let geracao = 0;

function emitir() {
  window.dispatchEvent(new CustomEvent(EVENTO));
}

export function assinarMudancas(callback) {
  window.addEventListener(EVENTO, callback);
  return () => window.removeEventListener(EVENTO, callback);
}

export function obterStatusCarga() {
  return { carregado: estado.carregado, carregando: estado.carregando, erro: estado.erro };
}

/** Carrega (ou recarrega) tudo da API. Chamadas simultâneas compartilham a mesma carga. */
export function carregar() {
  if (cargaEmAndamento) return cargaEmAndamento;
  estado = { ...estado, carregando: true, erro: null };
  emitir();
  const minhaGeracao = geracao;
  cargaEmAndamento = (async () => {
    try {
      const [transportadoras, veiculos, condutores, ocorrencias, contestacoes] = await Promise.all([
        api.get("/api/v1/transportadoras"),
        buscarTodos("/api/v1/veiculos"),
        buscarTodos("/api/v1/condutores"),
        // Histórico pode crescer muito: as telas mostram as 500 mais recentes.
        api.get("/api/v1/ocorrencias", { tamanhoPagina: 500 }),
        api.get("/api/v1/contestacoes", { tamanhoPagina: 500 })
      ]);
      if (minhaGeracao !== geracao) return;
      estado = {
        transportadoras: transportadoras.map(adaptarTransportadora),
        veiculos: veiculos.itens.map(adaptarVeiculo),
        condutores: condutores.itens.map(adaptarCondutor),
        ocorrencias: ocorrencias.itens.map(adaptarOcorrencia),
        contestacoes: contestacoes.itens.map(adaptarContestacao),
        carregado: true, carregando: false, erro: null
      };
    } catch (erro) {
      if (minhaGeracao === geracao) estado = { ...estado, carregando: false, erro };
    } finally {
      if (minhaGeracao === geracao) {
        cargaEmAndamento = null;
        emitir();
      }
    }
  })();
  return cargaEmAndamento;
}

/** Esvazia o cache (logout): dados de um usuário não podem aparecer pro próximo. */
export function limparCache() {
  geracao++;
  cargaEmAndamento = null;
  estado = { transportadoras: [], veiculos: [], condutores: [], ocorrencias: [], contestacoes: [], carregado: false, carregando: false, erro: null };
  emitir();
}

// ---------- Leitura (cache) ----------

export function listarTransportadoras() {
  return estado.transportadoras;
}

export function obterTransportadora(nomeOuId) {
  return estado.transportadoras.find(t => t.id === nomeOuId || t.nome === nomeOuId);
}

export function listarOcorrencias(filtroTransportadora) {
  return filtroTransportadora ? estado.ocorrencias.filter(o => o.transportadora === filtroTransportadora) : estado.ocorrencias;
}

export function listarContestacoes(filtroTransportadora) {
  return filtroTransportadora ? estado.contestacoes.filter(c => c.transportadora === filtroTransportadora) : estado.contestacoes;
}

/** Reincidência N2 nos últimos 30 dias — calculada no backend. */
export function reincidenciasN2(transportadora) {
  return obterTransportadora(transportadora)?.reincidenciasN2 ?? 0;
}

export function estaAptaParaOperar(nome) {
  const t = obterTransportadora(nome);
  return t ? t.status !== "negativada" : true;
}

export function listarVeiculos(filtroTransportadora) {
  return filtroTransportadora ? estado.veiculos.filter(v => v.transportadora === filtroTransportadora) : estado.veiculos;
}

export function buscarVeiculoPorPlaca(placa) {
  const consulta = (placa || "").trim().toUpperCase().replace(/[\s-]/g, "");
  if (!consulta) return null;
  return estado.veiculos.find(v => v.placa.replace("-", "") === consulta) || null;
}

export function listarCondutores(filtroTransportadora) {
  return filtroTransportadora ? estado.condutores.filter(c => c.transportadora === filtroTransportadora) : estado.condutores;
}

// ---------- Gravação (API) ----------

function idTransportadora(nomeOuId) {
  const t = obterTransportadora(nomeOuId);
  if (!t) throw new Error("Transportadora não encontrada. Recarregue a página.");
  return t.id;
}

async function gravar(operacao) {
  const resultado = await operacao();
  await carregar();
  return resultado;
}

/** N3 → a API bloqueia a carreta na mesma transação. Devolve { ocorrencia, veiculoBloqueado, ... }. */
export function registrarOcorrencia({ nivel, placa, transportadora, descricao, local, responsavel, condutorId }, chave = novaChaveIdempotencia()) {
  return gravar(() => api.post("/api/v1/ocorrencias", {
    nivel, placa, transportadoraId: idTransportadora(transportadora), descricao,
    local: local || null, responsavel: responsavel || null, condutorId: condutorId || null
  }, chave));
}

export function abrirContestacao({ ocorrenciaId, justificativa }, chave = novaChaveIdempotencia()) {
  return gravar(() => api.post("/api/v1/contestacoes", { ocorrenciaId, justificativa }, chave));
}

export function responderContestacao(contestacaoId, aprovado, respostaOperador, chave = novaChaveIdempotencia()) {
  return gravar(() => api.post(`/api/v1/contestacoes/${contestacaoId}/${aprovado ? "aprovar" : "rejeitar"}`, { respostaOperador }, chave));
}

export function negativarVeiculo(veiculoId, motivo, chave = novaChaveIdempotencia()) {
  return gravar(() => api.post(`/api/v1/veiculos/${veiculoId}/negativar`, { motivo }, chave));
}

export function desnegativarVeiculo(veiculoId, chave = novaChaveIdempotencia()) {
  return gravar(() => api.post(`/api/v1/veiculos/${veiculoId}/desnegativar`, undefined, chave));
}

export function cadastrarVeiculo({ placa, transportadora, modelo, terminal }, chave = novaChaveIdempotencia()) {
  return gravar(() => api.post("/api/v1/veiculos", {
    placa, transportadoraId: idTransportadora(transportadora), modelo: modelo || null, terminalId: terminal || "unitapajos"
  }, chave));
}

export function removerVeiculo(id) {
  return gravar(() => api.del(`/api/v1/veiculos/${id}`));
}

export function atualizarStatusPortaria(id, statusPortaria) {
  return gravar(() => api.patch(`/api/v1/veiculos/${id}/status-portaria`, { statusPortaria: chavePorRotulo(STATUS_PORTARIA, statusPortaria) }));
}

export function cadastrarCondutor({ nome, transportadora, placaVinculada }, chave = novaChaveIdempotencia()) {
  return gravar(() => api.post("/api/v1/condutores", {
    nome, transportadoraId: idTransportadora(transportadora), placaVinculada: placaVinculada || null
  }, chave));
}

export function removerCondutor(id) {
  return gravar(() => api.del(`/api/v1/condutores/${id}`));
}

export { EVENTO as EVENTO_MUDANCA };
