using BarcaLog.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BarcaLog.Infrastructure.Idempotencia;

public enum SituacaoReserva { Reservada, JaConcluida, EmAndamento, CorpoDiferente }

public sealed record ReservaIdempotencia(SituacaoReserva Situacao, ChaveIdempotencia? Existente = null);

public class ServicoIdempotencia(BarcaLogDbContext db, TimeProvider tempo)
{
    public static readonly TimeSpan Validade = TimeSpan.FromHours(24);

    /// <summary>
    /// Tenta reservar a chave. O índice único (Escopo, Chave) resolve a corrida:
    /// de duas requisições simultâneas, só uma consegue inserir.
    /// </summary>
    public async Task<ReservaIdempotencia> ReservarAsync(string escopo, string chave, string hash, CancellationToken ct)
    {
        var registro = new ChaveIdempotencia { Escopo = escopo, Chave = chave, HashRequisicao = hash, CriadaEm = tempo.GetUtcNow().UtcDateTime };
        db.Add(registro);
        try
        {
            await db.SaveChangesAsync(ct);
            return new ReservaIdempotencia(SituacaoReserva.Reservada);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            var existente = await db.Set<ChaveIdempotencia>().AsNoTracking()
                .SingleAsync(c => c.Escopo == escopo && c.Chave == chave, ct);
            if (existente.HashRequisicao != hash) return new ReservaIdempotencia(SituacaoReserva.CorpoDiferente, existente);
            return new ReservaIdempotencia(existente.Concluida ? SituacaoReserva.JaConcluida : SituacaoReserva.EmAndamento, existente);
        }
        finally
        {
            db.Entry(registro).State = EntityState.Detached;
        }
    }

    public Task ConcluirAsync(string escopo, string chave, int status, string? corpo, string? location, CancellationToken ct) =>
        db.Set<ChaveIdempotencia>()
            .Where(c => c.Escopo == escopo && c.Chave == chave)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Concluida, true)
                .SetProperty(c => c.StatusHttp, status)
                .SetProperty(c => c.CorpoResposta, corpo)
                .SetProperty(c => c.Location, location), ct);

    /// <summary>A operação falhou: libera a chave pra o cliente poder tentar de novo.</summary>
    public Task LiberarAsync(string escopo, string chave, CancellationToken ct) =>
        db.Set<ChaveIdempotencia>().Where(c => c.Escopo == escopo && c.Chave == chave && !c.Concluida).ExecuteDeleteAsync(ct);

    public Task<int> RemoverExpiradasAsync(CancellationToken ct)
    {
        var limite = tempo.GetUtcNow().UtcDateTime - Validade;
        return db.Set<ChaveIdempotencia>().Where(c => c.CriadaEm < limite).ExecuteDeleteAsync(ct);
    }
}
