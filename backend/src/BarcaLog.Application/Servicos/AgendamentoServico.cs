using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Application.Servicos;

public class AgendamentoServico(
    IAgendamentoRepositorio agendamentos,
    IVeiculoRepositorio veiculos,
    ITransportadoraRepositorio transportadoras,
    ITerminalRepositorio terminais,
    IUnitOfWork uow,
    IContextoAuditoria auditoria,
    RelogioOperacional relogio)
{
    /// <summary>Status que contam como "confirmados" no resumo (mesma lista da tela).</summary>
    private static readonly StatusAgendamento[] StatusConfirmados =
    [
        StatusAgendamento.Confirmado, StatusAgendamento.ACaminho, StatusAgendamento.AguardandoEntrada,
        StatusAgendamento.EmOperacao, StatusAgendamento.Finalizado
    ];

    public async Task<Pagina<AgendamentoDto>> ListarAsync(FiltroAgendamentos filtro, CancellationToken ct = default) =>
        (await agendamentos.ListarAsync(filtro, ct)).Mapear(a => a.ParaDto());

    public async Task<AgendamentoDto> ObterAsync(int id, CancellationToken ct = default) =>
        (await ObterEntidadeAsync(id, ct)).ParaDto();

    public async Task<ResumoAgendamentosDto> ResumoAsync(DateOnly? data, string? terminalId, CancellationToken ct = default)
    {
        var dia = data ?? relogio.HojeLocalPorto;
        var lista = await agendamentos.ListarDoDiaAsync(dia, terminalId, ct);
        var porHora = lista
            .GroupBy(a => a.Hora)
            .OrderBy(g => g.Key)
            .Select(g => new AgendamentosPorHoraDto(g.Key, g.Count(), g.Count() >= 3 ? "critico" : g.Count() >= 2 ? "atencao" : "normal"))
            .ToList();
        return new ResumoAgendamentosDto(
            dia,
            lista.Count,
            lista.Count(a => StatusConfirmados.Contains(a.Status)),
            lista.Count(a => a.Status == StatusAgendamento.EmOperacao),
            lista.Count(a => a.Status == StatusAgendamento.Atrasado),
            porHora);
    }

    public async Task<AgendamentoDto> CriarAsync(CriarAgendamentoRequest req, CancellationToken ct = default)
    {
        var placa = Veiculo.NormalizarPlaca(req.Placa);
        var data = req.Data ?? relogio.HojeLocalPorto;
        if (data < relogio.HojeLocalPorto) throw new RegraNegocioException("Não é possível criar agendamento em data passada.");
        await ValidarAsync(placa, req.TransportadoraId, req.TerminalId, ct);
        await ValidarConflitoAsync(placa, data, req.Hora, null, ct);
        var agendamento = new Agendamento
        {
            Data = data,
            Hora = req.Hora,
            Placa = placa,
            TransportadoraId = req.TransportadoraId,
            TerminalId = req.TerminalId,
            Carga = req.Carga.Trim(),
            Motorista = req.Motorista?.Trim(),
            JanelaConformidade = req.JanelaConformidade,
            Status = StatusAgendamento.Agendado
        };
        agendamentos.Adicionar(agendamento);
        auditoria.DefinirAcao("Agendamento criado", $"Placa {placa} — {agendamento.Data:dd/MM/yyyy} {agendamento.Hora:HH\\:mm}");
        await uow.SalvarAsync(ct);
        return (await ObterEntidadeAsync(agendamento.Id, ct)).ParaDto();
    }

    public async Task<AgendamentoDto> AtualizarAsync(int id, AtualizarAgendamentoRequest req, CancellationToken ct = default)
    {
        var agendamento = await ObterEntidadeAsync(id, ct);
        var placa = Veiculo.NormalizarPlaca(req.Placa);
        var mudouVeiculo = placa != agendamento.Placa;
        await ValidarAsync(placa, req.TransportadoraId, req.TerminalId, ct, exigirVeiculoRegular: mudouVeiculo);
        if (req.Status != StatusAgendamento.Cancelado)
            await ValidarConflitoAsync(placa, req.Data ?? agendamento.Data, req.Hora, id, ct);
        agendamento.Data = req.Data ?? agendamento.Data;
        agendamento.Hora = req.Hora;
        agendamento.Placa = placa;
        agendamento.TransportadoraId = req.TransportadoraId;
        agendamento.TerminalId = req.TerminalId;
        agendamento.Carga = req.Carga.Trim();
        agendamento.Motorista = req.Motorista?.Trim();
        agendamento.JanelaConformidade = req.JanelaConformidade;
        agendamento.Status = req.Status;
        await uow.SalvarAsync(ct);
        return (await ObterEntidadeAsync(id, ct)).ParaDto();
    }

    public async Task<AgendamentoDto> AtualizarStatusAsync(int id, StatusAgendamento status, CancellationToken ct = default)
    {
        var agendamento = await ObterEntidadeAsync(id, ct);
        agendamento.Status = status;
        auditoria.DefinirAcao("Status de agendamento atualizado", $"{agendamento.Codigo} ({agendamento.Placa}) — {status}");
        await uow.SalvarAsync(ct);
        return agendamento.ParaDto();
    }

    /// <summary>
    /// Além de validar cadastro, aplica o efeito do N3 ("bloqueio automático
    /// para novos carregamentos"): carreta negativada não pode ser agendada.
    /// </summary>
    private async Task ValidarAsync(string placa, int transportadoraId, string terminalId, CancellationToken ct, bool exigirVeiculoRegular = true)
    {
        if (await transportadoras.ObterAsync(transportadoraId, ct) is null)
            throw new RegraNegocioException($"Transportadora {transportadoraId} não existe.");
        if (await terminais.ObterAsync(terminalId, ct) is null)
            throw new RegraNegocioException($"Terminal '{terminalId}' não existe.");
        var veiculo = await veiculos.ObterPorPlacaAsync(placa, ct);
        if (veiculo is not null && veiculo.TransportadoraId != transportadoraId)
            throw new RegraNegocioException($"A placa {placa} está cadastrada em outra transportadora.");
        if (exigirVeiculoRegular && veiculo is { EstaNegativado: true })
            throw new RegraNegocioException($"Carreta {placa} está negativada (bloqueio N3) e não pode ser agendada.");
    }

    /// <summary>A mesma carreta não pode ter dois agendamentos ativos no mesmo dia e horário (também garantido por índice único).</summary>
    private async Task ValidarConflitoAsync(string placa, DateOnly data, TimeOnly hora, int? ignorarId, CancellationToken ct)
    {
        if (await agendamentos.ExisteConflitoAsync(placa, data, hora, ignorarId, ct))
            throw new RegraNegocioException($"A carreta {placa} já tem agendamento ativo em {data:dd/MM/yyyy} às {hora:HH\\:mm}.");
    }

    private async Task<Agendamento> ObterEntidadeAsync(int id, CancellationToken ct) =>
        await agendamentos.ObterAsync(id, ct) ?? throw new NaoEncontradoException($"Agendamento {id} não encontrado.");
}
