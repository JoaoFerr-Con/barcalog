// Núcleo de regras do Sistema de Negativação, conforme a documentação oficial
// do BarcaLog (infográfico "Inteligência e Controle no Fluxo Logístico de
// Grãos"):
//   N1 (leve)     -> registro histórico e advertência, não bloqueia.
//   N2 (moderada) -> registro de reincidência e monitoramento.
//   N3 (grave)    -> BLOQUEIO AUTOMÁTICO IMEDIATO para novos carregamentos.
//
// Ainda não existe backend/banco (ver README do projeto), então este store
// persiste em localStorage — é o mesmo dado tanto pro Portal do
// Transportador quanto pra tela interna de Negativação, dentro do mesmo
// navegador. Quando a API real existir, só trocar as funções abaixo por
// chamadas fetch; nenhuma tela precisa mudar de contrato.

const CHAVE = "barcalog:negativacao:v1";
const EVENTO = "barcalog:negativacao:mudou";

const SEED = {
  transportadoras: [
    { id: "t1", nome: "Rota Amazônia Cargas", cnpj: "07.123.456/0001-10", senha: "1234", status: "regular" },
    { id: "t2", nome: "AgroTransportes Sul", cnpj: "09.234.567/0001-21", senha: "1234", status: "regular" },
    { id: "t3", nome: "Norte Grãos Logística", cnpj: "11.345.678/0001-32", senha: "1234", status: "negativada" },
    { id: "t4", nome: "TransNorte Cargas", cnpj: "13.456.789/0001-43", senha: "1234", status: "regular" },
    { id: "t5", nome: "Barcarena Transportes", cnpj: "15.567.890/0001-54", senha: "1234", status: "regular" }
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
      status: "ativa",
      criadoEm: new Date(Date.now() - 5 * 86400000).toISOString()
    }
  ],
  contestacoes: [],
  veiculos: [
    { id: "v1", placa: "ENM-1001", transportadora: "Rota Amazônia Cargas", modelo: "Carreta graneleira", statusPortaria: "Aguardando" },
    { id: "v2", placa: "NGL-3021", transportadora: "Norte Grãos Logística", modelo: "Carreta graneleira", statusPortaria: "No Pátio" }
  ],
  condutores: [
    { id: "c1", nome: "José Ribeiro", cpf: "123.456.789-00", transportadora: "Norte Grãos Logística", placaVinculada: "NGL-3021" }
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

export function listarTransportadoras() {
  return estado.transportadoras;
}

export function obterTransportadora(nomeOuId) {
  return estado.transportadoras.find(t => t.id === nomeOuId || t.nome === nomeOuId);
}

export function autenticarTransportadora(nome, senha) {
  const t = estado.transportadoras.find(t => t.nome === nome && t.senha === senha);
  return t || null;
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

  // Regra oficial: N3 gera bloqueio automático imediato.
  if (nivel === "N3") {
    const t = estado.transportadoras.find(t => t.nome === transportadora);
    if (t) t.status = "negativada";
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

  if (aprovado) {
    const t = estado.transportadoras.find(t => t.nome === c.transportadora);
    // Só regulariza se não houver outra ocorrência N3 ainda ativa/contestada.
    const outrasN3Ativas = estado.ocorrencias.some(
      o => o.transportadora === c.transportadora && o.nivel === "N3" && o.status !== "resolvida" && o.id !== oc?.id
    );
    if (t && !outrasN3Ativas) t.status = "regular";
  }
  salvar();
}

// ---------- Gestão de Frotas e Condutores ----------
// Placas e CPFs são os alvos reais dos gatilhos de infração — cadastrar
// aqui é o que substitui a antiga tela "Cadastrar Carga".
export function listarVeiculos() {
  return estado.veiculos;
}

export function cadastrarVeiculo({ placa, transportadora, modelo }) {
  const veiculo = { id: `v${Date.now()}`, placa, transportadora, modelo, statusPortaria: "Aguardando" };
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

export function listarCondutores() {
  return estado.condutores;
}

export function cadastrarCondutor({ nome, cpf, transportadora, placaVinculada }) {
  const condutor = { id: `c${Date.now()}`, nome, cpf, transportadora, placaVinculada };
  estado.condutores = [condutor, ...estado.condutores];
  salvar();
  return condutor;
}

export function removerCondutor(id) {
  estado.condutores = estado.condutores.filter(c => c.id !== id);
  salvar();
}

export { EVENTO as EVENTO_MUDANCA };
