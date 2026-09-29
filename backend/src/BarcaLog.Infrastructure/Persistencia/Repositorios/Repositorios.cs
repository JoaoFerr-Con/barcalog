using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BarcaLog.Infrastructure.Persistencia.Repositorios;

internal static class Paginacao
{
    public static async Task<Pagina<T>> PaginarAsync<T>(this IQueryable<T> query, FiltroPaginado filtro, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var itens = await query.Skip((filtro.Pagina - 1) * filtro.TamanhoPagina).Take(filtro.TamanhoPagina).ToListAsync(ct);
        return new Pagina<T>(itens, total, filtro.Pagina, filtro.TamanhoPagina);
    }
}

public class TerminalRepositorio(BarcaLogDbContext db) : ITerminalRepositorio
{
    public Task<List<Terminal>> ListarAsync(CancellationToken ct = default) =>
        db.Terminais.AsNoTracking().OrderBy(t => t.Nome).ToListAsync(ct);

    public Task<Terminal?> ObterAsync(string id, CancellationToken ct = default) =>
        db.Terminais.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
}

public class MarcacaoRepositorio(BarcaLogDbContext db) : IMarcacaoRepositorio
{
    public Task<Pagina<Marcacao>> ListarAsync(FiltroMarcacoes f, CancellationToken ct = default)
    {
        IQueryable<Marcacao> q = db.Marcacoes.AsNoTracking().Include(m => m.Terminal);
        if (!string.IsNullOrWhiteSpace(f.TerminalId) && f.TerminalId != "todas") q = q.Where(m => m.TerminalId == f.TerminalId);
        if (f.De is { } de) { var inicio = de.ToDateTime(TimeOnly.MinValue); q = q.Where(m => m.DataMarcacao >= inicio); }
        if (f.Ate is { } ate) { var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue); q = q.Where(m => m.DataMarcacao < fim); }
        if (!string.IsNullOrWhiteSpace(f.Convenio)) q = q.Where(m => m.Convenio.Contains(f.Convenio));
        if (!string.IsNullOrWhiteSpace(f.Operador)) q = q.Where(m => m.Operador == f.Operador);
        if (!string.IsNullOrWhiteSpace(f.Carga)) q = q.Where(m => m.Carga == f.Carga);
        if (!string.IsNullOrWhiteSpace(f.Senha)) q = q.Where(m => m.Senha == f.Senha);
        return q.OrderByDescending(m => m.DataMarcacao).ThenBy(m => m.MovimentoId).PaginarAsync(f, ct);
    }

    public Task<Marcacao?> ObterAsync(string movimentoId, CancellationToken ct = default) =>
        db.Marcacoes.AsNoTracking().Include(m => m.Terminal).FirstOrDefaultAsync(m => m.MovimentoId == movimentoId, ct);

    public async Task<List<RegistroMarcacao>> ListarRegistrosLiberadosAsync(CancellationToken ct = default)
    {
        var linhas = await db.Marcacoes.AsNoTracking()
            .Where(m => m.DataLiberacao != null)
            .OrderBy(m => m.DataMarcacao).ThenBy(m => m.MovimentoId)
            .Select(m => new
            {
                m.MovimentoId, m.Senha, m.Convenio, m.Operador, m.Carga, m.Ciclo,
                m.DataMarcacao, DataLiberacao = m.DataLiberacao!.Value, m.EsperaHoras,
                m.TerminalId, TerminalNome = m.Terminal!.Nome
            })
            .ToListAsync(ct);
        return linhas.Select(l => new RegistroMarcacao(
            l.MovimentoId, l.Senha, l.Convenio, l.Operador, l.Carga, l.Ciclo,
            l.DataMarcacao, l.DataLiberacao, l.EsperaHoras ?? 0, l.TerminalId, l.TerminalNome)).ToList();
    }

    public async Task<HashSet<string>> ListarIdsExistentesAsync(IEnumerable<string> movimentoIds, CancellationToken ct = default)
    {
        var ids = movimentoIds.Distinct().ToList();
        var existentes = new HashSet<string>(StringComparer.Ordinal);
        // Lotes de 1000 pra não estourar o limite de parâmetros do SQL Server (2100).
        foreach (var lote in ids.Chunk(1000))
        {
            var encontrados = await db.Marcacoes.AsNoTracking().Where(m => lote.Contains(m.MovimentoId)).Select(m => m.MovimentoId).ToListAsync(ct);
            existentes.UnionWith(encontrados);
        }
        return existentes;
    }

    public async Task<Dictionary<string, Marcacao>> ObterVariasAsync(IEnumerable<string> movimentoIds, CancellationToken ct = default)
    {
        var resultado = new Dictionary<string, Marcacao>(StringComparer.Ordinal);
        foreach (var lote in movimentoIds.Distinct().Chunk(1000))
        {
            foreach (var m in await db.Marcacoes.Where(m => lote.Contains(m.MovimentoId)).ToListAsync(ct))
                resultado[m.MovimentoId] = m;
        }
        return resultado;
    }

    public void Adicionar(Marcacao marcacao) => db.Marcacoes.Add(marcacao);

    public void AdicionarVarias(IEnumerable<Marcacao> marcacoes) => db.Marcacoes.AddRange(marcacoes);

    public void LimparRastreamento() => db.ChangeTracker.Clear();
}

public class TransportadoraRepositorio(BarcaLogDbContext db) : ITransportadoraRepositorio
{
    public Task<List<Transportadora>> ListarComFrotaAsync(CancellationToken ct = default) =>
        db.Transportadoras.Include(t => t.Veiculos).OrderBy(t => t.Nome).ToListAsync(ct);

    public Task<Transportadora?> ObterComFrotaAsync(int id, CancellationToken ct = default) =>
        db.Transportadoras.Include(t => t.Veiculos).FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Transportadora?> ObterAsync(int id, CancellationToken ct = default) =>
        db.Transportadoras.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<bool> ExisteNomeOuCnpjAsync(string nome, string cnpj, int? ignorarId, CancellationToken ct = default) =>
        db.Transportadoras.AnyAsync(t => (t.Nome == nome || t.Cnpj == cnpj) && (ignorarId == null || t.Id != ignorarId), ct);

    public async Task<bool> PossuiVinculosAsync(int id, CancellationToken ct = default) =>
        await db.Veiculos.AnyAsync(v => v.TransportadoraId == id, ct)
        || await db.Condutores.AnyAsync(c => c.TransportadoraId == id, ct)
        || await db.Ocorrencias.AnyAsync(o => o.TransportadoraId == id, ct)
        || await db.Agendamentos.AnyAsync(a => a.TransportadoraId == id, ct);

    public void Adicionar(Transportadora transportadora) => db.Transportadoras.Add(transportadora);

    public void Remover(Transportadora transportadora) => db.Transportadoras.Remove(transportadora);
}

public class VeiculoRepositorio(BarcaLogDbContext db) : IVeiculoRepositorio
{
    private IQueryable<Veiculo> ComDetalhes => db.Veiculos.Include(v => v.Transportadora).Include(v => v.Terminal);

    public Task<List<Veiculo>> ListarAsync(FiltroVeiculos f, CancellationToken ct = default)
    {
        var q = ComDetalhes;
        if (f.TransportadoraId is { } tid) q = q.Where(v => v.TransportadoraId == tid);
        if (!string.IsNullOrWhiteSpace(f.TerminalId)) q = q.Where(v => v.TerminalId == f.TerminalId);
        if (f.StatusPortaria is { } sp) q = q.Where(v => v.StatusPortaria == sp);
        if (f.StatusNegativacao is { } sn) q = q.Where(v => v.StatusNegativacao == sn);
        if (!string.IsNullOrWhiteSpace(f.Placa))
        {
            var placa = Veiculo.NormalizarPlaca(f.Placa);
            q = q.Where(v => v.Placa.Contains(placa));
        }
        return q.OrderBy(v => v.Placa).ToListAsync(ct);
    }

    public Task<Veiculo?> ObterAsync(int id, CancellationToken ct = default) =>
        ComDetalhes.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<Veiculo?> ObterPorPlacaAsync(string placa, CancellationToken ct = default)
    {
        var normalizada = Veiculo.NormalizarPlaca(placa);
        return ComDetalhes.FirstOrDefaultAsync(v => v.Placa == normalizada, ct);
    }

    public Task<bool> PlacaExisteAsync(string placa, int? ignorarId, CancellationToken ct = default) =>
        db.Veiculos.AnyAsync(v => v.Placa == placa && (ignorarId == null || v.Id != ignorarId), ct);

    public Task<List<Veiculo>> ListarFilaAsync(CancellationToken ct = default) =>
        ComDetalhes
            .Where(v => v.StatusPortaria == StatusPortaria.NoPatio || v.StatusPortaria == StatusPortaria.Aguardando)
            .OrderBy(v => v.StatusPortariaDesde)
            .ToListAsync(ct);

    public Task<List<Veiculo>> ListarTodosAsync(CancellationToken ct = default) =>
        ComDetalhes.OrderBy(v => v.StatusPortariaDesde).ToListAsync(ct);

    public void Adicionar(Veiculo veiculo) => db.Veiculos.Add(veiculo);

    public void Remover(Veiculo veiculo) => db.Veiculos.Remove(veiculo);
}

public class CondutorRepositorio(BarcaLogDbContext db) : ICondutorRepositorio
{
    public Task<List<Condutor>> ListarAsync(int? transportadoraId, CancellationToken ct = default)
    {
        var q = db.Condutores.Include(c => c.Transportadora).AsQueryable();
        if (transportadoraId is { } tid) q = q.Where(c => c.TransportadoraId == tid);
        return q.OrderBy(c => c.Nome).ToListAsync(ct);
    }

    public Task<Condutor?> ObterAsync(int id, CancellationToken ct = default) =>
        db.Condutores.Include(c => c.Transportadora).FirstOrDefaultAsync(c => c.Id == id, ct);

    public void Adicionar(Condutor condutor) => db.Condutores.Add(condutor);

    public void Remover(Condutor condutor) => db.Condutores.Remove(condutor);
}

public class OcorrenciaRepositorio(BarcaLogDbContext db) : IOcorrenciaRepositorio
{
    private IQueryable<Ocorrencia> ComDetalhes => db.Ocorrencias.Include(o => o.Transportadora).Include(o => o.Condutor);

    public Task<List<Ocorrencia>> ListarAsync(FiltroOcorrencias f, CancellationToken ct = default)
    {
        var q = ComDetalhes;
        if (f.TransportadoraId is { } tid) q = q.Where(o => o.TransportadoraId == tid);
        if (f.Nivel is { } nivel) q = q.Where(o => o.Nivel == nivel);
        if (f.Status is { } status) q = q.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(f.Placa))
        {
            var placa = Veiculo.NormalizarPlaca(f.Placa);
            q = q.Where(o => o.Placa == placa);
        }
        return q.OrderByDescending(o => o.CriadoEm).ThenByDescending(o => o.Id).ToListAsync(ct);
    }

    public Task<Ocorrencia?> ObterAsync(int id, CancellationToken ct = default) =>
        ComDetalhes.FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<List<Ocorrencia>> ListarNaoResolvidasPorPlacaAsync(string placa, CancellationToken ct = default) =>
        db.Ocorrencias.Where(o => o.Placa == placa && o.Status != StatusOcorrencia.Resolvida).ToListAsync(ct);

    public Task<Dictionary<int, int>> ContarPorTransportadoraDesdeAsync(NivelOcorrencia nivel, DateTime desdeUtc, CancellationToken ct = default) =>
        db.Ocorrencias
            .Where(o => o.Nivel == nivel && o.CriadoEm >= desdeUtc)
            .GroupBy(o => o.TransportadoraId)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

    public void Adicionar(Ocorrencia ocorrencia) => db.Ocorrencias.Add(ocorrencia);
}

public class ContestacaoRepositorio(BarcaLogDbContext db) : IContestacaoRepositorio
{
    private IQueryable<Contestacao> ComDetalhes => db.Contestacoes.Include(c => c.Ocorrencia).Include(c => c.Transportadora);

    public Task<List<Contestacao>> ListarAsync(FiltroContestacoes f, CancellationToken ct = default)
    {
        var q = ComDetalhes;
        if (f.TransportadoraId is { } tid) q = q.Where(c => c.TransportadoraId == tid);
        if (f.OcorrenciaId is { } oid) q = q.Where(c => c.OcorrenciaId == oid);
        if (f.Status is { } status) q = q.Where(c => c.Status == status);
        return q.OrderByDescending(c => c.CriadoEm).ThenByDescending(c => c.Id).ToListAsync(ct);
    }

    public Task<Contestacao?> ObterAsync(int id, CancellationToken ct = default) =>
        ComDetalhes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistePendenteAsync(int ocorrenciaId, CancellationToken ct = default) =>
        db.Contestacoes.AnyAsync(c => c.OcorrenciaId == ocorrenciaId && c.Status == StatusContestacao.Pendente, ct);

    public void Adicionar(Contestacao contestacao) => db.Contestacoes.Add(contestacao);
}

public class AuditoriaRepositorio(BarcaLogDbContext db) : IAuditoriaRepositorio
{
    public Task<Pagina<LogAuditoria>> BuscarAsync(FiltroAuditoria f, CancellationToken ct = default)
    {
        var q = db.LogsAuditoria.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(f.Autor)) q = q.Where(l => l.Autor.Contains(f.Autor));
        if (!string.IsNullOrWhiteSpace(f.Acao)) q = q.Where(l => l.Acao.Contains(f.Acao));
        if (!string.IsNullOrWhiteSpace(f.Texto))
            q = q.Where(l => l.Autor.Contains(f.Texto) || l.Acao.Contains(f.Texto) || l.Detalhes.Contains(f.Texto));
        if (f.De is { } de) q = q.Where(l => l.Quando >= de);
        if (f.Ate is { } ate) q = q.Where(l => l.Quando <= ate);
        return q.OrderByDescending(l => l.Quando).ThenByDescending(l => l.Id).PaginarAsync(f, ct);
    }
}

public class AgendamentoRepositorio(BarcaLogDbContext db) : IAgendamentoRepositorio
{
    private IQueryable<Agendamento> ComDetalhes => db.Agendamentos.Include(a => a.Transportadora).Include(a => a.Terminal);

    public Task<List<Agendamento>> ListarAsync(FiltroAgendamentos f, CancellationToken ct = default)
    {
        var q = ComDetalhes;
        if (f.Data is { } data) q = q.Where(a => a.Data == data);
        if (!string.IsNullOrWhiteSpace(f.TerminalId) && f.TerminalId != "todos") q = q.Where(a => a.TerminalId == f.TerminalId);
        if (f.Status is { } status) q = q.Where(a => a.Status == status);
        if (f.TransportadoraId is { } tid) q = q.Where(a => a.TransportadoraId == tid);
        if (!string.IsNullOrWhiteSpace(f.Placa))
        {
            var placa = Veiculo.NormalizarPlaca(f.Placa);
            q = q.Where(a => a.Placa == placa);
        }
        return q.OrderBy(a => a.Data).ThenBy(a => a.Hora).ThenBy(a => a.Id).ToListAsync(ct);
    }

    public Task<Agendamento?> ObterAsync(int id, CancellationToken ct = default) =>
        ComDetalhes.FirstOrDefaultAsync(a => a.Id == id, ct);

    public void Adicionar(Agendamento agendamento) => db.Agendamentos.Add(agendamento);
}

public class UsuarioRepositorio(BarcaLogDbContext db) : IUsuarioRepositorio
{
    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct = default) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<Usuario?> ObterAsync(int id, CancellationToken ct = default) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> ExisteAlgumAsync(CancellationToken ct = default) => db.Usuarios.AnyAsync(ct);

    public void Adicionar(Usuario usuario) => db.Usuarios.Add(usuario);
}
