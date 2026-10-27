using BarcaLog.Domain.Entidades;

namespace BarcaLog.Application.Abstracoes;

/// <summary>Quem está fazendo a requisição (usuário do JWT, sistema integrado via API Key ou "Sistema").</summary>
public interface IUsuarioAtual
{
    string Autor { get; }
    string? Nome { get; }
    int? UsuarioId { get; }
}

/// <summary>
/// Escopo de dados do usuário interno. Hoje: o terminal ao qual está vinculado
/// (Usuario.TerminalId, levado no token). Sem vínculo (ou sem usuário: tarefas
/// em segundo plano, CLI, integração) = todos os terminais.
/// Preferências pessoais NUNCA passam por aqui: não ampliam nem definem escopo.
/// </summary>
public interface IEscopoUsuario
{
    string? TerminalVinculado { get; }

    /// <summary>
    /// Terminal efetivo de uma consulta: sem vínculo devolve o pedido; com vínculo,
    /// pedido vazio vira o terminal vinculado e pedido de outro terminal é negado (403).
    /// </summary>
    string? RestringirTerminal(string? pedido) => Seguranca.EscopoTerminal.Restringir(TerminalVinculado, pedido);

    /// <summary>Pode ver um registro deste terminal? (null = registro geral, de todos os terminais.)</summary>
    bool PodeVerTerminal(string? terminalId) => Seguranca.EscopoTerminal.PodeVer(TerminalVinculado, terminalId);
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

    /// <summary>Quando true, o log registra QUAIS campos mudaram, sem os valores (dados pessoais, LGPD).</summary>
    bool OcultarValores { get; }

    void DefinirAcao(string acao, string? detalhes = null, bool ocultarValores = false);
    void Limpar();
}

public enum ResultadoVerificacaoSenha
{
    Falha,
    Sucesso,
    /// <summary>Senha correta, mas o hash usa parâmetros antigos — regravar.</summary>
    SucessoPrecisaRehash
}

public interface IHashSenha
{
    string Gerar(string senha);
    ResultadoVerificacaoSenha Verificar(string hash, string senha);
}

public sealed record TokenGerado(string Token, DateTime ExpiraEmUtc);

public interface IGeradorToken
{
    /// <param name="usuario">Usuário autenticado.</param>
    /// <param name="restricao">null = acesso normal; "trocar-senha" ou "configurar-mfa" = token restrito.</param>
    TokenGerado Gerar(Usuario usuario, string? restricao);
}

/// <summary>Cifra/decifra segredos guardados no banco (ex.: semente TOTP).</summary>
public interface IProtetorSegredos
{
    string Cifrar(string textoClaro);
    string Decifrar(string textoCifrado);
}

/// <summary>
/// Confere, a cada requisição, se o token ainda vale (usuário ativo e mesma
/// VersaoToken). Com cache curto; <see cref="Invalidar"/> derruba o cache na hora.
/// </summary>
public interface IValidadorSessao
{
    Task<bool> SessaoValidaAsync(int usuarioId, int versaoToken, CancellationToken ct = default);
    void Invalidar(int usuarioId);
}
