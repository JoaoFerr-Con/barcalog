using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Application.Servicos;

public class CondutorServico(
    ICondutorRepositorio condutores,
    ITransportadoraRepositorio transportadoras,
    IUnitOfWork uow,
    IContextoAuditoria auditoria)
{
    public async Task<List<CondutorDto>> ListarAsync(int? transportadoraId, CancellationToken ct = default) =>
        (await condutores.ListarAsync(transportadoraId, ct)).Select(c => c.ParaDto()).ToList();

    public async Task<CondutorDto> ObterAsync(int id, CancellationToken ct = default) =>
        (await ObterEntidadeAsync(id, ct)).ParaDto();

    public async Task<CondutorDto> CriarAsync(SalvarCondutorRequest req, CancellationToken ct = default)
    {
        await ValidarTransportadoraAsync(req.TransportadoraId, ct);
        var condutor = new Condutor
        {
            Nome = req.Nome.Trim(),
            TransportadoraId = req.TransportadoraId,
            PlacaVinculada = NormalizarPlacaOpcional(req.PlacaVinculada)
        };
        condutores.Adicionar(condutor);
        auditoria.DefinirAcao("Condutor cadastrado", condutor.Nome);
        await uow.SalvarAsync(ct);
        return (await ObterEntidadeAsync(condutor.Id, ct)).ParaDto();
    }

    public async Task<CondutorDto> AtualizarAsync(int id, SalvarCondutorRequest req, CancellationToken ct = default)
    {
        var condutor = await ObterEntidadeAsync(id, ct);
        await ValidarTransportadoraAsync(req.TransportadoraId, ct);
        condutor.Nome = req.Nome.Trim();
        condutor.TransportadoraId = req.TransportadoraId;
        condutor.PlacaVinculada = NormalizarPlacaOpcional(req.PlacaVinculada);
        await uow.SalvarAsync(ct);
        return (await ObterEntidadeAsync(id, ct)).ParaDto();
    }

    public async Task RemoverAsync(int id, CancellationToken ct = default)
    {
        var condutor = await ObterEntidadeAsync(id, ct);
        condutores.Remover(condutor);
        auditoria.DefinirAcao("Condutor removido", condutor.Nome);
        await uow.SalvarAsync(ct);
    }

    private static string? NormalizarPlacaOpcional(string? placa) =>
        string.IsNullOrWhiteSpace(placa) ? null : Veiculo.NormalizarPlaca(placa);

    private async Task ValidarTransportadoraAsync(int id, CancellationToken ct)
    {
        if (await transportadoras.ObterAsync(id, ct) is null)
            throw new RegraNegocioException($"Transportadora {id} não existe.");
    }

    private async Task<Condutor> ObterEntidadeAsync(int id, CancellationToken ct) =>
        await condutores.ObterAsync(id, ct) ?? throw new NaoEncontradoException($"Condutor {id} não encontrado.");
}
