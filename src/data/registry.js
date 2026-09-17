// Registro central de fontes de dados reais (marcação → liberação por empresa).
// Pra importar uma NOVA carga de dados no futuro:
//   1. Gerar o JSON no mesmo formato de src/data/datasets/*.json
//      (id, senha, convenio, operador, carga, ciclo, marcadoEm, liberadoEm, esperaHoras)
//   2. Colocar o arquivo em src/data/datasets/<empresa_id>.json
//   3. Adicionar uma linha aqui embaixo
// Nenhum outro componente precisa mudar — VisaoGeral, MapaOperacional e os
// filtros por empresa já leem daqui.
export const EMPRESAS = [
  {
    id: "unitapajos",
    nome: "Unitapajós",
    carregar: () => import("./datasets/unitapajos.json").then(m => m.default)
  },
  {
    id: "tgpm",
    nome: "TGPM",
    carregar: () => import("./datasets/tgpm.json").then(m => m.default)
  },
  {
    id: "hidrovias",
    nome: "Hidrovias",
    carregar: () => import("./datasets/hidrovias.json").then(m => m.default)
  }
];

const cache = {};

// Carrega os registros de uma empresa (com cache em memória) ou de todas
// as empresas combinadas quando empresaId é "todas" / undefined.
export async function carregarRegistros(empresaId) {
  if (!empresaId || empresaId === "todas") {
    const todos = await Promise.all(EMPRESAS.map(e => carregarRegistros(e.id)));
    return todos.flat();
  }
  if (cache[empresaId]) return cache[empresaId];
  const empresa = EMPRESAS.find(e => e.id === empresaId);
  if (!empresa) return [];
  const dados = await empresa.carregar();
  const comEmpresa = dados.map(r => ({ ...r, empresaId, empresaNome: empresa.nome }));
  cache[empresaId] = comEmpresa;
  return comEmpresa;
}
