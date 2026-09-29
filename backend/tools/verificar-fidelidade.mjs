// Confere se as métricas da API batem com as fórmulas originais do frontend.
//
// Roda src/data/metricsEngine.js e src/data/relatorio.js (o código JS de
// verdade, sem cópia) sobre os mesmos datasets e compara campo a campo com
// o que a API devolve. Uso (API rodando e com os 103k registros importados):
//
//   node backend/tools/verificar-fidelidade.mjs [urlDaApi] [email] [senha]
//
// Padrão: http://localhost:5080 auditor@barcalog.local BarcaLog@2026
//
// Observação: esperaHoras é recalculado de (liberadoEm − marcadoEm) com 2
// casas, como faz a coluna computada do banco. O campo esperaHoras gravado
// nos JSON difere em 0,01h em ~1,4% dos registros (a planilha de origem
// tinha frações de segundo que o JSON não guardou).
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const [api = "http://localhost:5080", email = "auditor@barcalog.local", senha = "BarcaLog@2026"] = process.argv.slice(2);
const raiz = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../src/data");
const me = await import(pathToFileURL(path.join(raiz, "metricsEngine.js")));
const rel = await import(pathToFileURL(path.join(raiz, "relatorio.js")));

const TERMINAIS = [["unitapajos", "Unitapajós"], ["tgpm", "TGPM"], ["hidrovias", "Hidrovias"]];
const registros = TERMINAIS.flatMap(([id, nome]) =>
  JSON.parse(fs.readFileSync(path.join(raiz, "datasets", `${id}.json`), "utf8")).map(r => ({
    ...r,
    esperaHoras: Math.round((new Date(r.liberadoEm) - new Date(r.marcadoEm)) / 36000) / 100,
    empresaId: id,
    empresaNome: nome
  })));

const { token } = await (await fetch(`${api}/api/auth/login`, {
  method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, senha })
})).json();
const get = async p => (await fetch(`${api}${p}`, { headers: { Authorization: `Bearer ${token}` } })).json();

// [endpoint, referência JS, chave natural p/ listas cuja ordem de desempate depende da ordem de leitura dos registros]
const casos = [
  ["/api/marcacoes/kpis", rel.obterKpisGerais(registros)],
  ["/api/marcacoes/visao-terminal", me.visaoPorTerminal(registros)],
  ["/api/marcacoes/por-mes", rel.agruparPorMesDetalhado(registros)],
  ["/api/marcacoes/ranking-esperas", rel.rankingMaioresEsperas(registros, 10)],
  ["/api/marcacoes/indice-risco-hora", me.indiceRiscoGargalo(registros)],
  ["/api/marcacoes/janelas-permanencia", me.distribuicaoJanelas(registros)],
  ["/api/marcacoes/tma-terminal", me.tmaPorTerminal(registros)],
  ["/api/marcacoes/indicadores-performance", me.indicadoresPerformance(registros), x => x.terminal],
  ["/api/marcacoes/alertas-operacionais", me.alertasOperacionais(registros)],
  ["/api/marcacoes/concentracao-turno", me.concentracaoPorTurno(registros)],
  ["/api/marcacoes/horarios-criticos", me.horariosCriticos(registros), x => `${x.total}|${x.diaSemana}|${x.turno}`],
  ["/api/marcacoes/picos-entrada-saida", me.picosEntradaSaida(registros)],
  ["/api/marcacoes/analise-preditiva", me.analisePreditiva(registros, 3)],
  ["/api/marcacoes/score-operadores", rel.scoreEficienciaPorOperador(registros)],
  ["/api/marcacoes/atrasos-recorrentes", rel.atrasosRecorrentes(registros, 24, 3)],
  ["/api/marcacoes/por-ciclo", rel.distribuicaoPorCiclo(registros)],
  ["/api/marcacoes/tendencia-sla", rel.tendenciaSLA(registros)],
  ["/api/marcacoes/por-operador", rel.totaisPorOperador(registros)],
  ["/api/marcacoes/por-carga", rel.totaisPorCarga(registros)],
  ["/api/marcacoes/por-terminal", rel.totaisPorEmpresa(registros)],
  ["/api/marcacoes/recomendacoes", me.recomendacoesPrescritivas(registros)],
  ["/api/marcacoes/top-dias", me.top30DiasCompacto(registros), x => `${x.data}|${x.empresa}`],
  ["/api/marcacoes/detalhamento-diario", rel.detalhamentoDiarioPorMes(registros)],
  ["/api/marcacoes/concentracao-turno?terminalId=hidrovias", me.concentracaoPorTurno(registros.filter(r => r.empresaId === "hidrovias"))],
  ["/api/marcacoes/indice-risco-hora?terminalId=tgpm", me.indiceRiscoGargalo(registros.filter(r => r.empresaId === "tgpm"))]
];

// Campos renomeados no porte / intermediários do JS que a API não expõe.
const RENOMEADOS = { id: "movimentoId", empresaNome: "terminalNome", empresaId: "terminalId" };
const IGNORADOS = new Set(["cor", "somaEspera", "obterValor"]);

function comparar(a, b, caminho, difs, chaveOrdem) {
  if (Array.isArray(a)) {
    if (!Array.isArray(b) || a.length !== b.length) return difs.push(`${caminho}: tamanho ${a.length} ≠ ${b?.length}`);
    if (chaveOrdem) { // desempate depende da ordem de leitura dos registros → compara pela chave natural
      const ordenar = l => [...l].sort((x, y) => chaveOrdem(x).localeCompare(chaveOrdem(y)));
      a = ordenar(a); b = ordenar(b);
    }
    a.forEach((x, i) => comparar(x, b[i], `${caminho}[${i}]`, difs));
  } else if (a && typeof a === "object") {
    for (const [k, v] of Object.entries(a)) {
      if (IGNORADOS.has(k)) continue;
      const kb = k in b ? k : RENOMEADOS[k];
      if (!kb || !(kb in b)) { difs.push(`${caminho}.${k}: ausente na API`); continue; }
      comparar(v, b[kb], `${caminho}.${k}`, difs);
    }
  } else if (typeof a === "number") {
    if (typeof b !== "number" || Math.abs(a - b) > 1e-9 * Math.max(1, Math.abs(a))) difs.push(`${caminho}: ${a} ≠ ${b}`);
  } else if ((a ?? null) !== (b ?? null) && !(a === Infinity && b === null)) {
    difs.push(`${caminho}: ${JSON.stringify(a)} ≠ ${JSON.stringify(b)}`);
  }
}

let falhas = 0;
for (const [rota, referencia, chaveOrdem] of casos) {
  const difs = [];
  comparar(JSON.parse(JSON.stringify(referencia, (k, v) => (v === Infinity ? null : v))), await get(rota), "", difs, chaveOrdem);
  console.log(`${difs.length ? "✗" : "✓"} ${rota}${difs.length ? `  (${difs.length} diferenças)` : ""}`);
  difs.slice(0, 5).forEach(d => console.log(`    ${d}`));
  falhas += difs.length ? 1 : 0;
}
console.log(falhas ? `\n${falhas} endpoint(s) divergentes.` : `\nTodos os ${casos.length} endpoints batem com o JS.`);
process.exit(falhas ? 1 : 0);
