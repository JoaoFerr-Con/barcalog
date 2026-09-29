using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Excecoes;
using BarcaLog.Domain.Regras;

namespace BarcaLog.Application.Servicos;

public class TransportadoraServico(
    ITransportadoraRepositorio transportadoras,
    IOcorrenciaRepositorio ocorrencias,
    IUnitOfWork uow,
    RelogioOperacional relogio)
{
    public async Task<List<TransportadoraDto>> ListarAsync(CancellationToken ct = default)
    {
        var lista = await transportadoras.ListarComContagemAsync(ct);
        var n2 = await ContarN2Async(ct);
        return lista.Select(t => ParaDto(t, n2.GetValueOrDefault(t.Transportadora.Id))).ToList();
    }

    public async Task<TransportadoraDto> ObterAsync(int id, CancellationToken ct = default)
    {
        var t = await transportadoras.ObterComContagemAsync(id, ct) ?? throw NaoEncontrada(id);
        var n2 = await ContarN2Async(ct);
        return ParaDto(t, n2.GetValueOrDefault(id));
    }

    public async Task<TransportadoraDto> CriarAsync(SalvarTransportadoraRequest req, CancellationToken ct = default)
    {
        var (nome, cnpj) = Normalizar(req);
        if (await transportadoras.ExisteNomeOuCnpjAsync(nome, cnpj, null, ct))
            throw new RegraNegocioException("Já existe transportadora com esse nome ou CNPJ.");
        var t = new Transportadora { Nome = nome, Cnpj = cnpj };
        transportadoras.Adicionar(t);
        await uow.SalvarAsync(ct);
        return ParaDto(new TransportadoraComFrota(t, 0, 0), 0);
    }

    public async Task<TransportadoraDto> AtualizarAsync(int id, SalvarTransportadoraRequest req, CancellationToken ct = default)
    {
        var t = await transportadoras.ObterAsync(id, ct) ?? throw NaoEncontrada(id);
        var (nome, cnpj) = Normalizar(req);
        if (await transportadoras.ExisteNomeOuCnpjAsync(nome, cnpj, id, ct))
            throw new RegraNegocioException("Já existe transportadora com esse nome ou CNPJ.");
        t.Nome = nome;
        t.Cnpj = cnpj;
        await uow.SalvarAsync(ct);
        return await ObterAsync(id, ct);
    }

    public async Task RemoverAsync(int id, CancellationToken ct = default)
    {
        var t = await transportadoras.ObterAsync(id, ct) ?? throw NaoEncontrada(id);
        if (await transportadoras.PossuiVinculosAsync(id, ct))
            throw new RegraNegocioException("Transportadora possui veículos, condutores, ocorrências ou agendamentos vinculados — remova-os antes.");
        transportadoras.Remover(t);
        await uow.SalvarAsync(ct);
    }

    private static (string Nome, string Cnpj) Normalizar(SalvarTransportadoraRequest req) =>
        (req.Nome.Trim(), Cnpj.Normalizar(req.Cnpj));

    private Task<Dictionary<int, int>> ContarN2Async(CancellationToken ct) =>
        ocorrencias.ContarPorTransportadoraDesdeAsync(
            NivelOcorrencia.N2, relogio.AgoraUtc.AddDays(-RegrasNegativacao.JanelaReincidenciaN2Dias), ct);

    private static TransportadoraDto ParaDto(TransportadoraComFrota t, int reincidenciasN2)
    {
        var status = RegrasNegativacao.CalcularStatusTransportadora(t.CarretasNegativadas);
        return new TransportadoraDto(
            t.Transportadora.Id, t.Transportadora.Nome, t.Transportadora.Cnpj, status,
            t.CarretasNegativadas, t.CarretasTotal, reincidenciasN2,
            status == StatusNegativacao.Regular);
    }

    private static NaoEncontradoException NaoEncontrada(int id) => new($"Transportadora {id} não encontrada.");
}
