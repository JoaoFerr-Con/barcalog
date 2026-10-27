using BarcaLog.Application.Metricas;
using BarcaLog.Application.Relatorios;
using ClosedXML.Excel;

namespace BarcaLog.Infrastructure.Relatorios;

/// <summary>PDF (PDFsharp, MIT) e Excel (ClosedXML, MIT) a partir do mesmo documento da prévia.</summary>
public sealed class RenderizadorRelatorio : IRenderizadorRelatorio
{
    public byte[] Pdf(DocumentoRelatorio documento) => PdfRelatorio.Gerar(documento);

    public byte[] Excel(DocumentoRelatorio d, IReadOnlyList<RegistroAnalise> movimentos)
    {
        using var wb = new XLWorkbook();
        wb.Properties.Title = $"{d.Titulo} — {d.PeriodoRotulo}";
        wb.Properties.Author = "BarcaLog";
        var nomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var navy = XLColor.FromHtml("#102A4C");
        var ambar = XLColor.FromHtml("#FFF4E0");

        // ---- Resumo
        var r = wb.Worksheets.Add(Nome("Resumo", nomes));
        r.Cell(1, 1).Value = d.Titulo;
        r.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(navy);
        r.Cell(2, 1).Value = d.Subtitulo;
        r.Cell(2, 1).Style.Font.SetFontColor(XLColor.FromHtml("#4A5870"));
        var linha = 4;
        foreach (var (rot, val) in new (string, string)[]
                 {
                     ("Período", d.PeriodoRotulo), ("Abrangência", d.Abrangencia), ("Emitido em", d.GeradoEm.ToString("dd/MM/yyyy HH:mm")),
                     ("Código", d.Codigo ?? "prévia"), ("Fonte dos dados", d.FonteDados), ("Responsável pelos dados", d.Organizacao ?? "Consolidação: sistema BarcaLog")
                 })
        {
            r.Cell(linha, 1).Value = rot;
            r.Cell(linha, 1).Style.Font.SetBold();
            r.Cell(linha, 2).Value = val;
            linha++;
        }
        linha++;
        var indicadores = d.Secoes.SelectMany(s => s.Blocos).OfType<BlocoIndicadores>().FirstOrDefault();
        if (indicadores is not null)
        {
            Cabecalho(r, linha, ["Indicador", "Valor", "Comparação"], navy);
            linha++;
            foreach (var i in indicadores.Itens)
            {
                r.Cell(linha, 1).Value = i.Rotulo;
                r.Cell(linha, 2).Value = i.Valor;
                r.Cell(linha, 3).Value = i.Comparacao ?? "";
                linha++;
            }
            linha++;
        }
        foreach (var p in d.Secoes.SelectMany(s => s.Blocos).OfType<BlocoParagrafo>().Where(p => p.Estilo == "destaque"))
        {
            r.Cell(linha, 1).Value = p.Texto;
            r.Range(linha, 1, linha, 6).Merge().Style.Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Top);
            r.Row(linha).Height = 90;
            linha += 2;
        }
        r.Column(1).Width = 30;
        r.Column(2).Width = 40;
        r.Column(3).Width = 40;

        // ---- Uma aba por tabela e por gráfico (os dados por trás do gráfico)
        foreach (var secao in d.Secoes)
        foreach (var bloco in secao.Blocos)
        {
            if (bloco is BlocoTabela t)
            {
                var ws = wb.Worksheets.Add(Nome(t.Titulo, nomes));
                ws.Cell(1, 1).Value = t.Titulo;
                ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(12).Font.SetFontColor(navy);
                Cabecalho(ws, 3, t.Colunas.Select(c => c.Titulo).ToList(), navy);
                for (var i = 0; i < t.Linhas.Count; i++)
                {
                    for (var j = 0; j < t.Colunas.Count; j++)
                    {
                        var c = ws.Cell(4 + i, 1 + j);
                        var v = t.Valores.ElementAtOrDefault(i)?.ElementAtOrDefault(j);
                        if (v is { } num && t.Colunas[j].Formato is { } fmt)
                        {
                            c.Value = num;
                            c.Style.NumberFormat.Format = fmt;
                        }
                        else c.Value = t.Linhas[i][j];
                        if (t.Colunas[j].Alinhamento == "direita") c.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                    }
                    if (t.LinhasDestacadas.Contains(i)) ws.Range(4 + i, 1, 4 + i, t.Colunas.Count).Style.Fill.SetBackgroundColor(ambar).Font.SetBold();
                }
                if (t.Linhas.Count > 0) ws.Range(3, 1, 3 + t.Linhas.Count, t.Colunas.Count).SetAutoFilter();
                if (t.Nota is { Length: > 0 } nota)
                {
                    ws.Cell(5 + t.Linhas.Count, 1).Value = nota;
                    ws.Cell(5 + t.Linhas.Count, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.FromHtml("#6B7890"));
                }
                ws.SheetView.FreezeRows(3);
                ws.Columns(1, t.Colunas.Count).AdjustToContents(3, 3 + t.Linhas.Count, 8, 60);
            }
            else if (bloco is BlocoGrafico g)
            {
                var ws = wb.Worksheets.Add(Nome("Dados - " + g.Titulo, nomes));
                ws.Cell(1, 1).Value = g.Titulo;
                ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(12).Font.SetFontColor(navy);
                ws.Cell(2, 1).Value = $"Unidade: {g.Unidade}";
                Cabecalho(ws, 4, new[] { "Categoria" }.Concat(g.Series.Select(s => s.Nome + (s.Estimado ? " (estimado)" : ""))).ToList(), navy);
                for (var i = 0; i < g.Categorias.Count; i++)
                {
                    ws.Cell(5 + i, 1).Value = g.Categorias[i];
                    for (var k = 0; k < g.Series.Count; k++)
                        if (g.Series[k].Valores.ElementAtOrDefault(i) is { } v)
                        {
                            ws.Cell(5 + i, 2 + k).Value = v;
                            ws.Cell(5 + i, 2 + k).Style.NumberFormat.Format = Math.Abs(v - Math.Round(v)) < 1e-9 ? "#,##0" : "#,##0.0";
                        }
                }
                if (g.Nota is { Length: > 0 } nota) ws.Cell(6 + g.Categorias.Count, 1).Value = nota;
                ws.SheetView.FreezeRows(4);
                ws.Columns(1, 1 + g.Series.Count).AdjustToContents(4, 4 + g.Categorias.Count, 8, 40);
            }
        }

        // ---- Movimentos do período (dados detalhados)
        var m = wb.Worksheets.Add(Nome("Movimentações do período", nomes));
        var cols = new[] { "Movimento", "Terminal", "Operador", "Carga", "Convênio", "Marcação", "Liberação", "Espera (h)", "Origem" };
        Cabecalho(m, 1, cols, navy);
        var lin = 2;
        foreach (var x in movimentos.OrderBy(x => x.DataMarcacao).ThenBy(x => x.MovimentoId))
        {
            m.Cell(lin, 1).Value = x.MovimentoId;
            m.Cell(lin, 2).Value = x.Terminal;
            m.Cell(lin, 3).Value = x.Operador;
            m.Cell(lin, 4).Value = x.Carga;
            m.Cell(lin, 5).Value = x.Convenio;
            m.Cell(lin, 6).Value = x.DataMarcacao;
            m.Cell(lin, 6).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";
            if (x.DataLiberacao is { } lib)
            {
                m.Cell(lin, 7).Value = lib;
                m.Cell(lin, 7).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";
            }
            if (x.EsperaHoras is { } e)
            {
                m.Cell(lin, 8).Value = Math.Round(e, 2);
                m.Cell(lin, 8).Style.NumberFormat.Format = "#,##0.00";
            }
            m.Cell(lin, 9).Value = x.Origem switch
            {
                Domain.Enums.OrigemMarcacao.CargaInicial => "Carga inicial",
                Domain.Enums.OrigemMarcacao.Planilha => "Planilha",
                _ => "Integração"
            };
            lin++;
        }
        if (lin > 2) m.Range(1, 1, lin - 1, cols.Length).SetAutoFilter();
        m.SheetView.FreezeRows(1);
        m.Columns(1, cols.Length).AdjustToContents(1, Math.Min(lin, 500), 8, 50);

        // ---- Metodologia e fontes (textos)
        var t2 = wb.Worksheets.Add(Nome("Metodologia", nomes));
        var l2 = 1;
        foreach (var secao in d.Secoes.Where(s => s.Id is "metodologia" or "ocorrencias" or "conclusoes" or "cargas" or "picos"))
        {
            t2.Cell(l2, 1).Value = secao.Titulo;
            t2.Cell(l2, 1).Style.Font.SetBold().Font.SetFontColor(navy);
            l2++;
            foreach (var b in secao.Blocos)
            {
                IEnumerable<(string, string)> linhas = b switch
                {
                    BlocoParagrafo p => [("", p.Texto)],
                    BlocoLista ls => ls.Itens.Select(i => ("•", i)),
                    BlocoQuadro q => q.Itens.Select(i => (i.Rotulo, i.Valor)).Prepend((q.Titulo, "")),
                    _ => []
                };
                foreach (var (a, b2) in linhas)
                {
                    t2.Cell(l2, 1).Value = a;
                    t2.Cell(l2, 1).Style.Font.SetBold();
                    t2.Cell(l2, 2).Value = b2;
                    t2.Cell(l2, 2).Style.Alignment.SetWrapText();
                    l2++;
                }
            }
            l2++;
        }
        t2.Column(1).Width = 28;
        t2.Column(2).Width = 110;

        using var saida = new MemoryStream();
        wb.SaveAs(saida);
        return saida.ToArray();
    }

    private static void Cabecalho(IXLWorksheet ws, int linha, IReadOnlyList<string> titulos, XLColor cor)
    {
        for (var j = 0; j < titulos.Count; j++)
        {
            var c = ws.Cell(linha, j + 1);
            c.Value = titulos[j];
            c.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(cor);
        }
    }

    /// <summary>Nome de aba válido (até 31 caracteres, sem : \ / ? * [ ]) e único.</summary>
    private static string Nome(string desejado, HashSet<string> usados)
    {
        var limpo = new string(desejado.Where(c => !":\\/?*[]".Contains(c)).ToArray()).Trim();
        if (limpo.Length > 31) limpo = limpo[..31].Trim();
        var nome = limpo.Length == 0 ? "Planilha" : limpo;
        var n = 2;
        while (!usados.Add(nome))
        {
            var suf = $" ({n++})";
            nome = (limpo.Length + suf.Length > 31 ? limpo[..(31 - suf.Length)] : limpo) + suf;
        }
        return nome;
    }
}
