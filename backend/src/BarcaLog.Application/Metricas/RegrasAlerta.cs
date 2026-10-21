using System.Globalization;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;

namespace BarcaLog.Application.Metricas;

/// <summary>Alerta proposto por uma regra (antes de virar registro com estado e histórico).</summary>
public sealed record AlertaCandidato(
    string Chave, TipoAlerta Tipo, string Regra, PrioridadeAlerta Prioridade, string? TerminalId, string Periodo,
    string Titulo, string Indicador, string ValorObservado, string Limite, string Evidencia, string Origem, string? AcaoRecomendada);

/// <summary>Tudo que as regras precisam — montado pelo serviço a partir do banco.</summary>
public sealed record EntradaAlertas(
    IReadOnlyList<RegistroMarcacao> Liberadas,
    IReadOnlyList<(string Id, string Nome)> Terminais,
    ParametrosOperacionais Parametros,
    IReadOnlyDictionary<string, DateTime?> UltimaMarcacaoPorTerminal,
    IReadOnlyList<(string MovimentoId, string TerminalId, DateTime DataMarcacao)> AbertasAntigas,
    IReadOnlyList<(string Placa, string TerminalId, TimeOnly Hora)> AgendamentosAtrasadosHoje,
    DateTime AgoraLocalPorto,
    IReadOnlyDictionary<string, int?>? CapacidadeHoraria = null);

/// <summary>
/// Regras transparentes de alerta. Cada uma diz o que mediu, o limite e de
/// onde vieram os dados. Nenhuma inventa evento: sem dado, sem alerta.
/// </summary>
public static class RegrasAlerta
{

    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static string Duracao(double horas)
    {
        var totalMin = (int)Math.Round(horas * 60, MidpointRounding.AwayFromZero);
        return $"{totalMin / 60}h{totalMin % 60:D2}";
    }

    private static string Mes(DateTime d) => d.ToString("MMMM 'de' yyyy", Br);
    private static string Num(double v, int casas = 1) => v.ToString("N" + casas, Br);

    public static List<AlertaCandidato> Avaliar(EntradaAlertas e)
    {
        var alertas = new List<AlertaCandidato>();
        var porTerminal = e.Liberadas.GroupBy(r => r.TerminalId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (id, nome) in e.Terminais)
        {
            if (porTerminal.TryGetValue(id, out var registros) && UltimoMesCompleto(registros) is { } mes)
            {
                var doMes = registros.Where(r => r.MarcadoEm >= mes && r.MarcadoEm < mes.AddMonths(1)).ToList();
                var anterior = registros.Where(r => r.MarcadoEm >= mes.AddMonths(-1) && r.MarcadoEm < mes).ToList();
                alertas.AddRange(EsperaMediana(e.Parametros, id, nome, mes, doMes, anterior));
                alertas.AddRange(EsperasLongas(e.Parametros, id, nome, mes, doMes));
            }
            alertas.AddRange(DadosDesatualizados(e, id, nome));
        }
        alertas.AddRange(RiscoHorario(e));
        alertas.AddRange(MovimentosAbertos(e));
        alertas.AddRange(AgendamentosAtrasados(e));
        return alertas;
    }

    /// <summary>Último mês em que o terminal tem dados do primeiro ao último dia.</summary>
    public static DateTime? UltimoMesCompleto(IReadOnlyCollection<RegistroMarcacao> registros)
    {
        if (registros.Count == 0) return null;
        var inicio = registros.Min(r => r.MarcadoEm).Date;
        var fim = registros.Max(r => r.MarcadoEm).Date;
        var mes = new DateTime(fim.Year, fim.Month, 1);
        if (fim < mes.AddMonths(1).AddDays(-1)) mes = mes.AddMonths(-1); // mês do fim incompleto
        return mes >= inicio ? mes : null;
    }

    private static IEnumerable<AlertaCandidato> EsperaMediana(ParametrosOperacionais p, string id, string nome, DateTime mes, List<RegistroMarcacao> doMes, List<RegistroMarcacao> anterior)
    {
        var esperas = doMes.Select(r => r.EsperaHoras).OrderBy(x => x).ToList();
        if (Indicadores.Percentil(esperas, 0.5) is not { } mediana || mediana <= p.LimiteEsperaMedianaHoras) yield break;
        var p90 = Indicadores.Percentil(esperas, 0.9)!.Value;
        var medAnterior = Indicadores.Percentil(anterior.Select(r => r.EsperaHoras).OrderBy(x => x).ToList(), 0.5);
        var comparacao = medAnterior is { } ma ? $" contra {Duracao(ma)} em {Mes(mes.AddMonths(-1))}" : "";
        var subiuMuito = medAnterior is { } m0 && m0 > 0 && mediana >= m0 * 1.5;
        yield return new AlertaCandidato(
            $"espera-mediana:{id}:{mes:yyyy-MM}", TipoAlerta.Historico, "espera-mediana",
            mediana >= p.LimiteEsperaMedianaHoras * 1.5 || subiuMuito ? PrioridadeAlerta.Alta : PrioridadeAlerta.Media,
            id, Mes(mes), "Espera mediana acima do limite",
            "Espera mediana (marcação → liberação)", Duracao(mediana), Duracao(p.LimiteEsperaMedianaHoras),
            $"{Duracao(mediana)} em {Mes(mes)}{comparacao} · P90 {Duracao(p90)} · {doMes.Count.ToString("N0", Br)} movimentos liberados.",
            $"Movimentos de {nome} importados/integrados",
            "Comparar a programação de chamadas do mês com a do mês anterior e registrar a causa (navio, chuva, restrição na moega).");
    }

    private static IEnumerable<AlertaCandidato> EsperasLongas(ParametrosOperacionais p, string id, string nome, DateTime mes, List<RegistroMarcacao> doMes)
    {
        var longas = doMes.Where(r => r.EsperaHoras > p.LimiteEsperaLongaHoras).ToList();
        if (longas.Count == 0) yield break;
        // O maior grupo liberado junto costuma explicar o caso (ex.: um lote retido).
        var grupo = longas.GroupBy(r => (r.Convenio, Dia: r.LiberadoEm.Date)).OrderByDescending(g => g.Count()).First().ToList();
        var maior = longas.Max(r => r.EsperaHoras);
        var detalhe = grupo.Count > 1
            ? $" Maior grupo: {grupo.Count} carretas de {grupo[0].Convenio}, marcadas a partir de {grupo.Min(r => r.MarcadoEm):dd/MM HH:mm} e liberadas em {grupo.Max(r => r.LiberadoEm):dd/MM HH:mm}."
            : "";
        yield return new AlertaCandidato(
            $"esperas-longas:{id}:{mes:yyyy-MM}", TipoAlerta.Historico, "esperas-longas",
            longas.Count >= 10 ? PrioridadeAlerta.Alta : PrioridadeAlerta.Media,
            id, Mes(mes), $"{longas.Count} carreta(s) com espera acima de {Duracao(p.LimiteEsperaLongaHoras)}",
            "Carretas com espera acima do limite de espera longa", longas.Count.ToString(Br), $"nenhuma acima de {Duracao(p.LimiteEsperaLongaHoras)}",
            $"Maior espera: {Duracao(maior)}.{detalhe}",
            $"Movimentos de {nome} importados/integrados",
            "Registrar o motivo da retenção (falta de navio, chuva, manutenção) para separar causa operacional de erro de registro.");
    }

    private static IEnumerable<AlertaCandidato> DadosDesatualizados(EntradaAlertas e, string id, string nome)
    {
        var ultima = e.UltimaMarcacaoPorTerminal.GetValueOrDefault(id);
        var hoje = DateOnly.FromDateTime(e.AgoraLocalPorto);
        var dias = ultima is { } u ? hoje.DayNumber - DateOnly.FromDateTime(u).DayNumber : (int?)null;
        if (dias is not null && dias <= e.Parametros.DiasSemDadosAlerta) yield break;
        var texto = ultima is { } um
            ? $"Última marcação recebida em {um:dd/MM/yyyy HH:mm} ({dias} dias). Indicadores depois dessa data não incluem {nome}."
            : $"Nenhuma marcação de {nome} na base.";
        yield return new AlertaCandidato(
            $"dados-desatualizados:{id}:{ultima:yyyyMMdd}", TipoAlerta.Dados, "dados-desatualizados",
            dias is null or > 30 ? PrioridadeAlerta.Alta : PrioridadeAlerta.Media,
            id, ultima is { } x ? $"desde {x:dd/MM/yyyy}" : "sem dados", "Sem registros novos",
            "Dias desde a última marcação", dias?.ToString(Br) ?? "—", $"{e.Parametros.DiasSemDadosAlerta} dias",
            texto, "Controle de importações e integração",
            $"Solicitar a {nome} as planilhas do período e importá-las (ou verificar a integração).");
    }

    /// <summary>
    /// Previsão de fila nas próximas horas de hoje, por terminal (EstimativaJanelas):
    /// só janelas em "risco de fila" e com dados suficientes. Estimativa, não ocorrência.
    /// </summary>
    private static IEnumerable<AlertaCandidato> RiscoHorario(EntradaAlertas e)
    {
        if (e.Liberadas.Count == 0) yield break;
        var terminais = e.Terminais.Select(t => new TerminalEstimativa(t.Id, t.Nome,
            e.CapacidadeHoraria?.GetValueOrDefault(t.Id), e.UltimaMarcacaoPorTerminal.GetValueOrDefault(t.Id))).ToList();
        var (_, porTerminal) = EstimativaJanelas.Calcular(e.Liberadas, terminais, e.AgoraLocalPorto,
            e.Parametros.LimiteRazaoChegadaLiberacao, e.Parametros.DiasSemDadosAlerta);
        var hoje = e.AgoraLocalPorto.Date;
        foreach (var t in porTerminal)
        {
            for (var i = 0; i < t.Janelas.Count; i++)
            {
                if (e.AgoraLocalPorto.AddHours(i).Date != hoje) break; // só o dia corrente
                var j = t.Janelas[i];
                if (j.Situacao != SituacaoJanela.Risco || j.Confiabilidade == Confiabilidade.Insuficiente) continue;
                var faixa = EstimativaJanelas.Faixa(j.Hora);
                var limitada = j.Confiabilidade == Confiabilidade.Limitada;
                var prioridade = !limitada && j.DemandaPrevista >= 2 * j.CapacidadeAtendimento ? PrioridadeAlerta.Alta : PrioridadeAlerta.Media;
                var referencia = j.FonteCapacidade == "cadastrada" ? "capacidade cadastrada" : "atendimento habitual (média de liberações nesta hora)";
                yield return new AlertaCandidato(
                    $"risco-horario:{t.TerminalId}:{hoje:yyyy-MM-dd}:{j.Hora:D2}", TipoAlerta.Previsao, "risco-horario", prioridade,
                    t.TerminalId, $"hoje, {faixa}", $"Risco de fila às {j.Hora}h",
                    "Demanda prevista × capacidade de atendimento",
                    $"{Num(j.DemandaPrevista)} carretas previstas",
                    $"{Num(j.CapacidadeAtendimento)} de {referencia}; risco a partir de {Num(e.Parametros.LimiteRazaoChegadaLiberacao, 1)}×",
                    $"De {t.BaseDe:dd/MM} a {t.BaseAte:dd/MM}, às {j.Hora}h chegaram em média {Num(j.DemandaPrevista)} carretas por dia e foram liberadas {Num(j.AtendimentoObservado)}. "
                    + "Estimativa pelo padrão histórico, não é ocorrência."
                    + (limitada ? " Histórico limitado — estimativa com menor confiabilidade." : ""),
                    "Padrão histórico do terminal",
                    "Avaliar antecipar chamadas do pátio na hora anterior ou redistribuir agendamentos. Confirme com a fila real antes de agir.");
            }
        }
    }

    private static IEnumerable<AlertaCandidato> MovimentosAbertos(EntradaAlertas e) =>
        e.AbertasAntigas.GroupBy(a => a.TerminalId).Select(g =>
        {
            var nome = e.Terminais.FirstOrDefault(t => t.Id == g.Key).Nome ?? g.Key;
            var maisAntiga = g.Min(a => a.DataMarcacao);
            var horas = (e.AgoraLocalPorto - maisAntiga).TotalHours;
            return new AlertaCandidato(
                $"movimento-aberto:{g.Key}", TipoAlerta.Operacional, "movimento-aberto", g.Count() >= 5 ? PrioridadeAlerta.Alta : PrioridadeAlerta.Media,
                g.Key, "agora", $"{g.Count()} carreta(s) marcada(s) sem liberação há mais de {Duracao(e.Parametros.LimiteEsperaLongaHoras)}",
                "Movimentos abertos além do limite de espera longa", g.Count().ToString(Br), "0",
                $"A mais antiga ({g.OrderBy(a => a.DataMarcacao).First().MovimentoId}) foi marcada em {maisAntiga:dd/MM HH:mm} — {Duracao(horas)} atrás.",
                $"Marcações de {nome} sem liberação registrada",
                "Confirmar com o terminal se a carreta ainda está no pátio ou se a liberação não foi registrada.");
        });

    private static IEnumerable<AlertaCandidato> AgendamentosAtrasados(EntradaAlertas e)
    {
        if (e.AgendamentosAtrasadosHoje.Count == 0) yield break;
        var hoje = DateOnly.FromDateTime(e.AgoraLocalPorto);
        var lista = string.Join(", ", e.AgendamentosAtrasadosHoje.OrderBy(a => a.Hora).Select(a => $"{a.Placa} ({a.Hora:HH\\:mm})"));
        yield return new AlertaCandidato(
            $"agendamento-atrasado:{hoje:yyyy-MM-dd}", TipoAlerta.Operacional, "agendamento-atrasado", PrioridadeAlerta.Media,
            null, "hoje", $"{e.AgendamentosAtrasadosHoje.Count} agendamento(s) atrasado(s)",
            "Agendamentos com situação \"Atrasado\"", e.AgendamentosAtrasadosHoje.Count.ToString(Br), "0",
            $"Carretas: {lista}.", "Situação registrada manualmente na Agenda",
            "Contatar a transportadora; sem entrada até 2 h após a janela, registrar como não comparecimento.");
    }
}
