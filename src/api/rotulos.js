// Tradução entre os valores da API (enums sem espaço/acento) e os rótulos
// que as telas exibem. Mantém um único lugar pra mudar texto de interface.

export const STATUS_PORTARIA = {
  NoPatio: "No Pátio",
  Aguardando: "Aguardando",
  NoPorto: "No Porto",
  DescargaFinalizada: "Descarga Finalizada"
};

export const STATUS_AGENDAMENTO = {
  Agendado: "Agendado",
  Confirmado: "Confirmado",
  ACaminho: "A caminho",
  AguardandoEntrada: "Aguardando entrada",
  EmOperacao: "Em operação",
  Finalizado: "Finalizado",
  Atrasado: "Atrasado",
  Cancelado: "Cancelado"
};

export function chavePorRotulo(mapa, rotulo) {
  return Object.keys(mapa).find(k => mapa[k] === rotulo) ?? rotulo;
}

// Terminais (mesmos ids do backend).
export const TERMINAIS = [
  { id: "unitapajos", nome: "Unitapajós" },
  { id: "tgpm", nome: "TGPM" },
  { id: "hidrovias", nome: "Hidrovias" }
];

export function nomeTerminal(id) {
  return TERMINAIS.find(t => t.id === id)?.nome || id;
}

// Janelas de permanência (legenda/cores). Os números vêm da API.
export const JANELAS = [
  { chave: "D0", rotulo: "D0 (0–24h)", cor: "#22C55E" },
  { chave: "D1", rotulo: "D1 (24–48h)", cor: "#F59E0B" },
  { chave: "D2", rotulo: "D2 (48–72h)", cor: "#F97316" },
  { chave: "D3", rotulo: "D3 (72–96h)", cor: "#EF4444" },
  { chave: "AL", rotulo: "Alerta (96–144h)", cor: "#DC2626" },
  { chave: "EC", rotulo: "Estouro Crítico (>144h)", cor: "#7F1D1D" }
];

export const CAPACIDADE_DIARIA = 1000;

const minusculo = v => (typeof v === "string" ? v.toLowerCase() : v);

// ---------- Adaptadores: DTO da API → formato usado pelas telas ----------

export function adaptarTransportadora(t) {
  return {
    id: t.id,
    nome: t.nome,
    cnpj: t.cnpj,
    status: minusculo(t.status), // "regular" | "negativada" (calculado no backend)
    carretasNegativadas: t.carretasNegativadas,
    carretasTotal: t.carretasTotal,
    reincidenciasN2: t.reincidenciasN2Ultimos30Dias,
    aptaParaOperar: t.aptaParaOperar
  };
}

export function adaptarVeiculo(v) {
  return {
    id: v.id,
    placa: v.placa,
    transportadoraId: v.transportadoraId,
    transportadora: v.transportadora,
    modelo: v.modelo,
    statusPortaria: STATUS_PORTARIA[v.statusPortaria] || v.statusPortaria,
    statusPortariaDesde: v.statusPortariaDesde,
    terminal: v.terminalId,
    terminalNome: v.terminal,
    statusNegativacao: minusculo(v.statusNegativacao)
  };
}

export function adaptarCondutor(c) {
  return {
    id: c.id,
    nome: c.nome,
    transportadoraId: c.transportadoraId,
    transportadora: c.transportadora,
    placaVinculada: c.placaVinculada,
    statusNegativacao: minusculo(c.statusNegativacao)
  };
}

export function adaptarOcorrencia(o) {
  return {
    id: o.id,
    nivel: o.nivel,
    placa: o.placa,
    transportadoraId: o.transportadoraId,
    transportadora: o.transportadora,
    condutor: o.condutor,
    descricao: o.descricao,
    local: o.local,
    responsavel: o.responsavel,
    status: minusculo(o.status), // "ativa" | "contestada" | "resolvida"
    criadoEm: o.criadoEm
  };
}

export function adaptarContestacao(c) {
  return {
    id: c.id,
    ocorrenciaId: c.ocorrenciaId,
    nivelOcorrencia: c.nivelOcorrencia,
    placa: c.placa,
    transportadoraId: c.transportadoraId,
    transportadora: c.transportadora,
    justificativa: c.justificativa,
    documentos: [], // anexos (GED) ainda não suportados pela API
    status: minusculo(c.status), // "pendente" | "aprovada" | "rejeitada"
    criadoEm: c.criadoEm,
    respondidoEm: c.respondidoEm,
    respostaOperador: c.respostaOperador
  };
}

/** Registro de marcação (métricas) → nomes de campo usados pelas telas. */
export function adaptarRegistro(r) {
  return { ...r, id: r.movimentoId, empresaId: r.terminalId, empresaNome: r.terminalNome };
}
