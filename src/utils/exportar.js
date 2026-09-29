// Exportação client-side, sem backend. CSV é gerado na hora a partir dos
// dados já carregados; PDF usa a caixa de impressão do navegador (a janela
// "Imprimir" tem opção "Salvar como PDF" em qualquer navegador moderno) —
// evita adicionar uma biblioteca pesada só pra isso.

// Injeção de fórmula (CSV injection): um texto que começa com = + - @ (ou
// tab/CR) é executado como fórmula pelo Excel/LibreOffice ao abrir o arquivo —
// ex.: uma descrição de ocorrência "=HYPERLINK(...)". Prefixamos com ' pra que
// vire texto. Números de verdade (ex.: -3.5) não são alterados.
function celulaSegura(valor) {
  if (typeof valor === "number") return String(valor);
  const texto = String(valor ?? "");
  return /^[=+\-@\t\r]/.test(texto) ? `'${texto}` : texto;
}

export function exportarCSV(linhas, colunas, nomeArquivo) {
  const cabecalho = colunas.map(c => `"${celulaSegura(c.rotulo).replace(/"/g, '""')}"`).join(";");
  const corpo = linhas.map(linha =>
    colunas.map(c => {
      const valor = typeof c.valor === "function" ? c.valor(linha) : linha[c.chave];
      return `"${celulaSegura(valor).replace(/"/g, '""')}"`;
    }).join(";")
  ).join("\n");
  const csv = `${cabecalho}\n${corpo}`;
  const blob = new Blob(["\uFEFF" + csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = nomeArquivo.endsWith(".csv") ? nomeArquivo : `${nomeArquivo}.csv`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

export function exportarPDF() {
  window.print();
}
