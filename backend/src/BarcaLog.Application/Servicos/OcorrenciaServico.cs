using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Excecoes;
using BarcaLog.Domain.Regras;

namespace BarcaLog.Application.Servicos;

public class OcorrenciaServico(
    IOcorrenciaRepositorio ocorrencias,
    IVeiculoRepositorio veiculos,
    ICondutorRepositorio condutores,
    ITransportadoraRepositorio transportadoras,
    IUnitOfWork uow,
    IContextoAuditoria auditoria,
    IUsuarioAtual usuario,
    RelogioOperacional relogio)
{
    public async Task<Pagina<OcorrenciaDto>> ListarAsync(FiltroOcorrencias filtro, CancellationToken ct = default) =>
        (await ocorrencias.ListarAsync(filtro, ct)).Mapear(o => o.ParaDto());

    public async Task<OcorrenciaDto> ObterAsync(int id, CancellationToken ct = default) =>
        (await ocorrencias.ObterAsync(id, ct) ?? throw new NaoEncontradoException($"Ocorrência {id} não encontrada.")).ParaDto();

    /// <summary>
    /// Registra a ocorrência e, na MESMA transação, aplica a regra oficial:
    /// N3 bloqueia automaticamente o veículo da placa (e o condutor, se
    /// identificado). N1/N2 só ficam registradas.
    /// </summary>
    public async Task<OcorrenciaRegistradaDto> RegistrarAsync(RegistrarOcorrenciaRequest req, CancellationToken ct = default)
    {
        var placa = Veiculo.NormalizarPlaca(req.Placa);
        var transportadora = await transportadoras.ObterAsync(req.TransportadoraId, ct)
            ?? throw new RegraNegocioException($"Transportadora {req.TransportadoraId} não existe.");

        var veiculo = await veiculos.ObterPorPlacaAsync(placa, ct);
        if (veiculo is not null && veiculo.TransportadoraId != transportadora.Id)
            throw new RegraNegocioException($"A placa {placa} está cadastrada em outra transportadora.");

        Condutor? condutor = null;
        if (req.CondutorId is { } condutorId)
        {
            condutor = await condutores.ObterAsync(condutorId, ct)
                ?? throw new RegraNegocioException($"Condutor {condutorId} não existe.");
            if (condutor.TransportadoraId != transportadora.Id)
                throw new RegraNegocioException("O condutor informado pertence a outra transportadora.");
        }

        var ocorrencia = new Ocorrencia
        {
            Nivel = req.Nivel,
            Placa = placa,
            TransportadoraId = transportadora.Id,
            CondutorId = condutor?.Id,
            Descricao = req.Descricao.Trim(),
            Local = req.Local?.Trim(),
            Responsavel = string.IsNullOrWhiteSpace(req.Responsavel) ? usuario.Nome ?? usuario.Autor : req.Responsavel.Trim(),
            Status = StatusOcorrencia.Ativa,
            CriadoEm = relogio.AgoraUtc
        };

        RegrasNegativacao.AplicarOcorrencia(ocorrencia, veiculo, condutor);
        ocorrencias.Adicionar(ocorrencia);

        auditoria.DefinirAcao(
            ocorrencia.BloqueiaAutomaticamente ? "Ocorrência N3 registrada (bloqueio automático)" : $"Ocorrência {ocorrencia.Nivel} registrada",
            $"Placa {placa} — {transportadora.Nome} — {ocorrencia.Descricao}");
        await uow.SalvarAsync(ct);

        var salva = await ocorrencias.ObterAsync(ocorrencia.Id, ct) ?? ocorrencia;
        return new OcorrenciaRegistradaDto(
            salva.ParaDto(),
            VeiculoBloqueado: ocorrencia.BloqueiaAutomaticamente && veiculo is not null,
            CondutorBloqueado: ocorrencia.BloqueiaAutomaticamente && condutor is not null,
            VeiculoCadastrado: veiculo is not null);
    }
}
