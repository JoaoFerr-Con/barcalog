// Métricas operacionais MOCKADAS pro Portal do Transportador — ainda não
// existe vínculo real entre uma transportadora do Sistema de Negativação
// (Rota Amazônia Cargas, etc.) e os operadores dos datasets reais de
// marcação (Bunge/Amaggi/TGPM/Hidrovias), então isso aqui é só pra mostrar
// a FORMA que o dashboard vai ter quando essa integração existir.
// Determinístico por nome (não muda a cada render), pra parecer um dado de
// verdade e não um número aleatório piscando na tela.
function hash(texto) {
  let h = 0;
  for (let i = 0; i < texto.length; i++) {
    h = (h * 31 + texto.charCodeAt(i)) >>> 0;
  }
  return h;
}

const MESES = ["Fev", "Mar", "Abr", "Mai", "Jun", "Jul"];

export function gerarMetricasMock(nomeTransportadora) {
  const base = hash(nomeTransportadora);
  const viagensNoMes = 18 + (base % 40);
  const volumeToneladas = 900 + (base % 2200);
  const tempoMedioHoras = 6 + ((base >> 3) % 14) + ((base % 100) / 100);
  const pontualidade = 78 + ((base >> 5) % 20);
  const tendencia = MESES.map((mes, i) => ({
    mes,
    viagens: Math.max(4, viagensNoMes - (MESES.length - 1 - i) * (2 + (base % 3)) + ((base >> (i + 1)) % 5))
  }));
  return { viagensNoMes, volumeToneladas, tempoMedioHoras, pontualidade, tendencia };
}
