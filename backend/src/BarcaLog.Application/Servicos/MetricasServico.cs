using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Configuracao;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;
using Microsoft.Extensions.Options;

namespace BarcaLog.Application.Servicos;

/// <summary>Registros já recortados + capacidade nominal de cada terminal (do cadastro).</summary>
public sealed class ContextoMetricas(IReadOnlyList<RegistroMarcacao> registros, IReadOnlyDictionary<string, (string Nome, int Capacidade)> terminais)
{
    public IReadOnlyList<RegistroMarcacao> Registros { get; } = registros;

    public int CapacidadePorId(string terminalId) =>
        terminais.TryGetValue(terminalId, out var t) ? t.Capacidade : MotorMetricas.CapacidadeDiaria;

    public int CapacidadePorNome(string nome) =>
        terminais.Values.Where(t => t.Nome == nome).Select(t => (int?)t.Capacidade).FirstOrDefault() ?? MotorMetricas.CapacidadeDiaria;
}

/// <summary>
/// Cache (singleton) da projeção leve das marcações liberadas.
///
/// - Uma carga por vez (single-flight): 50 requisições simultâneas no
///   arranque disparam UMA consulta, não 50.
/// - Invalidação "preguiçosa": importação/integração marcam o cache como
///   desatualizado, mas ele só é recarregado se a última carga tiver mais de
///   <see cref="OperacaoOptions.IntervaloMinimoRecargaSegundos"/>. Assim uma
///   integração que manda evento a cada segundo não força recarregar 100k+
///   linhas a cada segundo. Custo: os agregados podem ficar até esse
///   intervalo atrás do banco (a listagem /api/v1/marcacoes é sempre ao vivo).
/// </summary>
public sealed class CacheMarcacoes(TimeProvider tempo, IOptions<OperacaoOptions> opcoes)
{
    private readonly SemaphoreSlim _carga = new(1, 1);
    private IReadOnlyList<RegistroMarcacao>? _registros;
    private DateTimeOffset _carregadoEm;
    private volatile bool _desatualizado;

    public void MarcarDesatualizado() => _desatualizado = true;

    public async Task<IReadOnlyList<RegistroMarcacao>> ObterAsync(Func<CancellationToken, Task<List<RegistroMarcacao>>> carregar, CancellationToken ct)
    {
        if (Valido(out var atual)) return atual!;
        await _carga.WaitAsync(ct);
        try
        {
            if (Valido(out atual)) return atual!;
            _desatualizado = false; // marcações que chegarem durante a carga marcam de novo
            var lista = await carregar(ct);
            _registros = lista;
            _carregadoEm = tempo.GetUtcNow();
            return lista;
        }
        finally
        {
            _carga.Release();
        }
    }

    private bool Valido(out IReadOnlyList<RegistroMarcacao>? registros)
    {
        registros = _registros;
        if (registros is null) return false;
        var idade = tempo.GetUtcNow() - _carregadoEm;
        var o = opcoes.Value;
        if (idade > TimeSpan.FromMinutes(Math.Max(1, o.CacheMetricasMinutos))) return false;
        return !(_desatualizado && idade >= TimeSpan.FromSeconds(Math.Max(0, o.IntervaloMinimoRecargaSegundos)));
    }
}

/// <summary>Aplica os recortes; as fórmulas ficam em <see cref="MotorMetricas"/> e <see cref="Relatorio"/>.</summary>
public class MetricasServico(IMarcacaoRepositorio marcacoes, ITerminalRepositorio terminais, CacheMarcacoes cache)
{
    public async Task<ContextoMetricas> CarregarAsync(FiltroMetricas filtro, CancellationToken ct = default)
    {
        var todos = await cache.ObterAsync(marcacoes.ListarRegistrosLiberadosAsync, ct);
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

    public void InvalidarCache() => cache.MarcarDesatualizado();
}
