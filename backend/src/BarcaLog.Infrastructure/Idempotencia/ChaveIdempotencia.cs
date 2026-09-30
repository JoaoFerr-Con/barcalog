namespace BarcaLog.Infrastructure.Idempotencia;

/// <summary>
/// Registro de uma requisição com cabeçalho Idempotency-Key. Garante que um
/// POST repetido (clique duplo, retry após timeout, rede caindo) produza o
/// efeito UMA vez e devolva a mesma resposta. Detalhe de infraestrutura — não
/// é entidade de negócio, por isso não fica no Domain nem na auditoria.
/// </summary>
public class ChaveIdempotencia
{
    public long Id { get; set; }
    /// <summary>Quem pediu (usuário/sistema) + método + rota: a mesma chave de usuários diferentes não colide.</summary>
    public string Escopo { get; set; } = null!;
    public string Chave { get; set; } = null!;
    /// <summary>SHA-256 do corpo: mesma chave com corpo diferente é erro do cliente.</summary>
    public string HashRequisicao { get; set; } = null!;
    public bool Concluida { get; set; }
    public int? StatusHttp { get; set; }
    public string? CorpoResposta { get; set; }
    public string? Location { get; set; }
    public DateTime CriadaEm { get; set; }
}
