using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;
using BarcaLog.Application.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarcaLog.Api.Controllers;

/// <summary>
/// Marcações reais (marcação → liberação) e todas as métricas derivadas —
/// porte de metricsEngine.js/relatorio.js. Filtros comuns dos agregados:
/// terminalId, de, ate (datas de marcação, horário do porto) e convenio.
/// </summary>
[ApiController]
[Route("api/marcacoes")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class MarcacoesController(MarcacaoServico marcacoes, MetricasServico metricas, ImportacaoMarcacoesServico importacao, IConfiguration configuracao, IWebHostEnvironment ambiente) : ControllerBase
{
    /// <summary>Lista paginada de marcações (filtros: terminal, período, convênio, operador, carga, senha).</summary>
    [HttpGet]
    public Task<Pagina<MarcacaoDto>> Listar([FromQuery] FiltroMarcacoes filtro, CancellationToken ct) => marcacoes.ListarAsync(filtro, ct);

    /// <summary>Um movimento pelo id.</summary>
    [HttpGet("{movimentoId}")]
    public Task<MarcacaoDto> Obter(string movimentoId, CancellationToken ct) => marcacoes.ObterAsync(movimentoId, ct);

    /// <summary>KPIs gerais: total, média diária, dias operados, mês mais movimentado, tempo médio de espera (simples e ponderado) e maior atraso.</summary>
    /// <response code="204">Nenhuma marcação no recorte.</response>
    [HttpGet("kpis")]
    [ProducesResponseType<KpisGerais>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Kpis([FromQuery] FiltroMetricas f, CancellationToken ct)
    {
        var kpis = Relatorio.ObterKpisGerais((await metricas.CarregarAsync(f, ct)).Registros);
        return kpis is null ? NoContent() : Ok(kpis);
    }

    /// <summary>Cards "Visão por Terminal": volume, espera média, % de ocupação vs. capacidade nominal e semáforo (verde/amarelo/vermelho).</summary>
    [HttpGet("visao-terminal")]
    public async Task<List<VisaoTerminal>> VisaoTerminal([FromQuery] FiltroMetricas f, CancellationToken ct)
    {
        var c = await metricas.CarregarAsync(f, ct);
        return MotorMetricas.VisaoPorTerminal(c.Registros, c.CapacidadePorId);
    }

    /// <summary>Volume por mês com representatividade, dias operados e média diária.</summary>
    [HttpGet("por-mes")]
    public async Task<List<MesDetalhado>> PorMes([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        Relatorio.AgruparPorMesDetalhado((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Ranking das maiores esperas (padrão: top 10, máx. 500).</summary>
    [HttpGet("ranking-esperas")]
    public async Task<List<RegistroMarcacao>> RankingEsperas([FromQuery] FiltroMetricas f, [FromQuery] int limite = 10, CancellationToken ct = default) =>
        Relatorio.RankingMaioresEsperas((await metricas.CarregarAsync(f, ct)).Registros, Math.Clamp(limite, 1, 500));

    /// <summary>Índice de risco por hora do dia: ρ = λ/μ sobre todo o histórico do recorte. ρ ≥ 1.4 crítico, ≥ 1.2 alto, ≥ 1.0 atenção, abaixo normal.</summary>
    [HttpGet("indice-risco-hora")]
    public async Task<List<RiscoHora>> IndiceRiscoHora([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        MotorMetricas.IndiceRiscoGargalo((await metricas.CarregarAsync(f, ct)).Registros);

    // ---------- Demais métricas do motor (metricsEngine.js / relatorio.js) ----------

    /// <summary>Distribuição nas janelas de permanência D0, D1, D2, D3, Alerta e Estouro Crítico.</summary>
    [HttpGet("janelas-permanencia")]
    public async Task<List<JanelaDistribuicao>> Janelas([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        MotorMetricas.DistribuicaoJanelas((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Top 15 permanências acima do SLA de 144h.</summary>
    [HttpGet("estouro-critico")]
    public async Task<List<RegistroMarcacao>> EstouroCritico([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        MotorMetricas.OutliersEstouroCritico((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Ritmo operacional λ (chegadas/h) × μ (liberações/h) por hora do dia, com alertas de λ &gt; μ por 2h+ seguidas.</summary>
    [HttpGet("ritmo-operacional")]
    public async Task<RitmoOperacional> Ritmo([FromQuery] FiltroMetricas f, CancellationToken ct)
    {
        var ritmo = MotorMetricas.RitmoOperacional((await metricas.CarregarAsync(f, ct)).Registros);
        return new RitmoOperacional(ritmo, MotorMetricas.AlertasRitmo(ritmo));
    }

    /// <summary>Concentração de tráfego e % de retenção (&gt;24h) por turno do dia.</summary>
    [HttpGet("concentracao-turno")]
    public async Task<List<TurnoConcentracao>> ConcentracaoTurno([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        MotorMetricas.ConcentracaoPorTurno((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Top 6 combinações dia-da-semana × turno com mais retenção.</summary>
    [HttpGet("horarios-criticos")]
    public async Task<List<HorarioCritico>> HorariosCriticos([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        MotorMetricas.HorariosCriticos((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>TMA por terminal (geral, último mês e variação).</summary>
    [HttpGet("tma-terminal")]
    public async Task<List<TmaTerminal>> Tma([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        MotorMetricas.TmaPorTerminal((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Indicadores de performance por terminal com Δ mês a mês (TMA e taxa D0).</summary>
    [HttpGet("indicadores-performance")]
    public async Task<List<IndicadorPerformance>> Indicadores([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        MotorMetricas.IndicadoresPerformance((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Alertas operacionais (hora crítica, atraso crescente, ocupação crítica).</summary>
    [HttpGet("alertas-operacionais")]
    public async Task<List<AlertaOperacional>> AlertasOperacionais([FromQuery] FiltroMetricas f, CancellationToken ct)
    {
        var c = await metricas.CarregarAsync(f, ct);
        return MotorMetricas.AlertasOperacionais(c.Registros, c.CapacidadePorId);
    }

    /// <summary>Horário de pico de entrada (marcação) × saída (liberação). Aceita filtro mes=yyyy-MM.</summary>
    [HttpGet("picos-entrada-saida")]
    public async Task<PicosEntradaSaida> Picos([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        MotorMetricas.PicosEntradaSaida((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Projeção mensal por tendência recente com faixa de confiança (~80%).</summary>
    [HttpGet("analise-preditiva")]
    public async Task<AnalisePreditiva> Preditiva([FromQuery] FiltroMetricas f, [FromQuery] int meses = 3, CancellationToken ct = default) =>
        MotorMetricas.AnalisePreditivaMensal((await metricas.CarregarAsync(f, ct)).Registros, Math.Clamp(meses, 1, 12));

    /// <summary>Projeção de volume por regressão linear simples sobre todo o histórico mensal.</summary>
    [HttpGet("projecao-volume")]
    public async Task<IActionResult> ProjecaoVolume([FromQuery] FiltroMetricas f, [FromQuery] int meses = 2, CancellationToken ct = default)
    {
        var projecao = Relatorio.ProjetarVolume(Relatorio.AgruparPorMes((await metricas.CarregarAsync(f, ct)).Registros), Math.Clamp(meses, 1, 12));
        return projecao is null ? NoContent() : Ok(projecao);
    }

    /// <summary>Alerta de saturação com base na tendência dos últimos 7 dias do recorte.</summary>
    [HttpGet("alertas-saturacao")]
    public async Task<List<AlertaSaturacao>> Saturacao([FromQuery] FiltroMetricas f, CancellationToken ct)
    {
        var c = await metricas.CarregarAsync(f, ct);
        return MotorMetricas.AlertasSaturacao(c.Registros, f.TerminalId is { } t ? c.CapacidadePorId(t) : MotorMetricas.CapacidadeDiaria);
    }

    /// <summary>Volume por dia e % da capacidade nominal (dias mais cheios primeiro).</summary>
    [HttpGet("capacidade-diaria")]
    public async Task<List<CapacidadeDia>> CapacidadeDiaria([FromQuery] FiltroMetricas f, CancellationToken ct)
    {
        var c = await metricas.CarregarAsync(f, ct);
        return MotorMetricas.CapacidadeDiariaPorDia(c.Registros, f.TerminalId is { } t ? c.CapacidadePorId(t) : MotorMetricas.CapacidadeDiaria);
    }

    /// <summary>Top 30 dias × terminal com maior volume, marcando gargalo (&gt;80% da capacidade).</summary>
    [HttpGet("top-dias")]
    public async Task<List<DiaCompacto>> TopDias([FromQuery] FiltroMetricas f, CancellationToken ct)
    {
        var c = await metricas.CarregarAsync(f, ct);
        return MotorMetricas.Top30DiasCompacto(c.Registros, c.CapacidadePorNome);
    }

    /// <summary>Dias × terminal em que o volume passou da capacidade nominal.</summary>
    [HttpGet("dias-acima-capacidade")]
    public async Task<List<DiaEmpresaTotal>> DiasAcimaCapacidade([FromQuery] FiltroMetricas f, CancellationToken ct)
    {
        var c = await metricas.CarregarAsync(f, ct);
        return Relatorio.DiasQueExcederamCapacidade(c.Registros, c.CapacidadePorNome);
    }

    /// <summary>Matriz volume mês × terminal.</summary>
    [HttpGet("matriz-volume-mensal")]
    public async Task<MotorMetricas.MatrizVolumeMensal> Matriz([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        MotorMetricas.MatrizVolume((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Detalhamento diário (dia 1..31) de cada mês.</summary>
    [HttpGet("detalhamento-diario")]
    public async Task<List<DetalhamentoMes>> DetalhamentoDiario([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        Relatorio.DetalhamentoDiarioPorMes((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Totais por operador (Bunge e Amaggi consolidados).</summary>
    [HttpGet("por-operador")]
    public async Task<List<OperadorTotal>> PorOperador([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        Relatorio.TotaisPorOperador((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Totais por tipo de carga.</summary>
    [HttpGet("por-carga")]
    public async Task<List<CargaTotal>> PorCarga([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        Relatorio.TotaisPorCarga((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Totais e espera média por terminal.</summary>
    [HttpGet("por-terminal")]
    public async Task<List<EmpresaTotal>> PorTerminal([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        Relatorio.TotaisPorEmpresa((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Volume de marcações por hora do dia.</summary>
    [HttpGet("por-hora")]
    public async Task<List<HoraTotal>> PorHora([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        Relatorio.PorHoraDoDia((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Volume de marcações por dia da semana.</summary>
    [HttpGet("por-dia-semana")]
    public async Task<List<DiaSemanaTotal>> PorDiaSemana([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        Relatorio.PorDiaDaSemana((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Distribuição e espera média por ciclo (1, 2, 3, 4, 5+).</summary>
    [HttpGet("por-ciclo")]
    public async Task<List<CicloBalde>> PorCiclo([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        Relatorio.DistribuicaoPorCiclo((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Tendência de SLA: espera do último mês vs. média dos 3 anteriores.</summary>
    [HttpGet("tendencia-sla")]
    public async Task<IActionResult> TendenciaSla([FromQuery] FiltroMetricas f, CancellationToken ct)
    {
        var t = Relatorio.TendenciaSLA((await metricas.CarregarAsync(f, ct)).Registros);
        return t is null ? NoContent() : Ok(t);
    }

    /// <summary>Score de eficiência 0–100 por operador (volume 40%, espera 40%, consistência 20%).</summary>
    [HttpGet("score-operadores")]
    public async Task<List<ScoreOperador>> ScoreOperadores([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        Relatorio.ScoreEficienciaPorOperador((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Convênios com atrasos recorrentes (≥ limiteHoras, pelo menos minOcorrencias vezes).</summary>
    [HttpGet("atrasos-recorrentes")]
    public async Task<List<AtrasoRecorrente>> AtrasosRecorrentes([FromQuery] FiltroMetricas f, [FromQuery] double limiteHoras = 24, [FromQuery] int minOcorrencias = 3, CancellationToken ct = default) =>
        Relatorio.AtrasosRecorrentes((await metricas.CarregarAsync(f, ct)).Registros, limiteHoras, minOcorrencias);

    /// <summary>Recomendações prescritivas derivadas das janelas e do ritmo λ/μ.</summary>
    [HttpGet("recomendacoes")]
    public async Task<List<Recomendacao>> Recomendacoes([FromQuery] FiltroMetricas f, CancellationToken ct) =>
        MotorMetricas.RecomendacoesPrescritivas((await metricas.CarregarAsync(f, ct)).Registros);

    /// <summary>Business case: custo de demurrage, custo ampliado do gargalo, fatores de utilização, SLA por etapa, ROI e ESG.</summary>
    [HttpGet("business-case")]
    public async Task<object> BusinessCase([FromQuery] FiltroMetricas f, CancellationToken ct)
    {
        var r = (await metricas.CarregarAsync(f, ct)).Registros;
        return new
        {
            demurrage = MotorMetricas.CustoTotalDemurrage(r),
            custoAmpliado = MotorMetricas.CustoAmpliado(r),
            fatoresUtilizacao = MotorMetricas.FatoresUtilizacaoCriticos(r),
            slaPorEtapa = MotorMetricas.SlaPorEtapa(MotorMetricas.MediaPonderadaPermanencia(r)),
            roi = MotorMetricas.SimulacaoRoi(r),
            esg = MotorMetricas.ImpactoESG(r)
        };
    }

    // ---------- Importação dos datasets reais ----------

    /// <summary>Importa os JSON reais do diretório configurado (Seed:DiretorioDatasets). Idempotente. Somente Gestor.</summary>
    [HttpPost("importar")]
    [Authorize(Policy = Politicas.Gestao)]
    public Task<List<ResultadoImportacaoDto>> ImportarDiretorio(CancellationToken ct)
    {
        var dir = Path.GetFullPath(Path.Combine(ambiente.ContentRootPath, configuracao["Seed:DiretorioDatasets"] ?? "../../../src/data/datasets"));
        return importacao.ImportarDiretorioAsync(dir, ct);
    }

    /// <summary>Importa um arquivo JSON (mesmo formato de src/data/datasets/*.json) para o terminal informado. Somente Gestor.</summary>
    [HttpPost("importar/{terminalId}")]
    [Authorize(Policy = Politicas.Gestao)]
    [RequestSizeLimit(100_000_000)]
    public async Task<ResultadoImportacaoDto> ImportarArquivo(string terminalId, IFormFile arquivo, CancellationToken ct)
    {
        await using var stream = arquivo.OpenReadStream();
        return await importacao.ImportarAsync(terminalId, stream, arquivo.FileName, ct);
    }
}
