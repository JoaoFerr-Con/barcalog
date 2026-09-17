// Dados MOCK usados apenas na fila operacional ao vivo (Carretas / Portaria / Pesagem / Mapa).
// Isso ainda não vem de sistema real — é só pra essas telas não ficarem vazias enquanto
// não existe uma origem de dados ao vivo (gate real, RFID, etc). O que já é real (marcações
// históricas do Unitapajós) está em ./marcacoesReal.json + ./relatorio.js.

const TRANSPORTADORAS = ["Logística Juruá S/A", "TransNorte Cargas", "Via Rápida Log", "AgroTransportes Sul", "Rota Amazônia Cargas", "Barcarena Transportes", "Norte Grãos Logística", "TransPará Express"];
const CARGAS = ["Soja", "Milho", "Farelo de Soja"];
const JANELAS = ["D0", "D1", "D2", "D3"];
const LISTA_STATUS = ["Aguardando", "No Pátio", "No Porto", "Descarga Finalizada"];
const TERMINAIS = ["Terminal Sul", "Terminal Leste", "Terminal Oeste"];
const ORIGENS = ["Sinop/MT", "Sorriso/MT", "Lucas do Rio Verde/MT", "Rondonópolis/MT", "Marabá/PA", "Paragominas/PA"];
function escolherAleatorio(lista) {
  return lista[Math.floor(Math.random() * lista.length)];
}
function letraAleatoria() {
  return String.fromCharCode(65 + Math.floor(Math.random() * 26));
}
function gerarPlaca(i) {
  return `${letraAleatoria()}${letraAleatoria()}${letraAleatoria()}-${String(1000 + i).slice(-4)}`;
}
function gerarDadosIniciais(quantidade = 48) {
  const linhas = [];
  for (let i = 0; i < quantidade; i++) {
    linhas.push({
      id: `carreta-${i + 1}`,
      placa: gerarPlaca(i),
      transportadora: escolherAleatorio(TRANSPORTADORAS),
      carga: escolherAleatorio(CARGAS),
      volume: 30 + Math.floor(Math.random() * 25),
      origem: escolherAleatorio(ORIGENS),
      terminal: escolherAleatorio(TERMINAIS),
      janela: escolherAleatorio(JANELAS),
      status: escolherAleatorio(LISTA_STATUS),
      criadoEm: new Date(Date.now() - Math.floor(Math.random() * 7) * 86400000 - Math.floor(Math.random() * 20) * 3600000).toISOString()
    });
  }
  return linhas;
}

export {
  TRANSPORTADORAS,
  CARGAS,
  JANELAS,
  LISTA_STATUS,
  TERMINAIS,
  ORIGENS,
  escolherAleatorio,
  gerarPlaca,
  gerarDadosIniciais
};
