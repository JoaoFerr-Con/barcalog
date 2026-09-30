using System.Net;
using System.Net.Http.Json;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Enums;

namespace BarcaLog.IntegrationTests;

/// <summary>
/// Portal da Transportadora: tentativas de uma transportadora acessar dados de
/// outra (BOLA/IDOR) e de escapar do Portal para a área interna.
/// </summary>
[Collection(ColecaoApi.Nome)]
public class PortalTests(ApiFixture api)
{
    private Task<HttpClient> Norte() => ApiFixture.ClienteAsync(api.Factory, "norte@portal.local", ApiFixture.SenhaUsuarios);
    private Task<HttpClient> Agro() => ApiFixture.ClienteAsync(api.Factory, "agro@portal.local", ApiFixture.SenhaUsuarios);

    [SkippableFact]
    public async Task Portal_ve_apenas_a_propria_transportadora_mesmo_forcando_filtro()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var norte = await Norte();
        var resumo = await norte.GetFromJsonAsync<TransportadoraDto>("/api/v1/portal/resumo", Json.Opcoes);
        Assert.Equal("Norte Grãos Logística", resumo!.Nome);

        var agroId = (await (await api.ClienteAsync("Auditor")).GetFromJsonAsync<List<TransportadoraDto>>("/api/v1/transportadoras", Json.Opcoes))!
            .Single(t => t.Nome == "AgroTransportes Sul").Id;

        // Tenta pedir os dados da AgroTransportes pelo filtro: o servidor ignora e usa a do token.
        var veiculos = await norte.GetFromJsonAsync<Pagina<VeiculoDto>>($"/api/v1/portal/veiculos?transportadoraId={agroId}", Json.Opcoes);
        Assert.NotEmpty(veiculos!.Itens);
        Assert.All(veiculos.Itens, v => Assert.Equal(resumo.Id, v.TransportadoraId));
        var ocorrencias = await norte.GetFromJsonAsync<Pagina<OcorrenciaDto>>($"/api/v1/portal/ocorrencias?transportadoraId={agroId}", Json.Opcoes);
        Assert.All(ocorrencias!.Itens, o => Assert.Equal(resumo.Id, o.TransportadoraId));
        var condutores = await norte.GetFromJsonAsync<Pagina<CondutorDto>>($"/api/v1/portal/condutores?transportadoraId={agroId}", Json.Opcoes);
        Assert.All(condutores!.Itens, c => Assert.Equal(resumo.Id, c.TransportadoraId));
    }

    [SkippableFact]
    public async Task Transportadora_nao_contesta_ocorrencia_de_outra()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        // Ocorrência N2 do seed é da AgroTransportes (ATS-4410).
        var interno = await api.ClienteAsync("Auditor");
        var daAgro = (await interno.GetFromJsonAsync<Pagina<OcorrenciaDto>>("/api/v1/ocorrencias?placa=ATS-4410", Json.Opcoes))!.Itens.First();

        var norte = await Norte();
        var r = await norte.PostAsJsonAsync("/api/v1/portal/contestacoes", new { ocorrenciaId = daAgro.Id, justificativa = "Tentando contestar ocorrência alheia." });

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); // nem revela que existe
        var daOcorrencia = await interno.GetFromJsonAsync<Pagina<ContestacaoDto>>($"/api/v1/contestacoes?ocorrenciaId={daAgro.Id}", Json.Opcoes);
        Assert.DoesNotContain(daOcorrencia!.Itens, c => c.Justificativa.Contains("ocorrência alheia"));
    }

    [SkippableFact]
    public async Task Transportadora_contesta_a_propria_ocorrencia_e_ve_so_os_proprios_chamados()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var agro = await Agro();
        var minha = (await agro.GetFromJsonAsync<Pagina<OcorrenciaDto>>("/api/v1/portal/ocorrencias?status=Ativa", Json.Opcoes))!.Itens
            .FirstOrDefault(o => o.Placa == "ATS-4410");
        if (minha is null) return; // outro teste do mesmo banco já contestou/resolveu

        var r = await agro.PostAsJsonAsync("/api/v1/portal/contestacoes", new { ocorrenciaId = minha.Id, justificativa = "Atraso causado por bloqueio na rodovia PA-483." });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var chamados = await agro.GetFromJsonAsync<Pagina<ContestacaoDto>>("/api/v1/portal/contestacoes", Json.Opcoes);
        Assert.Contains(chamados!.Itens, c => c.OcorrenciaId == minha.Id && c.Status == StatusContestacao.Pendente);
        Assert.All(chamados.Itens, c => Assert.Equal("AgroTransportes Sul", c.Transportadora));

        var logs = await (await api.ClienteAsync("Auditor")).GetFromJsonAsync<Pagina<LogAuditoriaDto>>("/api/v1/auditoria?autor=agro@portal.local", Json.Opcoes);
        Assert.Contains(logs!.Itens, l => l.Acao == "Contestação aberta (GED)");
    }

    [SkippableTheory]
    [InlineData("GET", "/api/v1/transportadoras")]
    [InlineData("GET", "/api/v1/veiculos")]
    [InlineData("GET", "/api/v1/ocorrencias")]
    [InlineData("GET", "/api/v1/auditoria")]
    [InlineData("GET", "/api/v1/marcacoes/kpis")]
    [InlineData("GET", "/api/v1/marcacoes/exportar")]
    [InlineData("GET", "/api/v1/usuarios")]
    [InlineData("GET", "/api/v1/condutores/1/dados-pessoais")]
    [InlineData("POST", "/api/v1/contestacoes/1/aprovar")]
    public async Task Usuario_do_portal_nao_acessa_a_area_interna(string metodo, string rota)
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var norte = await Norte();
        var r = await norte.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), rota) { Content = JsonContent.Create(new { }) });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [SkippableFact]
    public async Task Usuario_interno_nao_usa_endpoints_do_portal()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        Assert.Equal(HttpStatusCode.Forbidden, (await gestor.GetAsync("/api/v1/portal/resumo")).StatusCode);
    }

    [SkippableFact]
    public async Task Usuario_do_portal_exige_transportadora_e_nao_vira_interno()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        var semTransportadora = await gestor.PostAsJsonAsync("/api/v1/usuarios",
            new { nome = "Portal Sem Empresa", email = $"p{Dados.Proximo()}@portal.local", senha = "Senha-Forte-Portal#1", papel = "Transportadora" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, semTransportadora.StatusCode);

        var interno = await gestor.PostAsJsonAsync("/api/v1/usuarios",
            new { nome = "Operador Com Empresa", email = $"o{Dados.Proximo()}@testes.local", senha = "Senha-Forte-Portal#1", papel = "Operador", transportadoraId = 1 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, interno.StatusCode);

        var norteId = (await (await Norte()).GetFromJsonAsync<UsuarioDto>("/api/v1/auth/me", Json.Opcoes))!.Id;
        var promover = await gestor.PutAsJsonAsync($"/api/v1/usuarios/{norteId}/papel", new { papel = "Gestor" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, promover.StatusCode);
    }

    [SkippableFact]
    public async Task Exportacao_CSV_neutraliza_formula_e_tem_cabecalho()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var auditor = await api.ClienteAsync("Auditor");
        var r = await auditor.GetAsync("/api/v1/marcacoes/exportar?terminalId=tgpm");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("text/csv", r.Content.Headers.ContentType!.MediaType);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.StartsWith("Movimento;Senha;", texto.TrimStart('﻿'));
        Assert.Equal("\"'=HYPERLINK(1)\"", BarcaLog.Api.Infra.Csv.Celula("=HYPERLINK(1)"));
        Assert.Equal("\"a\"\"b\"", BarcaLog.Api.Infra.Csv.Celula("a\"b"));
    }
}
