using System.Net;
using System.Net.Http.Json;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Enums;

namespace BarcaLog.IntegrationTests;

[Collection(ColecaoApi.Nome)]
public class AlertasTests(ApiFixture api)
{
    [SkippableFact]
    public async Task Tratamento_do_alerta_fica_registrado_e_nao_e_desfeito_pela_reavaliacao()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var operador = await api.ClienteAsync("Operador");
        var auditor = await api.ClienteAsync("Auditor");

        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PostAsync("/api/v1/alertas/avaliar", null)).StatusCode);
        var avaliacao = await operador.PostAsync("/api/v1/alertas/avaliar", null);
        Assert.Equal(HttpStatusCode.OK, avaliacao.StatusCode);

        // A base de testes não tem marcações recentes: algum terminal sempre aparece com dados desatualizados.
        var lista = await auditor.GetFromJsonAsync<Pagina<AlertaDto>>("/api/v1/alertas?tipo=Dados", Json.Opcoes);
        var alerta = Assert.IsType<AlertaDto>(lista!.Itens.FirstOrDefault(a => a.Estado == EstadoAlerta.Novo) ?? lista.Itens.First());
        Assert.Equal(TipoAlerta.Dados, alerta.Tipo);
        Assert.False(string.IsNullOrWhiteSpace(alerta.Evidencia));
        Assert.False(string.IsNullOrWhiteSpace(alerta.Origem));

        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PostAsJsonAsync($"/api/v1/alertas/{alerta.Id}/assumir", new { })).StatusCode);
        var assumido = await (await operador.PostAsJsonAsync($"/api/v1/alertas/{alerta.Id}/assumir", new { })).Content.ReadFromJsonAsync<AlertaDto>(Json.Opcoes);
        Assert.Equal(EstadoAlerta.EmAnalise, assumido!.Estado);
        Assert.Contains("operador@testes.local", assumido.Responsavel);

        var semTexto = await operador.PostAsJsonAsync($"/api/v1/alertas/{alerta.Id}/resolver", new { texto = "  " });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, semTexto.StatusCode);

        var resolvido = await (await operador.PostAsJsonAsync($"/api/v1/alertas/{alerta.Id}/resolver", new { texto = "Planilhas solicitadas ao terminal" }))
            .Content.ReadFromJsonAsync<AlertaDto>(Json.Opcoes);
        Assert.Equal(EstadoAlerta.Resolvido, resolvido!.Estado);
        Assert.Equal(["Gerado", "Assumido", "Resolvido"], resolvido.Eventos!.Select(e => e.Acao).Where(a => a is "Gerado" or "Assumido" or "Resolvido"));

        // Mesma condição na próxima avaliação: continua resolvido (decisão registrada vale para aquele período).
        await operador.PostAsync("/api/v1/alertas/avaliar", null);
        var depois = await auditor.GetFromJsonAsync<AlertaDto>($"/api/v1/alertas/{alerta.Id}", Json.Opcoes);
        Assert.Equal(EstadoAlerta.Resolvido, depois!.Estado);
        var abertos = await auditor.GetFromJsonAsync<Pagina<AlertaDto>>("/api/v1/alertas", Json.Opcoes);
        Assert.DoesNotContain(abertos!.Itens, a => a.Id == alerta.Id);

        var reaberto = await (await operador.PostAsJsonAsync($"/api/v1/alertas/{alerta.Id}/reabrir", new { })).Content.ReadFromJsonAsync<AlertaDto>(Json.Opcoes);
        Assert.Equal(EstadoAlerta.Novo, reaberto!.Estado);

        var descartarSemJustificativa = await operador.PostAsJsonAsync($"/api/v1/alertas/{alerta.Id}/descartar", new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, descartarSemJustificativa.StatusCode);

        var resumo = await auditor.GetFromJsonAsync<ResumoAlertasDto>("/api/v1/alertas/resumo", Json.Opcoes);
        Assert.True(resumo!.PorTipo[TipoAlerta.Dados] >= 1);

        var logs = await auditor.GetFromJsonAsync<Pagina<LogAuditoriaDto>>("/api/v1/auditoria?texto=Planilhas solicitadas ao terminal", Json.Opcoes);
        Assert.Contains(logs!.Itens, l => l.Acao == "Alerta resolvido");
    }
}
