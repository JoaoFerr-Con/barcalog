using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Excecoes;
using BarcaLog.Domain.Regras;

namespace BarcaLog.Application.Servicos;

public class VeiculoServico(
    IVeiculoRepositorio veiculos,
    ITransportadoraRepositorio transportadoras,
    ITerminalRepositorio terminais,
    IOcorrenciaRepositorio ocorrencias,
    IUnitOfWork uow,
    IContextoAuditoria auditoria,
    IUsuarioAtual usuario,
    RelogioOperacional relogio)
{
    public async Task<List<VeiculoDto>> ListarAsync(FiltroVeiculos filtro, CancellationToken ct = default) =>
        (await veiculos.ListarAsync(filtro, ct)).Select(v => v.ParaDto()).ToList();

    public async Task<VeiculoDto> ObterAsync(int id, CancellationToken ct = default) =>
        (await ObterEntidadeAsync(id, ct)).ParaDto();

    public async Task<VeiculoDto?> BuscarPorPlacaAsync(string placa, CancellationToken ct = default) =>
        (await veiculos.ObterPorPlacaAsync(Veiculo.NormalizarPlaca(placa), ct))?.ParaDto();

    public async Task<VeiculoDto> CriarAsync(CriarVeiculoRequest req, CancellationToken ct = default)
    {
        var placa = Veiculo.NormalizarPlaca(req.Placa);
        await ValidarAsync(placa, req.TransportadoraId, req.TerminalId, null, ct);
        var veiculo = new Veiculo
        {
            Placa = placa,
            TransportadoraId = req.TransportadoraId,
            Modelo = req.Modelo?.Trim(),
            TerminalId = req.TerminalId,
            StatusNegativacao = StatusNegativacao.Regular
        };
        veiculo.AtualizarStatusPortaria(req.StatusPortaria ?? StatusPortaria.Aguardando, relogio.AgoraUtc);
        veiculos.Adicionar(veiculo);
        auditoria.DefinirAcao("Veículo cadastrado", $"Placa {placa}");
        await uow.SalvarAsync(ct);
        return (await ObterEntidadeAsync(veiculo.Id, ct)).ParaDto();
    }

    public async Task<VeiculoDto> AtualizarAsync(int id, AtualizarVeiculoRequest req, CancellationToken ct = default)
    {
        var veiculo = await ObterEntidadeAsync(id, ct);
        var placa = Veiculo.NormalizarPlaca(req.Placa);
        await ValidarAsync(placa, req.TransportadoraId, req.TerminalId, id, ct);
        veiculo.Placa = placa;
        veiculo.TransportadoraId = req.TransportadoraId;
        veiculo.Modelo = req.Modelo?.Trim();
        veiculo.TerminalId = req.TerminalId;
        await uow.SalvarAsync(ct);
        return (await ObterEntidadeAsync(id, ct)).ParaDto();
    }

    public async Task RemoverAsync(int id, CancellationToken ct = default)
    {
        var veiculo = await ObterEntidadeAsync(id, ct);
        veiculos.Remover(veiculo);
        auditoria.DefinirAcao("Veículo removido", $"Placa {veiculo.Placa}");
        await uow.SalvarAsync(ct);
    }

    public async Task<VeiculoDto> AtualizarStatusPortariaAsync(int id, StatusPortaria status, CancellationToken ct = default)
    {
        var veiculo = await ObterEntidadeAsync(id, ct);
        veiculo.AtualizarStatusPortaria(status, relogio.AgoraUtc);
        auditoria.DefinirAcao("Status de portaria atualizado", $"Placa {veiculo.Placa} — {status}");
        await uow.SalvarAsync(ct);
        return veiculo.ParaDto();
    }

    /// <summary>
    /// Negativação manual direto na carreta: registra uma ocorrência N3
    /// administrativa e aplica o bloqueio pela mesma regra do registro normal.
    /// </summary>
    public async Task<VeiculoDto> NegativarAsync(int id, string? motivo, CancellationToken ct = default)
    {
        var veiculo = await ObterEntidadeAsync(id, ct);
        var ocorrencia = new Ocorrencia
        {
            Nivel = NivelOcorrencia.N3,
            Placa = veiculo.Placa,
            TransportadoraId = veiculo.TransportadoraId,
            Descricao = string.IsNullOrWhiteSpace(motivo) ? "Negativação manual aplicada pela equipe do porto." : motivo.Trim(),
            Local = "Administrativo",
            Responsavel = usuario.Nome ?? usuario.Autor,
            Status = StatusOcorrencia.Ativa,
            CriadoEm = relogio.AgoraUtc
        };
        RegrasNegativacao.AplicarOcorrencia(ocorrencia, veiculo, condutor: null);
        ocorrencias.Adicionar(ocorrencia);
        auditoria.DefinirAcao("Negativação manual", $"Carreta {veiculo.Placa} — {(string.IsNullOrWhiteSpace(motivo) ? "sem motivo informado" : motivo.Trim())}");
        await uow.SalvarAsync(ct);
        return veiculo.ParaDto();
    }

    /// <summary>Desnegativação manual: regulariza a carreta e resolve as ocorrências em aberto da placa.</summary>
    public async Task<VeiculoDto> DesnegativarAsync(int id, CancellationToken ct = default)
    {
        var veiculo = await ObterEntidadeAsync(id, ct);
        veiculo.Regularizar();
        foreach (var o in await ocorrencias.ListarNaoResolvidasPorPlacaAsync(veiculo.Placa, ct)) o.Resolver();
        auditoria.DefinirAcao("Desnegativação manual", $"Carreta {veiculo.Placa}");
        await uow.SalvarAsync(ct);
        return veiculo.ParaDto();
    }

    private async Task ValidarAsync(string placa, int transportadoraId, string terminalId, int? ignorarId, CancellationToken ct)
    {
        if (await veiculos.PlacaExisteAsync(placa, ignorarId, ct))
            throw new RegraNegocioException($"Já existe veículo com a placa {placa}.");
        if (await transportadoras.ObterAsync(transportadoraId, ct) is null)
            throw new RegraNegocioException($"Transportadora {transportadoraId} não existe.");
        if (await terminais.ObterAsync(terminalId, ct) is null)
            throw new RegraNegocioException($"Terminal '{terminalId}' não existe.");
    }

    private async Task<Veiculo> ObterEntidadeAsync(int id, CancellationToken ct) =>
        await veiculos.ObterAsync(id, ct) ?? throw new NaoEncontradoException($"Veículo {id} não encontrado.");
}
