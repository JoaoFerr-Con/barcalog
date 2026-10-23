using System.Data;
using System.Runtime.CompilerServices;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BarcaLog.Infrastructure.Persistencia.Repositorios;

public class ImportacaoRepositorio(BarcaLogDbContext db) : IImportacaoRepositorio
{
    public void AdicionarLote(LoteImportacao lote) => db.LotesImportacao.Add(lote);

    public Task<LoteImportacao?> ObterLoteAsync(int id, CancellationToken ct = default) =>
        db.LotesImportacao.Include(l => l.Terminal).FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<Pagina<LoteImportacao>> ListarLotesAsync(FiltroLotesImportacao f, CancellationToken ct = default)
    {
        IQueryable<LoteImportacao> q = db.LotesImportacao.AsNoTracking().Include(l => l.Terminal);
        if (!string.IsNullOrWhiteSpace(f.TerminalId)) q = q.Where(l => l.TerminalId == f.TerminalId);
        if (f.Status is { } s) q = q.Where(l => l.Status == s);
        return q.OrderByDescending(l => l.CriadoEm).ThenByDescending(l => l.Id).PaginarAsync(f, ct);
    }

    public Task<DateTime?> ConfirmadoComMesmoHashAsync(string hashSha256, string terminalId, int ignorarLoteId, CancellationToken ct = default) =>
        db.LotesImportacao.AsNoTracking()
            .Where(l => l.HashSha256 == hashSha256 && l.TerminalId == terminalId && l.Status == StatusLoteImportacao.Confirmado && l.Id != ignorarLoteId)
            .OrderByDescending(l => l.ConcluidoEm).Select(l => l.ConcluidoEm).FirstOrDefaultAsync(ct);

    public async Task<Dictionary<string, (DateTime Quando, string Arquivo)>> UltimaConfirmadaPorTerminalAsync(CancellationToken ct = default)
    {
        var lotes = await db.LotesImportacao.AsNoTracking()
            .Where(l => l.Status == StatusLoteImportacao.Confirmado && l.ConcluidoEm != null)
            .GroupBy(l => l.TerminalId)
            .Select(g => g.OrderByDescending(l => l.ConcluidoEm).Select(l => new { l.TerminalId, l.ConcluidoEm, l.Arquivo }).First())
            .ToListAsync(ct);
        return lotes.ToDictionary(l => l.TerminalId, l => (l.ConcluidoEm!.Value, l.Arquivo));
    }

    public Task<LoteImportacao?> ConfirmadoDepoisAsync(string terminalId, DateTime desdeUtc, int ignorarLoteId, CancellationToken ct = default) =>
        db.LotesImportacao.AsNoTracking()
            .Where(l => l.TerminalId == terminalId && l.Status == StatusLoteImportacao.Confirmado && l.ConcluidoEm > desdeUtc && l.Id != ignorarLoteId)
            .OrderBy(l => l.ConcluidoEm).FirstOrDefaultAsync(ct);

    /// <summary>SqlBulkCopy na transação corrente: dezenas de milhares de linhas em segundos.</summary>
    public async Task GravarLinhasAsync(IReadOnlyList<LinhaImportacao> linhas, CancellationToken ct = default)
    {
        if (linhas.Count == 0) return;
        var tabela = new DataTable();
        tabela.Columns.Add("LoteId", typeof(int));
        tabela.Columns.Add("NumeroLinha", typeof(int));
        tabela.Columns.Add("Classificacao", typeof(string));
        tabela.Columns.Add("Decisao", typeof(string));
        tabela.Columns.Add("Motivo", typeof(string));
        tabela.Columns.Add("Avisos", typeof(int));
        tabela.Columns.Add("MovimentoId", typeof(string));
        tabela.Columns.Add("Senha", typeof(string));
        tabela.Columns.Add("Convenio", typeof(string));
        tabela.Columns.Add("CodConvenio", typeof(string));
        tabela.Columns.Add("Operador", typeof(string));
        tabela.Columns.Add("Carga", typeof(string));
        tabela.Columns.Add("Ciclo", typeof(int));
        tabela.Columns.Add("DataMarcacao", typeof(DateTime));
        tabela.Columns.Add("DataLiberacao", typeof(DateTime));
        foreach (var l in linhas)
        {
            tabela.Rows.Add(l.LoteId, l.NumeroLinha, l.Classificacao.ToString(), l.Decisao.ToString(),
                (object?)Cortar(l.Motivo, 1000) ?? DBNull.Value, (int)l.Avisos, l.MovimentoId,
                (object?)l.Senha ?? DBNull.Value, (object?)l.Convenio ?? DBNull.Value, (object?)l.CodConvenio ?? DBNull.Value,
                (object?)l.Operador ?? DBNull.Value, (object?)l.Carga ?? DBNull.Value, (object?)l.Ciclo ?? DBNull.Value,
                (object?)l.DataMarcacao ?? DBNull.Value, (object?)l.DataLiberacao ?? DBNull.Value);
        }
        var conexao = (SqlConnection)db.Database.GetDbConnection();
        var transacao = (SqlTransaction?)db.Database.CurrentTransaction?.GetDbTransaction();
        if (conexao.State != ConnectionState.Open) await conexao.OpenAsync(ct);
        using var copia = new SqlBulkCopy(conexao, SqlBulkCopyOptions.CheckConstraints, transacao)
        {
            DestinationTableName = "dbo.LinhasImportacao", BatchSize = 5000, BulkCopyTimeout = 300
        };
        foreach (DataColumn c in tabela.Columns) copia.ColumnMappings.Add(c.ColumnName, c.ColumnName);
        await copia.WriteToServerAsync(tabela, ct);
    }

    private static string? Cortar(string? s, int max) => s is null || s.Length <= max ? s : s[..(max - 1)] + "…";

    public Task<Pagina<LinhaImportacao>> ListarLinhasAsync(int loteId, FiltroLinhasImportacao f, CancellationToken ct = default)
    {
        var q = db.LinhasImportacao.AsNoTracking().Where(l => l.LoteId == loteId);
        if (f.Classificacao is { } c) q = q.Where(l => l.Classificacao == c);
        if (f.Decisao is { } d) q = q.Where(l => l.Decisao == d);
        if (f.Aviso is { } a && a != AvisoImportacao.Nenhum) q = q.Where(l => (l.Avisos & a) == a);
        return q.OrderBy(l => l.NumeroLinha).PaginarAsync(f, ct);
    }

    public async IAsyncEnumerable<LinhaImportacao> ListarLinhasParaRelatorioAsync(int loteId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Só o que precisa de atenção: rejeitadas, conflitos e linhas com aviso.
        var q = db.LinhasImportacao.AsNoTracking()
            .Where(l => l.LoteId == loteId && (l.Classificacao == ClassificacaoLinhaImportacao.Rejeitado || l.Classificacao == ClassificacaoLinhaImportacao.Conflito || l.Avisos != AvisoImportacao.Nenhum))
            .OrderBy(l => l.NumeroLinha);
        await foreach (var l in q.AsAsyncEnumerable().WithCancellation(ct)) yield return l;
    }

    public Task<int> ContarConflitosPendentesAsync(int loteId, CancellationToken ct = default) =>
        db.LinhasImportacao.CountAsync(l => l.LoteId == loteId && l.Classificacao == ClassificacaoLinhaImportacao.Conflito && l.Decisao == DecisaoConflitoImportacao.Pendente, ct);

    public Task<int> DecidirConflitosAsync(int loteId, IReadOnlyCollection<long>? linhaIds, DecisaoConflitoImportacao decisao, CancellationToken ct = default)
    {
        var q = db.LinhasImportacao.Where(l => l.LoteId == loteId && l.Classificacao == ClassificacaoLinhaImportacao.Conflito);
        q = linhaIds is null ? q.Where(l => l.Decisao == DecisaoConflitoImportacao.Pendente) : q.Where(l => linhaIds.Contains(l.Id));
        return q.ExecuteUpdateAsync(s => s.SetProperty(l => l.Decisao, decisao), ct);
    }

    public async Task<ResultadoAplicacaoLote> AplicarAsync(LoteImportacao lote, CancellationToken ct = default)
    {
        var p = new object[] { new SqlParameter("@lote", lote.Id), new SqlParameter("@terminal", lote.TerminalId) };

        // 1) Novos — NOT EXISTS: se outro processo gravou o movimento depois da análise, ele é ignorado (não duplica).
        var inseridos = await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO dbo.Marcacoes (MovimentoId, Senha, Convenio, CodConvenio, Operador, Carga, Ciclo, DataMarcacao, DataLiberacao, TerminalId, Origem, LoteImportacaoId, LinhaOrigem)
            SELECT l.MovimentoId, l.Senha, l.Convenio, l.CodConvenio, l.Operador, l.Carga, l.Ciclo, l.DataMarcacao, l.DataLiberacao, @terminal, N'Planilha', l.LoteId, l.NumeroLinha
            FROM dbo.LinhasImportacao l
            WHERE l.LoteId = @lote AND l.Classificacao = N'Novo'
              AND NOT EXISTS (SELECT 1 FROM dbo.Marcacoes m WITH (UPDLOCK, HOLDLOCK) WHERE m.MovimentoId = l.MovimentoId);
            """, p, ct);

        // 2) Complementos — só preenche o que ainda está vazio.
        var complementados = await db.Database.ExecuteSqlRawAsync("""
            UPDATE m SET
                m.CodConvenio = COALESCE(m.CodConvenio, l.CodConvenio),
                m.DataLiberacao = COALESCE(m.DataLiberacao, l.DataLiberacao)
            FROM dbo.Marcacoes m
            JOIN dbo.LinhasImportacao l ON l.MovimentoId = m.MovimentoId
            WHERE l.LoteId = @lote AND l.Classificacao = N'Complementar'
              AND ((m.CodConvenio IS NULL AND l.CodConvenio IS NOT NULL) OR (m.DataLiberacao IS NULL AND l.DataLiberacao IS NOT NULL))
              AND COALESCE(m.DataLiberacao, l.DataLiberacao, m.DataMarcacao) >= m.DataMarcacao;
            """, p, ct);

        // 3) Conflitos aprovados — grava os valores da planilha (operador/carga seguem o cadastro do convênio).
        var aplicados = await db.Database.ExecuteSqlRawAsync("""
            UPDATE m SET
                m.Senha = l.Senha, m.Convenio = l.Convenio, m.CodConvenio = COALESCE(l.CodConvenio, m.CodConvenio),
                m.Operador = l.Operador, m.Carga = l.Carga, m.Ciclo = l.Ciclo,
                m.DataMarcacao = l.DataMarcacao, m.DataLiberacao = COALESCE(l.DataLiberacao, m.DataLiberacao),
                m.TerminalId = @terminal
            FROM dbo.Marcacoes m
            JOIN dbo.LinhasImportacao l ON l.MovimentoId = m.MovimentoId
            WHERE l.LoteId = @lote AND l.Classificacao = N'Conflito' AND l.Decisao = N'Aplicar'
              AND COALESCE(l.DataLiberacao, m.DataLiberacao, l.DataMarcacao) >= l.DataMarcacao;
            """, p, ct);

        return new ResultadoAplicacaoLote(inseridos, complementados, aplicados);
    }

    public Task RemoverLinhasAsync(int loteId, IReadOnlyCollection<ClassificacaoLinhaImportacao> classificacoes, CancellationToken ct = default) =>
        db.LinhasImportacao.Where(l => l.LoteId == loteId && classificacoes.Contains(l.Classificacao)).ExecuteDeleteAsync(ct);

    public async Task RegistrarEventoAsync(string autor, string acao, string detalhes, DateTime quandoUtc, CancellationToken ct = default)
    {
        db.LogsAuditoria.Add(new LogAuditoria { Autor = autor, Acao = acao, Detalhes = detalhes, Quando = quandoUtc });
        await db.SaveChangesAsync(ct);
    }
}

public class ConvenioRepositorio(BarcaLogDbContext db) : IConvenioRepositorio
{
    public Task<List<Convenio>> ListarAsync(CancellationToken ct = default) =>
        db.Convenios.AsNoTracking().OrderBy(c => c.Codigo).ToListAsync(ct);

    public Task<Convenio?> ObterAsync(string codigo, CancellationToken ct = default) =>
        db.Convenios.FirstOrDefaultAsync(c => c.Codigo == codigo, ct);

    public void Adicionar(Convenio convenio) => db.Convenios.Add(convenio);

    public async Task<Dictionary<string, (string Operador, string Carga)>> OperadorCargaPorNomeAsync(CancellationToken ct = default)
    {
        var contagem = await db.Marcacoes.AsNoTracking()
            .GroupBy(m => new { m.Convenio, m.Operador, m.Carga })
            .Select(g => new { g.Key.Convenio, g.Key.Operador, g.Key.Carga, Total = g.Count() })
            .ToListAsync(ct);
        return contagem.GroupBy(c => c.Convenio, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => { var top = g.OrderByDescending(x => x.Total).First(); return (top.Operador, top.Carga); }, StringComparer.Ordinal);
    }
}

public class AlertaRepositorio(BarcaLogDbContext db) : IAlertaRepositorio
{
    public void AdicionarAvaliacao(AvaliacaoAlertas avaliacao) => db.AvaliacoesAlertas.Add(avaliacao);

    public Task<AvaliacaoAlertas?> UltimaAvaliacaoAsync(bool somenteSucesso, CancellationToken ct = default) =>
        db.AvaliacoesAlertas.AsNoTracking().Where(a => !somenteSucesso || a.Sucesso).OrderByDescending(a => a.Inicio).ThenByDescending(a => a.Id).FirstOrDefaultAsync(ct);

    public Task<Pagina<AvaliacaoAlertas>> ListarAvaliacoesAsync(FiltroPaginado filtro, CancellationToken ct = default) =>
        db.AvaliacoesAlertas.AsNoTracking().OrderByDescending(a => a.Inicio).ThenByDescending(a => a.Id).PaginarAsync(filtro, ct);

    public async Task<Dictionary<string, Alerta>> ObterPorChavesAsync(IEnumerable<string> chaves, CancellationToken ct = default)
    {
        var lista = chaves.Distinct().ToList();
        return await db.Alertas.Include(a => a.Eventos).Where(a => lista.Contains(a.Chave)).ToDictionaryAsync(a => a.Chave, ct);
    }

    public Task<List<Alerta>> ListarAtivosAsync(CancellationToken ct = default) =>
        db.Alertas.Include(a => a.Eventos).Where(a => a.Ativo).ToListAsync(ct);

    public Task<Pagina<Alerta>> ListarAsync(FiltroAlertas f, CancellationToken ct = default)
    {
        IQueryable<Alerta> q = db.Alertas.AsNoTracking();
        if (f.SomenteAbertos) q = q.Where(a => a.Ativo && (a.Estado == EstadoAlerta.Novo || a.Estado == EstadoAlerta.EmAnalise));
        if (f.Tipo is { } t) q = q.Where(a => a.Tipo == t);
        if (f.Estado is { } e) q = q.Where(a => a.Estado == e);
        if (!string.IsNullOrWhiteSpace(f.TerminalId)) q = q.Where(a => a.TerminalId == f.TerminalId);
        // Alta primeiro; dentro da prioridade, os mais recentes.
        return q.OrderByDescending(a => a.Prioridade == PrioridadeAlerta.Alta).ThenByDescending(a => a.Prioridade == PrioridadeAlerta.Media)
            .ThenByDescending(a => a.IdentificadoEm).ThenByDescending(a => a.Id).PaginarAsync(f, ct);
    }

    public Task<Alerta?> ObterAsync(int id, CancellationToken ct = default) =>
        db.Alertas.AsNoTracking().Include(a => a.Eventos).FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Alerta?> ObterParaAlterarAsync(int id, CancellationToken ct = default) =>
        db.Alertas.Include(a => a.Eventos).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Dictionary<TipoAlerta, int>> ContarAbertosPorTipoAsync(string? terminalId = null, CancellationToken ct = default)
    {
        var q = db.Alertas.AsNoTracking().Where(a => a.Ativo && (a.Estado == EstadoAlerta.Novo || a.Estado == EstadoAlerta.EmAnalise));
        if (terminalId is not null) q = q.Where(a => a.TerminalId == terminalId);
        var contagem = await q
            .GroupBy(a => a.Tipo).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        return Enum.GetValues<TipoAlerta>().ToDictionary(t => t, t => contagem.FirstOrDefault(c => c.Key == t)?.N ?? 0);
    }

    public Task<DateTime?> UltimaAtualizacaoAsync(CancellationToken ct = default) =>
        db.Alertas.AsNoTracking().MaxAsync(a => (DateTime?)a.AtualizadoEm, ct);

    public void Adicionar(Alerta alerta) => db.Alertas.Add(alerta);
}
