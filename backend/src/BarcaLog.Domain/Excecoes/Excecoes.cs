namespace BarcaLog.Domain.Excecoes;

/// <summary>Violação de regra de negócio (vira HTTP 409/422 na API).</summary>
public class RegraNegocioException(string mensagem) : Exception(mensagem);

/// <summary>Recurso não encontrado (vira HTTP 404 na API).</summary>
public class NaoEncontradoException(string mensagem) : Exception(mensagem);

/// <summary>O usuário está autenticado mas o recurso está fora do escopo dele (vira HTTP 403 na API).</summary>
public class AcessoNegadoException(string mensagem) : Exception(mensagem);
