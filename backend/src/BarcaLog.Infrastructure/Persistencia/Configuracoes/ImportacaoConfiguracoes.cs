using BarcaLog.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BarcaLog.Infrastructure.Persistencia.Configuracoes;

public class ConvenioConfiguracao : IEntityTypeConfiguration<Convenio>
{
    public void Configure(EntityTypeBuilder<Convenio> b)
    {
        b.ToTable("Convenios");
        b.HasKey(x => x.Codigo);
        b.Property(x => x.Codigo).HasMaxLength(32);
        b.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        b.Property(x => x.Operador).HasMaxLength(50).IsRequired();
        b.Property(x => x.Carga).HasMaxLength(50).IsRequired();
        b.Property(x => x.CriadoEm).EmUtc();
        b.VersaoLinha();
    }
}

public class LoteImportacaoConfiguracao : IEntityTypeConfiguration<LoteImportacao>
{
    public void Configure(EntityTypeBuilder<LoteImportacao> b)
    {
        b.ToTable("LotesImportacao");
        b.Property(x => x.Arquivo).HasMaxLength(260).IsRequired();
        b.Property(x => x.HashSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        b.Property(x => x.TerminalId).HasMaxLength(32).IsRequired();
        b.Property(x => x.CriadoPor).HasMaxLength(200).IsRequired();
        b.Property(x => x.ConcluidoPor).HasMaxLength(200);
        b.Property(x => x.CriadoEm).EmUtc();
        b.Property(x => x.ConcluidoEm).EmUtc();
        b.Property(x => x.MarcacaoInicial).HasColumnType("datetime2(0)");
        b.Property(x => x.MarcacaoFinal).HasColumnType("datetime2(0)");
        b.HasOne(x => x.Terminal).WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.CriadoEm);
        b.HasIndex(x => new { x.HashSha256, x.TerminalId, x.Status });
        b.VersaoLinha();
    }
}

public class LinhaImportacaoConfiguracao : IEntityTypeConfiguration<LinhaImportacao>
{
    public void Configure(EntityTypeBuilder<LinhaImportacao> b)
    {
        b.ToTable("LinhasImportacao");
        b.Property(x => x.MovimentoId).HasMaxLength(32).IsRequired();
        b.Property(x => x.Senha).HasMaxLength(32);
        b.Property(x => x.Convenio).HasMaxLength(100);
        b.Property(x => x.CodConvenio).HasMaxLength(32);
        b.Property(x => x.Operador).HasMaxLength(50);
        b.Property(x => x.Carga).HasMaxLength(50);
        b.Property(x => x.Motivo).HasMaxLength(1000);
        // [Flags]: combinação de bits, gravada como número (não cabe no CHECK de texto dos outros enums).
        b.Property(x => x.Avisos).HasConversion<int>();
        b.Property(x => x.DataMarcacao).HasColumnType("datetime2(0)");
        b.Property(x => x.DataLiberacao).HasColumnType("datetime2(0)");
        b.HasOne<LoteImportacao>().WithMany().HasForeignKey(x => x.LoteId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.LoteId, x.Classificacao, x.NumeroLinha });
        b.HasIndex(x => new { x.LoteId, x.MovimentoId });
    }
}
