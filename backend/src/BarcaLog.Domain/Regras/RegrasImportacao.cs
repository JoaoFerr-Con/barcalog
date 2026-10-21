using System.Globalization;
using BarcaLog.Domain.Enums;

namespace BarcaLog.Domain.Regras;

/// <summary>Valores de um movimento, como estão no banco ou como vieram da planilha.</summary>
public sealed record DadosMovimento(
    string TerminalId, string Senha, string Convenio, string? CodConvenio, int Ciclo,
    DateTime DataMarcacao, DateTime? DataLiberacao);

/// <summary>Um campo que difere entre o BarcaLog e a planilha.</summary>
public sealed record DiferencaCampo(string Campo, string? Atual, string? Novo);

public sealed record ResultadoComparacao(ClassificacaoLinhaImportacao Classificacao, IReadOnlyList<DiferencaCampo> Diferencas)
{
    /// <summary>"Convênio: A → B; Ciclo: 1 → 2" — gravado como motivo da linha.</summary>
    public string? Descrever() => Diferencas.Count == 0 ? null
        : string.Join("; ", Diferencas.Select(d => $"{d.Campo}: {d.Atual ?? "(vazio)"} → {d.Novo ?? "(vazio)"}"));
}

/// <summary>
/// Regras puras da importação de planilhas. Nenhuma conversão silenciosa:
/// o que muda um movimento existente vira "conflito" e precisa de decisão;
/// só o preenchimento de um campo vazio (código do convênio, liberação) é
/// aplicado direto, como "complemento".
/// </summary>
public static class RegrasImportacao
{
    /// <summary>Esperas acima disso entram com aviso (casos para investigar).</summary>
    public const double LimiteEsperaAvisoHoras = 72;

    /// <summary>Mesma senha e convênio marcados de novo em até X minutos, com a mesma liberação = provável remarcação.</summary>
    public const int JanelaRemarcacaoMinutos = 10;

    /// <summary>
    /// Compara o que existe com o que veio. Igual → null (não gera linha);
    /// só campos vazios preenchidos → Complementar; qualquer outra diferença → Conflito.
    /// </summary>
    public static ResultadoComparacao? Comparar(DadosMovimento atual, DadosMovimento novo)
    {
        var difs = new List<DiferencaCampo>();
        var soComplemento = true;

        void Campo(string nome, string? a, string? n, bool podeComplementar)
        {
            if (string.Equals(a, n, StringComparison.Ordinal)) return;
            if (n is null) return; // a planilha não informou: mantém o que existe, não é diferença
            difs.Add(new DiferencaCampo(nome, a, n));
            if (!(podeComplementar && a is null)) soComplemento = false;
        }

        Campo("Terminal", atual.TerminalId, novo.TerminalId, false);
        Campo("Senha", atual.Senha, novo.Senha, false);
        Campo("Convênio", atual.Convenio, novo.Convenio, false);
        Campo("Código do convênio", atual.CodConvenio, novo.CodConvenio, true);
        Campo("Ciclo", atual.Ciclo.ToString(CultureInfo.InvariantCulture), novo.Ciclo.ToString(CultureInfo.InvariantCulture), false);
        Campo("Marcação", Formatar(atual.DataMarcacao), Formatar(novo.DataMarcacao), false);
        Campo("Liberação", Formatar(atual.DataLiberacao), Formatar(novo.DataLiberacao), true);

        if (difs.Count == 0) return null;
        return new ResultadoComparacao(soComplemento ? ClassificacaoLinhaImportacao.Complementar : ClassificacaoLinhaImportacao.Conflito, difs);
    }

    public static string? Formatar(DateTime? d) => d?.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    // Ordem importa: o operador de carga (Bunge, Amaggi) vem antes do nome do terminal
    // ("TGPM - BUNGE" é carga da Bunge; "TGPM - MILHO" é da própria TGPM).
    private static readonly (string Termo, string Operador)[] Operadores =
        [("BUNGE", "BUNGE"), ("AMAGGI", "AMAGGI"), ("TGPM", "TGPM"), ("HIDROVIAS", "HIDROVIAS"), ("UNITAPAJO", "UNITAPAJOS")];

    // "SOJA SEGREGADO" antes de "SOJA".
    private static readonly (string Termo, string Carga)[] Cargas =
        [("SOJA SEGREGADO", "Soja Segregado"), ("SOJA", "Soja"), ("MILHO", "Milho"), ("SORGO", "Sorgo"), ("CACAMBA", "Caçamba"), ("CAÇAMBA", "Caçamba")];

    /// <summary>
    /// Dedução de operador e carga pelo nome do convênio — a mesma que gerou
    /// os campos operador/carga dos datasets originais (ex.: "UNITAPAJOS BUNGE SOJA"
    /// → BUNGE / Soja; "HIDROVIAS" → HIDROVIAS / Outro). É só um ponto de partida:
    /// o convênio fica marcado como "deduzido" até um Gestor confirmar.
    /// </summary>
    public static (string Operador, string Carga) DeduzirOperadorCarga(string nomeConvenio)
    {
        var nome = nomeConvenio.ToUpperInvariant();
        var operador = Operadores.FirstOrDefault(o => nome.Contains(o.Termo)).Operador ?? "OUTRO";
        var carga = Cargas.FirstOrDefault(c => nome.Contains(c.Termo)).Carga ?? "Outro";
        return (operador, carga);
    }
}
