using System.Globalization;
using System.Text;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BarcaLog.Infrastructure.Auditoria;

/// <summary>
/// Gera automaticamente uma linha de LogAuditoria para TODA gravação que
/// cria/altera/remove entidades — dentro da mesma transação do SaveChanges.
/// Nenhum endpoint precisa lembrar de logar; os serviços só podem, se
/// quiserem, dar um rótulo de negócio à ação via <see cref="IContextoAuditoria"/>.
/// </summary>
public class AuditoriaInterceptor(IUsuarioAtual usuarioAtual, IContextoAuditoria contexto, TimeProvider tempo) : SaveChangesInterceptor
{
    /// <summary>Acima disso por tipo/estado, o detalhe vira só uma contagem (ex.: importação em lote).</summary>
    private const int LimiteDetalhePorGrupo = 10;

    /// <summary>Nunca aparecem com valor no log (segredos). Só "campo alterado".</summary>
    private static readonly HashSet<string> PropriedadesOcultas = new(StringComparer.Ordinal)
    {
        nameof(Usuario.SenhaHash), nameof(Usuario.MfaSegredoCifrado), nameof(Usuario.MfaUltimoPassoUsado), "VersaoLinha"
    };
    private static readonly string[] PropriedadesIdentificadoras = ["Placa", "Nome", "Email", "MovimentoId"];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) Registrar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) Registrar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        contexto.Limpar();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        contexto.Limpar();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        contexto.Limpar();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        contexto.Limpar();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Registrar(DbContext ctx)
    {
        // O interceptor roda antes do DetectChanges automático do SaveChanges.
        ctx.ChangeTracker.DetectChanges();
        var entradas = ctx.ChangeTracker.Entries()
            .Where(e => e.Entity is not (LogAuditoria or Idempotencia.ChaveIdempotencia) && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.State != EntityState.Modified || e.Properties.Any(p => p.IsModified))
            .ToList();
        if (entradas.Count == 0) return;

        var grupos = entradas.GroupBy(e => (Tipo: e.Metadata.ClrType.Name, e.State)).ToList();
        var detalhes = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(contexto.Detalhes)) detalhes.AppendLine(contexto.Detalhes);

        foreach (var grupo in grupos)
        {
            var lista = grupo.ToList();
            if (lista.Count > LimiteDetalhePorGrupo)
            {
                detalhes.AppendLine($"{grupo.Key.Tipo}: {lista.Count} registro(s) {Verbo(grupo.Key.State)}");
                continue;
            }
            foreach (var entrada in lista) detalhes.AppendLine(Descrever(entrada, contexto.OcultarValores));
        }

        var acao = contexto.Acao ?? string.Join("; ", grupos.Select(g => $"{g.Key.Tipo} {Verbo(g.Key.State)}"));
        ctx.Set<LogAuditoria>().Add(new LogAuditoria
        {
            Autor = Truncar(usuarioAtual.Autor, 200),
            Acao = Truncar(acao, 200),
            Detalhes = detalhes.ToString().TrimEnd(),
            Quando = tempo.GetUtcNow().UtcDateTime
        });
    }

    private static string Descrever(EntityEntry entrada, bool ocultarValores)
    {
        var tipo = entrada.Metadata.ClrType.Name;
        var identificacao = Identificar(entrada, ocultarValores);
        switch (entrada.State)
        {
            case EntityState.Added:
                var valores = entrada.Properties
                    .Where(p => !p.Metadata.IsPrimaryKey() && !PropriedadesOcultas.Contains(p.Metadata.Name) && p.Metadata.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never && p.CurrentValue is not null)
                    .Select(p => ocultarValores ? p.Metadata.Name : $"{p.Metadata.Name}={Formatar(p.CurrentValue)}");
                return $"{tipo} criado {identificacao}: {string.Join(", ", valores)}";
            case EntityState.Modified:
                var mudancas = entrada.Properties
                    .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                    .Select(p => ocultarValores || PropriedadesOcultas.Contains(p.Metadata.Name)
                        ? $"{p.Metadata.Name} alterado"
                        : $"{p.Metadata.Name}: {Formatar(p.OriginalValue)} → {Formatar(p.CurrentValue)}");
                return $"{tipo} alterado {identificacao}: {string.Join(", ", mudancas)}";
            default:
                return $"{tipo} removido {identificacao}";
        }
    }

    private static string Identificar(EntityEntry entrada, bool ocultarValores)
    {
        var partes = new List<string>();
        var chave = entrada.Metadata.FindPrimaryKey();
        if (chave is not null)
        {
            foreach (var p in chave.Properties)
            {
                var prop = entrada.Property(p.Name);
                if (!prop.IsTemporary) partes.Add($"#{Formatar(prop.CurrentValue)}");
            }
        }
        if (ocultarValores) return string.Join(" ", partes);
        foreach (var nome in PropriedadesIdentificadoras)
        {
            var metaProp = entrada.Metadata.FindProperty(nome);
            if (metaProp is not null && !metaProp.IsPrimaryKey() && entrada.Property(nome).CurrentValue is { } v)
            {
                partes.Add($"({v})");
                break;
            }
        }
        return string.Join(" ", partes);
    }

    private static string Verbo(EntityState estado) => estado switch
    {
        EntityState.Added => "criado",
        EntityState.Modified => "alterado",
        _ => "removido"
    };

    private static string Formatar(object? valor) => valor switch
    {
        null => "∅",
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString() ?? ""
    };

    private static string Truncar(string texto, int max) => texto.Length <= max ? texto : texto[..(max - 1)] + "…";
}

