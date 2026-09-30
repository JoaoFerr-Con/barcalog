namespace BarcaLog.Domain.Entidades;

/// <summary>
/// Um movimento real de marcação → liberação de carreta (os 103k registros
/// importados de src/data/datasets/*.json, e os que chegam via integração).
/// </summary>
public class Marcacao
{
    public string MovimentoId { get; set; } = null!;
    public string Senha { get; set; } = null!;
    public string Convenio { get; set; } = null!;
    /// <summary>Código do convênio no sistema de origem. Os JSON atuais não trazem esse campo.</summary>
    public string? CodConvenio { get; set; }
    public string Operador { get; set; } = null!;
    public string Carga { get; set; } = null!;
    public int Ciclo { get; set; }

    /// <summary>Horário local do porto (sem fuso), igual ao dado de origem.</summary>
    public DateTime DataMarcacao { get; set; }

    /// <summary>Nulo enquanto a carreta ainda não foi liberada (evento de integração pendente).</summary>
    public DateTime? DataLiberacao { get; set; }

    public string TerminalId { get; set; } = null!;
    public Terminal? Terminal { get; set; }

    /// <summary>
    /// Coluna calculada no banco: (DataLiberacao − DataMarcacao) em horas,
    /// arredondada em 2 casas — mesma regra do campo esperaHoras dos JSON.
    /// </summary>
    public double? EsperaHoras { get; private set; }

    /// <summary>Mesmo cálculo da coluna computada, disponível em memória.</summary>
    public double? CalcularEsperaHoras() =>
        DataLiberacao is null ? null : Regras.JanelaPermanencia.EsperaEmHoras(DataMarcacao, DataLiberacao.Value);
}
