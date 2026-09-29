using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BarcaLog.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Timestamps operacionais (CriadoEm, Quando, StatusPortariaDesde…) são
/// gravados em UTC; o SQL Server devolve DateTime sem Kind — marcamos como
/// UTC na leitura pra API serializar com "Z". As datas das marcações NÃO usam
/// isso: são horário local do porto, como no dado de origem.
/// </summary>
internal static class ConversoresUtc
{
    private static readonly ValueConverter<DateTime, DateTime> Utc =
        new(v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> UtcNulavel =
        new(v => v.HasValue && v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    public static PropertyBuilder<DateTime> EmUtc(this PropertyBuilder<DateTime> p) => p.HasConversion(Utc);

    public static PropertyBuilder<DateTime?> EmUtc(this PropertyBuilder<DateTime?> p) => p.HasConversion(UtcNulavel);

    /// <summary>
    /// Concorrência otimista: coluna rowversion (shadow, fora do Domain). Se
    /// duas requisições alteram o mesmo registro ao mesmo tempo, a segunda
    /// recebe DbUpdateConcurrencyException (→ HTTP 409) em vez de sobrescrever.
    /// </summary>
    public static void VersaoLinha<T>(this EntityTypeBuilder<T> b) where T : class =>
        b.Property<byte[]>("VersaoLinha").IsRowVersion();
}
