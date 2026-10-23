using BarcaLog.Domain.Enums;

namespace BarcaLog.Domain.Regras;

/// <summary>
/// Telas internas que podem ser escolhidas como tela inicial e o padrão de cada papel.
/// Padrão por papel (proposta, ajustável pelo próprio usuário):
/// Gestor → Visão Geral; Operador → Gargalos e alertas (o que precisa ser tratado);
/// Auditor → Movimentações (registros e qualidade dos dados).
/// </summary>
public static class PreferenciasUsuario
{
    public static readonly IReadOnlySet<string> Telas = new HashSet<string>(StringComparer.Ordinal)
    {
        "visao-geral", "portaria", "agendamentos", "gargalos", "movimentacoes", "movimentacoes-analise",
        "importacao", "negativacao", "carretas", "frotas", "auditoria", "configuracoes"
    };

    public static string TelaPadrao(PapelUsuario papel) => papel switch
    {
        PapelUsuario.Operador => "gargalos",
        PapelUsuario.Auditor => "movimentacoes",
        _ => "visao-geral"
    };
}
