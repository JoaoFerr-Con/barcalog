namespace BarcaLog.Application.Metricas;

/// <summary>
/// Linha de marcação já liberada, no formato consumido pelas fórmulas (equivale
/// ao objeto { id, senha, convenio, operador, carga, ciclo, marcadoEm,
/// liberadoEm, esperaHoras, empresaId, empresaNome } do frontend).
/// </summary>
public sealed record RegistroMarcacao(
    string MovimentoId,
    string Senha,
    string Convenio,
    string Operador,
    string Carga,
    int Ciclo,
    DateTime MarcadoEm,
    DateTime LiberadoEm,
    double EsperaHoras,
    string TerminalId,
    string TerminalNome);

public sealed record MesTotal(string Chave, string Rotulo, int Total);

public sealed record MesDetalhado(string Chave, string Rotulo, int Total, double Representatividade, int DiasOperados, double MediaDiaria);

public sealed record KpisGerais(
    int Total,
    double MediaDiaria,
    int DiasOperados,
    MesTotal MesMaisMovimentado,
    double TempoMedioEspera,
    double TempoMedioPonderado,
    RegistroMarcacao MaiorAtraso);

public sealed record OperadorTotal(string Operador, int Total);
public sealed record CargaTotal(string Carga, int Total);
public sealed record EmpresaTotal(string Empresa, int Total, double EsperaMedia);
public sealed record ProjecaoPonto(string Chave, string Rotulo, int Total, bool Projetado);
public sealed record ProjecaoVolume(string Tendencia, IReadOnlyList<ProjecaoPonto> Pontos);
public sealed record HoraTotal(int Hora, int Total);
public sealed record DiaSemanaTotal(string Rotulo, int Total);
public sealed record TendenciaSla(string UltimoMesRotulo, double EsperaUltimoMes, double EsperaMediaAnterior, double VariacaoPct, bool Piorou, bool Melhorou);
public sealed record ScoreOperador(string Operador, int Total, double EsperaMedia, double Desvio, int Score);
public sealed record AtrasoRecorrente(string Convenio, string EmpresaNome, int Ocorrencias, double EsperaMedia);
public sealed record CicloBalde(string Balde, int Total, double EsperaMedia);
public sealed record DiaEmpresaTotal(string Data, string Empresa, int Total);
public sealed record DiaDoMes(int Dia, int Total);
public sealed record DetalhamentoMes(string Chave, string Rotulo, int Total, IReadOnlyList<DiaDoMes> Dias);

public sealed record JanelaDistribuicao(string Chave, string Rotulo, double Min, double? Max, string Cor, int Total, double Pct);
public sealed record FaixaRitmo(int Hora, int Chegadas, int Saidas, double Lambda, double Mu, bool Congestionado);
public sealed record AlertaRitmo(int De, int Ate, int DuracaoHoras);
public sealed record RitmoOperacional(IReadOnlyList<FaixaRitmo> Faixas, IReadOnlyList<AlertaRitmo> Alertas);
public sealed record CustoDemurrage(double CustoTotal, int CarretasRetidas, double LimiteHoras, double CustoHora);
public sealed record TurnoConcentracao(string Rotulo, int Min, int Max, int Total, int Retidos, double PctRetencao);
public sealed record HorarioCritico(string DiaSemana, string Turno, int Total);
public sealed record CapacidadeDia(string Data, int Total, double PctCapacidade);
public sealed record DiaCompacto(string Data, string Empresa, int Total, double PctCapacidade, bool Gargalo);
public sealed record AlertaSaturacao(string Tipo, int MediaRecente, int TendenciaDiaria, int? DiasParaSaturacao, string Mensagem);
public sealed record Recomendacao(string Prioridade, string Acao, string Detalhe);
public sealed record FaixaCustoGargalo(string Chave, string Rotulo, double Min, double? Max, int Qtd, double MediaExcedente, double CustoPorVeiculo, double ImpactoTotal);
public sealed record CustoAmpliadoGargalo(IReadOnlyList<FaixaCustoGargalo> Faixas, int TotalVeiculos, double TotalImpacto);
public sealed record FatorUtilizacao(int Hora, double Lambda, double Mu, double Rho, string Diagnostico);
public sealed record SlaEtapa(string Etapa, double PctAtual, string SlaAlvo, string Controle, string TempoAtual);
public sealed record CenarioRoi(string Rotulo, double Reducao, string NovoTmp, double HorasEconomizadas, double EconomiaSafra);
public sealed record ImpactoEsg(int TotalCarretas, double TmpMedio, long LitrosDiesel, long ToneladasCO2);
public sealed record ProjecaoMensal(string Chave, string Rotulo, int Total, bool Projetado, int MargemInferior, int MargemSuperior, int Confianca);
public sealed record AnalisePreditiva(IReadOnlyList<ProjecaoMensal> Projecoes, string Tendencia, int Inclinacao);
public sealed record TmaTerminal(string Terminal, double TmaGeral, double TmaUltimoMes, double? Variacao, int Total);
public sealed record AlertaGargaloPortaria(int Hora, int EmQuantasHoras, double Lambda, double Mu, string Risco, string Mensagem);
public sealed record HoraPico(int Hora, int Total, bool AcimaMedia);
public sealed record PicosEntradaSaida(
    IReadOnlyList<HoraPico> Entrada,
    IReadOnlyList<HoraPico> Saida,
    HoraPico PicoEntrada,
    HoraPico PicoSaida,
    HoraPico ValeEntrada,
    double MediaEntrada,
    double MediaSaida);
public sealed record SerieMensal(string Mes, double Valor);
public sealed record IndicadorPerformance(
    string Terminal,
    double TmaGeral,
    double TaxaD0Geral,
    int TotalGeral,
    double TmaAtual,
    double TaxaD0Atual,
    double? DeltaTMA,
    double? DeltaD0,
    IReadOnlyList<SerieMensal> SerieTMA,
    IReadOnlyList<SerieMensal> SerieD0,
    string UltimoMesRotulo);

/// <summary>Card da "Visão por Terminal". Semáforo: normal=verde, atencao=amarelo, critico=vermelho.</summary>
public sealed record VisaoTerminal(
    string TerminalId,
    string Terminal,
    int Total,
    double MediaDiaria,
    int CapacidadeDiaria,
    double EsperaMedia,
    double OcupacaoPct,
    string Status,
    string Semaforo);

public sealed record RiscoHora(int Hora, double Lambda, double Mu, double Rho, int Indice, string Nivel, string Explicacao);
public sealed record AlertaOperacional(string Nivel, string Titulo, string Detalhe);
