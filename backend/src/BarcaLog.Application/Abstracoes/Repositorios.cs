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

public sealed record TransportadoraComFrota(Transportadora Transportadora, int CarretasTotal, int CarretasNegativadas);

public interface ITransportadoraRepositorio
{
    /// <summary>Transportadoras com contagem de frota calculada no banco (sem carregar os veículos).</summary>
    Task<List<TransportadoraComFrota>> ListarComContagemAsync(CancellationToken ct = default);
    Task<TransportadoraComFrota?> ObterComContagemAsync(int id, CancellationToken ct = default);
    Task<Transportadora?> ObterAsync(int id, CancellationToken ct = default);
    Task<bool> ExisteNomeOuCnpjAsync(string nome, string cnpj, int? ignorarId, CancellationToken ct = default);
    Task<bool> PossuiVinculosAsync(int id, CancellationToken ct = default);
    void Adicionar(Transportadora transportadora);
    void Remover(Transportadora transportadora);
}

public interface IVeiculoRepositorio
{
    Task<Pagina<Veiculo>> ListarAsync(FiltroVeiculos filtro, CancellationToken ct = default);
    Task<Veiculo?> ObterAsync(int id, CancellationToken ct = default);
    Task<Veiculo?> ObterPorPlacaAsync(string placa, CancellationToken ct = default);
    Task<bool> PlacaExisteAsync(string placa, int? ignorarId, CancellationToken ct = default);
    /// <summary>Veículos No Pátio / Aguardando, em ordem de chegada no status.</summary>
    Task<List<Veiculo>> ListarFilaAsync(CancellationToken ct = default);
    Task<Dictionary<StatusPortaria, int>> ContarPorStatusPortariaAsync(CancellationToken ct = default);
    void Adicionar(Veiculo veiculo);
    void Remover(Veiculo veiculo);
}

public interface ICondutorRepositorio
{
    Task<Pagina<Condutor>> ListarAsync(FiltroCondutores filtro, CancellationToken ct = default);
    Task<Condutor?> ObterAsync(int id, CancellationToken ct = default);
    void Adicionar(Condutor condutor);
    void Remover(Condutor condutor);
}

public interface IOcorrenciaRepositorio
{
    Task<Pagina<Ocorrencia>> ListarAsync(FiltroOcorrencias filtro, CancellationToken ct = default);
    Task<List<Ocorrencia>> ListarPorCondutorAsync(int condutorId, CancellationToken ct = default);
    Task<Ocorrencia?> ObterAsync(int id, CancellationToken ct = default);
    Task<List<Ocorrencia>> ListarNaoResolvidasPorPlacaAsync(string placa, CancellationToken ct = default);
    Task<Dictionary<int, int>> ContarPorTransportadoraDesdeAsync(NivelOcorrencia nivel, DateTime desdeUtc, CancellationToken ct = default);
    void Adicionar(Ocorrencia ocorrencia);
}

public interface IContestacaoRepositorio
{
    Task<Pagina<Contestacao>> ListarAsync(FiltroContestacoes filtro, CancellationToken ct = default);
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
    Task<Pagina<Agendamento>> ListarAsync(FiltroAgendamentos filtro, CancellationToken ct = default);
    /// <summary>Todos os agendamentos de um dia (resumo diário — volume limitado por natureza).</summary>
    Task<List<Agendamento>> ListarDoDiaAsync(DateOnly data, string? terminalId, CancellationToken ct = default);
    Task<List<Agendamento>> ListarPorMotoristaAsync(string nomeMotorista, CancellationToken ct = default);
    Task<bool> ExisteConflitoAsync(string placa, DateOnly data, TimeOnly hora, int? ignorarId, CancellationToken ct = default);
    Task<Agendamento?> ObterAsync(int id, CancellationToken ct = default);
    void Adicionar(Agendamento agendamento);
}

public interface IUsuarioRepositorio
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct = default);
    Task<Usuario?> ObterAsync(int id, CancellationToken ct = default);
    Task<bool> ExisteAlgumAsync(CancellationToken ct = default);
    Task<Pagina<Usuario>> ListarAsync(FiltroUsuarios filtro, CancellationToken ct = default);
    Task<int> ContarGestoresAtivosAsync(CancellationToken ct = default);

    // Contabilidade de login: UPDATE atômico no SQL (sem ler-modificar-gravar),
    // senão tentativas paralelas escapariam do contador de força bruta. Cada
    // chamada grava sua própria linha de auditoria na mesma transação.

    /// <summary>FalhasLoginConsecutivas += 1; ao atingir <paramref name="maximo"/>, bloqueia até agora+duração e zera. Devolve true se bloqueou.</summary>
    Task<bool> RegistrarFalhaLoginAsync(int usuarioId, DateTime agoraUtc, int maximo, TimeSpan duracaoBloqueio, string motivo, CancellationToken ct = default);

    /// <summary>Zera falhas/bloqueio, grava UltimoLoginEm e, se informado, o hash com parâmetros novos.</summary>
    Task RegistrarLoginAsync(int usuarioId, DateTime agoraUtc, string? novoHash, CancellationToken ct = default);

    /// <summary>Grava o passo TOTP usado só se for maior que o último — false = código já usado (replay), inclusive em corrida.</summary>
    Task<bool> ConsumirPassoMfaAsync(int usuarioId, long passo, CancellationToken ct = default);
    void Adicionar(Usuario usuario);
}
