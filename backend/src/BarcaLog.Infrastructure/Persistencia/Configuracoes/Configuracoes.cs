using BarcaLog.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BarcaLog.Infrastructure.Persistencia.Configuracoes;

public class TerminalConfiguracao : IEntityTypeConfiguration<Terminal>
{
    public void Configure(EntityTypeBuilder<Terminal> b)
    {
        b.ToTable("Terminais", t => t.HasCheckConstraint("CK_Terminais_Capacidade", "[CapacidadeDiariaCarretas] > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(32);
        b.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        b.Property(x => x.CapacidadeDiariaCarretas).HasDefaultValue(Terminal.CapacidadeNominalPadrao);
        b.HasIndex(x => x.Nome).IsUnique();

        // Os 3 terminais reais (mesmos ids de src/data/registry.js).
        b.HasData(
            new Terminal { Id = "unitapajos", Nome = "Unitapajós", CapacidadeDiariaCarretas = 1000 },
            new Terminal { Id = "tgpm", Nome = "TGPM", CapacidadeDiariaCarretas = 1000 },
            new Terminal { Id = "hidrovias", Nome = "Hidrovias", CapacidadeDiariaCarretas = 1000 });
    }
}

public class MarcacaoConfiguracao : IEntityTypeConfiguration<Marcacao>
{
    public void Configure(EntityTypeBuilder<Marcacao> b)
    {
        b.ToTable("Marcacoes", t => t.HasCheckConstraint("CK_Marcacoes_Liberacao", "[DataLiberacao] IS NULL OR [DataLiberacao] >= [DataMarcacao]"));
        b.HasKey(x => x.MovimentoId);
        b.Property(x => x.MovimentoId).HasMaxLength(32);
        b.Property(x => x.Senha).HasMaxLength(32).IsRequired();
        b.Property(x => x.Convenio).HasMaxLength(100).IsRequired();
        b.Property(x => x.CodConvenio).HasMaxLength(32);
        b.Property(x => x.Operador).HasMaxLength(50).IsRequired();
        b.Property(x => x.Carga).HasMaxLength(50).IsRequired();
        b.Property(x => x.TerminalId).HasMaxLength(32).IsRequired();
        b.Property(x => x.DataMarcacao).HasColumnType("datetime2(0)");
        b.Property(x => x.DataLiberacao).HasColumnType("datetime2(0)");

        // Mesma regra do campo esperaHoras dos datasets: horas com 2 casas.
        b.Property(x => x.EsperaHoras)
            .HasColumnType("decimal(10,2)")
            .HasConversion<decimal?>()
            .HasComputedColumnSql(
                "CONVERT(decimal(10,2), ROUND(DATEDIFF(SECOND, [DataMarcacao], [DataLiberacao]) / 3600.0, 2))",
                stored: true);

        b.HasOne(x => x.Terminal).WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TerminalId, x.DataMarcacao });
        b.HasIndex(x => x.DataMarcacao);
        b.HasIndex(x => x.Senha);
        b.HasIndex(x => x.Convenio);
    }
}

public class TransportadoraConfiguracao : IEntityTypeConfiguration<Transportadora>
{
    public void Configure(EntityTypeBuilder<Transportadora> b)
    {
        b.ToTable("Transportadoras");
        b.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        b.Property(x => x.Cnpj).HasMaxLength(18).IsRequired();
        b.HasIndex(x => x.Nome).IsUnique();
        b.HasIndex(x => x.Cnpj).IsUnique();
    }
}

public class VeiculoConfiguracao : IEntityTypeConfiguration<Veiculo>
{
    public void Configure(EntityTypeBuilder<Veiculo> b)
    {
        b.ToTable("Veiculos");
        b.Property(x => x.Placa).HasMaxLength(10).IsRequired();
        b.Property(x => x.Modelo).HasMaxLength(100);
        b.Property(x => x.TerminalId).HasMaxLength(32).IsRequired();
        b.Property(x => x.StatusPortariaDesde).EmUtc();
        b.Ignore(x => x.EstaNegativado);
        b.HasIndex(x => x.Placa).IsUnique();
        b.HasIndex(x => new { x.StatusPortaria, x.StatusPortariaDesde });
        b.HasIndex(x => x.StatusNegativacao);
        b.HasOne(x => x.Transportadora).WithMany(t => t.Veiculos).HasForeignKey(x => x.TransportadoraId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Terminal).WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CondutorConfiguracao : IEntityTypeConfiguration<Condutor>
{
    public void Configure(EntityTypeBuilder<Condutor> b)
    {
        b.ToTable("Condutores");
        b.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        b.Property(x => x.PlacaVinculada).HasMaxLength(10);
        b.HasIndex(x => x.PlacaVinculada);
        b.HasOne(x => x.Transportadora).WithMany(t => t.Condutores).HasForeignKey(x => x.TransportadoraId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OcorrenciaConfiguracao : IEntityTypeConfiguration<Ocorrencia>
{
    public void Configure(EntityTypeBuilder<Ocorrencia> b)
    {
        b.ToTable("Ocorrencias");
        b.Property(x => x.Placa).HasMaxLength(10).IsRequired();
        b.Property(x => x.Descricao).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Local).HasMaxLength(150);
        b.Property(x => x.Responsavel).HasMaxLength(150);
        b.Property(x => x.CriadoEm).EmUtc();
        b.Ignore(x => x.BloqueiaAutomaticamente);
        b.HasIndex(x => x.Placa);
        b.HasIndex(x => new { x.TransportadoraId, x.Nivel, x.CriadoEm });
        b.HasIndex(x => x.CriadoEm);
        b.HasOne(x => x.Transportadora).WithMany().HasForeignKey(x => x.TransportadoraId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Condutor).WithMany().HasForeignKey(x => x.CondutorId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ContestacaoConfiguracao : IEntityTypeConfiguration<Contestacao>
{
    public void Configure(EntityTypeBuilder<Contestacao> b)
    {
        b.ToTable("Contestacoes");
        b.Property(x => x.Justificativa).HasMaxLength(4000).IsRequired();
        b.Property(x => x.RespostaOperador).HasMaxLength(2000);
        b.Property(x => x.CriadoEm).EmUtc();
        b.Property(x => x.RespondidoEm).EmUtc();
        b.HasIndex(x => new { x.OcorrenciaId, x.Status });
        b.HasIndex(x => x.TransportadoraId);
        b.HasOne(x => x.Ocorrencia).WithMany().HasForeignKey(x => x.OcorrenciaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Transportadora).WithMany().HasForeignKey(x => x.TransportadoraId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class LogAuditoriaConfiguracao : IEntityTypeConfiguration<LogAuditoria>
{
    public void Configure(EntityTypeBuilder<LogAuditoria> b)
    {
        b.ToTable("LogsAuditoria");
        b.Property(x => x.Autor).HasMaxLength(200).IsRequired();
        b.Property(x => x.Acao).HasMaxLength(200).IsRequired();
        b.Property(x => x.Detalhes).IsRequired();
        b.Property(x => x.Quando).EmUtc();
        b.HasIndex(x => x.Quando);
        b.HasIndex(x => x.Autor);
        b.HasIndex(x => x.Acao);
    }
}

public class AgendamentoConfiguracao : IEntityTypeConfiguration<Agendamento>
{
    public void Configure(EntityTypeBuilder<Agendamento> b)
    {
        b.ToTable("Agendamentos");
        b.Property(x => x.Placa).HasMaxLength(10).IsRequired();
        b.Property(x => x.TerminalId).HasMaxLength(32).IsRequired();
        b.Property(x => x.Carga).HasMaxLength(50).IsRequired();
        b.Property(x => x.Motorista).HasMaxLength(150);
        b.Ignore(x => x.Codigo);
        b.HasIndex(x => new { x.Data, x.Hora });
        b.HasIndex(x => x.Placa);
        b.HasIndex(x => new { x.TerminalId, x.Data });
        b.HasOne(x => x.Transportadora).WithMany().HasForeignKey(x => x.TransportadoraId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Terminal).WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.SenhaHash).HasMaxLength(500).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
    }
}
