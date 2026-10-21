// Formatação de exibição (os cálculos em si vêm da API).

const BR = "pt-BR";

/** 13.93 → "13h56" (padrão do painel). Nulo → "—". */
export function duracao(horas) {
  if (horas == null || Number.isNaN(horas)) return "—";
  const totalMin = Math.round(horas * 60);
  const sinal = totalMin < 0 ? "−" : "";
  const m = Math.abs(totalMin);
  return `${sinal}${Math.floor(m / 60)}h${String(m % 60).padStart(2, "0")}`;
}

/** Diferença de durações com sinal: +2h18 / −1h45. */
export function variacaoDuracao(atual, anterior) {
  if (atual == null || anterior == null) return null;
  const d = atual - anterior;
  return (d > 0 ? "+" : d < 0 ? "−" : "") + duracao(Math.abs(d));
}

export function numero(valor, casas = 0) {
  if (valor == null || Number.isNaN(valor)) return "—";
  return Number(valor).toLocaleString(BR, { minimumFractionDigits: casas, maximumFractionDigits: casas });
}

export function percentual(valor, casas = 1) {
  return valor == null ? "—" : `${numero(valor, casas)}%`;
}

/** Variação percentual com sinal e base válida: −18,7%. */
export function variacaoPct(atual, anterior, casas = 1) {
  if (atual == null || !anterior) return null;
  const v = ((atual - anterior) / anterior) * 100;
  return `${v > 0 ? "+" : v < 0 ? "−" : ""}${numero(Math.abs(v), casas)}%`;
}

/** "2026-08-11" ou "2026-08-11T09:10:00" → "11/08/2026". */
export function formatarDataPtBR(dataISO) {
  if (!dataISO) return "—";
  const [ano, mes, dia] = String(dataISO).slice(0, 10).split("-");
  return `${dia}/${mes}/${ano}`;
}

export function dataCurta(dataISO) {
  if (!dataISO) return "—";
  const [, mes, dia] = String(dataISO).slice(0, 10).split("-");
  return `${dia}/${mes}`;
}

/**
 * Data/hora do PORTO (as marcações vêm sem fuso, no horário local do porto):
 * "2026-08-12T11:55:41" → "12/08/2026 11:55". Não converte fuso.
 */
export function dataHoraPorto(valor, { segundos = false, ano = true } = {}) {
  if (!valor) return "—";
  const s = String(valor);
  const [d, h = ""] = s.split("T");
  const [aa, mm, dd] = d.split("-");
  const hora = h.slice(0, segundos ? 8 : 5);
  return `${dd}/${mm}${ano ? `/${aa}` : ""}${hora ? ` ${hora}` : ""}`;
}

/** Timestamp UTC da API ("...Z") → horário local do navegador. */
export function dataHoraLocal(valorUtc) {
  if (!valorUtc) return "—";
  const d = new Date(valorUtc);
  return `${d.toLocaleDateString(BR)} ${d.toLocaleTimeString(BR, { hour: "2-digit", minute: "2-digit" })}`;
}

const MESES = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];
const MESES_LONGOS = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];

/** "2026-08" → "ago/26". */
export function mesCurto(chave) {
  const [a, m] = chave.split("-");
  return `${MESES[Number(m) - 1]}/${a.slice(2)}`;
}

/** "2026-08" → "agosto de 2026". */
export function mesLongo(chave) {
  const [a, m] = chave.split("-");
  return `${MESES_LONGOS[Number(m) - 1]} de ${a}`;
}

/** Primeiro e último dia do mês "2026-08" → ["2026-08-01", "2026-08-31"]. */
export function limitesDoMes(chave) {
  const [a, m] = chave.split("-").map(Number);
  const ultimo = new Date(a, m, 0).getDate();
  return [`${chave}-01`, `${chave}-${String(ultimo).padStart(2, "0")}`];
}

export function mesAnterior(chave) {
  const [a, m] = chave.split("-").map(Number);
  const d = new Date(a, m - 2, 1);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`;
}

export function tamanhoArquivo(bytes) {
  if (bytes == null) return "—";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${numero(bytes / 1024, 0)} KB`;
  return `${numero(bytes / 1024 / 1024, 1)} MB`;
}

// ----- compatibilidade com telas antigas -----

export function formatarHoras(horas) {
  return duracao(horas);
}

export function formatarBRL(valor) {
  return (valor ?? 0).toLocaleString(BR, { style: "currency", currency: "BRL" });
}

/** Data de hoje no fuso do navegador, formato yyyy-MM-dd (inputs type="date" e API). */
export function hojeISO() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
