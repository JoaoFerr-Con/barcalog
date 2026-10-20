using BarcaLog.Application.Abstracoes;
using BarcaLog.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace BarcaLog.Infrastructure.Persistencia;

public class BarcaLogDbContext(DbContextOptions<BarcaLogDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Terminal> Terminais => Set<Terminal>();
    public DbSet<Marcacao> Marcacoes => Set<Marcacao>();
    public DbSet<Transportadora> Transportadoras => Set<Transportadora>();
    public DbSet<Veiculo> Veiculos => Set<Veiculo>();
    public DbSet<Condutor> Condutores => Set<Condutor>();
    public DbSet<Ocorrencia> Ocorrencias => Set<Ocorrencia>();
    public DbSet<Contestacao> Contestacoes => Set<Contestacao>();
    public DbSet<LogAuditoria> LogsAuditoria => Set<LogAuditoria>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Idempotencia.ChaveIdempotencia> ChavesIdempotencia => Set<Idempotencia.ChaveIdempotencia>();
    public DbSet<Convenio> Convenios => Set<Convenio>();
    public DbSet<LoteImportacao> LotesImportacao => Set<LoteImportacao>();
    public DbSet<LinhaImportacao> LinhasImportacao => Set<LinhaImportacao>();

    public Task<int> SalvarAsync(CancellationToken ct = default) => SaveChangesAsync(ct);

    public Task ExecutarEmTransacaoAsync(Func<CancellationToken, Task> acao, CancellationToken ct = default)
    {
        // Já dentro de uma transação (chamada aninhada): só executa.
        if (Database.CurrentTransaction is not null) return acao(ct);
        return Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await Database.BeginTransactionAsync(ct);
            await acao(ct);
            await tx.CommitAsync(ct);
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BarcaLogDbContext).Assembly);
        AdicionarChecksDeEnum(modelBuilder);
    }

    /// <summary>
    /// Enums são gravados como texto; sem CHECK, um UPDATE manual ou bug poderia
    /// gravar "Negativadaa". Geramos CK_{Tabela}_{Coluna} IN (...) pra todos.
    /// </summary>
    private static void AdicionarChecksDeEnum(ModelBuilder modelBuilder)
    {
        foreach (var entidade in modelBuilder.Model.GetEntityTypes())
        {
            var tabela = entidade.GetTableName();
            if (tabela is null) continue;
            foreach (var prop in entidade.GetProperties())
            {
                var tipo = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
                if (!tipo.IsEnum) continue;
                // [Flags] guarda combinações (gravadas como número): não cabem num IN (...) de nomes.
                if (tipo.IsDefined(typeof(FlagsAttribute), false)) continue;
                var coluna = prop.GetColumnName();
                var valores = string.Join(", ", Enum.GetNames(tipo).Select(n => $"N'{n}'"));
                var nulo = prop.IsNullable ? $"[{coluna}] IS NULL OR " : "";
                modelBuilder.Entity(entidade.ClrType).ToTable(t => t.HasCheckConstraint($"CK_{tabela}_{coluna}", $"{nulo}[{coluna}] IN ({valores})"));
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums gravados como texto: legível em consultas SQL e estável se a ordem do enum mudar.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
