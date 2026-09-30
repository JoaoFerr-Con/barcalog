using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Application.Servicos;

/// <summary>
/// Portal da Transportadora. Toda consulta é FORÇADA para a transportadora
/// do usuário autenticado (vinda do token, nunca do cliente) — proteção
/// contra BOLA/IDOR: mandar ?transportadoraId=outra ou o id de uma ocorrência
/// alheia não dá acesso a nada. Registro de outra transportadora responde
/// "não encontrado" (não revela que existe).
/// </summary>
public class PortalServico(
    TransportadoraServico transportadoras,
    OcorrenciaServico ocorrencias,
    ContestacaoServico contestacoes,
    VeiculoServico veiculos,
    CondutorServico condutores,
    IOcorrenciaRepositorio repositorioOcorrencias)
{
    public Task<TransportadoraDto> ResumoAsync(int transportadoraId, CancellationToken ct = default) =>
        transportadoras.ObterAsync(transportadoraId, ct);

    public Task<Pagina<OcorrenciaDto>> OcorrenciasAsync(int transportadoraId, FiltroOcorrencias filtro, CancellationToken ct = default)
    {
        filtro.TransportadoraId = transportadoraId;
        return ocorrencias.ListarAsync(filtro, ct);
    }

    public Task<Pagina<ContestacaoDto>> ContestacoesAsync(int transportadoraId, FiltroContestacoes filtro, CancellationToken ct = default)
    {
        filtro.TransportadoraId = transportadoraId;
        return contestacoes.ListarAsync(filtro, ct);
    }

    public Task<Pagina<VeiculoDto>> VeiculosAsync(int transportadoraId, FiltroVeiculos filtro, CancellationToken ct = default)
    {
        filtro.TransportadoraId = transportadoraId;
        return veiculos.ListarAsync(filtro, ct);
    }

    public Task<Pagina<CondutorDto>> CondutoresAsync(int transportadoraId, FiltroCondutores filtro, CancellationToken ct = default)
    {
        filtro.TransportadoraId = transportadoraId;
        return condutores.ListarAsync(filtro, ct);
    }

    public async Task<ContestacaoDto> AbrirContestacaoAsync(int transportadoraId, AbrirContestacaoPortalRequest req, CancellationToken ct = default)
    {
        var ocorrencia = await repositorioOcorrencias.ObterAsync(req.OcorrenciaId, ct);
        if (ocorrencia is null || ocorrencia.TransportadoraId != transportadoraId)
            throw new NaoEncontradoException($"Ocorrência {req.OcorrenciaId} não encontrada.");
        return await contestacoes.AbrirAsync(new AbrirContestacaoRequest
        {
            OcorrenciaId = ocorrencia.Id,
            TransportadoraId = transportadoraId,
            Justificativa = req.Justificativa
        }, ct);
    }
}
