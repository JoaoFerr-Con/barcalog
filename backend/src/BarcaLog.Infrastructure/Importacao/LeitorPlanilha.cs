using System.Globalization;
using System.Text;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Domain.Enums;
using ExcelDataReader;

namespace BarcaLog.Infrastructure.Importacao;

/// <summary>
/// Leitura de XLSX e CSV com ExcelDataReader (streaming, sem carregar a planilha
/// inteira como objeto). Primeira aba do XLSX; CSV com separador detectado
/// (vírgula, ponto e vírgula, tabulação) e UTF-8 com recuo para Windows-1252.
/// </summary>
public sealed class LeitorPlanilha : ILeitorPlanilha
{
    static LeitorPlanilha() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public (IReadOnlyList<string> Colunas, IEnumerable<LinhaPlanilha> Linhas) Ler(Stream conteudo, FormatoArquivoImportacao formato)
    {
        var leitor = formato == FormatoArquivoImportacao.Xlsx
            ? ExcelReaderFactory.CreateOpenXmlReader(conteudo)
            : ExcelReaderFactory.CreateCsvReader(conteudo, new ExcelReaderConfiguration
            {
                FallbackEncoding = Encoding.GetEncoding(1252),
                AutodetectSeparators = [';', ',', '\t', '|'],
                LeaveOpen = true
            });
        if (!leitor.Read())
        {
            leitor.Dispose();
            throw new Domain.Excecoes.RegraNegocioException("O arquivo está vazio (sem linha de cabeçalho).");
        }
        var colunas = new List<string>();
        for (var i = 0; i < leitor.FieldCount; i++) colunas.Add(Normalizar(Convert.ToString(leitor.GetValue(i), CultureInfo.InvariantCulture)));
        return (colunas, Linhas(leitor, colunas));
    }

    private static IEnumerable<LinhaPlanilha> Linhas(IExcelDataReader leitor, List<string> colunas)
    {
        using (leitor)
        {
            var numero = 1;
            while (leitor.Read())
            {
                numero++;
                var valores = new Dictionary<string, object?>(StringComparer.Ordinal);
                var vazia = true;
                for (var i = 0; i < colunas.Count && i < leitor.FieldCount; i++)
                {
                    if (colunas[i].Length == 0) continue;
                    var v = leitor.GetValue(i);
                    if (v is string s && string.IsNullOrWhiteSpace(s)) v = null;
                    if (v is not null) vazia = false;
                    valores[colunas[i]] = v;
                }
                if (!vazia) yield return new LinhaPlanilha(numero, valores);
            }
        }
    }

    /// <summary>"Data Marcação" → "DATA_MARCACAO".</summary>
    public static string Normalizar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "";
        var semAcento = new StringBuilder();
        foreach (var c in nome.Trim().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) semAcento.Append(c);
        var r = new StringBuilder();
        foreach (var c in semAcento.ToString().ToUpperInvariant())
            r.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        return r.ToString().Trim('_');
    }
}
