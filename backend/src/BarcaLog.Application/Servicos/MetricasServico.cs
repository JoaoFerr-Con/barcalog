using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Configuracao;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BarcaLog.Application.Servicos;

/// <summary>Registros já recortados + capacidade nominal de cada terminal (do cadastro).</summary>
public sealed class ContextoMetricas(IReadOnlyList<RegistroMarcacao> registros, IReadOnlyDictionary<string, (string Nome, int Capacidade)> terminais)
{
    public IReadOnlyList<RegistroMarcacao> Registros { get; } = registros;

    public int CapacidadePorId(string terminalId) =>
        terminais.TryGetValue(terminalId, out var t) ? t.Capacidade : MotorMetricas.CapacidadeDiaria;

    public int CapacidadePorNome(string nome) =>
        terminais.Values.FirstOrDefault(t => t.Nome == nome) is { Nome: not null } t ? t.Capacidade : MotorMetricas.CapacidadeDiaria;
}

/// <summary>
/// Carrega (uma vez, em cache) a projeção leve das ~103k marcações liberadas e
/// aplica os recortes; as fórmulas ficam em <see cref="MotorMetricas"/> e
/// <see cref="Relatorio"/>. O cache é invalidado por importação e integração.
/// </summary>
public class MetricasServico(
    IMarcacaoRepositorio marcacoes,
    ITerminalRepositorio terminais,
    IMemoryCache cache,
    IOptions<OperacaoOptions> opcoes)
{
    private const string ChaveCache = "barcalog:marcacoes:liberadas";

    public async Task<ContextoMetricas> CarregarAsync(FiltroMetricas filtro, CancellationToken ct = default)
    {
        var todos = await ObterTodosAsync(ct);
        IEnumerable<RegistroMarcacao> q = todos;
        if (!string.IsNullOrWhiteSpace(filtro.TerminalId) && !filtro.TerminalId.Equals("todas", StringComparison.OrdinalIgnoreCase))
            q = q.Where(r => r.TerminalId == filtro.TerminalId);
        if (filtro.De is { } de)
        {
            var inicio = de.ToDateTime(TimeOnly.MinValue);
            q = q.Where(r => r.MarcadoEm >= inicio);
        }
        if (filtro.Ate is { } ate)
        {
            var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            q = q.Where(r => r.MarcadoEm < fim);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Convenio))
            q = q.Where(r => r.Convenio.Contains(filtro.Convenio, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filtro.Mes) && !filtro.Mes.Equals("todos", StringComparison.OrdinalIgnoreCase))
            q = q.Where(r => Relatorio.ChaveMes(r.MarcadoEm) == filtro.Mes);

        var listaTerminais = await terminais.ListarAsync(ct);
        return new ContextoMetricas(
            q.ToList(),
            listaTerminais.ToDictionary(t => t.Id, t => (t.Nome, t.CapacidadeDiariaCarretas)));
    }

    public void InvalidarCache() => cache.Remove(ChaveCache);

    private async Task<IReadOnlyList<RegistroMarcacao>> ObterTodosAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(ChaveCache, out IReadOnlyList<RegistroMarcacao>? emCache) && emCache is not null)
            return emCache;
        var lista = await marcacoes.ListarRegistrosLiberadosAsync(ct);
        cache.Set(ChaveCache, (IReadOnlyList<RegistroMarcacao>)lista, TimeSpan.FromMinutes(Math.Max(1, opcoes.Value.CacheMetricasMinutos)));
        return lista;
    }
}
