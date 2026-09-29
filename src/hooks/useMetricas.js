import { api } from "../api/cliente.js";
import { useApi } from "./useApi.js";
import { adaptarRegistro } from "../api/rotulos.js";

// Métricas calculadas na API (porte fiel de metricsEngine.js/relatorio.js —
// validado por backend/tools/verificar-fidelidade.mjs). O navegador não baixa
// mais os 103 mil registros: busca só os agregados, em paralelo.

const M = "/api/v1/marcacoes";

async function buscarPainel(terminalId, sinal) {
  const p = { terminalId };
  const g = (rota, extra) => api.get(`${M}/${rota}`, { ...p, ...extra }, sinal);
  const [
    kpis, porMesDet, porEmpresa, porOperador, porCarga, top10, recentes, preditiva, tma, sla,
    scoreOp, recorrentes, janelas, desempenho, terminais, alertas, agora
  ] = await Promise.all([
    g("kpis"), g("por-mes"), g("por-terminal"), g("por-operador"), g("por-carga"),
    g("ranking-esperas", { limite: 10 }), api.get(M, { ...p, tamanhoPagina: 8 }, sinal), g("analise-preditiva", { meses: 3 }),
    g("tma-terminal"), g("tendencia-sla"), g("score-operadores"), g("atrasos-recorrentes"),
    g("janelas-permanencia"), g("indicadores-performance"), g("visao-terminal"), g("alertas-operacionais"),
    api.get("/api/v1/portaria/operacao-agora", null, sinal)
  ]);
  return {
    kpis,
    porMesDet: porMesDet || [],
    porMes: (porMesDet || []).map(m => ({ chave: m.chave, rotulo: m.rotulo, total: m.total })),
    porEmpresa, porOperador, porCarga,
    top10: (top10 || []).map(adaptarRegistro),
    recentes: (recentes?.itens || []).map(m => ({
      id: m.movimentoId, convenio: m.convenio, empresaNome: m.terminal, empresaId: m.terminalId,
      marcadoEm: m.dataMarcacao, esperaHoras: m.esperaHoras
    })),
    preditiva: preditiva || { projecoes: [] },
    tma, sla, scoreOp, recorrentes, janelas, desempenho, terminais, alertas, agora
  };
}

/** Todos os indicadores da Visão Geral pro recorte (terminal). */
export function usePainel(terminalId) {
  return useApi(sinal => buscarPainel(terminalId, sinal), [terminalId]);
}

/** Uma métrica isolada, ex.: useMetrica("picos-entrada-saida", { terminalId, mes }). */
export function useMetrica(rota, parametros = {}) {
  const chave = JSON.stringify(parametros);
  return useApi(sinal => api.get(`${M}/${rota}`, parametros, sinal), [rota, chave]);
}

/** Dados do relatório executivo em PDF (buscados só quando o usuário pede o PDF). */
export async function buscarRelatorio(terminalId) {
  const p = { terminalId };
  const g = rota => api.get(`${M}/${rota}`, p);
  const [kpis, porMes, matriz, top30, janelas, outliers, turnos, criticos, ritmo, alertasSat, recs, business] = await Promise.all([
    g("kpis"), g("por-mes"), g("matriz-volume-mensal"), g("top-dias"), g("janelas-permanencia"), g("estouro-critico"),
    g("concentracao-turno"), g("horarios-criticos"), g("ritmo-operacional"), g("alertas-saturacao"), g("recomendacoes"), g("business-case")
  ]);
  return {
    kpis, porMes, top30, janelas, turnos, criticos, alertasSat, recs,
    outliers: (outliers || []).map(adaptarRegistro),
    ritmo: ritmo.faixas,
    alertasRitmo: ritmo.alertas,
    matriz: {
      meses: matriz.meses,
      empresas: matriz.empresas,
      obterValor: (mes, empresa) => matriz.valores?.[mes]?.[empresa] || 0
    },
    tmpPonderado: kpis?.tempoMedioPonderado ?? 0,
    demurrage: business.demurrage,
    custoAmpliado: business.custoAmpliado,
    fatoresRho: business.fatoresUtilizacao,
    etapasSLA: business.slaPorEtapa,
    roi: business.roi,
    esg: business.esg
  };
}
