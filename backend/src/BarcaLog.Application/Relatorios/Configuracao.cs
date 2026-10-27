using System.ComponentModel.DataAnnotations;

namespace BarcaLog.Application.Relatorios
{
    /// <summary>Configuração da central de relatórios (seção "Relatorios" do appsettings).</summary>
    public class RelatoriosOptions
    {
        public const string Secao = "Relatorios";

        /// <summary>
        /// Referência operacional provisória de carretas por dia, POR TERMINAL, usada em
        /// "dias acima da referência diária" quando o terminal não tem capacidade cadastrada.
        /// Não é capacidade oficial. Altere aqui (ou Relatorios__ReferenciaDiariaCarretas)
        /// sem mexer no código.
        /// </summary>
        public int ReferenciaDiariaCarretas { get; set; } = 1000;

        /// <summary>Organização responsável, impressa no cabeçalho quando preenchida (ex.: o cliente que opera o sistema).</summary>
        public string? Organizacao { get; set; }

        /// <summary>
        /// Contexto externo documentado (safra, mercado, operação portuária), com fonte e data de
        /// consulta. Vem de Relatorios/contexto-externo.json — atualizável sem recompilar; nada é
        /// buscado na internet na hora de gerar o relatório.
        /// </summary>
        public List<FonteContexto> ContextoExterno { get; set; } = [];
    }

    /// <remarks>
    /// <para>Tema: soja | milho | arco-norte | barcarena.</para>
    /// </remarks>
    public class FonteContexto
    {
        public string Tema { get; set; } = "";
        public string Texto { get; set; } = "";
        public string Fonte { get; set; } = "";
        public string? Url { get; set; }
        public string? Publicacao { get; set; }
        public string? ConsultadoEm { get; set; }
    }

    /// <summary>Pedido de relatório: tipo, período (datas de marcação) e recorte.</summary>
    public class FiltroRelatorio : IValidatableObject
    {
        public static readonly string[] Tipos = ["gerencial", "operacional", "analise360"];

        /// <summary>gerencial | operacional | analise360.</summary>
        [Required(ErrorMessage = "Escolha o tipo de relatório.")]
        [RegularExpression("^(gerencial|operacional|analise360)$", ErrorMessage = "Tipo inválido: use gerencial, operacional ou analise360.")]
        public string Tipo { get; set; } = "gerencial";

        /// <summary>Início do período (data de marcação). Sem datas: o último mês completo com dados.</summary>
        public DateOnly? De { get; set; }
        public DateOnly? Ate { get; set; }
        [StringLength(32)] public string? TerminalId { get; set; }
        [StringLength(50)] public string? Operador { get; set; }
        [StringLength(50)] public string? Carga { get; set; }

        /// <summary>resumido | completo (completo inclui tabelas diárias, convênios e rankings maiores).</summary>
        [RegularExpression("^(resumido|completo)$", ErrorMessage = "Use resumido ou completo.")]
        public string Detalhamento { get; set; } = "completo";

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if ((De is null) != (Ate is null))
                yield return new ValidationResult("Informe as duas datas do período, ou nenhuma para usar o último mês completo.", [nameof(De), nameof(Ate)]);
            if (De is { } d && Ate is { } a)
            {
                if (d > a) yield return new ValidationResult("A data inicial não pode ser maior que a final.", [nameof(De), nameof(Ate)]);
                else if (a.DayNumber - d.DayNumber > 366) yield return new ValidationResult("Período máximo de um ano por relatório.", [nameof(De), nameof(Ate)]);
            }
        }
    }
}
