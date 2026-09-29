using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Excecoes;
using BarcaLog.Domain.Regras;

namespace BarcaLog.Application.Servicos;

public class ContestacaoServico(
    IContestacaoRepositorio contestacoes,
    IOcorrenciaRepositorio ocorrencias,
    IVeiculoRepositorio veiculos,
    ICondutorRepositorio condutores,
    IUnitOfWork uow,
    IContextoAuditoria auditoria,
    RelogioOperacional relogio)
{
    public async Task<Pagina<ContestacaoDto>> ListarAsync(FiltroContestacoes filtro, CancellationToken ct = default) =>
        (await contestacoes.ListarAsync(filtro, ct)).Mapear(c => c.ParaDto());

    public async Task<ContestacaoDto> ObterAsync(int id, CancellationToken ct = default) =>
        (await ObterEntidadeAsync(id, ct)).ParaDto();

    public async Task<ContestacaoDto> AbrirAsync(AbrirContestacaoRequest req, CancellationToken ct = default)
    {
        var ocorrencia = await ocorrencias.ObterAsync(req.OcorrenciaId, ct)
            ?? throw new RegraNegocioException($"Ocorrência {req.OcorrenciaId} não existe.");
        if (req.TransportadoraId is { } tid && tid != ocorrencia.TransportadoraId)
            throw new RegraNegocioException("A ocorrência não pertence a essa transportadora.");
        if (await contestacoes.ExistePendenteAsync(ocorrencia.Id, ct))
            throw new RegraNegocioException("Já existe contestação pendente para essa ocorrência.");

        var contestacao = new Contestacao
        {
            OcorrenciaId = ocorrencia.Id,
            TransportadoraId = ocorrencia.TransportadoraId,
            Justificativa = req.Justificativa.Trim(),
            CriadoEm = relogio.AgoraUtc
        };
        RegrasNegativacao.AbrirContestacao(contestacao, ocorrencia);
        contestacoes.Adicionar(contestacao);
        auditoria.DefinirAcao("Contestação aberta (GED)", $"{ocorrencia.Transportadora?.Nome} — ocorrência {ocorrencia.Id}");
        await uow.SalvarAsync(ct);
        return (await ObterEntidadeAsync(contestacao.Id, ct)).ParaDto();
    }

    public Task<ContestacaoDto> AprovarAsync(int id, string? resposta, CancellationToken ct = default) =>
        ResponderAsync(id, aprovada: true, resposta, ct);

    public Task<ContestacaoDto> RejeitarAsync(int id, string? resposta, CancellationToken ct = default) =>
        ResponderAsync(id, aprovada: false, resposta, ct);

    /// <summary>
    /// Aprovar regulariza a carreta (e o condutor) DAQUELA ocorrência — nunca a
    /// transportadora inteira. Rejeitar devolve a ocorrência pra "ativa".
    /// </summary>
    private async Task<ContestacaoDto> ResponderAsync(int id, bool aprovada, string? resposta, CancellationToken ct)
    {
        var contestacao = await ObterEntidadeAsync(id, ct);
        var ocorrencia = contestacao.Ocorrencia ?? await ocorrencias.ObterAsync(contestacao.OcorrenciaId, ct)
            ?? throw new RegraNegocioException("Ocorrência da contestação não existe mais.");

        Veiculo? veiculo = null;
        Condutor? condutor = null;
        if (aprovada)
        {
            veiculo = await veiculos.ObterPorPlacaAsync(ocorrencia.Placa, ct);
            if (ocorrencia.CondutorId is { } condutorId) condutor = await condutores.ObterAsync(condutorId, ct);
        }

        RegrasNegativacao.ResponderContestacao(contestacao, ocorrencia, aprovada, resposta?.Trim(), relogio.AgoraUtc, veiculo, condutor);
        auditoria.DefinirAcao(
            aprovada ? "Contestação aprovada (negativação retirada)" : "Contestação rejeitada",
            $"{contestacao.Transportadora?.Nome} — {resposta ?? ""}");
        await uow.SalvarAsync(ct);
        return contestacao.ParaDto();
    }

    private async Task<Contestacao> ObterEntidadeAsync(int id, CancellationToken ct) =>
        await contestacoes.ObterAsync(id, ct) ?? throw new NaoEncontradoException($"Contestação {id} não encontrada.");
}
