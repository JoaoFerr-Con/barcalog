using System.ComponentModel.DataAnnotations;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;
using BarcaLog.Domain.Excecoes;
using BarcaLog.Domain.Regras;

namespace BarcaLog.Application.Dtos
{
    /// <summary>Recorte da análise 360°: período (data de marcação) e dimensões que existem nos dados.</summary>
    public class FiltroAnaliseMovimentacoes : IValidatableObject
    {
        /// <summary>Início (data de marcação). Sem período: os 30 dias até a última marcação do recorte.</summary>
        public DateOnly? De { get; set; }
        public DateOnly? Ate { get; set; }
        [StringLength(32)] public string? TerminalId { get; set; }
        [StringLength(50)] public string? Operador { get; set; }
        [StringLength(50)] public string? Carga { get; set; }
        /// <summary>Convênio exato (nome como na planilha).</summary>
        [StringLength(100)] public string? Convenio { get; set; }
        public Domain.Enums.OrigemMarcacao? Origem { get; set; }
        /// <summary>dia | semana | mes (padrão: pela duração do período).</summary>
        [RegularExpression("^(dia|semana|mes)$", ErrorMessage = "Use dia, semana ou mes.")]
        public string? Agrupar { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            foreach (var r in ValidacaoPeriodo.Validar(De, Ate)) yield return r;
            if (De is { } d && Ate is { } a && a.DayNumber - d.DayNumber > 731)
                yield return new ValidationResult("Período máximo de 2 anos.", [nameof(De), nameof(Ate)]);
        }
    }
}

namespace BarcaLog.Application.Servicos
{
    /// <summary>Análise 360° das movimentações (Movimentações → Análise).</summary>
    public class AnaliseMovimentacoesServico(IMarcacaoRepositorio marcacoes, ITerminalRepositorio terminais, IEscopoUsuario escopo)
    {
        public async Task<ResultadoAnalise> AnalisarAsync(FiltroAnaliseMovimentacoes f, CancellationToken ct = default)
        {
            f.TerminalId = escopo.RestringirTerminal(f.TerminalId);
            var parametros = await terminais.ObterParametrosAsync(false, ct);
            DateOnly de, ate;
            if (f.De is { } d && f.Ate is { } a) (de, ate) = (d, a);
            else
            {
                var ultima = await marcacoes.UltimaMarcacaoAsync(f.TerminalId, ct) ?? throw new NaoEncontradoException("Não há movimentações para analisar neste recorte.");
                ate = f.Ate ?? DateOnly.FromDateTime(ultima);
                de = f.De ?? ate.AddDays(-29);
                if (de > ate) throw new RegraNegocioException("A data inicial é posterior à última movimentação do recorte.");
            }
            var (ade, _) = AnaliseMovimentacoes.PeriodoAnterior(de, ate);
            var regs = await marcacoes.ListarParaAnaliseAsync(f, ade.ToDateTime(TimeOnly.MinValue), ate.AddDays(1).ToDateTime(TimeOnly.MinValue), ct);
            return AnaliseMovimentacoes.Calcular(regs, de, ate, f.Agrupar, parametros.LimiteEsperaLongaHoras, RegrasImportacao.JanelaRemarcacaoMinutos);
        }
    }
}
