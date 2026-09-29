using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;

namespace BarcaLog.Application.Abstracoes;

/// <summary>Fecha a transação. É aqui que o interceptor de auditoria grava o LogAuditoria.</summary>
public interface IUnitOfWork
{
    Task<int> SalvarAsync(CancellationToken ct = default);
}

public interface ITerminalRepositorio
{
    Task<List<Terminal>> ListarAsync(CancellationToken ct = default);
    Task<Terminal?> ObterAsync(string id, CancellationToken ct = default);
}

public interface IMarcacaoRepositorio
{
    Task<Pagina<Marcacao>> ListarAsync(FiltroMarcacoes filtro, CancellationToken ct = default);
    Task<Marcacao?> ObterAsync(string movimentoId, CancellationToken ct = default);
    /// <summary>Projeção leve de todas as marcações já liberadas, usada pelo motor de métricas.</summary>
    Task<List<RegistroMarcacao>> ListarRegistrosLiberadosAsync(CancellationToken ct = default);
    Task<HashSet<string>> ListarIdsExistentesAsync(IEnumerable<string> movimentoIds, CancellationToken ct = default);
    Task<Dictionary<string, Marcacao>> ObterVariasAsync(IEnumerable<string> movimentoIds, CancellationToken ct = default);
    void Adicionar(Marcacao marcacao);
    void AdicionarVarias(IEnumerable<Marcacao> marcacoes);
    /// <summary>Descarta entidades já salvas do change tracker (importação em lote).</summary>
    void LimparRastreamento();
}

public interface ITransportadoraRepositorio
{
    Task<List<Transportadora>> ListarComFrotaAsync(CancellationToken ct = default);
    Task<Transportadora?> ObterComFrotaAsync(int id, CancellationToken ct = default);
    Task<Transportadora?> ObterAsync(int id, CancellationToken ct = default);
    Task<bool> ExisteNomeOuCnpjAsync(string nome, string cnpj, int? ignorarId, CancellationToken ct = default);
    Task<bool> PossuiVinculosAsync(int id, CancellationToken ct = default);
    void Adicionar(Transportadora transportadora);
    void Remover(Transportadora transportadora);
}

public interface IVeiculoRepositorio
{
    Task<List<Veiculo>> ListarAsync(FiltroVeiculos filtro, CancellationToken ct = default);
    Task<Veiculo?> ObterAsync(int id, CancellationToken ct = default);
    Task<Veiculo?> ObterPorPlacaAsync(string placa, CancellationToken ct = default);
    Task<bool> PlacaExisteAsync(string placa, int? ignorarId, CancellationToken ct = default);
    /// <summary>Veículos No Pátio / Aguardando, em ordem de chegada no status.</summary>
    Task<List<Veiculo>> ListarFilaAsync(CancellationToken ct = default);
    Task<List<Veiculo>> ListarTodosAsync(CancellationToken ct = default);
    void Adicionar(Veiculo veiculo);
    void Remover(Veiculo veiculo);
}

public interface ICondutorRepositorio
{
    Task<List<Condutor>> ListarAsync(int? transportadoraId, CancellationToken ct = default);
    Task<Condutor?> ObterAsync(int id, CancellationToken ct = default);
    void Adicionar(Condutor condutor);
    void Remover(Condutor condutor);
}

public interface IOcorrenciaRepositorio
{
    Task<List<Ocorrencia>> ListarAsync(FiltroOcorrencias filtro, CancellationToken ct = default);
    Task<Ocorrencia?> ObterAsync(int id, CancellationToken ct = default);
    Task<List<Ocorrencia>> ListarNaoResolvidasPorPlacaAsync(string placa, CancellationToken ct = default);
    Task<Dictionary<int, int>> ContarPorTransportadoraDesdeAsync(NivelOcorrencia nivel, DateTime desdeUtc, CancellationToken ct = default);
    void Adicionar(Ocorrencia ocorrencia);
}

public interface IContestacaoRepositorio
{
    Task<List<Contestacao>> ListarAsync(FiltroContestacoes filtro, CancellationToken ct = default);
    Task<Contestacao?> ObterAsync(int id, CancellationToken ct = default);
    Task<bool> ExistePendenteAsync(int ocorrenciaId, CancellationToken ct = default);
    void Adicionar(Contestacao contestacao);
}

public interface IAuditoriaRepositorio
{
    Task<Pagina<LogAuditoria>> BuscarAsync(FiltroAuditoria filtro, CancellationToken ct = default);
}

public interface IAgendamentoRepositorio
{
    Task<List<Agendamento>> ListarAsync(FiltroAgendamentos filtro, CancellationToken ct = default);
    Task<Agendamento?> ObterAsync(int id, CancellationToken ct = default);
    void Adicionar(Agendamento agendamento);
}

public interface IUsuarioRepositorio
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct = default);
    Task<Usuario?> ObterAsync(int id, CancellationToken ct = default);
    Task<bool> ExisteAlgumAsync(CancellationToken ct = default);
    void Adicionar(Usuario usuario);
}
