using System.Text.Json;
using System.Text.Json.Serialization;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Application.Servicos;

/// <summary>
/// Importa os datasets reais no formato de src/data/datasets/*.json
/// ({ id, senha, convenio, operador, carga, ciclo, marcadoEm, liberadoEm, esperaHoras }).
/// O nome do arquivo é o id do terminal (unitapajos.json → "unitapajos").
/// Idempotente: movimentos já existentes são ignorados.
/// </summary>
public class ImportacaoMarcacoesServico(
    IMarcacaoRepositorio marcacoes,
    ITerminalRepositorio terminais,
    IUnitOfWork uow,
    IContextoAuditoria auditoria,
    MetricasServico metricas)
{
    private const int TamanhoLote = 5000;

    private sealed class RegistroJson
    {
        [JsonPropertyName("id")] public string Id { get; set; } = null!;
        [JsonPropertyName("senha")] public string Senha { get; set; } = null!;
        [JsonPropertyName("convenio")] public string Convenio { get; set; } = null!;
        [JsonPropertyName("operador")] public string Operador { get; set; } = null!;
        [JsonPropertyName("carga")] public string Carga { get; set; } = null!;
        [JsonPropertyName("ciclo")] public int Ciclo { get; set; }
        [JsonPropertyName("marcadoEm")] public DateTime MarcadoEm { get; set; }
        [JsonPropertyName("liberadoEm")] public DateTime? LiberadoEm { get; set; }
    }

    public async Task<List<ResultadoImportacaoDto>> ImportarDiretorioAsync(string diretorio, CancellationToken ct = default)
    {
        if (!Directory.Exists(diretorio))
            throw new RegraNegocioException($"Diretório de datasets não encontrado: {diretorio}");
        var resultados = new List<ResultadoImportacaoDto>();
        foreach (var terminal in await terminais.ListarAsync(ct))
        {
            var caminho = Path.Combine(diretorio, $"{terminal.Id}.json");
            if (!File.Exists(caminho)) continue;
            await using var arquivo = File.OpenRead(caminho);
            resultados.Add(await ImportarAsync(terminal.Id, arquivo, Path.GetFileName(caminho), ct));
        }
        return resultados;
    }

    public async Task<ResultadoImportacaoDto> ImportarAsync(string terminalId, Stream json, string nomeArquivo, CancellationToken ct = default)
    {
        var terminal = await terminais.ObterAsync(terminalId, ct)
            ?? throw new RegraNegocioException($"Terminal '{terminalId}' não existe.");

        int lidos = 0, inseridos = 0, ignorados = 0;
        var lote = new List<RegistroJson>(TamanhoLote);
        await foreach (var r in JsonSerializer.DeserializeAsyncEnumerable<RegistroJson>(json, cancellationToken: ct))
        {
            if (r is null) continue;
            lidos++;
            lote.Add(r);
            if (lote.Count == TamanhoLote)
            {
                var (i, ig) = await GravarLoteAsync(terminal, lote, nomeArquivo, ct);
                inseridos += i; ignorados += ig;
                lote.Clear();
            }
        }
        if (lote.Count > 0)
        {
            var (i, ig) = await GravarLoteAsync(terminal, lote, nomeArquivo, ct);
            inseridos += i; ignorados += ig;
        }
        metricas.InvalidarCache();
        return new ResultadoImportacaoDto(terminal.Id, nomeArquivo, lidos, inseridos, ignorados);
    }

    private async Task<(int Inseridos, int Ignorados)> GravarLoteAsync(Terminal terminal, List<RegistroJson> lote, string arquivo, CancellationToken ct)
    {
        var existentes = await marcacoes.ListarIdsExistentesAsync(lote.Select(r => r.Id), ct);
        var novos = lote
            .Where(r => !existentes.Contains(r.Id))
            .DistinctBy(r => r.Id)
            .Select(r => new Marcacao
            {
                MovimentoId = r.Id,
                Senha = r.Senha,
                Convenio = r.Convenio,
                Operador = r.Operador,
                Carga = r.Carga,
                Ciclo = r.Ciclo,
                DataMarcacao = r.MarcadoEm,
                DataLiberacao = r.LiberadoEm,
                TerminalId = terminal.Id
            })
            .ToList();
        if (novos.Count > 0)
        {
            marcacoes.AdicionarVarias(novos);
            auditoria.DefinirAcao("Importação de marcações", $"{terminal.Nome} ({arquivo}) — {novos.Count} movimentos inseridos neste lote");
            await uow.SalvarAsync(ct);
            marcacoes.LimparRastreamento();
        }
        return (novos.Count, lote.Count - novos.Count);
    }
}
