using System.ComponentModel.DataAnnotations;
using BarcaLog.Domain.Regras;

namespace BarcaLog.Application.Dtos;

/// <summary>Placa no padrão antigo (ABC-1234) ou Mercosul (ABC1D23). Nulo/vazio passa (use [Required]).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class PlacaValidaAttribute : ValidationAttribute
{
    public PlacaValidaAttribute() : base("Placa inválida. Use o padrão ABC-1234 ou Mercosul ABC1D23.") { }

    public override bool IsValid(object? value) => value is not string s || string.IsNullOrWhiteSpace(s) || Placa.EhValida(s);
}

/// <summary>CNPJ numérico ou alfanumérico com dígitos verificadores corretos.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class CnpjValidoAttribute : ValidationAttribute
{
    public CnpjValidoAttribute() : base("CNPJ inválido.") { }

    public override bool IsValid(object? value) => value is not string s || string.IsNullOrWhiteSpace(s) || Cnpj.EhValido(s);
}

/// <summary>Sem caracteres de controle (quebra de linha etc.) — evita log injection e lixo em relatórios.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class TextoSimplesAttribute : ValidationAttribute
{
    public TextoSimplesAttribute() : base("O campo {0} contém caracteres inválidos.") { }

    public override bool IsValid(object? value) => value is not string s || !s.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'));
}

internal static class ValidacaoPeriodo
{
    public static IEnumerable<ValidationResult> Validar(DateOnly? de, DateOnly? ate)
    {
        if (de is { } d && ate is { } a && d > a)
            yield return new ValidationResult("A data inicial ('de') não pode ser maior que a final ('ate').", ["De", "Ate"]);
    }
}
