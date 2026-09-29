using BarcaLog.Application.Abstracoes;

namespace BarcaLog.Infrastructure.Auditoria;

/// <summary>Scoped por requisição: guarda o rótulo de negócio da próxima gravação.</summary>
public class ContextoAuditoria : IContextoAuditoria
{
    public string? Acao { get; private set; }
    public string? Detalhes { get; private set; }

    public void DefinirAcao(string acao, string? detalhes = null)
    {
        Acao = acao;
        Detalhes = detalhes;
    }

    public void Limpar()
    {
        Acao = null;
        Detalhes = null;
    }
}
