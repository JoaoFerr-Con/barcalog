using BarcaLog.Domain.Entidades;

namespace BarcaLog.Application.Abstracoes;

/// <summary>Quem está fazendo a requisição (usuário do JWT, sistema integrado via API Key ou "Sistema").</summary>
public interface IUsuarioAtual
{
    string Autor { get; }
    string? Nome { get; }
}

/// <summary>
/// Rótulo de negócio opcional pra próxima gravação. O log em si é
/// automático (interceptor do EF): se ninguém definir rótulo, a ação é
/// derivada das entidades alteradas ("Veiculo alterado" etc.).
/// </summary>
public interface IContextoAuditoria
{
    string? Acao { get; }
    string? Detalhes { get; }
    void DefinirAcao(string acao, string? detalhes = null);
    void Limpar();
}

public interface IHashSenha
{
    string Gerar(string senha);
    bool Verificar(string hash, string senha);
}

public sealed record TokenGerado(string Token, DateTime ExpiraEmUtc);

public interface IGeradorToken
{
    TokenGerado Gerar(Usuario usuario);
}
