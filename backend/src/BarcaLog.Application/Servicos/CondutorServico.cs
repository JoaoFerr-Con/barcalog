using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Application.Servicos;

public class CondutorServico(
    ICondutorRepositorio condutores,
    ITransportadoraRepositorio transportadoras,
    IOcorrenciaRepositorio ocorrencias,
    IAgendamentoRepositorio agendamentos,
    IUnitOfWork uow,
    IContextoAuditoria auditoria)
{
    public async Task<Pagina<CondutorDto>> ListarAsync(FiltroCondutores filtro, CancellationToken ct = default) =>
        (await condutores.ListarAsync(filtro, ct)).Mapear(c => c.ParaDto());

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
        auditoria.DefinirAcao("Condutor removido", $"Condutor #{id}");
        await uow.SalvarAsync(ct);
    }

    /// <summary>
    /// LGPD (art. 18): relatório de todos os dados pessoais do condutor que o
    /// sistema guarda — cadastro, ocorrências vinculadas e agendamentos em que
    /// aparece como motorista.
    /// </summary>
    public async Task<DadosPessoaisCondutorDto> ExportarDadosPessoaisAsync(int id, CancellationToken ct = default)
    {
        var condutor = await ObterEntidadeAsync(id, ct);
        var ocorrenciasDoCondutor = await ocorrencias.ListarPorCondutorAsync(id, ct);
        var agendamentosDoCondutor = await agendamentos.ListarPorMotoristaAsync(condutor.Nome, ct);
        return new DadosPessoaisCondutorDto(
            condutor.ParaDto(),
            ocorrenciasDoCondutor.Select(o => o.ParaDto()).ToList(),
            agendamentosDoCondutor.Select(a => a.ParaDto()).ToList());
    }

    /// <summary>
    /// LGPD (art. 18, IV/VI): substitui o nome por um pseudônimo e desvincula a
    /// placa, preservando ocorrências/agendamentos para estatística e para as
    /// obrigações de auditoria. O log desta ação não guarda o nome antigo.
    /// </summary>
    public async Task<CondutorDto> AnonimizarAsync(int id, CancellationToken ct = default)
    {
        var condutor = await ObterEntidadeAsync(id, ct);
        var nomeAntigo = condutor.Nome;
        var pseudonimo = $"Condutor anonimizado #{id}";
        foreach (var a in await agendamentos.ListarPorMotoristaAsync(nomeAntigo, ct)) a.Motorista = pseudonimo;
        condutor.Nome = pseudonimo;
        condutor.PlacaVinculada = null;
        auditoria.DefinirAcao("Condutor anonimizado (LGPD)", $"Condutor #{id}", ocultarValores: true);
        await uow.SalvarAsync(ct);
        return condutor.ParaDto();
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
