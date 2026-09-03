// Núcleo de regras do Sistema de Negativação, conforme a documentação oficial
// do BarcaLog (infográfico "Inteligência e Controle no Fluxo Logístico de
// Grãos"):
//   N1 (leve)     -> registro histórico e advertência, não bloqueia.
//   N2 (moderada) -> registro de reincidência e monitoramento.
//   N3 (grave)    -> BLOQUEIO AUTOMÁTICO IMEDIATO para novos carregamentos.
//
// IMPORTANTE sobre o modelo de dados: quem é negativado é o VEÍCULO (placa)
// — e, quando o motorista está identificado, o CONDUTOR (CPF) — nunca a
// transportadora como um todo. O status da transportadora mostrado nas
// telas é sempre CALCULADO a partir das carretas dela (negativada se tiver
// pelo menos 1 veículo negativado); não existe mais um campo de status
// gravado direto na transportadora.
//
// Ainda não existe backend/banco (ver README do projeto), então este store
// persiste em localStorage — é o mesmo dado tanto pro Portal do
// Transportador quanto pra tela interna de Negativação, dentro do mesmo
// navegador. Quando a API real existir, só trocar as funções abaixo por
// chamadas fetch; nenhuma tela precisa mudar de contrato.

const CHAVE = "barcalog:negativacao:v3";
const EVENTO = "barcalog:negativacao:mudou";

// Senha padrão pra todas as transportadoras nesta fase de demonstração —
// ainda não existe autenticação real (ver aviso no Portal do Transportador).
export const SENHA_PADRAO_DEMO = "1234";

const SEED = {
  transportadoras: [
    { id: "t1", nome: "Rota Amazônia Cargas", cnpj: "07.123.456/0001-10", senha: SENHA_PADRAO_DEMO },
    { id: "t2", nome: "AgroTransportes Sul", cnpj: "09.234.567/0001-21", senha: SENHA_PADRAO_DEMO },
    { id: "t3", nome: "Norte Grãos Logística", cnpj: "11.345.678/0001-32", senha: SENHA_PADRAO_DEMO },
    { id: "t4", nome: "TransNorte Cargas", cnpj: "13.456.789/0001-43", senha: SENHA_PADRAO_DEMO },
    { id: "t5", nome: "Barcarena Transportes", cnpj: "15.567.890/0001-54", senha: SENHA_PADRAO_DEMO }
  ],
  ocorrencias: [
    {
      id: "oc1",
      nivel: "N3",
      placa: "NGL-3021",
      cpfMotorista: "123.456.789-00",
      transportadora: "Norte Grãos Logística",
      descricao: "Carga liberada fora da janela D0-D3 sem autorização, com divergência de peso na pesagem.",
      local: "Pátio de Triagem",
      responsavel: "Fiscal — Admin Teste",
      evidencias: ["ocorrencia_ngl3021_01.jpg"],
      status: "contestada",
      criadoEm: new Date(Date.now() - 5 * 86400000).toISOString()
    },
    {
      id: "oc2",
      nivel: "N2",
      placa: "ATS-4410",
      cpfMotorista: "234.567.890-11",
      transportadora: "AgroTransportes Sul",
      descricao: "Atraso de 2h40 além da janela D1, sem comunicação prévia à triagem.",
      local: "Pátio de Triagem",
      responsavel: "Fiscal — Admin Teste",
      evidencias: [],
      status: "ativa",
      criadoEm: new Date(Date.now() - 2 * 86400000).toISOString()
    }
  ],
  // Exemplo de contestação já aberta, pra "Contestações — GED" não nascer
  // vazio e dar pra ver o fluxo de aprovação/rejeição funcionando de cara.
  contestacoes: [
    {
      id: "ct1",
      ocorrenciaId: "oc1",
      transportadora: "Norte Grãos Logística",
      justificativa: "O atraso foi causado por pane mecânica documentada. Anexamos o laudo da oficina e o boletim de ocorrência do guincho.",
      documentos: ["laudo_oficina_ngl3021.pdf", "boletim_guincho.pdf"],
      status: "pendente",
      criadoEm: new Date(Date.now() - 2 * 86400000).toISOString(),
      respondidoEm: null,
      respostaOperador: null
    }
  ],
  // statusNegativacao é o campo que decide se a carreta pode rodar:
  // "regular" ou "negativada". statusPortaria é uma coisa totalmente
  // diferente (posição física no pátio/porto).
  veiculos: [
    { id: "v1", placa: "ENM-1001", transportadora: "Rota Amazônia Cargas", modelo: "Carreta graneleira", statusPortaria: "No Porto", statusNegativacao: "regular" },
    { id: "v2", placa: "NGL-3021", transportadora: "Norte Grãos Logística", modelo: "Carreta graneleira", statusPortaria: "No Pátio", statusNegativacao: "negativada" },
    { id: "v3", placa: "ATS-4410", transportadora: "AgroTransportes Sul", modelo: "Bitrem graneleiro", statusPortaria: "Aguardando", statusNegativacao: "regular" },
    { id: "v4", placa: "TNC-2290", transportadora: "TransNorte Cargas", modelo: "Carreta graneleira", statusPortaria: "Descarga Finalizada", statusNegativacao: "regular" },
    { id: "v5", placa: "BCT-7715", transportadora: "Barcarena Transportes", modelo: "Rodotrem", statusPortaria: "No Pátio", statusNegativacao: "regular" },
    { id: "v6", placa: "RAC-5502", transportadora: "Rota Amazônia Cargas", modelo: "Carreta graneleira", statusPortaria: "Aguardando", statusNegativacao: "regular" }
  ],
  condutores: [
    { id: "c1", nome: "José Ribeiro", cpf: "123.456.789-00", transportadora: "Norte Grãos Logística", placaVinculada: "NGL-3021", statusNegativacao: "negativada" },
    { id: "c2", nome: "Marcos Andrade", cpf: "234.567.890-11", transportadora: "AgroTransportes Sul", placaVinculada: "ATS-4410", statusNegativacao: "regular" },
    { id: "c3", nome: "Elias Farias", cpf: "345.678.901-22", transportadora: "Rota Amazônia Cargas", placaVinculada: "ENM-1001", statusNegativacao: "regular" }
  ]
};

function carregar() {
  try {
    const bruto = localStorage.getItem(CHAVE);
    if (!bruto) return estruturaClonada(SEED);
    return JSON.parse(bruto);
  } catch {
    return estruturaClonada(SEED);
  }
}

function estruturaClonada(obj) {
  return JSON.parse(JSON.stringify(obj));
}

let estado = carregar();

function salvar() {
  localStorage.setItem(CHAVE, JSON.stringify(estado));
  window.dispatchEvent(new CustomEvent(EVENTO));
}

export function assinarMudancas(callback) {
  window.addEventListener(EVENTO, callback);
  return () => window.removeEventListener(EVENTO, callback);
}

// listarTransportadoras() devolve cada transportadora com um `status`
// CALCULADO ("negativada" se tiver ≥1 carreta negativada) e uma contagem
// pronta pra exibir — não existe mais status gravado na própria empresa.
export function listarTransportadoras() {
  return estado.transportadoras.map(t => {
    const frota = estado.veiculos.filter(v => v.transportadora === t.nome);
    const negativados = frota.filter(v => v.statusNegativacao === "negativada").length;
    return {
      ...t,
      status: negativados > 0 ? "negativada" : "regular",
      carretasNegativadas: negativados,
      carretasTotal: frota.length
    };
  });
}

export function obterTransportadora(nomeOuId) {
  return listarTransportadoras().find(t => t.id === nomeOuId || t.nome === nomeOuId);
}

export function autenticarTransportadora(nome, senha) {
  const nomeNormalizado = (nome || "").trim().toLowerCase();
  const senhaNormalizada = (senha || "").trim();
  const t = estado.transportadoras.find(
    t => t.nome.trim().toLowerCase() === nomeNormalizado && t.senha === senhaNormalizada
  );
  return t ? obterTransportadora(t.nome) : null;
}

export function listarOcorrencias(filtroTransportadora) {
  const lista = estado.ocorrencias;
  if (!filtroTransportadora) return lista;
  return lista.filter(o => o.transportadora === filtroTransportadora);
}

export function listarContestacoes(filtroTransportadora) {
  const lista = estado.contestacoes;
  if (!filtroTransportadora) return lista;
  return lista.filter(c => c.transportadora === filtroTransportadora);
}

// Contagem de N2 nos últimos 30 dias — usado só como sinal de
// "monitoramento", não bloqueia sozinho (regra N3 é que bloqueia).
export function reincidenciasN2(transportadora, janelaDias = 30) {
  const limite = Date.now() - janelaDias * 86400000;
  return estado.ocorrencias.filter(
    o => o.transportadora === transportadora && o.nivel === "N2" && new Date(o.criadoEm).getTime() >= limite
  ).length;
}

export function registrarOcorrencia({ nivel, placa, cpfMotorista, transportadora, descricao, local, responsavel, evidencias }) {
  const ocorrencia = {
    id: `oc${Date.now()}`,
    nivel,
    placa,
    cpfMotorista,
    transportadora,
    descricao,
    local,
    responsavel,
    evidencias: evidencias || [],
    status: "ativa",
    criadoEm: new Date().toISOString()
  };
  estado.ocorrencias = [ocorrencia, ...estado.ocorrencias];

  // Regra oficial: N3 bloqueia automaticamente a CARRETA (e o CONDUTOR,
  // quando identificado) — nunca a transportadora inteira de uma vez.
  if (nivel === "N3") {
    const veiculo = estado.veiculos.find(v => v.placa.toUpperCase() === (placa || "").toUpperCase());
    if (veiculo) veiculo.statusNegativacao = "negativada";
    if (cpfMotorista) {
      const condutor = estado.condutores.find(c => c.cpf === cpfMotorista);
      if (condutor) condutor.statusNegativacao = "negativada";
    }
  }
  salvar();
  return ocorrencia;
}

export function abrirContestacao({ ocorrenciaId, transportadora, justificativa, documentos }) {
  const contestacao = {
    id: `ct${Date.now()}`,
    ocorrenciaId,
    transportadora,
    justificativa,
    documentos: documentos || [],
    status: "pendente",
    criadoEm: new Date().toISOString(),
    respondidoEm: null,
    respostaOperador: null
  };
  estado.contestacoes = [contestacao, ...estado.contestacoes];
  const oc = estado.ocorrencias.find(o => o.id === ocorrenciaId);
  if (oc) oc.status = "contestada";
  salvar();
  return contestacao;
}

export function responderContestacao(contestacaoId, aprovado, respostaOperador) {
  const c = estado.contestacoes.find(c => c.id === contestacaoId);
  if (!c) return;
  c.status = aprovado ? "aprovada" : "rejeitada";
  c.respondidoEm = new Date().toISOString();
  c.respostaOperador = respostaOperador;

  const oc = estado.ocorrencias.find(o => o.id === c.ocorrenciaId);
  if (oc) oc.status = aprovado ? "resolvida" : "ativa";

  if (aprovado && oc) {
    // Aprovar a contestação regulariza a carreta (e o condutor) daquela
    // ocorrência específica — não mexe em outras carretas da transportadora.
    const veiculo = estado.veiculos.find(v => v.placa.toUpperCase() === (oc.placa || "").toUpperCase());
    if (veiculo) veiculo.statusNegativacao = "regular";
    if (oc.cpfMotorista) {
      const condutor = estado.condutores.find(c2 => c2.cpf === oc.cpfMotorista);
      if (condutor) condutor.statusNegativacao = "regular";
    }
  }
  salvar();
}

// Controle manual, direto na carreta — a equipe do porto pode negativar ou
// desnegativar um veículo específico sem precisar passar pelo fluxo de
// ocorrência/contestação completo.
export function negativarVeiculo(veiculoId, motivo) {
  const v = estado.veiculos.find(v => v.id === veiculoId);
  if (!v) return;
  v.statusNegativacao = "negativada";
  estado.ocorrencias = [{
    id: `oc${Date.now()}`,
    nivel: "N3",
    placa: v.placa,
    cpfMotorista: "",
    transportadora: v.transportadora,
    descricao: motivo || "Negativação manual aplicada pela equipe do porto.",
    local: "Administrativo",
    responsavel: "Admin Teste",
    evidencias: [],
    status: "ativa",
    criadoEm: new Date().toISOString()
  }, ...estado.ocorrencias];
  salvar();
}

export function desnegativarVeiculo(veiculoId) {
  const v = estado.veiculos.find(v => v.id === veiculoId);
  if (!v) return;
  v.statusNegativacao = "regular";
  estado.ocorrencias = estado.ocorrencias.map(o =>
    o.placa === v.placa && o.status !== "resolvida" ? { ...o, status: "resolvida" } : o
  );
  salvar();
}

// Elegibilidade pra operar no município — a transportadora só é considerada
// apta se NENHUMA carreta dela estiver negativada.
export function estaAptaParaOperar(nome) {
  const t = obterTransportadora(nome);
  return t ? t.status !== "negativada" : true;
}

// ---------- Gestão de Frotas e Condutores ----------
// Placas e CPFs são os alvos reais dos gatilhos de infração — cadastrar
// aqui é o que substitui a antiga tela "Cadastrar Carga".
export function listarVeiculos(filtroTransportadora) {
  if (!filtroTransportadora) return estado.veiculos;
  return estado.veiculos.filter(v => v.transportadora === filtroTransportadora);
}

export function cadastrarVeiculo({ placa, transportadora, modelo }) {
  const veiculo = { id: `v${Date.now()}`, placa, transportadora, modelo, statusPortaria: "Aguardando", statusNegativacao: "regular" };
  estado.veiculos = [veiculo, ...estado.veiculos];
  salvar();
  return veiculo;
}

export function buscarVeiculoPorPlaca(placa) {
  const consulta = (placa || "").trim().toUpperCase();
  if (!consulta) return null;
  return estado.veiculos.find(v => v.placa.toUpperCase() === consulta) || null;
}

export function atualizarStatusPortaria(id, statusPortaria) {
  const v = estado.veiculos.find(v => v.id === id);
  if (v) v.statusPortaria = statusPortaria;
  salvar();
  return v;
}

export function removerVeiculo(id) {
  estado.veiculos = estado.veiculos.filter(v => v.id !== id);
  salvar();
}

export function listarCondutores(filtroTransportadora) {
  if (!filtroTransportadora) return estado.condutores;
  return estado.condutores.filter(c => c.transportadora === filtroTransportadora);
}

export function cadastrarCondutor({ nome, cpf, transportadora, placaVinculada }) {
  const condutor = { id: `c${Date.now()}`, nome, cpf, transportadora, placaVinculada, statusNegativacao: "regular" };
  estado.condutores = [condutor, ...estado.condutores];
  salvar();
  return condutor;
}

export function removerCondutor(id) {
  estado.condutores = estado.condutores.filter(c => c.id !== id);
  salvar();
}

export { EVENTO as EVENTO_MUDANCA };
