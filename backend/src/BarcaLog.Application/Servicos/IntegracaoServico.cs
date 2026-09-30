using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;

namespace BarcaLog.Application.Servicos;

/// <summary>
/// Recebe eventos de marcação/liberação empurrados por outros sistemas
/// (API Key). Cada evento é validado isoladamente; os válidos são gravados
/// numa única transação e os inválidos voltam na lista de erros.
/// </summary>
public class IntegracaoServico(
    IMarcacaoRepositorio marcacoes,
    ITerminalRepositorio terminais,
    IUnitOfWork uow,
    IContextoAuditoria auditoria,
    MetricasServico metricas,
    RelogioOperacional relogio)
{
    /// <summary>Tolerância pra relógio adiantado do sistema de origem.</summary>
    private static readonly TimeSpan ToleranciaFuturo = TimeSpan.FromHours(2);
    private static readonly DateTime DataMinima = new(2000, 1, 1);

    public const int MaximoEventosPorRequisicao = 1000;

    public async Task<ResultadoIntegracaoDto> ProcessarAsync(IReadOnlyList<EventoIntegracaoDto> eventos, CancellationToken ct = default)
    {
        var idsTerminais = (await terminais.ListarAsync(ct)).Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        var limiteFuturo = relogio.AgoraLocalPorto + ToleranciaFuturo;
        var existentes = await marcacoes.ObterVariasAsync(eventos.Select(e => e.MovimentoId.Trim()), ct);
        var erros = new List<ErroEventoDto>();
        var criados = new HashSet<string>();
        var atualizados = new HashSet<string>();

        for (var i = 0; i < eventos.Count; i++)
        {
            var e = eventos[i];
            var id = e.MovimentoId.Trim();
            try
            {
                ValidarDatas(e, limiteFuturo);
                existentes.TryGetValue(id, out var marcacao);
                if (marcacao is null)
                {
                    marcacao = CriarNova(e, id, idsTerminais);
                    marcacoes.Adicionar(marcacao);
                    existentes[id] = marcacao;
                    criados.Add(id);
                }
                else
                {
                    Atualizar(marcacao, e, idsTerminais);
                    if (!criados.Contains(id)) atualizados.Add(id);
                }
            }
            catch (ArgumentException ex)
            {
                erros.Add(new ErroEventoDto(i, id, ex.Message));
            }
        }

        if (criados.Count + atualizados.Count > 0)
        {
            auditoria.DefinirAcao("Eventos de integração recebidos", $"{eventos.Count} eventos — {criados.Count} movimentos criados, {atualizados.Count} atualizados, {erros.Count} rejeitados");
            await uow.SalvarAsync(ct);
            metricas.InvalidarCache();
        }
        return new ResultadoIntegracaoDto(eventos.Count, criados.Count, atualizados.Count, erros);
    }

    /// <summary>Datas no horário local do porto: nem antes de 2000, nem no futuro (evento "de amanhã" é erro de origem).</summary>
    private static void ValidarDatas(EventoIntegracaoDto e, DateTime limiteFuturo)
    {
        foreach (var (campo, valor) in new[] { ("dataMarcacao", e.DataMarcacao), ("dataLiberacao", e.DataLiberacao) })
        {
            if (valor is not { } d) continue;
            if (d < DataMinima || d > limiteFuturo) throw new ArgumentException($"{campo} fora da faixa aceita (2000 até agora).");
        }
    }

    private static Marcacao CriarNova(EventoIntegracaoDto e, string id, HashSet<string> terminais)
    {
        if (e.Tipo == TipoEventoIntegracao.Liberacao && e.DataMarcacao is null)
            throw new ArgumentException("Liberação de movimento inexistente: envie antes o evento de marcação (ou inclua dataMarcacao e os demais campos).");
        var terminalId = Exigir(e.TerminalId, "terminalId");
        if (!terminais.Contains(terminalId)) throw new ArgumentException($"Terminal '{terminalId}' não existe.");
        var marcacao = new Marcacao
        {
            MovimentoId = id,
            TerminalId = terminalId,
            Senha = Exigir(e.Senha, "senha"),
            Convenio = Exigir(e.Convenio, "convenio"),
            CodConvenio = e.CodConvenio?.Trim(),
            Operador = Exigir(e.Operador, "operador"),
            Carga = Exigir(e.Carga, "carga"),
            Ciclo = e.Ciclo ?? 1,
            DataMarcacao = e.DataMarcacao ?? throw new ArgumentException("dataMarcacao é obrigatório."),
        };
        AplicarLiberacao(marcacao, e);
        return marcacao;
    }

    private static void Atualizar(Marcacao m, EventoIntegracaoDto e, HashSet<string> terminais)
    {
        // Valida tudo antes de mexer na entidade: evento rejeitado não deixa alteração parcial.
        var terminalId = string.IsNullOrWhiteSpace(e.TerminalId) ? m.TerminalId : e.TerminalId.Trim();
        if (!terminais.Contains(terminalId)) throw new ArgumentException($"Terminal '{terminalId}' não existe.");
        var dataMarcacao = e.DataMarcacao ?? m.DataMarcacao;
        if (e.Tipo == TipoEventoIntegracao.Liberacao && e.DataLiberacao is null)
            throw new ArgumentException("dataLiberacao é obrigatório em eventos de liberação.");
        var dataLiberacao = e.DataLiberacao ?? m.DataLiberacao;
        if (dataLiberacao < dataMarcacao) throw new ArgumentException("dataLiberacao não pode ser anterior à dataMarcacao.");

        m.TerminalId = terminalId;
        if (!string.IsNullOrWhiteSpace(e.Senha)) m.Senha = e.Senha.Trim();
        if (!string.IsNullOrWhiteSpace(e.Convenio)) m.Convenio = e.Convenio.Trim();
        if (!string.IsNullOrWhiteSpace(e.CodConvenio)) m.CodConvenio = e.CodConvenio.Trim();
        if (!string.IsNullOrWhiteSpace(e.Operador)) m.Operador = e.Operador.Trim();
        if (!string.IsNullOrWhiteSpace(e.Carga)) m.Carga = e.Carga.Trim();
        if (e.Ciclo is { } ciclo) m.Ciclo = ciclo;
        m.DataMarcacao = dataMarcacao;
        m.DataLiberacao = dataLiberacao;
    }

    private static void AplicarLiberacao(Marcacao m, EventoIntegracaoDto e)
    {
        if (e.DataLiberacao is null) return;
        if (e.DataLiberacao < m.DataMarcacao) throw new ArgumentException("dataLiberacao não pode ser anterior à dataMarcacao.");
        m.DataLiberacao = e.DataLiberacao;
    }

    private static string Exigir(string? valor, string campo) =>
        string.IsNullOrWhiteSpace(valor) ? throw new ArgumentException($"{campo} é obrigatório para criar um movimento.") : valor.Trim();
}
