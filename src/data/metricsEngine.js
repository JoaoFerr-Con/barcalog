// ====================================================================
// ENGINE DE MÉTRICAS AVANÇADAS — Relatório Executivo de 3 Páginas
// Projetado conforme especificação do Arquiteto de Dados / Lead BI
// ====================================================================
// Todas as funções recebem `registros` já carregados via registry.js.
// Nenhuma depende de backend — roda inteiro no navegador, sobre os
// datasets reais importados (Unitapajós, TGPM, Hidrovias).

// ---------- CONSTANTES OPERACIONAIS ----------
export const CAPACIDADE_DIARIA = 1000;         // carretas/dia por terminal
export const CUSTO_DEMURRAGE_HORA = 12.50;     // R$/hora por carreta retida além do SLA
export const SLA_LIMITE_HORAS = 144;           // 6 dias = estouro crítico
export const JANELAS = [
  { chave: "D0", rotulo: "D0 (0–24h)",    min: 0,   max: 24,  cor: "#22C55E" },
  { chave: "D1", rotulo: "D1 (24–48h)",   min: 24,  max: 48,  cor: "#F59E0B" },
  { chave: "D2", rotulo: "D2 (48–72h)",   min: 48,  max: 72,  cor: "#F97316" },
  { chave: "D3", rotulo: "D3 (72–96h)",   min: 72,  max: 96,  cor: "#EF4444" },
  { chave: "AL", rotulo: "Alerta (96–144h)", min: 96, max: 144, cor: "#DC2626" },
  { chave: "EC", rotulo: "Estouro Crítico (>144h)", min: 144, max: Infinity, cor: "#7F1D1D" }
];

const NOMES_MES = ["Janeiro","Fevereiro","Março","Abril","Maio","Junho","Julho","Agosto","Setembro","Outubro","Novembro","Dezembro"];
const DIAS_SEMANA = ["Dom","Seg","Ter","Qua","Qui","Sex","Sáb"];

function chaveMes(iso) { return iso.slice(0, 7); }

// ---------- A. CICLO DE PERMANÊNCIA E JANELAS ----------
export function classificarJanela(horasEspera) {
  return JANELAS.find(j => horasEspera >= j.min && horasEspera < j.max) || JANELAS[JANELAS.length - 1];
}

export function distribuicaoJanelas(registros) {
  const contagem = {};
  JANELAS.forEach(j => { contagem[j.chave] = 0; });
  registros.forEach(m => {
    const janela = classificarJanela(m.esperaHoras);
    contagem[janela.chave] += 1;
  });
  const total = registros.length || 1;
  return JANELAS.map(j => ({
    ...j,
    total: contagem[j.chave],
    pct: (contagem[j.chave] / total) * 100
  }));
}

export function outliersEstouroCritico(registros, limite = SLA_LIMITE_HORAS) {
  return registros
    .filter(m => m.esperaHoras > limite)
    .sort((a, b) => b.esperaHoras - a.esperaHoras)
    .slice(0, 15);
}

// ---------- B. MÉDIA PONDERADA DE PERMANÊNCIA ----------
// Tp_ponderado = Σ(Permanência_i × Volume_i) / Σ(Volume_i)
// onde Volume_i é o total de carretas do terminal naquele dia.
export function mediaPonderadaPermanencia(registros) {
  const porDia = {};
  registros.forEach(m => {
    const dia = m.marcadoEm.slice(0, 10);
    const terminal = m.empresaNome || m.empresaId || "—";
    const chave = `${dia}|${terminal}`;
    if (!porDia[chave]) porDia[chave] = { somaEspera: 0, n: 0 };
    porDia[chave].somaEspera += m.esperaHoras;
    porDia[chave].n += 1;
  });
  let numerador = 0, denominador = 0;
  Object.values(porDia).forEach(({ somaEspera, n }) => {
    const mediaLocal = somaEspera / n;
    numerador += mediaLocal * n;
    denominador += n;
  });
  return denominador > 0 ? numerador / denominador : 0;
}

// ---------- C. TEORIA DAS FILAS: RITMO OPERACIONAL λ/μ ----------
// λ = taxa de chegada (carretas/hora), μ = taxa de saída (carretas/hora)
// Calculamos por faixa horária pra identificar momentos onde λ > μ.
export function ritmoOperacional(registros) {
  const faixas = Array.from({ length: 24 }, (_, h) => ({
    hora: h,
    chegadas: 0,
    saidas: 0
  }));
  registros.forEach(m => {
    const hChegada = new Date(m.marcadoEm).getHours();
    const hSaida = new Date(m.liberadoEm).getHours();
    faixas[hChegada].chegadas += 1;
    faixas[hSaida].saidas += 1;
  });
  // Normaliza por número de dias no dataset pra ter taxa/hora real
  const dias = new Set(registros.map(m => m.marcadoEm.slice(0, 10))).size || 1;
  return faixas.map(f => ({
    ...f,
    lambda: f.chegadas / dias,  // carretas/hora médio
    mu: f.saidas / dias,
    congestionado: (f.chegadas / dias) > (f.saidas / dias) * 1.15
  }));
}

// Alerta: λ > μ por mais de 2 horas consecutivas
export function alertasRitmo(ritmo) {
  const alertas = [];
  let inicioSequencia = null;
  let sequencia = 0;
  for (let i = 0; i < ritmo.length; i++) {
    if (ritmo[i].lambda > ritmo[i].mu) {
      if (inicioSequencia === null) inicioSequencia = i;
      sequencia++;
    } else {
      if (sequencia >= 2) {
        alertas.push({ de: inicioSequencia, ate: i - 1, duracaoHoras: sequencia });
      }
      inicioSequencia = null;
      sequencia = 0;
    }
  }
  if (sequencia >= 2) alertas.push({ de: inicioSequencia, ate: 23, duracaoHoras: sequencia });
  return alertas;
}

// ---------- D. CUSTO DE DEMURRAGE ----------
export function custoTotalDemurrage(registros, limiteHoras = SLA_LIMITE_HORAS, custoHora = CUSTO_DEMURRAGE_HORA) {
  let custoTotal = 0;
  let carretasRetidas = 0;
  registros.forEach(m => {
    if (m.esperaHoras > limiteHoras) {
      custoTotal += (m.esperaHoras - limiteHoras) * custoHora;
      carretasRetidas++;
    }
  });
  return { custoTotal, carretasRetidas };
}

// ---------- E. CONCENTRAÇÃO DE TRÁFEGO POR TURNO ----------
// Substitui o heatmap (que ficava poluído no PDF com muitos números) por
// uma tabela limpa de 4 turnos do dia, mostrando apenas o volume e o
// percentual de retenção (>24h) de cada faixa — mais legível pra impressão.
const TURNOS_DO_DIA = [
  { rotulo: "Madrugada (00–06h)", min: 0, max: 6 },
  { rotulo: "Manhã (06–12h)", min: 6, max: 12 },
  { rotulo: "Tarde (12–18h)", min: 12, max: 18 },
  { rotulo: "Noite (18–00h)", min: 18, max: 24 }
];

export function concentracaoPorTurno(registros) {
  const turnos = TURNOS_DO_DIA.map(t => ({ ...t, total: 0, retidos: 0 }));
  registros.forEach(m => {
    const hora = new Date(m.marcadoEm).getHours();
    const idx = TURNOS_DO_DIA.findIndex(t => hora >= t.min && hora < t.max);
    if (idx >= 0) {
      turnos[idx].total += 1;
      if (m.esperaHoras > 24) turnos[idx].retidos += 1;
    }
  });
  return turnos.map(t => ({
    ...t,
    pctRetencao: t.total > 0 ? (t.retidos / t.total) * 100 : 0
  }));
}

// Top 3 faixas horárias com maior acúmulo, por dia da semana — versão
// resumida que substitui a grade 6×7 do heatmap antigo.
export function horariosCriticos(registros) {
  const grade = {};
  registros.forEach(m => {
    if (m.esperaHoras < 24) return;
    const dt = new Date(m.marcadoEm);
    const diaSemana = DIAS_SEMANA[dt.getDay()];
    const turnoIdx = TURNOS_DO_DIA.findIndex(t => dt.getHours() >= t.min && dt.getHours() < t.max);
    if (turnoIdx < 0) return;
    const chave = `${diaSemana}|${TURNOS_DO_DIA[turnoIdx].rotulo}`;
    grade[chave] = (grade[chave] || 0) + 1;
  });
  return Object.entries(grade)
    .map(([chave, total]) => {
      const [diaSemana, turno] = chave.split("|");
      return { diaSemana, turno, total };
    })
    .sort((a, b) => b.total - a.total)
    .slice(0, 6);
}

// ---------- F. CAPACIDADE E SATURAÇÃO ----------
export function matrizVolumeMensal(registros) {
  const mapa = {};
  const empresas = new Set();
  registros.forEach(m => {
    const mes = chaveMes(m.marcadoEm);
    const empresa = m.empresaNome || m.empresaId || "—";
    empresas.add(empresa);
    const chave = `${mes}|${empresa}`;
    mapa[chave] = (mapa[chave] || 0) + 1;
  });
  const meses = [...new Set(registros.map(m => chaveMes(m.marcadoEm)))].sort();
  const listaEmpresas = [...empresas].sort();
  return {
    meses: meses.map(m => {
      const [ano, mesNum] = m.split("-");
      return { chave: m, rotulo: `${NOMES_MES[Number(mesNum) - 1].slice(0,3)}/${ano.slice(2)}` };
    }),
    empresas: listaEmpresas,
    obterValor: (mes, empresa) => mapa[`${mes}|${empresa}`] || 0
  };
}

export function capacidadeDiaria(registros) {
  const porDia = {};
  registros.forEach(m => {
    const dia = m.marcadoEm.slice(0, 10);
    porDia[dia] = (porDia[dia] || 0) + 1;
  });
  const dias = Object.entries(porDia)
    .map(([data, total]) => ({ data, total, pctCapacidade: (total / CAPACIDADE_DIARIA) * 100 }))
    .sort((a, b) => b.total - a.total);
  return dias;
}

// ---------- G. TOP 30 DIAS (COMPACTO) ----------
export function top30DiasCompacto(registros) {
  const porDiaTerminal = {};
  registros.forEach(m => {
    const data = m.marcadoEm.slice(0, 10);
    const empresa = m.empresaNome || m.empresaId || "—";
    const chave = `${data}|${empresa}`;
    if (!porDiaTerminal[chave]) porDiaTerminal[chave] = { data, empresa, total: 0 };
    porDiaTerminal[chave].total += 1;
  });
  return Object.values(porDiaTerminal)
    .sort((a, b) => b.total - a.total)
    .slice(0, 30)
    .map(d => ({
      ...d,
      pctCapacidade: (d.total / CAPACIDADE_DIARIA) * 100,
      gargalo: d.total > CAPACIDADE_DIARIA * 0.8
    }));
}

// ---------- H. ALERTAS PREDITIVOS (7 DIAS) ----------
// Projeção simples: se a tendência dos últimos 7 dias continuar,
// em quantos dias o terminal satura?
export function alertasSaturacao(registros) {
  const agora = new Date(Math.max(...registros.map(m => new Date(m.marcadoEm).getTime())));
  const limite7d = new Date(agora.getTime() - 7 * 86400000);
  const recentes = registros.filter(m => new Date(m.marcadoEm) >= limite7d);
  const porDia = {};
  recentes.forEach(m => {
    const dia = m.marcadoEm.slice(0, 10);
    porDia[dia] = (porDia[dia] || 0) + 1;
  });
  const volumes = Object.values(porDia);
  if (volumes.length < 3) return [];
  const mediaRecente = volumes.reduce((a, b) => a + b, 0) / volumes.length;
  const tendenciaDiaria = volumes.length > 1
    ? (volumes[volumes.length - 1] - volumes[0]) / (volumes.length - 1)
    : 0;
  const diasParaSaturacao = tendenciaDiaria > 0
    ? Math.ceil((CAPACIDADE_DIARIA - mediaRecente) / tendenciaDiaria)
    : null;
  return [{
    tipo: mediaRecente > CAPACIDADE_DIARIA * 0.8 ? "critico" : tendenciaDiaria > 0 ? "atencao" : "estavel",
    mediaRecente: Math.round(mediaRecente),
    tendenciaDiaria: Math.round(tendenciaDiaria),
    diasParaSaturacao,
    mensagem: mediaRecente > CAPACIDADE_DIARIA * 0.8
      ? `Volume médio recente (${Math.round(mediaRecente)}/dia) está acima de 80% da capacidade nominal. Risco de saturação iminente.`
      : tendenciaDiaria > 5
        ? `Tendência de alta: +${Math.round(tendenciaDiaria)} carretas/dia. Saturação projetada em ~${diasParaSaturacao || "?"} dias se mantido o ritmo.`
        : `Operação estável. Volume médio recente: ${Math.round(mediaRecente)}/dia (${((mediaRecente / CAPACIDADE_DIARIA) * 100).toFixed(0)}% da capacidade).`
  }];
}

// ---------- I. RECOMENDAÇÕES PRESCRITIVAS ----------
export function recomendacoesPrescritivas(registros) {
  const recs = [];
  const dist = distribuicaoJanelas(registros);
  const pctEstouro = dist.find(d => d.chave === "EC")?.pct || 0;
  const pctAlerta = dist.find(d => d.chave === "AL")?.pct || 0;
  if (pctEstouro > 0.5) {
    recs.push({
      prioridade: "alta",
      acao: "Revisar agendamento de descargas",
      detalhe: `${pctEstouro.toFixed(1)}% dos veículos permanecem >144h. Recomenda-se redistribuir agendamentos entre terminais e ampliar janela D0 para reduzir acúmulo.`
    });
  }
  if (pctAlerta > 2) {
    recs.push({
      prioridade: "media",
      acao: "Monitorar trocas de turno",
      detalhe: `${pctAlerta.toFixed(1)}% dos veículos ficam entre 96–144h. Verificar se gargalos coincidem com trocas de turno ou finais de semana.`
    });
  }
  const ritmo = ritmoOperacional(registros);
  const horasCongestionadas = ritmo.filter(r => r.congestionado);
  if (horasCongestionadas.length > 4) {
    recs.push({
      prioridade: "media",
      acao: "Rebalancear horários de entrada",
      detalhe: `${horasCongestionadas.length} faixas horárias com λ > μ. Considerar escalonar chegadas para horários de menor demanda (madrugada/manhã cedo).`
    });
  }
  if (recs.length === 0) {
    recs.push({
      prioridade: "baixa",
      acao: "Operação dentro dos parâmetros",
      detalhe: "Nenhum gargalo crítico identificado no período. Manter monitoramento contínuo."
    });
  }
  return recs;
}

// ---------- UTILIDADES ----------
export function formatarHoras(horas) {
  const h = Math.floor(horas);
  const min = Math.round((horas - h) * 60);
  return `${h}h${min > 0 ? ` ${String(min).padStart(2, "0")}min` : ""}`;
}

export function formatarBRL(valor) {
  return valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

export function formatarDataPtBR(dataISO) {
  const [ano, mes, dia] = dataISO.split("-");
  return `${dia}/${mes}/${ano}`;
}
