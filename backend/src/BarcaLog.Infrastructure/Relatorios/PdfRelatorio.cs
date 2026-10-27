using System.Reflection;
using BarcaLog.Application.Relatorios;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace BarcaLog.Infrastructure.Relatorios;

/// <summary>Fontes do documento (Inter e Space Grotesk, licença OFL), embutidas no assembly.</summary>
internal sealed class FontesRelatorio : IFontResolver
{
    public const string Texto = "Inter", TextoMedio = "Inter Medium", Titulo = "Space Grotesk";
    private static readonly object Trava = new();
    private static bool _registrado;

    public static void Registrar()
    {
        lock (Trava)
        {
            if (_registrado) return;
            GlobalFontSettings.FontResolver = new FontesRelatorio();
            _registrado = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) => familyName switch
    {
        Texto => new FontResolverInfo(bold ? "Inter-SemiBold" : "Inter-Regular"),
        TextoMedio => new FontResolverInfo("Inter-Medium"),
        Titulo => new FontResolverInfo("SpaceGrotesk-Bold"),
        _ => new FontResolverInfo(bold ? "Inter-SemiBold" : "Inter-Regular")
    };

    public byte[]? GetFont(string faceName)
    {
        var asm = typeof(FontesRelatorio).Assembly;
        var nome = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith($".{faceName}.ttf", StringComparison.Ordinal));
        if (nome is null) return null;
        using var s = asm.GetManifestResourceStream(nome)!;
        using var m = new MemoryStream();
        s.CopyTo(m);
        return m.ToArray();
    }
}

/// <summary>Paleta do papel timbrado — a mesma identidade do sistema (azul-noite e âmbar).</summary>
internal static class Cores
{
    public static readonly XColor Navio900 = XColor.FromArgb(0x0A, 0x1B, 0x33);
    public static readonly XColor Navio800 = XColor.FromArgb(0x10, 0x2A, 0x4C);
    public static readonly XColor Navio600 = XColor.FromArgb(0x1F, 0x4E, 0x88);
    public static readonly XColor Ambar500 = XColor.FromArgb(0xF2, 0xA9, 0x3B);
    public static readonly XColor Ambar600 = XColor.FromArgb(0xD9, 0x8D, 0x1C);
    public static readonly XColor AmbarFundo = XColor.FromArgb(0xFF, 0xF4, 0xE0);
    public static readonly XColor Tinta = XColor.FromArgb(0x0E, 0x1A, 0x2B);
    public static readonly XColor TintaSuave = XColor.FromArgb(0x4A, 0x58, 0x70);
    public static readonly XColor TintaFraca = XColor.FromArgb(0x6B, 0x78, 0x90);
    public static readonly XColor Borda = XColor.FromArgb(0xE3, 0xE8, 0xEF);
    public static readonly XColor BordaForte = XColor.FromArgb(0xCD, 0xD5, 0xE1);
    public static readonly XColor Fundo = XColor.FromArgb(0xF4, 0xF6, 0xF9);
    public static readonly XColor FundoAlt = XColor.FromArgb(0xF7, 0xF9, 0xFC);
    public static readonly XColor FundoMarca = XColor.FromArgb(0xEC, 0xF1, 0xF8);
    public static readonly XColor Ok = XColor.FromArgb(0x17, 0x73, 0x4F);
    public static readonly XColor Critico = XColor.FromArgb(0xA8, 0x32, 0x2A);
    public static readonly XColor Neutra = XColor.FromArgb(0xB8, 0xC4, 0xD6);
    public static readonly XColor Branco = XColor.FromArgb(0xFF, 0xFF, 0xFF);

    public static XColor Serie(string nome) => nome switch
    {
        "marca" => Navio600,
        "ambar" => Ambar500,
        "unitapajos" => Navio600,
        "tgpm" => XColor.FromArgb(0xE0, 0x86, 0x1A),
        "hidrovias" => XColor.FromArgb(0x2A, 0x9D, 0x8F),
        "estimado" => XColor.FromArgb(0x9A, 0xA9, 0xBE),
        _ => Neutra
    };
}

/// <summary>
/// Diagramação do PDF em A4 retrato: papel timbrado na primeira página, cabeçalho discreto nas
/// seguintes, rodapé com código, fonte e "Página X de Y" em todas. Blocos nunca são cortados no
/// meio (tabelas quebram entre linhas e repetem o cabeçalho).
/// </summary>
internal sealed class PdfRelatorio
{
    private const double Largura = 595.28, Altura = 841.89, MargemX = 48, Topo = 74, Base = 64;
    private const double LarguraUtil = Largura - 2 * MargemX;

    private readonly DocumentoRelatorio _doc;
    private readonly PdfDocument _pdf = new();
    private XGraphics _g = null!;
    private double _y;

    private static XFont F(double tam, bool negrito = false) => new(FontesRelatorio.Texto, tam, negrito ? XFontStyleEx.Bold : XFontStyleEx.Regular);
    private static XFont Fm(double tam) => new(FontesRelatorio.TextoMedio, tam, XFontStyleEx.Regular);
    private static XFont Ft(double tam) => new(FontesRelatorio.Titulo, tam, XFontStyleEx.Bold);

    private PdfRelatorio(DocumentoRelatorio doc) => _doc = doc;

    public static byte[] Gerar(DocumentoRelatorio doc)
    {
        FontesRelatorio.Registrar();
        return new PdfRelatorio(doc).Compor();
    }

    private byte[] Compor()
    {
        _pdf.Info.Title = $"{_doc.Titulo} — {_doc.PeriodoRotulo}";
        _pdf.Info.Subject = $"{_doc.Subtitulo}. {_doc.Abrangencia}.";
        _pdf.Info.Author = "BarcaLog";
        _pdf.Info.Creator = "BarcaLog — central de relatórios";
        _pdf.Info.Keywords = _doc.Codigo ?? "";

        NovaPagina(primeira: true);
        var n = 0;
        foreach (var secao in _doc.Secoes)
        {
            n++;
            TituloSecao(n, secao.Titulo, secao.Blocos.FirstOrDefault());
            foreach (var bloco in secao.Blocos) Bloco(bloco);
            _y += 10;
        }
        _g.Dispose();
        Rodapes();

        using var saida = new MemoryStream();
        _pdf.Save(saida, false);
        return saida.ToArray();
    }

    // ------------------------------------------------------------------ páginas

    private void NovaPagina(bool primeira = false)
    {
        _g?.Dispose();
        var p = _pdf.AddPage();
        p.Size = PageSize.A4;
        _g = XGraphics.FromPdfPage(p);
        if (primeira) Timbre(); else CabecalhoSeguinte();
    }

    private void Garantir(double altura)
    {
        if (_y + altura > Altura - Base) NovaPagina();
    }

    private void Marca(double x, double y, double lado)
    {
        _g.DrawRoundedRectangle(new XSolidBrush(Cores.Ambar500), x, y, lado, lado, lado * 0.45, lado * 0.45);
        // Âncora estilizada (o mesmo símbolo do menu do sistema).
        var c = new XPen(Cores.Navio900, lado * 0.085) { LineCap = XLineCap.Round, LineJoin = XLineJoin.Round };
        double cx = x + lado / 2, top = y + lado * 0.2, bot = y + lado * 0.8;
        _g.DrawEllipse(c, cx - lado * 0.07, top, lado * 0.14, lado * 0.14);
        _g.DrawLine(c, cx, top + lado * 0.14, cx, bot);
        _g.DrawLine(c, cx - lado * 0.16, top + lado * 0.27, cx + lado * 0.16, top + lado * 0.27);
        _g.DrawArc(c, cx - lado * 0.27, bot - lado * 0.36, lado * 0.54, lado * 0.36, 20, 140);
    }

    private void Timbre()
    {
        // Faixa superior: identidade do sistema.
        _g.DrawRectangle(new XSolidBrush(Cores.Navio900), 0, 0, Largura, 86);
        _g.DrawRectangle(new XSolidBrush(Cores.Ambar500), 0, 86, Largura, 3);
        Marca(MargemX, 26, 32);
        _g.DrawString("BarcaLog", Ft(20), new XSolidBrush(Cores.Branco), MargemX + 42, 26 + 1, XStringFormats.TopLeft);
        _g.DrawString("Inteligência logística · Barcarena (PA)", F(8.5), new XSolidBrush(XColor.FromArgb(0x9F, 0xB0, 0xC8)), MargemX + 42, 26 + 24, XStringFormats.TopLeft);

        var tipo = _doc.Titulo.ToUpperInvariant();
        DireitaEm(tipo, Fm(8), Cores.Ambar500, 30, espacado: true);
        DireitaEm(_doc.Codigo is null ? "PRÉVIA — sem código" : $"Código {_doc.Codigo}", F(8), XColor.FromArgb(0xC9, 0xD3, 0xE3), 44);
        DireitaEm($"Emitido em {_doc.GeradoEm:dd/MM/yyyy 'às' HH:mm}", F(8), XColor.FromArgb(0xC9, 0xD3, 0xE3), 56);

        _y = 112;
        _g.DrawString(_doc.Titulo, Ft(24), new XSolidBrush(Cores.Navio900), MargemX, _y, XStringFormats.TopLeft);
        _y += 32;
        _g.DrawString(_doc.Subtitulo, F(11), new XSolidBrush(Cores.TintaSuave), MargemX, _y, XStringFormats.TopLeft);
        _y += 26;

        // Ficha do documento.
        var itens = new List<(string, string)>
        {
            ("PERÍODO", _doc.PeriodoRotulo),
            ("ABRANGÊNCIA", _doc.Abrangencia),
            ("EMISSÃO", _doc.GeradoEm.ToString("dd/MM/yyyy HH:mm")),
            ("RESPONSÁVEL PELOS DADOS", _doc.Organizacao ?? "Consolidação: sistema BarcaLog")
        };
        var largCol = (LarguraUtil - 24) / 4;
        var alturas = itens.Select(i => Linhas(i.Item2, F(9, true), largCol - 8).Count).Max();
        var h = 30 + alturas * 12;
        _g.DrawRoundedRectangle(new XSolidBrush(Cores.FundoMarca), MargemX, _y, LarguraUtil, h, 8, 8);
        for (var i = 0; i < itens.Count; i++)
        {
            var x = MargemX + 12 + i * largCol;
            _g.DrawString(itens[i].Item1, Fm(6.5), new XSolidBrush(Cores.TintaFraca), x, _y + 10, XStringFormats.TopLeft);
            var yy = _y + 22;
            foreach (var l in Linhas(itens[i].Item2, F(9, true), largCol - 8))
            {
                _g.DrawString(l, F(9, true), new XSolidBrush(Cores.Navio900), x, yy, XStringFormats.TopLeft);
                yy += 12;
            }
        }
        _y += h + 18;
        if (_doc.Codigo is null)
        {
            Paragrafo("Prévia: este documento ainda não foi emitido. O código e o registro no histórico são criados na exportação.", F(8), Cores.Ambar600);
            _y += 4;
        }
    }

    private void CabecalhoSeguinte()
    {
        Marca(MargemX, 30, 16);
        _g.DrawString("BarcaLog", Ft(11), new XSolidBrush(Cores.Navio900), MargemX + 22, 30.5, XStringFormats.TopLeft);
        DireitaEm($"{_doc.Titulo} · {_doc.PeriodoRotulo}", F(8), Cores.TintaFraca, 34);
        _g.DrawLine(new XPen(Cores.Borda, 0.8), MargemX, 54, Largura - MargemX, 54);
        _y = Topo;
    }

    private void Rodapes()
    {
        var total = _pdf.PageCount;
        for (var i = 0; i < total; i++)
        {
            using var g = XGraphics.FromPdfPage(_pdf.Pages[i], XGraphicsPdfPageOptions.Append);
            var y = Altura - 44;
            g.DrawLine(new XPen(Cores.Borda, 0.8), MargemX, y, Largura - MargemX, y);
            g.DrawString($"BarcaLog · consolidação de dados de movimentação logística{(_doc.Codigo is null ? " · prévia" : $" · {_doc.Codigo}")}",
                F(7), new XSolidBrush(Cores.TintaSuave), MargemX, y + 8, XStringFormats.TopLeft);
            g.DrawString($"Fonte: {char.ToLowerInvariant(_doc.FonteDados[0])}{_doc.FonteDados[1..]} · {_doc.PeriodoRotulo}", F(7), new XSolidBrush(Cores.TintaFraca), MargemX, y + 18, XStringFormats.TopLeft);
            var pag = $"Página {i + 1} de {total}";
            var f = Fm(7.5);
            g.DrawString(pag, f, new XSolidBrush(Cores.Navio800), Largura - MargemX - g.MeasureString(pag, f).Width, y + 8, XStringFormats.TopLeft);
        }
    }

    // ------------------------------------------------------------------ blocos

    private void TituloSecao(int n, string titulo, BlocoRelatorio? primeiro)
    {
        // Mantém o título com o começo do conteúdo.
        Garantir(46 + Math.Min(AlturaEstimada(primeiro), 260));
        _y += 6;
        _g.DrawString($"{n:00}", Ft(12), new XSolidBrush(Cores.Ambar600), MargemX, _y, XStringFormats.TopLeft);
        _g.DrawString(titulo, Ft(14), new XSolidBrush(Cores.Navio900), MargemX + 24, _y - 1.5, XStringFormats.TopLeft);
        _y += 22;
        _g.DrawLine(new XPen(Cores.Borda, 0.8), MargemX, _y, Largura - MargemX, _y);
        _y += 10;
    }

    private double AlturaEstimada(BlocoRelatorio? b) => b switch
    {
        null => 0,
        BlocoParagrafo p => Linhas(p.Texto, F(p.Estilo == "destaque" ? 10.5 : 9.5), LarguraUtil - (p.Estilo == "destaque" ? 28 : 0)).Count * 14 + 16,
        BlocoIndicadores => 70,
        BlocoGrafico => 210,
        BlocoTabela => 80,
        BlocoQuadro => 80,
        _ => 40
    };

    private void Bloco(BlocoRelatorio b)
    {
        switch (b)
        {
            case BlocoParagrafo p: ParagrafoBloco(p); break;
            case BlocoIndicadores i: Indicadores(i); break;
            case BlocoTabela t: Tabela(t); break;
            case BlocoGrafico gr: Grafico(gr); break;
            case BlocoLista l: Lista(l); break;
            case BlocoQuadro q: Quadro(q); break;
        }
    }

    private void ParagrafoBloco(BlocoParagrafo p)
    {
        if (p.Estilo == "destaque")
        {
            var f = F(10.5);
            var linhas = Linhas(p.Texto, f, LarguraUtil - 30);
            var h = linhas.Count * 15.5 + 22;
            Garantir(h);
            _g.DrawRectangle(new XSolidBrush(Cores.FundoMarca), MargemX, _y, LarguraUtil, h);
            _g.DrawRectangle(new XSolidBrush(Cores.Ambar500), MargemX, _y, 4, h);
            var yy = _y + 11;
            foreach (var l in linhas) { _g.DrawString(l, f, new XSolidBrush(Cores.Tinta), MargemX + 18, yy, XStringFormats.TopLeft); yy += 15.5; }
            _y += h + 12;
            return;
        }
        if (p.Estilo == "nota") { Paragrafo(p.Texto, F(8), Cores.TintaFraca); _y += 6; return; }
        Paragrafo(p.Texto, F(9.5), Cores.Tinta);
        _y += 8;
    }

    private void Paragrafo(string texto, XFont f, XColor cor, double x = MargemX, double largura = LarguraUtil)
    {
        var alt = f.Size * 1.45;
        foreach (var l in Linhas(texto, f, largura))
        {
            Garantir(alt);
            _g.DrawString(l, f, new XSolidBrush(cor), x, _y, XStringFormats.TopLeft);
            _y += alt;
        }
    }

    private void Indicadores(BlocoIndicadores b)
    {
        const int porLinha = 4;
        const double gap = 8;
        var larg = (LarguraUtil - gap * (porLinha - 1)) / porLinha;
        foreach (var linha in b.Itens.Chunk(porLinha))
        {
            var alturas = linha.Select(i => 46 + Linhas(i.Comparacao ?? "", F(7), larg - 16).Count * 9.5).ToList();
            var h = Math.Max(58, alturas.Max());
            Garantir(h + gap);
            for (var k = 0; k < linha.Length; k++)
            {
                var it = linha[k];
                var x = MargemX + k * (larg + gap);
                _g.DrawRoundedRectangle(new XPen(Cores.Borda, 0.8), new XSolidBrush(Cores.FundoAlt), x, _y, larg, h, 7, 7);
                var yy = _y + 8;
                foreach (var l in Linhas(it.Rotulo, Fm(7), larg - 16).Take(2)) { _g.DrawString(l, Fm(7), new XSolidBrush(Cores.TintaSuave), x + 8, yy, XStringFormats.TopLeft); yy += 9; }
                yy = _y + 22;
                _g.DrawString(it.Valor, Ft(15), new XSolidBrush(Cores.Navio900), x + 8, yy, XStringFormats.TopLeft);
                yy += 21;
                if (it.Comparacao is { Length: > 0 } c)
                {
                    var cor = it.Sentido switch { "melhor" => Cores.Ok, "pior" => Cores.Critico, _ => Cores.TintaFraca };
                    foreach (var l in Linhas(c, F(7), larg - 16)) { _g.DrawString(l, F(7), new XSolidBrush(cor), x + 8, yy, XStringFormats.TopLeft); yy += 9.5; }
                }
            }
            _y += h + gap;
        }
        _y += 4;
    }

    private void Tabela(BlocoTabela t)
    {
        var fCab = F(7, true);
        var fCel = F(7.6);
        var fCelN = F(7.6, true);
        var pesos = t.Colunas.Sum(c => c.Largura);
        var larguras = t.Colunas.Select(c => LarguraUtil * c.Largura / pesos).ToArray();
        const double pad = 5;

        List<string>[] Cel(IReadOnlyList<string> cels, XFont f) => cels.Select((s, i) => Linhas(s, f, larguras[i] - 2 * pad)).ToArray();
        var cab = Cel(t.Colunas.Select(c => c.Titulo).ToList(), fCab);
        var hCab = cab.Max(l => l.Count) * 9 + 10;

        void DesenharCabecalho()
        {
            _g.DrawRectangle(new XSolidBrush(Cores.Navio800), MargemX, _y, LarguraUtil, hCab);
            var x = MargemX;
            for (var i = 0; i < larguras.Length; i++)
            {
                var yy = _y + 5;
                foreach (var l in cab[i])
                {
                    var w = _g.MeasureString(l, fCab).Width;
                    var xx = t.Colunas[i].Alinhamento == "direita" ? x + larguras[i] - pad - w : x + pad;
                    _g.DrawString(l, fCab, new XSolidBrush(Cores.Branco), xx, yy, XStringFormats.TopLeft);
                    yy += 9;
                }
                x += larguras[i];
            }
            _y += hCab;
        }

        var hTitulo = 16;
        var primeira = t.Linhas.Count == 0 ? 20 : Cel(t.Linhas[0], fCel).Max(l => l.Count) * 10 + 8;
        // Tabela curta não se divide: vai inteira (com a nota) para a próxima página se preciso.
        var hTotal = hTitulo + hCab + t.Linhas.Select((l, r) => Cel(l, t.LinhasDestacadas.Contains(r) ? fCelN : fCel).Max(c => c.Count) * 10.0 + 8).Sum()
                     + (t.Nota is { Length: > 0 } nt ? Linhas(nt, F(7.5), LarguraUtil).Count * 10.9 + 6 : 0);
        Garantir(hTotal <= 420 ? hTotal : hTitulo + hCab + primeira * Math.Min(3, Math.Max(1, t.Linhas.Count)));
        _g.DrawString(t.Titulo, F(9, true), new XSolidBrush(Cores.Navio900), MargemX, _y, XStringFormats.TopLeft);
        _y += hTitulo;
        DesenharCabecalho();

        for (var r = 0; r < t.Linhas.Count; r++)
        {
            var destaque = t.LinhasDestacadas.Contains(r);
            var f = destaque ? fCelN : fCel;
            var cels = Cel(t.Linhas[r], f);
            var h = cels.Max(l => l.Count) * 10 + 8;
            var reserva = r == t.Linhas.Count - 1 && t.Nota is { Length: > 0 } n ? Linhas(n, F(7.5), LarguraUtil).Count * 10.9 + 6 : 0;
            if (_y + h + reserva > Altura - Base) { NovaPagina(); DesenharCabecalho(); }
            if (destaque) _g.DrawRectangle(new XSolidBrush(Cores.AmbarFundo), MargemX, _y, LarguraUtil, h);
            else if (r % 2 == 1) _g.DrawRectangle(new XSolidBrush(Cores.FundoAlt), MargemX, _y, LarguraUtil, h);
            var x = MargemX;
            for (var i = 0; i < larguras.Length; i++)
            {
                var yy = _y + 4;
                foreach (var l in cels[i])
                {
                    var w = _g.MeasureString(l, f).Width;
                    var xx = t.Colunas[i].Alinhamento == "direita" ? x + larguras[i] - pad - w : x + pad;
                    _g.DrawString(l, f, new XSolidBrush(Cores.Tinta), xx, yy, XStringFormats.TopLeft);
                    yy += 10;
                }
                x += larguras[i];
            }
            _y += h;
            _g.DrawLine(new XPen(Cores.Borda, 0.5), MargemX, _y, Largura - MargemX, _y);
        }
        _y += 5;
        if (t.Nota is { Length: > 0 } nota) Paragrafo(nota, F(7.5), Cores.TintaFraca);
        _y += 10;
    }

    private void Lista(BlocoLista l)
    {
        var f = F(9.5);
        foreach (var item in l.Itens)
        {
            var linhas = Linhas(item, f, LarguraUtil - 16);
            Garantir(linhas.Count * 13.2 + 3);
            _g.DrawEllipse(new XSolidBrush(Cores.Ambar500), MargemX + 2, _y + 4.5, 4.5, 4.5);
            foreach (var t in linhas) { _g.DrawString(t, f, new XSolidBrush(Cores.Tinta), MargemX + 14, _y, XStringFormats.TopLeft); _y += 13.2; }
            _y += 3;
        }
        _y += 6;
    }

    private void Quadro(BlocoQuadro q)
    {
        var (fundo, barra) = q.Tom switch
        {
            "atencao" => (Cores.AmbarFundo, Cores.Ambar500),
            "limitacao" => (Cores.Fundo, Cores.TintaFraca),
            _ => (Cores.FundoMarca, Cores.Navio600)
        };
        var fRot = F(8, true);
        var fTxt = F(8.4);
        var largRot = Math.Min(130, q.Itens.Count == 0 ? 0 : q.Itens.Max(i => _g.MeasureString(i.Rotulo, fRot).Width) + 14);
        var largTxt = LarguraUtil - 30 - largRot;
        var itens = q.Itens.Select(i => (Rot: Linhas(i.Rotulo, fRot, largRot - 8), Txt: Linhas(i.Valor, fTxt, largTxt))).ToList();
        var alturaItem = itens.Select(i => Math.Max(i.Rot.Count, i.Txt.Count) * 11.2 + 5).ToList();

        // Desenha em pedaços: o quadro pode atravessar a página entre um item e outro.
        var idx = 0;
        var primeiroPedaco = true;
        while (idx < itens.Count || primeiroPedaco)
        {
            var hTitulo = primeiroPedaco ? 22 : 8;
            Garantir(hTitulo + (idx < alturaItem.Count ? alturaItem[idx] : 0) + 10);
            var fim = idx;
            double h = hTitulo + 6;
            while (fim < itens.Count && _y + h + alturaItem[fim] + 6 <= Altura - Base) h += alturaItem[fim++];
            _g.DrawRectangle(new XSolidBrush(fundo), MargemX, _y, LarguraUtil, h);
            _g.DrawRectangle(new XSolidBrush(barra), MargemX, _y, 3.5, h);
            var yy = _y + 9;
            if (primeiroPedaco)
            {
                _g.DrawString(q.Titulo, F(9.2, true), new XSolidBrush(Cores.Navio900), MargemX + 15, yy, XStringFormats.TopLeft);
                yy += 18;
            }
            for (var k = idx; k < fim; k++)
            {
                var y0 = yy;
                foreach (var l in itens[k].Rot) { _g.DrawString(l, fRot, new XSolidBrush(Cores.Navio800), MargemX + 15, yy, XStringFormats.TopLeft); yy += 11.2; }
                yy = y0;
                foreach (var l in itens[k].Txt) { _g.DrawString(l, fTxt, new XSolidBrush(Cores.Tinta), MargemX + 15 + largRot, yy, XStringFormats.TopLeft); yy += 11.2; }
                yy = y0 + alturaItem[k];
            }
            _y += h;
            primeiroPedaco = false;
            idx = fim;
            if (idx < itens.Count) NovaPagina();
        }
        _y += 12;
    }

    // ------------------------------------------------------------------ gráficos

    private void Grafico(BlocoGrafico gr)
    {
        const double altPlot = 132;
        var temNota = gr.Nota is { Length: > 0 };
        var hNota = temNota ? Linhas(gr.Nota!, F(7.3), LarguraUtil).Count * 10.5 + 4 : 0;
        var n = gr.Categorias.Count;
        var rotacionar = n > 14;
        var hRotulos = rotacionar ? 30 : 16;
        Garantir(20 + 14 + altPlot + hRotulos + hNota + 12);

        _g.DrawString(gr.Titulo, F(9, true), new XSolidBrush(Cores.Navio900), MargemX, _y, XStringFormats.TopLeft);
        // Legenda à direita do título.
        var lx = Largura - MargemX;
        foreach (var s in gr.Series.Reverse())
        {
            var w = _g.MeasureString(s.Nome, F(7.3)).Width;
            lx -= w;
            _g.DrawString(s.Nome, F(7.3), new XSolidBrush(Cores.TintaSuave), lx, _y + 1.5, XStringFormats.TopLeft);
            lx -= 11;
            _g.DrawRectangle(new XSolidBrush(s.Estimado ? Cores.Serie("estimado") : Cores.Serie(s.Cor)), lx, _y + 3, 7, 7);
            lx -= 12;
        }
        _y += 16;
        _g.DrawString(Primeira(gr.Unidade), F(7), new XSolidBrush(Cores.TintaFraca), MargemX, _y, XStringFormats.TopLeft);
        _y += 12;

        var empilhado = gr.TipoGrafico == "barrasEmpilhadas";
        var maximo = n == 0 ? 0 : empilhado
            ? Enumerable.Range(0, n).Max(i => gr.Series.Sum(s => s.Valores.ElementAtOrDefault(i) ?? 0))
            : gr.Series.SelectMany(s => s.Valores).Max(v => v ?? 0);
        var (topoEixo, passo) = EscalaBonita(maximo);
        var fEixo = F(6.8);
        var largEixo = Math.Max(_g.MeasureString(Num(topoEixo), fEixo).Width, 14) + 6;
        double x0 = MargemX + largEixo, larguraPlot = LarguraUtil - largEixo, yBase = _y + altPlot;

        for (var v = 0.0; v <= topoEixo + passo / 2; v += passo)
        {
            var yy = yBase - v / topoEixo * altPlot;
            _g.DrawLine(new XPen(v == 0 ? Cores.BordaForte : Cores.Borda, v == 0 ? 0.8 : 0.5), x0, yy, x0 + larguraPlot, yy);
            var r = Num(v);
            _g.DrawString(r, fEixo, new XSolidBrush(Cores.TintaFraca), x0 - 5 - _g.MeasureString(r, fEixo).Width, yy - 4, XStringFormats.TopLeft);
        }

        var larguraCat = n == 0 ? larguraPlot : larguraPlot / n;
        var mostrarValores = n <= 12;
        var passoRotulo = Math.Max(1, (int)Math.Ceiling(n / 31.0));
        for (var i = 0; i < n; i++)
        {
            var cx = x0 + i * larguraCat;
            var destaque = gr.CategoriasDestacadas.Contains(i);
            if (gr.TipoGrafico == "linhas") continue;
            if (empilhado)
            {
                var larg = Math.Min(28, larguraCat * 0.62);
                var bx = cx + (larguraCat - larg) / 2;
                double acum = 0;
                foreach (var s in gr.Series)
                {
                    var v = s.Valores.ElementAtOrDefault(i) ?? 0;
                    if (v <= 0) continue;
                    var h = v / topoEixo * altPlot;
                    var y1 = yBase - (acum + v) / topoEixo * altPlot;
                    _g.DrawRectangle(new XSolidBrush(s.Estimado ? Cores.Serie("estimado") : Cores.Serie(s.Cor)), bx, y1, larg, h);
                    acum += v;
                }
                if (mostrarValores && acum > 0)
                    Centralizado(Num(acum), F(6.8, destaque), destaque ? Cores.Navio900 : Cores.TintaSuave, bx + larg / 2, yBase - acum / topoEixo * altPlot - 10);
            }
            else
            {
                var ns = gr.Series.Count;
                var grupo = Math.Min(ns * 16, larguraCat * 0.74);
                var larg = grupo / ns;
                var bx = cx + (larguraCat - grupo) / 2;
                for (var k = 0; k < ns; k++)
                {
                    var s = gr.Series[k];
                    if (s.Valores.ElementAtOrDefault(i) is not { } v || v <= 0) continue;
                    var h = v / topoEixo * altPlot;
                    var cor = s.Estimado ? Cores.Serie("estimado") : Cores.Serie(s.Cor);
                    _g.DrawRectangle(new XSolidBrush(cor), bx + k * larg + 0.4, yBase - h, larg - 0.8, h);
                    if (destaque && k == 0) _g.DrawRectangle(new XPen(Cores.Ambar600, 1.1), bx + k * larg + 0.4, yBase - h, larg - 0.8, h);
                    if (mostrarValores && ns == 1)
                        Centralizado(Num(v), F(6.8, destaque), destaque ? Cores.Navio900 : Cores.TintaSuave, bx + k * larg + larg / 2, yBase - h - 10);
                }
            }
        }
        if (gr.TipoGrafico == "linhas")
        {
            foreach (var s in gr.Series)
            {
                var pen = new XPen(s.Estimado ? Cores.Serie("estimado") : Cores.Serie(s.Cor), 1.6);
                if (s.Estimado) pen.DashStyle = XDashStyle.Dash;
                XPoint? anterior = null;
                for (var i = 0; i < n; i++)
                {
                    if (s.Valores.ElementAtOrDefault(i) is not { } v) { anterior = null; continue; }
                    var p = new XPoint(x0 + i * larguraCat + larguraCat / 2, yBase - v / topoEixo * altPlot);
                    if (anterior is { } a) _g.DrawLine(pen, a, p);
                    _g.DrawEllipse(new XSolidBrush(pen.Color), p.X - 1.8, p.Y - 1.8, 3.6, 3.6);
                    anterior = p;
                }
            }
        }

        // Rótulos das categorias.
        for (var i = 0; i < n; i += passoRotulo)
        {
            var rot = gr.Categorias[i];
            var destaque = gr.CategoriasDestacadas.Contains(i);
            var f = F(6.6, destaque);
            var cx = x0 + i * larguraCat + larguraCat / 2;
            if (rotacionar)
            {
                var st = _g.Save();
                _g.RotateAtTransform(-50, new XPoint(cx, yBase + 4));
                var w = _g.MeasureString(rot, f).Width;
                _g.DrawString(rot, f, new XSolidBrush(Cores.TintaSuave), cx - w, yBase + 1, XStringFormats.TopLeft);
                _g.Restore(st);
            }
            else Centralizado(rot, f, destaque ? Cores.Navio900 : Cores.TintaSuave, cx, yBase + 4);
        }
        _y = yBase + hRotulos + 4;
        if (temNota) { Paragrafo(gr.Nota!, F(7.3), Cores.TintaFraca); }
        _y += 12;
    }

    private static (double Topo, double Passo) EscalaBonita(double maximo)
    {
        if (maximo <= 0) return (1, 0.25);
        var bruto = maximo / 4;
        var mag = Math.Pow(10, Math.Floor(Math.Log10(bruto)));
        var norm = bruto / mag;
        var passo = (norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 2.5 ? 2.5 : norm <= 5 ? 5 : 10) * mag;
        return (Math.Ceiling(maximo / passo) * passo, passo);
    }

    // ------------------------------------------------------------------ texto

    private void Centralizado(string s, XFont f, XColor cor, double cx, double y)
    {
        var w = _g.MeasureString(s, f).Width;
        _g.DrawString(s, f, new XSolidBrush(cor), cx - w / 2, y, XStringFormats.TopLeft);
    }

    private void DireitaEm(string s, XFont f, XColor cor, double y, bool espacado = false)
    {
        var w = _g.MeasureString(s, f).Width;
        _g.DrawString(s, f, new XSolidBrush(cor), Largura - MargemX - w, y, XStringFormats.TopLeft);
    }

    /// <summary>Quebra o texto em linhas que cabem na largura (palavra longa demais é partida).</summary>
    private List<string> Linhas(string texto, XFont f, double largura)
    {
        var linhas = new List<string>();
        foreach (var paragrafo in (texto ?? "").Split('\n'))
        {
            var atual = "";
            foreach (var palavra in paragrafo.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var tentativa = atual.Length == 0 ? palavra : atual + " " + palavra;
                if (_g.MeasureString(tentativa, f).Width <= largura) { atual = tentativa; continue; }
                if (atual.Length > 0) linhas.Add(atual);
                atual = palavra;
                while (_g.MeasureString(atual, f).Width > largura && atual.Length > 1)
                {
                    var corte = atual.Length - 1;
                    while (corte > 1 && _g.MeasureString(atual[..corte], f).Width > largura) corte--;
                    linhas.Add(atual[..corte]);
                    atual = atual[corte..];
                }
            }
            linhas.Add(atual);
        }
        return linhas;
    }

    private static string Num(double v) => Math.Abs(v - Math.Round(v)) < 0.001
        ? v.ToString("#,##0", Fmt.Br)
        : v.ToString("#,##0.#", Fmt.Br);

    private static string Primeira(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Fmt.Br) + s[1..];
}
