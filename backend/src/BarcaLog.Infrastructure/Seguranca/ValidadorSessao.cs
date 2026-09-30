using BarcaLog.Application.Abstracoes;
using BarcaLog.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace BarcaLog.Infrastructure.Seguranca;

/// <summary>
/// Revogação de JWT: o token carrega "ver" (VersaoToken do usuário). A cada
/// requisição comparamos com o banco, com cache de 30 s por usuário.
/// Alterações feitas NESTA instância invalidam o cache na hora; em outras
/// instâncias o atraso máximo é o TTL do cache.
/// </summary>
public class ValidadorSessao(IMemoryCache cache, IServiceScopeFactory escopos) : IValidadorSessao
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private sealed record EstadoSessao(bool Ativo, int VersaoToken);

    public async Task<bool> SessaoValidaAsync(int usuarioId, int versaoToken, CancellationToken ct = default)
    {
        var estado = await cache.GetOrCreateAsync(Chave(usuarioId), async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = Ttl;
            await using var escopo = escopos.CreateAsyncScope();
            var db = escopo.ServiceProvider.GetRequiredService<BarcaLogDbContext>();
            return await db.Usuarios.AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => new EstadoSessao(u.Ativo, u.VersaoToken))
                .SingleOrDefaultAsync(ct);
        });
        return estado is { Ativo: true } && estado.VersaoToken == versaoToken;
    }

    public void Invalidar(int usuarioId) => cache.Remove(Chave(usuarioId));

    private static string Chave(int id) => $"sessao:{id}";
}
