using BarcaLog.Application.Relatorios;
using BarcaLog.Domain.Entidades;
using BarcaLog.Infrastructure.Persistencia.Configuracoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BarcaLog.Infrastructure.Persistencia.Repositorios;

public class RelatorioGeradoRepositorio(BarcaLogDbContext db) : IRelatorioGeradoRepositorio
{
    public Task AdicionarAsync(RelatorioGerado relatorio, CancellationToken ct = default)
    {
        db.RelatoriosGerados.Add(relatorio);
        return Task.CompletedTask;
    }

    public Task<RelatorioGerado?> ObterAsync(int id, CancellationToken ct = default) =>
        db.RelatoriosGerados.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<(List<RelatorioGerado> Itens, int Total)> ListarAsync(string? terminalId, int pagina, int tamanho, CancellationToken ct = default)
    {
        var q = db.RelatoriosGerados.AsNoTracking().Where(r => terminalId == null || r.TerminalId == terminalId);
        var total = await q.CountAsync(ct);
        var itens = await q.OrderByDescending(r => r.GeradoEm).ThenByDescending(r => r.Id).Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
        return (itens, total);
    }
}

public class RelatorioGeradoConfiguracao : IEntityTypeConfiguration<RelatorioGerado>
{
    public void Configure(EntityTypeBuilder<RelatorioGerado> b)
    {
        b.ToTable("RelatoriosGerados");
        b.Property(x => x.Codigo).HasMaxLength(RelatorioGerado.Tamanho.Codigo).IsRequired();
        b.Property(x => x.Tipo).HasMaxLength(RelatorioGerado.Tamanho.Tipo).IsRequired();
        b.Property(x => x.Titulo).HasMaxLength(RelatorioGerado.Tamanho.Titulo).IsRequired();
        b.Property(x => x.TerminalId).HasMaxLength(32);
        b.Property(x => x.Abrangencia).HasMaxLength(RelatorioGerado.Tamanho.Abrangencia).IsRequired();
        b.Property(x => x.Filtros).HasMaxLength(RelatorioGerado.Tamanho.Filtros).IsRequired();
        b.Property(x => x.Formato).HasMaxLength(RelatorioGerado.Tamanho.Formato).IsRequired();
        b.Property(x => x.GeradoPor).HasMaxLength(RelatorioGerado.Tamanho.Autor).IsRequired();
        b.Property(x => x.HashSha256).HasMaxLength(RelatorioGerado.Tamanho.Hash).IsRequired().IsUnicode(false);
        b.Property(x => x.GeradoEm).EmUtc();
        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasIndex(x => new { x.TerminalId, x.GeradoEm });
    }
}
