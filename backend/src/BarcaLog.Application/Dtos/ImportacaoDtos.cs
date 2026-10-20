using System.ComponentModel.DataAnnotations;
using BarcaLog.Domain.Enums;

namespace BarcaLog.Application.Dtos;

public sealed record ConvenioDto(string Codigo, string Nome, string Operador, string Carga, bool Deduzido);

public class AtualizarConvenioRequest
{
    [Required, StringLength(50, MinimumLength = 1), TextoSimples] public string Operador { get; set; } = null!;
    [Required, StringLength(50, MinimumLength = 1), TextoSimples] public string Carga { get; set; } = null!;
}

/// <summary>Quantas linhas novas/complementares entram com cada sinal de qualidade.</summary>
public sealed record AvisosImportacaoDto(int EsperaAcima72h, int SemLiberacao, int PossivelRemarcacao, int ConvenioNovo);

public sealed record LoteImportacaoDto(
    int Id,
    string Arquivo,
    FormatoArquivoImportacao Formato,
    long TamanhoBytes,
    string TerminalId,
    string? Terminal,
    StatusLoteImportacao Status,
    string CriadoPor,
    DateTime CriadoEm,
    string? ConcluidoPor,
    DateTime? ConcluidoEm,
    DateTime? MarcacaoInicial,
    DateTime? MarcacaoFinal,
    int Linhas,
    int Novos,
    int Iguais,
    int Complementares,
    int Conflitantes,
    int ConflitosPendentes,
    int Rejeitados,
    int ComAvisos,
    AvisosImportacaoDto Avisos,
    IReadOnlyList<ConvenioDto> ConveniosNovos,
    int Inseridos,
    int Atualizados,
    int Ignorados,
    /// <summary>Preenchido quando um arquivo idêntico (mesmo SHA-256) já foi confirmado para o terminal.</summary>
    DateTime? MesmoArquivoConfirmadoEm);

public sealed record LinhaImportacaoDto(
    long Id,
    int NumeroLinha,
    ClassificacaoLinhaImportacao Classificacao,
    DecisaoConflitoImportacao Decisao,
    string? Motivo,
    IReadOnlyList<string> Avisos,
    string MovimentoId,
    string? Senha,
    string? Convenio,
    string? CodConvenio,
    int? Ciclo,
    DateTime? DataMarcacao,
    DateTime? DataLiberacao,
    double? EsperaHoras);

public class FiltroLotesImportacao : FiltroPaginado
{
    [StringLength(32)] public string? TerminalId { get; set; }
    public StatusLoteImportacao? Status { get; set; }
}

public class FiltroLinhasImportacao : FiltroPaginado
{
    public ClassificacaoLinhaImportacao? Classificacao { get; set; }
    /// <summary>Só linhas com este aviso.</summary>
    public AvisoImportacao? Aviso { get; set; }
    public DecisaoConflitoImportacao? Decisao { get; set; }
}

public class DecidirConflitosRequest
{
    /// <summary>Aplicar (gravar os valores da planilha) ou Manter (deixar como está).</summary>
    [Required] public DecisaoConflitoImportacao Decisao { get; set; }
    /// <summary>Linhas a decidir. Vazio = todos os conflitos ainda pendentes do lote.</summary>
    [MaxLength(5000)] public List<long>? Linhas { get; set; }
}

public sealed record ResultadoDecisaoDto(int Decididas, int AindaPendentes);
