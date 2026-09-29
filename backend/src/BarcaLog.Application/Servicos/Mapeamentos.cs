using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Regras;

namespace BarcaLog.Application.Servicos;

internal static class Mapeamentos
{
    public static TerminalDto ParaDto(this Terminal t) => new(t.Id, t.Nome, t.CapacidadeDiariaCarretas);

    public static VeiculoDto ParaDto(this Veiculo v) => new(
        v.Id, v.Placa, v.TransportadoraId, v.Transportadora?.Nome, v.Modelo,
        v.StatusPortaria, v.StatusPortariaDesde, v.StatusNegativacao, v.TerminalId, v.Terminal?.Nome);

    public static CondutorDto ParaDto(this Condutor c) => new(
        c.Id, c.Nome, c.TransportadoraId, c.Transportadora?.Nome, c.PlacaVinculada, c.StatusNegativacao);

    public static OcorrenciaDto ParaDto(this Ocorrencia o) => new(
        o.Id, o.Nivel, o.Placa, o.TransportadoraId, o.Transportadora?.Nome, o.CondutorId, o.Condutor?.Nome,
        o.Descricao, o.Local, o.Responsavel, o.Status, o.CriadoEm);

    public static ContestacaoDto ParaDto(this Contestacao c) => new(
        c.Id, c.OcorrenciaId, c.Ocorrencia?.Nivel, c.Ocorrencia?.Placa, c.TransportadoraId, c.Transportadora?.Nome,
        c.Justificativa, c.Status, c.CriadoEm, c.RespondidoEm, c.RespostaOperador);

    public static LogAuditoriaDto ParaDto(this LogAuditoria l) => new(l.Id, l.Autor, l.Acao, l.Detalhes, l.Quando);

    public static AgendamentoDto ParaDto(this Agendamento a) => new(
        a.Id, a.Codigo, a.Data, a.Hora, a.Placa, a.TransportadoraId, a.Transportadora?.Nome,
        a.TerminalId, a.Terminal?.Nome, a.Carga, a.Motorista, a.JanelaConformidade, a.Status);

    public static MarcacaoDto ParaDto(this Marcacao m)
    {
        var espera = m.EsperaHoras ?? m.CalcularEsperaHoras();
        return new MarcacaoDto(
            m.MovimentoId, m.Senha, m.Convenio, m.CodConvenio, m.Operador, m.Carga, m.Ciclo,
            m.DataMarcacao, m.DataLiberacao, espera,
            espera is null ? null : JanelaPermanencia.Classificar(espera.Value).Chave,
            m.TerminalId, m.Terminal?.Nome);
    }

    public static UsuarioDto ParaDto(this Usuario u) => new(u.Id, u.Nome, u.Email, u.Papel);
}
