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

export function formatarHoras(horas) {
  const h = Math.floor(horas);
  const min = Math.round((horas - h) * 60);
  return `${h}h${min > 0 ? ` ${String(min).padStart(2, "0")}min` : ""}`;
}
