using System.Net;
using System.Net.Http.Json;
using BarcaLog.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BarcaLog.IntegrationTests;

[Collection(ColecaoApi.Nome)]
public class SegurancaEIntegracaoTests(ApiFixture api)
{
    [SkippableFact]
    public async Task Sem_token_retorna_401_e_auditor_nao_escreve()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var anonimo = api.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/transportadoras")).StatusCode);

        var auditor = await api.ClienteAsync("Auditor");
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/api/auditoria")).StatusCode);
        var post = await auditor.PostAsJsonAsync("/api/transportadoras", new { nome = "Não Pode", cnpj = "00.000.000/0001-00" });
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
    }

    [SkippableFact]
    public async Task Login_com_senha_errada_retorna_401()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var r = await api.Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "gestor@testes.local", senha = "errada" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [SkippableFact]
    public async Task Integracao_por_api_key_grava_marcacao_e_banco_calcula_espera()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var sistema = api.Factory.CreateClient();
        var id = $"T{Random.Shared.NextInt64(10_000_000, 99_999_999)}";
        object[] eventos =
        [
            new { tipo = "Marcacao", movimentoId = id, terminalId = "hidrovias", senha = "000001", convenio = "HIDROVIAS - SOJA", operador = "HIDROVIAS", carga = "Soja", ciclo = 1, dataMarcacao = "2026-08-01T06:00:00" },
            new { tipo = "Liberacao", movimentoId = id, dataLiberacao = "2026-08-02T08:30:00" }
        ];

        var semChave = await sistema.PostAsJsonAsync("/api/integracao/eventos", eventos);
        Assert.Equal(HttpStatusCode.Unauthorized, semChave.StatusCode);

        sistema.DefaultRequestHeaders.Add("X-Api-Key", ApiFixture.ApiKey);
        var resposta = await sistema.PostAsJsonAsync("/api/integracao/eventos", eventos);
        resposta.EnsureSuccessStatusCode();
        var resultado = await resposta.Content.ReadFromJsonAsync<ResultadoIntegracaoDto>(Json.Opcoes);
        Assert.Equal(1, resultado!.Criados);
        Assert.Empty(resultado.Erros);

        // Coluna computada no SQL Server: 26,5h → janela D1.
        var espera = await api.NoBancoAsync(db => db.Marcacoes.Where(m => m.MovimentoId == id).Select(m => m.EsperaHoras).SingleAsync());
        Assert.Equal(26.5, espera);
        var usuario = await api.ClienteAsync("Operador");
        var dto = await usuario.GetFromJsonAsync<MarcacaoDto>($"/api/marcacoes/{id}", Json.Opcoes);
        Assert.Equal("D1", dto!.Janela);

        // API Key não serve pros endpoints de usuário.
        Assert.Equal(HttpStatusCode.Unauthorized, (await sistema.GetAsync("/api/transportadoras")).StatusCode);

        var logs = await usuario.GetFromJsonAsync<Pagina<LogAuditoriaDto>>("/api/auditoria?autor=Integração", Json.Opcoes);
        Assert.Contains(logs!.Itens, l => l.Autor == "Integração: Testes");
    }
}
