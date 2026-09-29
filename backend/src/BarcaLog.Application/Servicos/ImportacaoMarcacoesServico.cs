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
    private const int MaxErrosListados = 100;
    private static readonly SemaphoreSlim Exclusao = new(1, 1);
    private static readonly JsonSerializerOptions OpcoesJson = new() { MaxDepth = 8 };

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
        nomeArquivo = Path.GetFileName(nomeArquivo);

        // Uma importação por vez neste processo; entre instâncias, a PK de
        // MovimentoId garante que ninguém grava o mesmo movimento duas vezes.
        if (!await Exclusao.WaitAsync(TimeSpan.Zero, ct))
            throw new RegraNegocioException("Já existe uma importação em andamento. Tente de novo em alguns minutos.");
        try
        {
            int lidos = 0, inseridos = 0, ignorados = 0, rejeitados = 0;
            var erros = new List<string>();
            var lote = new List<RegistroJson>(TamanhoLote);
            var registros = JsonSerializer.DeserializeAsyncEnumerable<RegistroJson>(json, OpcoesJson, ct);

            try
            {
                await foreach (var r in registros)
                {
                    lidos++;
                    var erro = r is null ? "registro nulo" : Validar(r);
                    if (erro is not null)
                    {
                        rejeitados++;
                        if (erros.Count < MaxErrosListados) erros.Add($"posição {lidos - 1}: {erro}");
                        continue;
                    }
                    lote.Add(r!);
                    if (lote.Count == TamanhoLote)
                    {
                        var (i, ig) = await GravarLoteAsync(terminal, lote, nomeArquivo, ct);
                        inseridos += i; ignorados += ig;
                        lote.Clear();
                    }
                }
            }
            catch (JsonException ex)
            {
                // Lotes anteriores já foram gravados (importação é idempotente: basta reenviar o arquivo corrigido).
                throw new RegraNegocioException($"JSON inválido perto do registro {lidos}: estrutura inesperada (linha {ex.LineNumber + 1}). {inseridos} registros já tinham sido gravados; reenviar o arquivo corrigido não duplica nada.");
            }
            if (lote.Count > 0)
            {
                var (i, ig) = await GravarLoteAsync(terminal, lote, nomeArquivo, ct);
                inseridos += i; ignorados += ig;
            }
            if (inseridos > 0) metricas.InvalidarCache();
            return new ResultadoImportacaoDto(terminal.Id, nomeArquivo, lidos, inseridos, ignorados, rejeitados, erros);
        }
        finally
        {
            Exclusao.Release();
        }
    }

    /// <summary>Mesmas restrições do banco, checadas antes — um registro ruim não derruba o lote inteiro.</summary>
    private static string? Validar(RegistroJson r)
    {
        if (string.IsNullOrWhiteSpace(r.Id) || r.Id.Length > 32 || !r.Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) return "id ausente ou inválido";
        if (string.IsNullOrWhiteSpace(r.Senha) || r.Senha.Length > 32) return "senha ausente ou com mais de 32 caracteres";
        if (string.IsNullOrWhiteSpace(r.Convenio) || r.Convenio.Length > 100) return "convenio ausente ou com mais de 100 caracteres";
        if (string.IsNullOrWhiteSpace(r.Operador) || r.Operador.Length > 50) return "operador ausente ou com mais de 50 caracteres";
        if (string.IsNullOrWhiteSpace(r.Carga) || r.Carga.Length > 50) return "carga ausente ou com mais de 50 caracteres";
        if (r.Ciclo is < 0 or > 100_000) return "ciclo fora da faixa";
        if (r.MarcadoEm.Year is < 2000 or > 2100) return "marcadoEm ausente ou fora da faixa";
        if (r.LiberadoEm is { } l && (l < r.MarcadoEm || l.Year > 2100)) return "liberadoEm anterior à marcação ou fora da faixa";
        return null;
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
