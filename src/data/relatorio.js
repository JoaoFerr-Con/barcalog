import marcacoes from "./marcacoesReal.json";

// Todas as funções abaixo trabalham em cima do dataset real importado da
// planilha M8_MARCAC_O_ES_UNITAPAJOS.xlsx (35.843 registros, Jan–Jul/2026).
// Nada aqui é sorteado — é o mesmo dado que já apareceu no PDF
// "Dashboard de Operação - Unitapajós", só que recalculado no cliente
// a partir do dataset bruto, então qualquer corte novo (por operador,
// por carga, por mês) sai daqui em vez de precisar de um PDF novo.

const NOMES_MES = [
  "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho",
  "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
];

function chaveMes(iso) {
  return iso.slice(0, 7); // "2026-05"
}

export function obterKpisGerais() {
  const total = marcacoes.length;
  const dias = new Set(marcacoes.map(m => m.marcadoEm.slice(0, 10)));
  const mediaDiaria = total / dias.size;

  const porMes = agruparPorMes();
  const mesMaisMovimentado = porMes.reduce((a, b) => b.total > a.total ? b : a, porMes[0]);

  const tempoMedioEspera = marcacoes.reduce((s, m) => s + m.esperaHoras, 0) / total;
  const maiorAtraso = marcacoes.reduce((a, b) => b.esperaHoras > a.esperaHoras ? b : a, marcacoes[0]);

  return {
    total,
    mediaDiaria,
    diasOperados: dias.size,
    mesMaisMovimentado,
    tempoMedioEspera,
    maiorAtraso
  };
}

export function agruparPorMes() {
  const mapa = {};
  marcacoes.forEach(m => {
    const chave = chaveMes(m.marcadoEm);
    mapa[chave] = (mapa[chave] || 0) + 1;
  });
  return Object.entries(mapa)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([chave, total]) => {
      const [ano, mesNum] = chave.split("-");
      return {
        chave,
        rotulo: `${NOMES_MES[Number(mesNum) - 1]}/${ano.slice(2)}`,
        total
      };
    });
}

export function rankingMaioresEsperas(limite = 10) {
  return [...marcacoes]
    .sort((a, b) => b.esperaHoras - a.esperaHoras)
    .slice(0, limite);
}

export function totaisPorOperador() {
  const mapa = {};
  marcacoes.forEach(m => {
    mapa[m.operador] = (mapa[m.operador] || 0) + 1;
  });
  return Object.entries(mapa)
    .map(([operador, total]) => ({ operador, total }))
    .sort((a, b) => b.total - a.total);
}

export function totaisPorCarga() {
  const mapa = {};
  marcacoes.forEach(m => {
    mapa[m.carga] = (mapa[m.carga] || 0) + 1;
  });
  return Object.entries(mapa)
    .map(([carga, total]) => ({ carga, total }))
    .sort((a, b) => b.total - a.total);
}

export function marcacoesDoMes(chaveMesAlvo) {
  return marcacoes.filter(m => chaveMes(m.marcadoEm) === chaveMesAlvo);
}

export function formatarHoras(horas) {
  const h = Math.floor(horas);
  const min = Math.round((horas - h) * 60);
  return `${h}h${min > 0 ? ` ${String(min).padStart(2, "0")}min` : ""}`;
}
