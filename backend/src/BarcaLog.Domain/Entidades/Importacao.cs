using BarcaLog.Domain.Enums;

namespace BarcaLog.Domain.Entidades;

/// <summary>
/// Convênio de um terminal, identificado pelo código do sistema de origem
/// (COD_CONVENIO das planilhas). O nome pode mudar na origem — o código não —,
/// por isso é a chave. Operador e carga são deduzidos do nome na primeira vez
/// e podem ser corrigidos por um Gestor.
/// </summary>
public class Convenio
{
    public string Codigo { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public string Operador { get; set; } = null!;
    public string Carga { get; set; } = null!;
    /// <summary>true enquanto operador/carga forem só a dedução automática pelo nome.</summary>
    public bool Deduzido { get; set; } = true;
    public DateTime CriadoEm { get; set; }
}

/// <summary>
/// Uma importação de planilha: analisada primeiro (nada gravado em Marcacoes),
/// confirmada depois por um Gestor. Guarda o arquivo de origem (nome + SHA-256)
/// e o resultado de cada etapa.
/// </summary>
public class LoteImportacao
{
    public int Id { get; set; }
    public string Arquivo { get; set; } = null!;
    public string HashSha256 { get; set; } = null!;
    public long TamanhoBytes { get; set; }
    public FormatoArquivoImportacao Formato { get; set; }
    public string TerminalId { get; set; } = null!;
    public Terminal? Terminal { get; set; }
    public StatusLoteImportacao Status { get; set; } = StatusLoteImportacao.Analisado;

    public string CriadoPor { get; set; } = null!;
    public DateTime CriadoEm { get; set; }
    public string? ConcluidoPor { get; set; }
    public DateTime? ConcluidoEm { get; set; }

    /// <summary>Período das marcações válidas no arquivo (horário local do porto).</summary>
    public DateTime? MarcacaoInicial { get; set; }
    public DateTime? MarcacaoFinal { get; set; }

    // Resultado da análise
    public int Linhas { get; set; }
    public int Novos { get; set; }
    public int Iguais { get; set; }
    public int Complementares { get; set; }
    public int Conflitantes { get; set; }
    public int Rejeitados { get; set; }
    public int ComAvisos { get; set; }
    public int AvisosEspera72h { get; set; }
    public int AvisosSemLiberacao { get; set; }
    public int AvisosRemarcacao { get; set; }
    public int AvisosConvenioNovo { get; set; }
    /// <summary>Convênios do arquivo que ainda não existem no cadastro (texto: "código · nome" por linha).</summary>
    public string? ConveniosNovos { get; set; }

    // Resultado da confirmação
    public int Inseridos { get; set; }
    public int Atualizados { get; set; }
    public int Ignorados { get; set; }
}

/// <summary>
/// Uma linha da planilha guardada para revisão. Linhas idênticas ao que já
/// existe não são guardadas (só contadas). Depois da confirmação ficam só as
/// rejeitadas e as conflitantes — o histórico do que foi recusado ou mudado.
/// </summary>
public class LinhaImportacao
{
    public long Id { get; set; }
    public int LoteId { get; set; }
    public int NumeroLinha { get; set; }
    public ClassificacaoLinhaImportacao Classificacao { get; set; }
    public DecisaoConflitoImportacao Decisao { get; set; } = DecisaoConflitoImportacao.NaoSeAplica;
    /// <summary>Motivo da rejeição ou lista das diferenças ("Convênio: A → B").</summary>
    public string? Motivo { get; set; }
    public AvisoImportacao Avisos { get; set; }

    // Valores lidos (texto original quando a linha foi rejeitada)
    public string MovimentoId { get; set; } = null!;
    public string? Senha { get; set; }
    public string? Convenio { get; set; }
    public string? CodConvenio { get; set; }
    public string? Operador { get; set; }
    public string? Carga { get; set; }
    public int? Ciclo { get; set; }
    public DateTime? DataMarcacao { get; set; }
    public DateTime? DataLiberacao { get; set; }
}
