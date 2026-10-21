using System.Globalization;

namespace BarcaLog.Application.Metricas;

/// <summary>Situação de uma janela de uma hora na estimativa.</summary>
public static class SituacaoJanela
{
    public const string Dentro = "dentro";
    public const string Atencao = "atencao";
    public const string Risco = "risco";
    public const string Insuficiente = "insuficiente";

    public static int Gravidade(string s) => s switch { Risco => 3, Atencao => 2, Dentro => 1, _ => 0 };
}

/// <summary>
/// Confiabilidade da estimativa (docs/INDICADORES.md, "Confiabilidade"):
/// confiavel = base completa, dados em dia e erro medido menor que o de repetir o dia anterior;
/// limitada = utilizável, com restrição explicada; insuficiente = não há base para estimar;
/// desatualizada = os dados pararam de chegar — exige importar ou corrigir a integração.
/// </summary>
public static class Confiabilidade
{
    public const string Confiavel = "confiavel";
    public const string Limitada = "limitada";
    public const string Insuficiente = "insuficiente";
    public const string Desatualizada = "desatualizada";

    /// <summary>Ordem para comparar (maior = melhor).</summary>
    public static int Nivel(string c) => c switch { Confiavel => 3, Limitada => 2, Desatualizada => 1, _ => 0 };

    /// <summary>Pior de várias (para a soma dos terminais).</summary>
    public static string Pior(IEnumerable<string> lista) => lista.DefaultIfEmpty(Insuficiente).MinBy(Nivel)!;
}

/// <summary>Por que a confiabilidade não é "confiável", o que isso muda e o que fazer.</summary>
/// <param name="Codigo">desatualizado | base-incompleta | erro-alto | erro-nao-medido | sem-base | sem-dados.</param>
/// <param name="Terminal">Terminal a que o motivo se refere (na soma de todos).</param>
/// <param name="Descricao">O que foi encontrado, com números.</param>
/// <param name="Impacto">O que isso muda na leitura da estimativa.</param>
/// <param name="Acao">O que fazer.</param>
public sealed record MotivoConfiabilidade(string Codigo, string? Terminal, string Descricao, string Impacto, string Acao);

/// <summary>Erro da estimativa medido nos dados do próprio terminal (backtest).</summary>
/// <param name="DiasAvaliados">Dias reestimados como na véspera.</param>
/// <param name="ErroPct">WAPE das chegadas por hora: soma dos erros ÷ soma do real, em %.</param>
/// <param name="ErroIngenuoPct">WAPE de "repetir o mesmo horário do último dia com dados", em %.</param>
public sealed record ErroMedido(int DiasAvaliados, double ErroPct, double ErroIngenuoPct)
{
    /// <summary>Critério objetivo: a estimativa não pode errar mais que a referência ingênua.</summary>
    public bool NaoPiorQueIngenua => ErroPct <= ErroIngenuoPct;
}

/// <summary>Uma janela de uma hora: demanda prevista × capacidade de atendimento.</summary>
/// <param name="Hora">Hora de início da janela (0–23), horário do porto.</param>
/// <param name="DemandaPrevista">Carretas que costumam chegar nesta hora (média por dia na base).</param>
/// <param name="CapacidadeAtendimento">Referência usada na classificação: capacidade cadastrada ou, sem ela, o atendimento observado.</param>
/// <param name="FonteCapacidade">cadastrada | observada | mista (soma de terminais com fontes diferentes).</param>
/// <param name="AtendimentoObservado">Carretas que costumam ser liberadas nesta hora (média por dia na base).</param>
/// <param name="CapacidadeCadastrada">Capacidade por hora cadastrada (soma, quando todos os terminais somados têm); nula se não configurada.</param>
/// <param name="Saldo">Capacidade − demanda. Positivo = folga; negativo = demanda acima da capacidade. Nulo sem dados.</param>
/// <param name="Situacao">dentro | atencao | risco | insuficiente.</param>
/// <param name="Confiabilidade">confiavel | limitada | insuficiente | desatualizada.</param>
/// <param name="Mensagem">Conclusão em linguagem operacional, gerada a partir dos números.</param>
/// <param name="ChegadasObservadas">Chegadas registradas nesta hora em toda a base (tamanho da amostra).</param>
/// <param name="TerminaisEmRisco">Na visão de todos os terminais: quais estão em risco de fila nesta janela.</param>
public sealed record JanelaEstimada(
    int Hora, double DemandaPrevista, double CapacidadeAtendimento, string FonteCapacidade, double AtendimentoObservado,
    int? CapacidadeCadastrada, double? Saldo, string Situacao, string Confiabilidade, string Mensagem, int ChegadasObservadas,
    IReadOnlyList<string> TerminaisEmRisco);

/// <summary>Resumo do horizonte (próximas N horas).</summary>
/// <param name="DemandaPrevista">Soma das demandas previstas das janelas com dados.</param>
/// <param name="CapacidadeAtendimento">Soma das capacidades de atendimento das mesmas janelas.</param>
/// <param name="Saldo">Capacidade − demanda no horizonte.</param>
/// <param name="PiorSituacao">Situação mais grave entre as janelas.</param>
/// <param name="HoraMaisCritica">Janela com o pior saldo entre as que estão em atenção ou risco; nula se todas dentro.</param>
/// <param name="HoraMaiorMovimento">Janela com a maior demanda prevista.</param>
/// <param name="Tendencia">Como a pressão evolui no horizonte, em uma frase.</param>
/// <param name="JanelasEmRisco">Quantidade de janelas em risco de fila.</param>
/// <param name="JanelasEmAtencao">Quantidade de janelas em atenção.</param>
public sealed record ResumoEstimativa(
    double DemandaPrevista, double CapacidadeAtendimento, double Saldo, string PiorSituacao, int? HoraMaisCritica,
    int? HoraMaiorMovimento, string Tendencia, int JanelasEmRisco, int JanelasEmAtencao);

/// <summary>Estimativa de um terminal (ou de todos somados).</summary>
/// <param name="TerminalId">Id do terminal; nulo na visão de todos.</param>
/// <param name="Terminal">Nome do terminal ou "Todos os terminais".</param>
/// <param name="BaseDe">Primeiro dia da base histórica usada.</param>
/// <param name="BaseAte">Último dia da base histórica usada.</param>
/// <param name="DiasComDados">Dias da base com ao menos uma marcação.</param>
/// <param name="UltimoRegistro">Última marcação recebida.</param>
/// <param name="Desatualizado">Último registro mais antigo que o limite de dias sem dados.</param>
/// <param name="Confiabilidade">confiavel | limitada | insuficiente | desatualizada.</param>
/// <param name="Motivos">Por que a confiabilidade não é "confiável" (motivo, impacto, ação).</param>
/// <param name="Erro">Erro medido nos dados do terminal (nulo na soma ou sem dias suficientes).</param>
/// <param name="Janelas">As próximas janelas de uma hora.</param>
/// <param name="Resumo">Resumo do horizonte.</param>
public sealed record EstimativaTerminal(
    string? TerminalId, string Terminal, DateOnly? BaseDe, DateOnly? BaseAte, int DiasComDados, DateTime? UltimoRegistro,
    bool Desatualizado, string Confiabilidade, IReadOnlyList<MotivoConfiabilidade> Motivos, ErroMedido? Erro,
    IReadOnlyList<JanelaEstimada> Janelas, ResumoEstimativa Resumo);

/// <summary>Terminal considerado na estimativa.</summary>
/// <param name="Id">Id do terminal.</param>
/// <param name="Nome">Nome de exibição.</param>
/// <param name="CapacidadeHoraria">Capacidade por hora cadastrada (nula = não configurada).</param>
/// <param name="UltimaMarcacao">Última marcação recebida (inclui não liberadas); nula usa a última da base.</param>
public sealed record TerminalEstimativa(string Id, string Nome, int? CapacidadeHoraria, DateTime? UltimaMarcacao);

/// <summary>
/// Estimativa das próximas horas pelo padrão histórico — não é modelo preditivo.
/// Para cada terminal: demanda prevista = média de chegadas (marcações) por dia
/// naquela hora nos últimos <see cref="DiasBase"/> dias com dados; capacidade de
/// atendimento = capacidade por hora cadastrada ou, sem ela, a média de liberações
/// naquela hora. Regras documentadas em docs/INDICADORES.md ("Estimativa das próximas horas").
/// </summary>
public static class EstimativaJanelas
{
    /// <summary>Base histórica: últimos N dias, terminando no último dia com dados do terminal.</summary>
    public const int DiasBase = 30;
    /// <summary>Horizonte padrão em horas.</summary>
    public const int HorasHorizonte = 6;
    /// <summary>Abaixo disso não se estima: menos de uma semana não cobre o ciclo semanal da operação.</summary>
    public const int DiasMinimosParaEstimar = 7;
    /// <summary>Menos chegadas registradas que isso na hora, em toda a base: amostra pequena, confiabilidade limitada.</summary>
    public const int ChegadasMinimasNaAmostra = 30;
    /// <summary>
    /// Demanda prevista abaixo disso (carretas/hora) não gera atenção nem risco: mesmo sem nenhuma
    /// liberação na hora, o acúmulo fica abaixo de 5 carretas e é absorvido nas horas seguintes.
    /// </summary>
    public const double DemandaMinimaRelevante = 5;
    /// <summary>Excesso menor que 1 carreta/hora não é tratado como pressão (carreta é indivisível; diferença de médias).</summary>
    public const double ExcessoMinimo = 1;

    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Faixa "20h–21h".</summary>
    public static string Faixa(int hora) => $"{hora:D2}h–{(hora + 1) % 24:D2}h";

    private static string Qtd(double v) => Math.Round(v, MidpointRounding.AwayFromZero).ToString("N0", Br);

    /// <summary>
    /// Classifica uma janela. limiteRisco = múltiplo da capacidade a partir do qual há risco de fila
    /// (parâmetro LimiteRazaoChegadaLiberacao, configurável).
    /// </summary>
    public static string Classificar(double demanda, double capacidade, double limiteRisco)
    {
        if (demanda < DemandaMinimaRelevante) return SituacaoJanela.Dentro;
        var excesso = demanda - capacidade;
        if (excesso < ExcessoMinimo) return SituacaoJanela.Dentro;
        return capacidade <= 0 || demanda >= capacidade * limiteRisco ? SituacaoJanela.Risco : SituacaoJanela.Atencao;
    }

    /// <summary>Conclusão em linguagem operacional para uma janela.</summary>
    public static string Mensagem(string situacao, double demanda, double capacidade, string fonte, double atendimentoObservado, string? terminalEmRisco = null)
    {
        var saldo = capacidade - demanda;
        var (doQue, acimaDe) = fonte switch
        {
            "cadastrada" => ("a capacidade cadastrada", "da capacidade cadastrada"),
            "mista" => ("a capacidade de atendimento", "da capacidade de atendimento"),
            _ => ("o atendimento habitual desta hora", "do atendimento habitual desta hora")
        };
        switch (situacao)
        {
            case SituacaoJanela.Insuficiente:
                return "Sem dados suficientes para estimar esta janela.";
            case SituacaoJanela.Risco:
                return $"Previsão de cerca de {Qtd(-saldo)} carretas a mais do que {doQue}. Avalie redistribuir horários ou reforçar a liberação.";
            case SituacaoJanela.Atencao:
                return $"Demanda cerca de {Qtd(-saldo)} carretas acima {acimaDe}. A fila pode crescer — acompanhe as próximas chegadas.";
        }
        if (terminalEmRisco is not null)
            return $"No conjunto há folga, mas {terminalEmRisco} tem risco de fila nesta janela.";
        if (demanda < DemandaMinimaRelevante)
            return "Pouco movimento previsto nesta janela. Sem sinal de sobrecarga.";
        if (fonte == "cadastrada" && demanda > atendimentoObservado + ExcessoMinimo)
            return $"Dentro da capacidade cadastrada, mas acima do que costuma ser liberado nesta hora (cerca de {Qtd(atendimentoObservado)}).";
        return saldo >= ExcessoMinimo
            ? $"Demanda dentro da capacidade, com folga de cerca de {Qtd(saldo)} carretas. Sem sinal de sobrecarga."
            : "Demanda no limite do atendimento habitual. Sem sinal de sobrecarga, mas sem folga.";
    }

    /// <summary>Estimativa de cada terminal e do conjunto.</summary>
    public static (EstimativaTerminal Geral, IReadOnlyList<EstimativaTerminal> PorTerminal) Calcular(
        IReadOnlyList<RegistroMarcacao> liberados, IReadOnlyList<TerminalEstimativa> terminais, DateTime agoraLocal,
        double limiteRisco, int diasSemDadosAlerta, int horas = HorasHorizonte)
    {
        var porId = liberados.GroupBy(r => r.TerminalId).ToDictionary(g => g.Key, g => g.ToList());
        var horasJanela = Enumerable.Range(0, horas).Select(i => agoraLocal.AddHours(i).Hour).ToList();
        var lista = terminais.Select(t => DoTerminal(t, porId.GetValueOrDefault(t.Id) ?? [], horasJanela, agoraLocal, limiteRisco, diasSemDadosAlerta)).ToList();
        return (Somar(lista, horasJanela), lista);
    }

    private static EstimativaTerminal DoTerminal(TerminalEstimativa t, List<RegistroMarcacao> regs, List<int> horasJanela,
        DateTime agora, double limiteRisco, int diasSemDados)
    {
        var ultimo = t.UltimaMarcacao ?? (regs.Count > 0 ? regs.Max(r => r.MarcadoEm) : null);
        if (regs.Count == 0)
            return Vazio(t.Id, t.Nome, ultimo, horasJanela, [new("sem-dados", t.Nome, "Nenhum movimento liberado deste terminal.",
                "Não há como estimar.", "Importar as planilhas do terminal em Importação.")]);

        var fim = regs.Max(r => r.MarcadoEm).Date.AddDays(1);
        var inicio = fim.AddDays(-DiasBase);
        var naBase = regs.Where(r => r.MarcadoEm >= inicio && r.MarcadoEm < fim).ToList();
        var dias = naBase.Select(r => r.MarcadoEm.Date).Distinct().Count();
        var diasSemRegistro = ultimo is { } u ? (int)(agora.Date - u.Date).TotalDays : (int?)null;
        var desatualizado = diasSemRegistro > diasSemDados;
        var baseDe = DateOnly.FromDateTime(inicio);
        var baseAte = DateOnly.FromDateTime(fim.AddDays(-1));

        if (dias < DiasMinimosParaEstimar)
            return Vazio(t.Id, t.Nome, ultimo, horasJanela, [new("sem-base", t.Nome,
                $"Só {dias} dia(s) com dados de {baseDe:dd/MM} a {baseAte:dd/MM}; o mínimo é {DiasMinimosParaEstimar} (uma semana de operação).",
                "Não há base para uma estimativa responsável.", "Importar ao menos uma semana de planilhas do terminal.")], baseDe, baseAte, dias, desatualizado);

        // Divide pelos dias COM dados: um dia sem nenhuma marcação no terminal é falta de dado, não dia parado.
        var chegadas = naBase.GroupBy(r => r.MarcadoEm.Hour).ToDictionary(g => g.Key, g => g.Count());
        var liberacoes = regs.Where(r => r.LiberadoEm >= inicio && r.LiberadoEm < fim).GroupBy(r => r.LiberadoEm.Hour).ToDictionary(g => g.Key, g => g.Count());
        var erro = MedirErro(regs);

        var motivos = new List<MotivoConfiabilidade>();
        if (desatualizado)
            motivos.Add(new("desatualizado", t.Nome,
                $"Último registro em {ultimo:dd/MM/yyyy}, há {diasSemRegistro} dias (limite: {diasSemDados}).",
                $"A estimativa repete o padrão de {baseDe:dd/MM} a {baseAte:dd/MM}; mudanças recentes de safra ou de agenda não aparecem.",
                "Importar as planilhas mais recentes do terminal (ou verificar a integração)."));
        if (dias < DiasBase)
            motivos.Add(new("base-incompleta", t.Nome,
                $"{dias} dias com dados de {baseDe:dd/MM} a {baseAte:dd/MM} (a base completa tem {DiasBase}).",
                "Médias com menos dias variam mais de um dia para outro.",
                "Importar o período que falta, se existir."));
        if (erro is null)
            motivos.Add(new("erro-nao-medido", t.Nome,
                $"Ainda não há dias suficientes para medir o erro (são necessários {DiasMinimosParaEstimar} dias anteriores a cada dia avaliado).",
                "Não se sabe quanto a estimativa erra neste terminal.", "Importar mais histórico."));
        else if (!erro.NaoPiorQueIngenua)
            motivos.Add(new("erro-alto", t.Nome,
                $"Nos últimos {erro.DiasAvaliados} dias com dados, a estimativa errou {erro.ErroPct:0.0}% das chegadas por hora, mais que repetir o dia anterior ({erro.ErroIngenuoPct:0.0}%).",
                "Use como referência do padrão do horário, não como número exato de carretas.",
                "Confirmar com a fila real antes de redistribuir agendamentos."));
        var confTerminal = desatualizado ? Confiabilidade.Desatualizada : motivos.Count > 0 ? Confiabilidade.Limitada : Confiabilidade.Confiavel;

        var janelas = horasJanela.Select(h =>
        {
            var obs = chegadas.GetValueOrDefault(h);
            var demanda = obs / (double)dias;
            var observado = liberacoes.GetValueOrDefault(h) / (double)dias;
            var (capacidade, fonte) = t.CapacidadeHoraria is { } c ? ((double)c, "cadastrada") : (observado, "observada");
            var situacao = Classificar(demanda, capacidade, limiteRisco);
            // Amostra pequena na hora (menos de 30 chegadas na base) rebaixa só a janela.
            var conf = obs < ChegadasMinimasNaAmostra && confTerminal == Confiabilidade.Confiavel ? Confiabilidade.Limitada : confTerminal;
            return new JanelaEstimada(h, Math.Round(demanda, 1), Math.Round(capacidade, 1), fonte, Math.Round(observado, 1), t.CapacidadeHoraria,
                Math.Round(capacidade - demanda, 1), situacao, conf, Mensagem(situacao, demanda, capacidade, fonte, observado), obs,
                situacao == SituacaoJanela.Risco ? [t.Nome] : []);
        }).ToList();

        return new EstimativaTerminal(t.Id, t.Nome, baseDe, baseAte, dias, ultimo, desatualizado, confTerminal, motivos, erro, janelas, Resumir(janelas));
    }

    /// <summary>Quantos dos últimos dias com dados são reestimados para medir o erro.</summary>
    public const int DiasMedicaoErro = 30;

    /// <summary>
    /// Backtest no próprio terminal: para cada um dos últimos <see cref="DiasMedicaoErro"/> dias com dados,
    /// refaz a estimativa como na véspera (base = dias com dados nos 30 dias anteriores, mínimo
    /// <see cref="DiasMinimosParaEstimar"/>) e compara, hora a hora, com as chegadas reais. Mesma conta de
    /// backend/tools/backtest_estimativa.py. Nulo se nenhum dia puder ser avaliado.
    /// </summary>
    public static ErroMedido? MedirErro(IReadOnlyList<RegistroMarcacao> regs)
    {
        if (regs.Count == 0) return null;
        var porDiaHora = regs.GroupBy(r => (r.MarcadoEm.Date, r.MarcadoEm.Hour)).ToDictionary(g => g.Key, g => g.Count());
        var dias = regs.Select(r => r.MarcadoEm.Date).Distinct().OrderBy(d => d).ToList();
        double erro = 0, erroIngenuo = 0, real = 0;
        var avaliados = 0;
        for (var i = Math.Max(0, dias.Count - DiasMedicaoErro); i < dias.Count; i++)
        {
            var dia = dias[i];
            var baseDias = dias.Take(i).Where(d => d >= dia.AddDays(-DiasBase)).ToList();
            if (baseDias.Count < DiasMinimosParaEstimar) continue;
            var anterior = baseDias[^1];
            avaliados++;
            for (var h = 0; h < 24; h++)
            {
                var r = porDiaHora.GetValueOrDefault((dia, h));
                var prev = baseDias.Sum(d => porDiaHora.GetValueOrDefault((d, h))) / (double)baseDias.Count;
                erro += Math.Abs(prev - r);
                erroIngenuo += Math.Abs(porDiaHora.GetValueOrDefault((anterior, h)) - r);
                real += r;
            }
        }
        if (avaliados == 0 || real == 0) return null;
        return new ErroMedido(avaliados, Math.Round(erro / real * 100, 1), Math.Round(erroIngenuo / real * 100, 1));
    }

    private static EstimativaTerminal Vazio(string? id, string nome, DateTime? ultimo, List<int> horasJanela, List<MotivoConfiabilidade> motivos,
        DateOnly? de = null, DateOnly? ate = null, int dias = 0, bool desatualizado = false)
    {
        var janelas = horasJanela.Select(h => new JanelaEstimada(h, 0, 0, "observada", 0, null, null, SituacaoJanela.Insuficiente,
            Confiabilidade.Insuficiente, Mensagem(SituacaoJanela.Insuficiente, 0, 0, "observada", 0), 0, [])).ToList();
        return new EstimativaTerminal(id, nome, de, ate, dias, ultimo, desatualizado, Confiabilidade.Insuficiente, motivos, null, janelas, Resumir(janelas));
    }

    /// <summary>
    /// Todos os terminais: números somados, mas a situação de cada janela é a do terminal
    /// em pior situação — carretas não migram de um terminal para outro. A confiabilidade
    /// é a pior entre os terminais somados; os sem base ficam fora da soma, com motivo.
    /// </summary>
    private static EstimativaTerminal Somar(List<EstimativaTerminal> lista, List<int> horasJanela)
    {
        var validos = lista.Where(t => t.Confiabilidade != Confiabilidade.Insuficiente).ToList();
        if (validos.Count == 0)
            return Vazio(null, "Todos os terminais", lista.Max(t => t.UltimoRegistro), horasJanela,
                lista.SelectMany(t => t.Motivos).ToList());

        var motivos = lista.Where(t => t.Confiabilidade == Confiabilidade.Insuficiente)
            .Select(t => new MotivoConfiabilidade("sem-base", t.Terminal, $"{t.Terminal} sem dados suficientes; ficou fora da soma.",
                "Os totais não incluem este terminal.", t.Motivos.FirstOrDefault()?.Acao ?? "Importar as planilhas do terminal."))
            .Concat(validos.SelectMany(t => t.Motivos))
            .ToList();
        var conf = Confiabilidade.Pior(validos.Select(t => t.Confiabilidade).Append(motivos.Count > 0 ? Confiabilidade.Limitada : Confiabilidade.Confiavel));
        var janelas = horasJanela.Select((h, i) =>
        {
            var partes = validos.Select(t => t.Janelas[i]).ToList();
            var demanda = partes.Sum(p => p.DemandaPrevista);
            var capacidade = partes.Sum(p => p.CapacidadeAtendimento);
            var observado = partes.Sum(p => p.AtendimentoObservado);
            var fontes = partes.Select(p => p.FonteCapacidade).Distinct().ToList();
            var fonte = fontes.Count == 1 ? fontes[0] : "mista";
            var cadastrada = partes.All(p => p.CapacidadeCadastrada is not null) ? partes.Sum(p => p.CapacidadeCadastrada!.Value) : (int?)null;
            var pior = partes.MaxBy(p => SituacaoJanela.Gravidade(p.Situacao))!;
            var emRisco = validos.Where(t => t.Janelas[i].Situacao == SituacaoJanela.Risco).Select(t => t.Terminal).ToList();
            var emAtencao = validos.Where(t => t.Janelas[i].Situacao == SituacaoJanela.Atencao).Select(t => t.Terminal).ToList();
            var situacao = pior.Situacao;
            var mensagem = situacao switch
            {
                SituacaoJanela.Risco => $"Risco de fila em {string.Join(" e ", emRisco)}. {pior.Mensagem}",
                SituacaoJanela.Atencao => $"Atenção em {string.Join(" e ", emAtencao)}. {pior.Mensagem}",
                _ => Mensagem(SituacaoJanela.Dentro, demanda, capacidade, fonte, observado)
            };
            var janelaConf = Confiabilidade.Pior(partes.Select(p => p.Confiabilidade).Append(conf));
            return new JanelaEstimada(h, Math.Round(demanda, 1), Math.Round(capacidade, 1), fonte, Math.Round(observado, 1), cadastrada,
                Math.Round(capacidade - demanda, 1), situacao, janelaConf, mensagem, partes.Sum(p => p.ChegadasObservadas), emRisco);
        }).ToList();

        return new EstimativaTerminal(null, "Todos os terminais", validos.Min(t => t.BaseDe), validos.Max(t => t.BaseAte),
            validos.Max(t => t.DiasComDados), lista.Max(t => t.UltimoRegistro), validos.Any(t => t.Desatualizado), conf, motivos, null, janelas, Resumir(janelas));
    }

    /// <summary>Resumo e tendência do horizonte, sem limiares novos: só a ordem das janelas.</summary>
    public static ResumoEstimativa Resumir(IReadOnlyList<JanelaEstimada> janelas)
    {
        var comDados = janelas.Where(j => j.Situacao != SituacaoJanela.Insuficiente).ToList();
        if (comDados.Count == 0)
            return new ResumoEstimativa(0, 0, 0, SituacaoJanela.Insuficiente, null, null, "Sem dados suficientes para estimar as próximas horas.", 0, 0);

        var demanda = comDados.Sum(j => j.DemandaPrevista);
        var capacidade = comDados.Sum(j => j.CapacidadeAtendimento);
        var pior = comDados.MaxBy(j => SituacaoJanela.Gravidade(j.Situacao))!.Situacao;
        var pressionadas = comDados.Where(j => j.Situacao is SituacaoJanela.Risco or SituacaoJanela.Atencao).ToList();
        var critica = pressionadas.OrderByDescending(j => SituacaoJanela.Gravidade(j.Situacao)).ThenBy(j => j.Saldo).FirstOrDefault();
        var maiorMovimento = comDados.MaxBy(j => j.DemandaPrevista)!;

        string tendencia;
        if (critica is null)
            tendencia = $"Movimento dentro da capacidade nas próximas {janelas.Count} horas. Maior movimento previsto: {Faixa(maiorMovimento.Hora)}.";
        else
        {
            var pos = janelas.ToList().IndexOf(critica);
            var faixa = Faixa(critica.Hora);
            var depoisMelhora = janelas.Skip(pos + 1).Any(j => SituacaoJanela.Gravidade(j.Situacao) < SituacaoJanela.Gravidade(critica.Situacao) && j.Situacao != SituacaoJanela.Insuficiente);
            tendencia = pos == 0
                ? depoisMelhora ? $"Pressão maior agora ({faixa}), diminuindo nas horas seguintes." : $"Pressão desde agora ({faixa}) e sem alívio previsto no horizonte."
                : pos == janelas.Count - 1
                    ? $"Pressão crescente, mais forte no fim do horizonte ({faixa})."
                    : depoisMelhora ? $"Pressão aumenta até {faixa} e depois diminui." : $"Pressão aumenta a partir de {faixa}.";
        }
        return new ResumoEstimativa(Math.Round(demanda, 1), Math.Round(capacidade, 1), Math.Round(capacidade - demanda, 1), pior,
            critica?.Hora, maiorMovimento.Hora, tendencia,
            comDados.Count(j => j.Situacao == SituacaoJanela.Risco), comDados.Count(j => j.Situacao == SituacaoJanela.Atencao));
    }
}
