using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;

namespace BarcaLog.Application.Abstracoes;

/// <summary>Fecha a transação. É aqui que o interceptor de auditoria grava o LogAuditoria.</summary>
public interface IUnitOfWork
{
    Task<int> SalvarAsync(CancellationToken ct = default);

    /// <summary>
    /// Executa <paramref name="acao"/> numa única transação (com a estratégia de
    /// nova tentativa do provedor: em falha transitória o bloco inteiro é repetido).
    /// </summary>
    Task ExecutarEmTransacaoAsync(Func<CancellationToken, Task> acao, CancellationToken ct = default);
}

/// <summary>Uma linha lida de planilha: número da linha (1 = cabeçalho) e valores por coluna normalizada.</summary>
public sealed record LinhaPlanilha(int Numero, IReadOnlyDictionary<string, object?> Valores);

/// <summary>Lê XLSX/CSV. Colunas normalizadas: MAIÚSCULAS, sem acento, espaços viram "_".</summary>
public interface ILeitorPlanilha
{
    /// <summary>Colunas do cabeçalho (normalizadas) e as linhas seguintes, em ordem. Linhas totalmente vazias são puladas.</summary>
    (IReadOnlyList<string> Colunas, IEnumerable<LinhaPlanilha> Linhas) Ler(Stream conteudo, FormatoArquivoImportacao formato);
}

public sealed record ResultadoAplicacaoLote(int Inseridos, int Complementados, int ConflitosAplicados);

public interface IImportacaoRepositorio
{
    void AdicionarLote(LoteImportacao lote);
    /// <summary>Lote rastreado (para alterar status/contagens).</summary>
    Task<LoteImportacao?> ObterLoteAsync(int id, CancellationToken ct = default);
    Task<Pagina<LoteImportacao>> ListarLotesAsync(FiltroLotesImportacao filtro, CancellationToken ct = default);
    /// <summary>Última importação confirmada de cada terminal (quando e qual arquivo).</summary>
    Task<Dictionary<string, (DateTime Quando, string Arquivo)>> UltimaConfirmadaPorTerminalAsync(CancellationToken ct = default);
    /// <summary>Outra importação do mesmo terminal confirmada depois de <paramref name="desdeUtc"/> (análise desatualizada).</summary>
    Task<LoteImportacao?> ConfirmadoDepoisAsync(string terminalId, DateTime desdeUtc, int ignorarLoteId, CancellationToken ct = default);
    Task<DateTime?> ConfirmadoComMesmoHashAsync(string hashSha256, string terminalId, int ignorarLoteId, CancellationToken ct = default);
    /// <summary>Gravação em massa das linhas de revisão (fora do change tracker).</summary>
    Task GravarLinhasAsync(IReadOnlyList<LinhaImportacao> linhas, CancellationToken ct = default);
    Task<Pagina<LinhaImportacao>> ListarLinhasAsync(int loteId, FiltroLinhasImportacao filtro, CancellationToken ct = default);
    IAsyncEnumerable<LinhaImportacao> ListarLinhasParaRelatorioAsync(int loteId, CancellationToken ct = default);
    Task<int> ContarConflitosPendentesAsync(int loteId, CancellationToken ct = default);
    Task<int> DecidirConflitosAsync(int loteId, IReadOnlyCollection<long>? linhaIds, DecisaoConflitoImportacao decisao, CancellationToken ct = default);
    /// <summary>
    /// Grava no banco, em operações de conjunto, o que a análise decidiu:
    /// insere os novos, completa campos vazios e aplica conflitos aprovados.
    /// Precisa rodar dentro de <see cref="IUnitOfWork.ExecutarEmTransacaoAsync"/>.
    /// </summary>
    Task<ResultadoAplicacaoLote> AplicarAsync(LoteImportacao lote, CancellationToken ct = default);
    Task RemoverLinhasAsync(int loteId, IReadOnlyCollection<ClassificacaoLinhaImportacao> classificacoes, CancellationToken ct = default);
    /// <summary>Evento de auditoria explícito, para operações em massa que não passam pelo change tracker.</summary>
    Task RegistrarEventoAsync(string autor, string acao, string detalhes, DateTime quandoUtc, CancellationToken ct = default);
}

public interface IAlertaRepositorio
{
    /// <summary>Alertas (rastreados, com eventos) pelas chaves das regras.</summary>
    Task<Dictionary<string, Alerta>> ObterPorChavesAsync(IEnumerable<string> chaves, CancellationToken ct = default);
    /// <summary>Alertas marcados como ativos (rastreados, com eventos).</summary>
    Task<List<Alerta>> ListarAtivosAsync(CancellationToken ct = default);
    Task<Pagina<Alerta>> ListarAsync(FiltroAlertas filtro, CancellationToken ct = default);
    Task<Alerta?> ObterAsync(int id, CancellationToken ct = default);
    Task<Alerta?> ObterParaAlterarAsync(int id, CancellationToken ct = default);
    /// <summary>Abertos = ativos e ainda não resolvidos/descartados.</summary>
    Task<Dictionary<TipoAlerta, int>> ContarAbertosPorTipoAsync(CancellationToken ct = default);
    Task<DateTime?> UltimaAtualizacaoAsync(CancellationToken ct = default);
    void Adicionar(Alerta alerta);
}

public interface IConvenioRepositorio
{
    Task<List<Convenio>> ListarAsync(CancellationToken ct = default);
    Task<Convenio?> ObterAsync(string codigo, CancellationToken ct = default);
    void Adicionar(Convenio convenio);
    /// <summary>Operador e carga mais frequentes já gravados para cada nome de convênio (base histórica).</summary>
    Task<Dictionary<string, (string Operador, string Carga)>> OperadorCargaPorNomeAsync(CancellationToken ct = default);
}

public interface ITerminalRepositorio
{
    Task<List<Terminal>> ListarAsync(CancellationToken ct = default);
    Task<Terminal?> ObterAsync(string id, CancellationToken ct = default);
    /// <summary>Terminal rastreado, para alterar a capacidade.</summary>
    Task<Terminal?> ObterParaAlterarAsync(string id, CancellationToken ct = default);
    /// <summary>Linha única de parâmetros (criada pela migration).</summary>
    Task<ParametrosOperacionais> ObterParametrosAsync(bool rastrear, CancellationToken ct = default);
    /// <summary>true se o banco contém o cadastro fictício de exemplo (placas, transportadoras, condutores…).</summary>
    Task<bool> ExistemDadosExemploAsync(CancellationToken ct = default);
}

public interface IMarcacaoRepositorio
{
    Task<Pagina<Marcacao>> ListarAsync(FiltroMarcacoes filtro, CancellationToken ct = default);
    Task<Marcacao?> ObterAsync(string movimentoId, CancellationToken ct = default);
    /// <summary>Projeção leve de todas as marcações já liberadas, usada pelo motor de métricas.</summary>
    Task<List<RegistroMarcacao>> ListarRegistrosLiberadosAsync(CancellationToken ct = default);
    Task<HashSet<string>> ListarIdsExistentesAsync(IEnumerable<string> movimentoIds, CancellationToken ct = default);
    Task<Dictionary<string, Marcacao>> ObterVariasAsync(IEnumerable<string> movimentoIds, CancellationToken ct = default);
    /// <summary>Movimentos do terminal liberados exatamente em <paramref name="liberacao"/>.</summary>
    Task<int> ContarLiberadosEmAsync(string terminalId, DateTime liberacao, CancellationToken ct = default);
    /// <summary>Marcações sem liberação, por terminal, com marcação no intervalo [inicio, fim).</summary>
    Task<Dictionary<string, int>> ContarSemLiberacaoAsync(DateTime? inicio, DateTime? fim, CancellationToken ct = default);
    /// <summary>Marcações ainda sem liberação marcadas antes de <paramref name="limite"/> (horário do porto).</summary>
    Task<List<(string MovimentoId, string TerminalId, DateTime DataMarcacao)>> ListarAbertasMarcadasAntesDeAsync(DateTime limite, CancellationToken ct = default);
    /// <summary>Última marcação e última liberação recebidas de cada terminal.</summary>
    Task<Dictionary<string, (DateTime? UltimaMarcacao, DateTime? UltimaLiberacao)>> UltimosEventosPorTerminalAsync(CancellationToken ct = default);
    /// <summary>Valores atuais dos movimentos informados (sem rastreamento), para comparar com uma planilha.</summary>
    Task<Dictionary<string, Domain.Regras.DadosMovimento>> ObterDadosAsync(IEnumerable<string> movimentoIds, CancellationToken ct = default);
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
