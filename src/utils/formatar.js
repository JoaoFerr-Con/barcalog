// Formatação de exibição (os cálculos em si vêm da API).

export function formatarHoras(horas) {
  if (horas == null || Number.isNaN(horas)) return "—";
  const h = Math.floor(horas);
  const min = Math.round((horas - h) * 60);
  return `${h}h${min > 0 ? ` ${String(min).padStart(2, "0")}min` : ""}`;
}

export function formatarBRL(valor) {
  return (valor ?? 0).toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

export function formatarDataPtBR(dataISO) {
  if (!dataISO) return "—";
  const [ano, mes, dia] = dataISO.slice(0, 10).split("-");
  return `${dia}/${mes}/${ano}`;
}

/** Data de hoje no fuso do navegador, formato yyyy-MM-dd (inputs type="date" e API). */
export function hojeISO() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
