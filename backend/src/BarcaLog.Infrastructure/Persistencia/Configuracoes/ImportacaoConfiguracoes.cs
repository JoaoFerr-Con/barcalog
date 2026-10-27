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

public class AlertaConfiguracao : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> b)
    {
        b.ToTable("Alertas");
        b.Property(x => x.Chave).HasMaxLength(Alerta.Tamanho.Chave).IsRequired();
        b.HasIndex(x => x.Chave).IsUnique();
        b.Property(x => x.Regra).HasMaxLength(Alerta.Tamanho.Regra).IsRequired();
        b.Property(x => x.TerminalId).HasMaxLength(32);
        b.Property(x => x.Periodo).HasMaxLength(Alerta.Tamanho.Periodo).IsRequired();
        b.Property(x => x.Titulo).HasMaxLength(Alerta.Tamanho.Titulo).IsRequired();
        b.Property(x => x.Indicador).HasMaxLength(Alerta.Tamanho.Indicador).IsRequired();
        b.Property(x => x.ValorObservado).HasMaxLength(Alerta.Tamanho.ValorObservado).IsRequired();
        b.Property(x => x.Limite).HasMaxLength(Alerta.Tamanho.Limite).IsRequired();
        b.Property(x => x.Evidencia).HasMaxLength(Alerta.Tamanho.Evidencia).IsRequired();
        b.Property(x => x.Origem).HasMaxLength(Alerta.Tamanho.Origem).IsRequired();
        b.Property(x => x.AcaoRecomendada).HasMaxLength(Alerta.Tamanho.AcaoRecomendada);
        b.Property(x => x.Responsavel).HasMaxLength(200);
        b.Property(x => x.IdentificadoEm).EmUtc();
        b.Property(x => x.AtualizadoEm).EmUtc();
        b.HasIndex(x => new { x.Ativo, x.Estado, x.Tipo });
        b.HasMany(x => x.Eventos).WithOne().HasForeignKey(e => e.AlertaId).OnDelete(DeleteBehavior.Cascade);
        b.VersaoLinha();
    }
}

public class EventoAlertaConfiguracao : IEntityTypeConfiguration<EventoAlerta>
{
    public void Configure(EntityTypeBuilder<EventoAlerta> b)
    {
        b.ToTable("EventosAlerta");
        b.Property(x => x.Autor).HasMaxLength(200).IsRequired();
        b.Property(x => x.Acao).HasMaxLength(50).IsRequired();
        b.Property(x => x.Texto).HasMaxLength(2000);
        b.Property(x => x.Quando).EmUtc();
        b.HasIndex(x => x.AlertaId);
    }
}

public class AvaliacaoAlertasConfiguracao : IEntityTypeConfiguration<AvaliacaoAlertas>
{
    public void Configure(EntityTypeBuilder<AvaliacaoAlertas> b)
    {
        b.ToTable("AvaliacoesAlertas");
        b.Property(x => x.Inicio).EmUtc();
        b.Property(x => x.Fim).EmUtc();
        b.Property(x => x.UltimaImportacaoEm).EmUtc();
        b.Property(x => x.Autor).HasMaxLength(200).IsRequired();
        b.Property(x => x.Erro).HasMaxLength(500);
        b.Property(x => x.Confiabilidade).HasMaxLength(20);
        b.HasIndex(x => new { x.Sucesso, x.Inicio });
    }
}
