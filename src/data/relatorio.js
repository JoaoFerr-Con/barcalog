// Funções de agregação sobre um array de registros de marcação/liberação.
// Não importam mais um dataset fixo — recebem `registros` já carregados via
// registry.js (carregarRegistros), o que permite: filtrar por empresa,
// combinar empresas, e plugar uma nova fonte de dados sem tocar aqui.

const NOMES_MES = [
  "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho",
  "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
];

function chaveMes(iso) {
  return iso.slice(0, 7); // "2026-05"
}

export function obterKpisGerais(registros) {
  if (!registros.length) return null;
  const total = registros.length;
  const dias = new Set(registros.map(m => m.marcadoEm.slice(0, 10)));
  const mediaDiaria = total / dias.size;

  const porMes = agruparPorMes(registros);
  const mesMaisMovimentado = porMes.reduce((a, b) => b.total > a.total ? b : a, porMes[0]);

  const tempoMedioEspera = registros.reduce((s, m) => s + m.esperaHoras, 0) / total;
  const maiorAtraso = registros.reduce((a, b) => b.esperaHoras > a.esperaHoras ? b : a, registros[0]);

  return {
    total,
    mediaDiaria,
    diasOperados: dias.size,
    mesMaisMovimentado,
    tempoMedioEspera,
    maiorAtraso
  };
}

export function agruparPorMes(registros) {
  const mapa = {};
  registros.forEach(m => {
    const chave = chaveMes(m.marcadoEm);
    mapa[chave] = (mapa[chave] || 0) + 1;
  });
  return Object.entries(mapa)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([chave, total]) => {
      const [ano, mesNum] = chave.split("-");
      return { chave, rotulo: `${NOMES_MES[Number(mesNum) - 1]}/${ano.slice(2)}`, total };
    });
}

// Igual a agruparPorMes, mas com as colunas extras do relatório de
// impressão (representatividade %, dias operados, média diária) — mantido
// separado pra não pesar quem só precisa do total por mês.
export function agruparPorMesDetalhado(registros) {
  const porMes = agruparPorMes(registros);
  const totalGeral = registros.length;
  const diasPorMes = {};
  registros.forEach(m => {
    const chave = chaveMes(m.marcadoEm);
    const dia = m.marcadoEm.slice(0, 10);
    if (!diasPorMes[chave]) diasPorMes[chave] = new Set();
    diasPorMes[chave].add(dia);
  });
  return porMes.map(m => {
    const diasOperados = diasPorMes[m.chave] ? diasPorMes[m.chave].size : 0;
    return {
      ...m,
      representatividade: totalGeral ? (m.total / totalGeral) * 100 : 0,
      diasOperados,
      mediaDiaria: diasOperados ? m.total / diasOperados : 0
    };
  });
}

export function rankingMaioresEsperas(registros, limite = 10) {
  return [...registros]
    .sort((a, b) => b.esperaHoras - a.esperaHoras)
    .slice(0, limite);
}

export function totaisPorOperador(registros) {
  const mapa = {};
  registros.forEach(m => { mapa[m.operador] = (mapa[m.operador] || 0) + 1; });
  return Object.entries(mapa)
    .map(([operador, total]) => ({ operador, total }))
    .sort((a, b) => b.total - a.total);
}

export function totaisPorCarga(registros) {
  const mapa = {};
  registros.forEach(m => { mapa[m.carga] = (mapa[m.carga] || 0) + 1; });
  return Object.entries(mapa)
    .map(([carga, total]) => ({ carga, total }))
    .sort((a, b) => b.total - a.total);
}

export function totaisPorEmpresa(registros) {
  const mapa = {};
  registros.forEach(m => {
    const chave = m.empresaNome || m.empresaId || "—";
    if (!mapa[chave]) mapa[chave] = { total: 0, somaEspera: 0 };
    mapa[chave].total += 1;
    mapa[chave].somaEspera += m.esperaHoras;
  });
  return Object.entries(mapa)
    .map(([empresa, v]) => ({
      empresa,
      total: v.total,
      esperaMedia: v.somaEspera / v.total
    }))
    .sort((a, b) => b.total - a.total);
}

export function marcacoesDoMes(registros, chaveMesAlvo) {
  return registros.filter(m => chaveMes(m.marcadoEm) === chaveMesAlvo);
}

// ---------- Projeção de volume ----------
// Regressão linear simples (mínimos quadrados) sobre o histórico mensal.
// É uma projeção estatística de tendência, não um modelo de IA — deixamos
// isso explícito na tela pra não prometer mais do que é.
export function projetarVolume(porMes, mesesAFrente = 2) {
  const n = porMes.length;
  if (n < 3) return [];
  const xs = porMes.map((_, i) => i);
  const ys = porMes.map(m => m.total);
  const mediaX = xs.reduce((a, b) => a + b, 0) / n;
  const mediaY = ys.reduce((a, b) => a + b, 0) / n;
  let numerador = 0, denominador = 0;
  for (let i = 0; i < n; i++) {
    numerador += (xs[i] - mediaX) * (ys[i] - mediaY);
    denominador += (xs[i] - mediaX) ** 2;
  }
  const inclinacao = denominador === 0 ? 0 : numerador / denominador;
  const intercepto = mediaY - inclinacao * mediaX;

  const ultimaChave = porMes[n - 1].chave;
  const [anoUlt, mesUlt] = ultimaChave.split("-").map(Number);

  const projecao = [];
  for (let p = 1; p <= mesesAFrente; p++) {
    const totalPrevisto = Math.max(0, Math.round(intercepto + inclinacao * (n - 1 + p)));
    let mes = mesUlt + p, ano = anoUlt;
    while (mes > 12) { mes -= 12; ano += 1; }
    projecao.push({
      chave: `${ano}-${String(mes).padStart(2, "0")}`,
      rotulo: `${NOMES_MES[mes - 1]}/${String(ano).slice(2)}`,
      total: totalPrevisto,
      projetado: true
    });
  }
  return { tendencia: inclinacao >= 0 ? "alta" : "queda", pontos: projecao };
}

// ---------- Horário de pico ----------
export function porHoraDoDia(registros) {
  const baldes = Array.from({ length: 24 }, (_, h) => ({ hora: h, total: 0 }));
  registros.forEach(m => {
    const hora = new Date(m.marcadoEm).getHours();
    baldes[hora].total += 1;
  });
  return baldes;
}

const DIAS_SEMANA = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];

export function porDiaDaSemana(registros) {
  const baldes = DIAS_SEMANA.map(rotulo => ({ rotulo, total: 0 }));
  registros.forEach(m => {
    const dia = new Date(m.marcadoEm).getDay();
    baldes[dia].total += 1;
  });
  return baldes;
}

// ---------- Alerta de tendência de SLA ----------
// Compara a espera média do último mês com dados contra a média dos 3
// meses anteriores. Não classifica "bom/ruim" em termos absolutos — só
// mostra a direção da variação, que é o que dá pra afirmar com esse dado.
export function tendenciaSLA(registros) {
  const porMes = agruparPorMes(registros);
  if (porMes.length < 2) return null;
  const mapaEspera = {};
  registros.forEach(m => {
    const chave = chaveMes(m.marcadoEm);
    if (!mapaEspera[chave]) mapaEspera[chave] = { soma: 0, n: 0 };
    mapaEspera[chave].soma += m.esperaHoras;
    mapaEspera[chave].n += 1;
  });
  const chavesOrdenadas = porMes.map(m => m.chave);
  const ultimaChave = chavesOrdenadas[chavesOrdenadas.length - 1];
  const anteriores = chavesOrdenadas.slice(Math.max(0, chavesOrdenadas.length - 4), chavesOrdenadas.length - 1);
  if (anteriores.length === 0) return null;

  const esperaUltimoMes = mapaEspera[ultimaChave].soma / mapaEspera[ultimaChave].n;
  const somaAnteriores = anteriores.reduce((s, c) => s + mapaEspera[c].soma, 0);
  const nAnteriores = anteriores.reduce((s, c) => s + mapaEspera[c].n, 0);
  const esperaMediaAnterior = somaAnteriores / nAnteriores;

  const variacaoPct = esperaMediaAnterior === 0 ? 0 : ((esperaUltimoMes - esperaMediaAnterior) / esperaMediaAnterior) * 100;
  return {
    ultimoMesRotulo: porMes[porMes.length - 1].rotulo,
    esperaUltimoMes,
    esperaMediaAnterior,
    variacaoPct,
    piorou: variacaoPct > 2,
    melhorou: variacaoPct < -2
  };
}

// ---------- Score de eficiência por operador ----------
// Combina volume (maior = melhor), espera média (menor = melhor) e
// consistência/desvio padrão da espera (menor = melhor) num índice 0–100.
// É um índice comparativo entre os operadores do próprio recorte, não uma
// nota absoluta.
export function scoreEficienciaPorOperador(registros) {
  const porOperador = {};
  registros.forEach(m => {
    if (!porOperador[m.operador]) porOperador[m.operador] = [];
    porOperador[m.operador].push(m.esperaHoras);
  });
  const operadores = Object.entries(porOperador).map(([operador, esperas]) => {
    const total = esperas.length;
    const media = esperas.reduce((a, b) => a + b, 0) / total;
    const variancia = esperas.reduce((s, e) => s + (e - media) ** 2, 0) / total;
    const desvio = Math.sqrt(variancia);
    return { operador, total, esperaMedia: media, desvio };
  });
  if (operadores.length === 0) return [];

  const maiorVolume = Math.max(...operadores.map(o => o.total));
  const maiorEspera = Math.max(...operadores.map(o => o.esperaMedia));
  const maiorDesvio = Math.max(...operadores.map(o => o.desvio), 1);

  return operadores.map(o => {
    const notaVolume = maiorVolume === 0 ? 0 : (o.total / maiorVolume) * 100;
    const notaEspera = maiorEspera === 0 ? 100 : (1 - o.esperaMedia / maiorEspera) * 100;
    const notaConsistencia = (1 - o.desvio / maiorDesvio) * 100;
    const score = notaVolume * 0.4 + notaEspera * 0.4 + notaConsistencia * 0.2;
    return { ...o, score: Math.round(Math.max(0, Math.min(100, score))) };
  }).sort((a, b) => b.score - a.score);
}

// ---------- Atrasos recorrentes ----------
// Diferente do ranking de "maiores esperas" (que pega os piores casos
// isolados), isto agrupa por convênio e conta quantas vezes ele ultrapassou
// o limite — sinaliza um padrão recorrente, não um outlier de uma vez só.
export function atrasosRecorrentes(registros, limiteHoras = 24, minOcorrencias = 3) {
  const mapa = {};
  registros.forEach(m => {
    if (m.esperaHoras < limiteHoras) return;
    if (!mapa[m.convenio]) mapa[m.convenio] = { convenio: m.convenio, empresaNome: m.empresaNome, ocorrencias: 0, somaEspera: 0 };
    mapa[m.convenio].ocorrencias += 1;
    mapa[m.convenio].somaEspera += m.esperaHoras;
  });
  return Object.values(mapa)
    .filter(c => c.ocorrencias >= minOcorrencias)
    .map(c => ({ ...c, esperaMedia: c.somaEspera / c.ocorrencias }))
    .sort((a, b) => b.ocorrencias - a.ocorrencias);
}

export function formatarHoras(horas) {
  const h = Math.floor(horas);
  const min = Math.round((horas - h) * 60);
  return `${h}h${min > 0 ? ` ${String(min).padStart(2, "0")}min` : ""}`;
}

// ---------- Ciclo ----------
// "ciclo" vem direto da planilha original e nunca tinha sido usado em
// nenhuma tela — indica quantas vezes aquela senha/ticket já circulou pelo
// terminal. Agrupamos em baldes (1, 2, 3, 4, 5+) pra ver se repetição alta
// vem acompanhada de mais ou menos espera.
export function distribuicaoPorCiclo(registros) {
  const baldes = { "1": [], "2": [], "3": [], "4": [], "5+": [] };
  registros.forEach(m => {
    const chave = m.ciclo >= 5 ? "5+" : String(m.ciclo);
    if (baldes[chave]) baldes[chave].push(m.esperaHoras);
  });
  return Object.entries(baldes).map(([balde, esperas]) => ({
    balde,
    total: esperas.length,
    esperaMedia: esperas.length ? esperas.reduce((a, b) => a + b, 0) / esperas.length : 0
  }));
}

// ---------- Volume por dia (base do relatório de impressão) ----------
// Cada registro é um movimento = uma carreta. Agrupamos por dia real
// (não por mês) e por empresa/terminal, já que "capacidade diária" e
// "dia de maior fluxo" só fazem sentido nesse nível de detalhe.
export function agruparPorDiaEmpresa(registros) {
  const mapa = {};
  registros.forEach(m => {
    const data = m.marcadoEm.slice(0, 10); // "2026-05-15"
    const empresa = m.empresaNome || m.empresaId || "—";
    const chave = `${data}|${empresa}`;
    if (!mapa[chave]) mapa[chave] = { data, empresa, total: 0 };
    mapa[chave].total += 1;
  });
  return Object.values(mapa).sort((a, b) => b.total - a.total);
}

export function formatarDataPtBR(dataISO) {
  const [ano, mes, dia] = dataISO.split("-");
  return `${dia}/${mes}/${ano}`;
}

// ---------- Capacidade operacional ----------
// Limite de referência informado: 1.000 carretas/dia por terminal. Dias
// que passam disso entram na lista de excedentes.
export const CAPACIDADE_DIARIA_CARRETAS = 1000;

export function diasQueExcederamCapacidade(registros, capacidade = CAPACIDADE_DIARIA_CARRETAS) {
  return agruparPorDiaEmpresa(registros).filter(d => d.total > capacidade);
}
