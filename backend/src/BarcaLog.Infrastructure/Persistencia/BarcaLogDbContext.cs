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

    public Task<int> SalvarAsync(CancellationToken ct = default) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BarcaLogDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums gravados como texto: legível em consultas SQL e estável se a ordem do enum mudar.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
