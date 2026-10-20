using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using BarcaLog.Application.Dtos;

namespace BarcaLog.IntegrationTests;

/// <summary>
/// Indicadores da Visão Geral calculados pela API sobre movimentos importados
/// num período isolado (2001), conferidos contra valores calculados à mão.
/// </summary>
[Collection(ColecaoApi.Nome)]
public class IndicadoresTests(ApiFixture api)
{
    private static async Task ImportarAsync(HttpClient gestor, string terminal, IEnumerable<string> linhas)
    {
        var csv = "COD_MOVIMENTO;SENHA;CONVENIO;COD_CONVENIO;CICLO;DATA_MARCACAO;HORA_MARCACAO;DATA_LIBERACAO;HORA_LIBERACAO\r\n" + string.Join("\r\n", linhas);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(terminal), "terminalId");
        var arquivo = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(arquivo, "arquivo", $"indicadores_{Guid.NewGuid():N}.csv");
        var r = await gestor.PostAsync("/api/v1/importacoes", form);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var lote = await r.Content.ReadFromJsonAsync<LoteImportacaoDto>(Json.Opcoes);
        Assert.True((await gestor.PostAsync($"/api/v1/importacoes/{lote!.Id}/confirmar", null)).IsSuccessStatusCode);
    }

    [SkippableFact]
    public async Task Resumo_terminais_mensal_e_fluxo_batem_com_o_calculo_manual()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        string Id() => $"I{Dados.Proximo()}";
        // Março/2001 no TGPM: esperas de 2 h, 10 h, 30 h e 80 h + 1 ainda aberta. Fevereiro: uma de 4 h.
        await ImportarAsync(gestor, "tgpm", [
            $"{Id()};1;TGPM - SOJA;;41;20010301;06:00:00;20010301;08:00:00",
            $"{Id()};2;TGPM - SOJA;;41;20010301;16:00:00;20010302;02:00:00",
            $"{Id()};3;TGPM - SOJA;;41;20010302;16:30:00;20010303;22:30:00",
            $"{Id()};4;TGPM - SOJA;;41;20010303;16:00:00;20010307;00:00:00",
            $"{Id()};5;TGPM - SOJA;;41;20010305;10:00:00;;",
            $"{Id()};6;TGPM - SOJA;;41;20010210;09:00:00;20010210;13:00:00",
        ]);

        var q = "terminalId=tgpm&de=2001-03-01&ate=2001-03-31&compararDe=2001-02-01&compararAte=2001-02-28";
        var resumo = await gestor.GetFromJsonAsync<ResumoIndicadoresDto>($"/api/v1/indicadores/resumo?{q}", Json.Opcoes);
        var a = resumo!.Atual;
        Assert.Equal(31, resumo.Periodo!.Dias);
        Assert.Equal(5, a.Movimentacoes);
        Assert.Equal(1, a.SemLiberacao);
        Assert.Equal(5.0 / 31, a.MediaDiaria!.Value, 6);
        Assert.Equal(20, a.EsperaMediana);
        Assert.Equal(65, a.EsperaP90!.Value, 6);
        Assert.Equal(50, a.LiberadasAte24hPct);
        Assert.Equal(1, a.EsperasLongas);
        Assert.Equal(1, resumo.Anterior!.Movimentacoes);
        Assert.Equal(4, resumo.Anterior.EsperaMediana);

        // Sem capacidade configurada não há ocupação; com capacidade, há.
        var semCap = (await gestor.GetFromJsonAsync<List<IndicadorTerminalDto>>($"/api/v1/indicadores/terminais?{q}", Json.Opcoes))!.Single();
        Assert.Equal(2, semCap.PicoDiario);
        Assert.Equal(new DateOnly(2001, 3, 1), semCap.PicoData);
        Assert.Null(semCap.OcupacaoMediaPct);

        var operador = await api.ClienteAsync("Operador");
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PutAsJsonAsync("/api/v1/configuracoes/terminais/tgpm", new { capacidadeDiariaCarretas = 10 })).StatusCode);
        var put = await gestor.PutAsJsonAsync("/api/v1/configuracoes/terminais/tgpm", new { capacidadeDiariaCarretas = 10, capacidadeHoraria = 2 });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        try
        {
            var comCap = (await gestor.GetFromJsonAsync<List<IndicadorTerminalDto>>($"/api/v1/indicadores/terminais?{q}", Json.Opcoes))!.Single();
            Assert.Equal(10, comCap.CapacidadeDiaria);
            Assert.Equal(100 * (5.0 / 31) / 10, comCap.OcupacaoMediaPct!.Value, 6);
        }
        finally
        {
            await gestor.PutAsJsonAsync("/api/v1/configuracoes/terminais/tgpm", new { capacidadeDiariaCarretas = (int?)null, capacidadeHoraria = (int?)null });
        }

        var mensal = await gestor.GetFromJsonAsync<List<VolumeMensalDto>>("/api/v1/indicadores/volume-mensal?terminalId=tgpm&de=2001-02-01&ate=2001-03-31", Json.Opcoes) ?? [];
        Assert.Equal(["2001-02", "2001-03"], mensal.Select(m => m.Mes));
        Assert.Equal(4, mensal[1].Movimentacoes); // só liberadas entram na espera mensal
        Assert.Equal(20, mensal[1].EsperaMediana);

        var fluxo = await gestor.GetFromJsonAsync<List<FluxoHorarioDto>>($"/api/v1/indicadores/fluxo-horario?terminalId=tgpm&de=2001-03-01&ate=2001-03-31", Json.Opcoes);
        Assert.Equal(24, fluxo!.Count);
        Assert.Equal(Math.Round(3.0 / 31, 2), fluxo[16].ChegadasPorDia); // 16:00 (dia 1), 16:30 (dia 2), 16:00 (dia 3)
        Assert.Equal(Math.Round(1.0 / 31, 2), fluxo[8].LiberacoesPorDia);

        var config = await gestor.GetFromJsonAsync<ConfiguracaoOperacionalDto>("/api/v1/configuracoes", Json.Opcoes);
        Assert.Equal(72, config!.Parametros.LimiteEsperaLongaHoras);
        Assert.All(config.Terminais, t => Assert.Null(t.CapacidadeDiariaCarretas));

        var frescor = await gestor.GetFromJsonAsync<List<FrescorTerminalDto>>("/api/v1/indicadores/frescor", Json.Opcoes);
        Assert.Equal(3, frescor!.Count);
    }

    [SkippableFact]
    public async Task Parametros_sao_validados_e_auditados()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        var invalido = await gestor.PutAsJsonAsync("/api/v1/configuracoes/parametros",
            new { limiteEsperaMedianaHoras = 0, metaLiberadas24hPct = 150, limiteEsperaLongaHoras = 72, diasSemDadosAlerta = 2, limiteRazaoChegadaLiberacao = 1.4 });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);

        var ok = await gestor.PutAsJsonAsync("/api/v1/configuracoes/parametros",
            new { limiteEsperaMedianaHoras = 12, metaLiberadas24hPct = 85, limiteEsperaLongaHoras = 72, diasSemDadosAlerta = 2, limiteRazaoChegadaLiberacao = 1.4 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var p = await ok.Content.ReadFromJsonAsync<ParametrosOperacionaisDto>(Json.Opcoes);
        Assert.Contains("gestor@testes.local", p!.AtualizadoPor);
        var logs = await gestor.GetFromJsonAsync<Pagina<LogAuditoriaDto>>("/api/v1/auditoria?acao=Parâmetros operacionais alterados", Json.Opcoes);
        Assert.NotEmpty(logs!.Itens);
    }
}
