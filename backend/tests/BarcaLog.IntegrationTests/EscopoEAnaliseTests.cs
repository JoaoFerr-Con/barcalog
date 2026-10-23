using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;

namespace BarcaLog.IntegrationTests;

/// <summary>
/// Vínculo de usuário a terminal (escopo aplicado no servidor), preferências que não
/// ampliam acesso, e a Análise 360° de Movimentações com o detalhamento (drill-down).
/// Cada teste usa um ano isolado para não depender dos demais.
/// </summary>
[Collection(ColecaoApi.Nome)]
public class EscopoEAnaliseTests(ApiFixture api)
{
    private const string Senha = "Porto-Barcarena#2026x";

    private static async Task ImportarAsync(HttpClient gestor, string terminal, IEnumerable<string> linhas)
    {
        var csv = "COD_MOVIMENTO;SENHA;CONVENIO;COD_CONVENIO;CICLO;DATA_MARCACAO;HORA_MARCACAO;DATA_LIBERACAO;HORA_LIBERACAO\r\n" + string.Join("\r\n", linhas);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(terminal), "terminalId");
        var arquivo = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(arquivo, "arquivo", $"escopo_{Guid.NewGuid():N}.csv");
        var r = await gestor.PostAsync("/api/v1/importacoes", form);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var lote = await r.Content.ReadFromJsonAsync<LoteImportacaoDto>(Json.Opcoes);
        Assert.True((await gestor.PostAsync($"/api/v1/importacoes/{lote!.Id}/confirmar", null)).IsSuccessStatusCode);
    }

    private async Task<(int Id, string Email)> CriarUsuarioAsync(HttpClient gestor, string papel)
    {
        var email = $"escopo{Dados.Proximo()}@testes.local";
        var r = await gestor.PostAsJsonAsync("/api/v1/usuarios", new { nome = "Pessoa Vinculada", email, senha = "Provisoria-Inicial#99", papel });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var u = await r.Content.ReadFromJsonAsync<UsuarioDto>(Json.Opcoes);
        var cliente = await ApiFixture.ClienteAsync(api.Factory, email, "Provisoria-Inicial#99");
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsJsonAsync("/api/v1/auth/trocar-senha", new { senhaAtual = "Provisoria-Inicial#99", novaSenha = Senha })).StatusCode);
        return (u!.Id, email);
    }

    [SkippableFact]
    public async Task Usuario_vinculado_so_ve_o_proprio_terminal_e_preferencia_nao_amplia_acesso()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        string Id() => $"E{Dados.Proximo()}";
        var idHidrovias = Id();
        await ImportarAsync(gestor, "tgpm", [
            $"{Id()};1;TGPM - SOJA;;41;20020301;06:00:00;20020301;08:00:00",
            $"{Id()};2;TGPM - SOJA;;41;20020302;06:00:00;20020302;10:00:00",
        ]);
        await ImportarAsync(gestor, "hidrovias", [
            $"{idHidrovias};3;HIDROVIAS - MILHO;;41;20020301;07:00:00;20020301;09:00:00",
        ]);

        var (id, email) = await CriarUsuarioAsync(gestor, "Operador");
        var antes = await ApiFixture.ClienteAsync(api.Factory, email, Senha);

        var vinculo = await gestor.PutAsJsonAsync($"/api/v1/usuarios/{id}/terminal", new { terminalId = "tgpm" });
        Assert.Equal(HttpStatusCode.OK, vinculo.StatusCode);
        Assert.Equal("tgpm", (await vinculo.Content.ReadFromJsonAsync<UsuarioDto>(Json.Opcoes))!.TerminalId);
        // O vínculo vai no token: o token anterior deixa de valer.
        Assert.Equal(HttpStatusCode.Unauthorized, (await antes.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await gestor.PutAsJsonAsync($"/api/v1/usuarios/{id}/terminal", new { terminalId = "nao-existe" })).StatusCode);

        var op = await ApiFixture.ClienteAsync(api.Factory, email, Senha);
        const string periodo = "de=2002-03-01&ate=2002-03-31";

        // Sem terminal no pedido = terminal vinculado; pedir outro = 403.
        var lista = await op.GetFromJsonAsync<Pagina<MarcacaoDto>>($"/api/v1/marcacoes?{periodo}", Json.Opcoes);
        Assert.Equal(2, lista!.Total);
        Assert.All(lista.Itens, m => Assert.Equal("tgpm", m.TerminalId));
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync($"/api/v1/marcacoes?{periodo}&terminalId=hidrovias")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync($"/api/v1/indicadores/resumo?{periodo}&terminalId=hidrovias")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync($"/api/v1/alertas/proximas-horas?terminalId=hidrovias")).StatusCode);
        // Registro de outro terminal: "não encontrado" (não revela que existe).
        Assert.Equal(HttpStatusCode.NotFound, (await op.GetAsync($"/api/v1/marcacoes/{idHidrovias}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await gestor.GetAsync($"/api/v1/marcacoes/{idHidrovias}")).StatusCode);

        var resumo = await op.GetFromJsonAsync<ResumoIndicadoresDto>($"/api/v1/indicadores/resumo?{periodo}", Json.Opcoes);
        Assert.Equal(2, resumo!.Atual.Movimentacoes);
        var frescor = await op.GetFromJsonAsync<List<FrescorTerminalDto>>("/api/v1/indicadores/frescor", Json.Opcoes);
        Assert.Equal("tgpm", Assert.Single(frescor!).TerminalId);
        var analise = await op.GetFromJsonAsync<ResultadoAnalise>($"/api/v1/marcacoes/analise?{periodo}", Json.Opcoes);
        Assert.Equal("tgpm", Assert.Single(analise!.Dimensoes.Single(d => d.Dimensao == "terminal").Grupos).Chave);
        var resumoAlertas = await op.GetFromJsonAsync<ResumoAlertasDto>("/api/v1/alertas/resumo", Json.Opcoes);
        var alertasTgpm = await op.GetFromJsonAsync<Pagina<AlertaDto>>("/api/v1/alertas?tamanhoPagina=100", Json.Opcoes);
        Assert.All(alertasTgpm!.Itens, a => Assert.Equal("tgpm", a.TerminalId));
        Assert.Equal(alertasTgpm.Total, resumoAlertas!.Ativos);

        // Preferência: tela válida e terminal dentro do vínculo. Outro terminal = 403; tela inexistente = 422.
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PutAsJsonAsync("/api/v1/auth/me/preferencias", new { terminalPreferido = "hidrovias" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await op.PutAsJsonAsync("/api/v1/auth/me/preferencias", new { telaInicial = "painel-secreto" })).StatusCode);
        var pref = await op.PutAsJsonAsync("/api/v1/auth/me/preferencias", new { telaInicial = "movimentacoes-analise", terminalPreferido = "tgpm" });
        Assert.Equal(HttpStatusCode.OK, pref.StatusCode);
        var me = await op.GetFromJsonAsync<UsuarioDto>("/api/v1/auth/me", Json.Opcoes);
        Assert.Equal("movimentacoes-analise", me!.Preferencias!.TelaInicial);
        Assert.Equal("gargalos", me.Preferencias.TelaPadrao);
        Assert.Equal("tgpm", me.TerminalId);
        // Preferência não é permissão: continua Operador, sem acesso à gestão de usuários.
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync("/api/v1/usuarios")).StatusCode);

        // Desvincular devolve a visão de todos os terminais (após novo login).
        Assert.Equal(HttpStatusCode.OK, (await gestor.PutAsJsonAsync($"/api/v1/usuarios/{id}/terminal", new { terminalId = "" })).StatusCode);
        var livre = await ApiFixture.ClienteAsync(api.Factory, email, Senha);
        Assert.Equal(3, (await livre.GetFromJsonAsync<Pagina<MarcacaoDto>>($"/api/v1/marcacoes?{periodo}", Json.Opcoes))!.Total);
    }

    [SkippableFact]
    public async Task Usuario_do_portal_nao_recebe_vinculo_de_terminal()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        var portal = await gestor.GetFromJsonAsync<Pagina<UsuarioDto>>("/api/v1/usuarios?papel=Transportadora", Json.Opcoes);
        var alvo = portal!.Itens.First();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await gestor.PutAsJsonAsync($"/api/v1/usuarios/{alvo.Id}/terminal", new { terminalId = "tgpm" })).StatusCode);
    }

    [SkippableFact]
    public async Task Analise_360_bate_com_o_calculo_manual_e_o_detalhamento_traz_os_mesmos_registros()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        string Id() => $"A{Dados.Proximo()}";
        // Unitapajós, abril/2003 (01/04/2003 = terça). Período: 01 a 10/04; anterior: 22 a 31/03.
        // Esperas: 2 h, 4 h, 30 h; uma sem liberação; uma com código de convênio.
        await ImportarAsync(gestor, "unitapajos", [
            $"{Id()};10;UNITAPAJOS - SOJA;;41;20030401;08:00:00;20030401;10:00:00",
            $"{Id()};11;UNITAPAJOS - SOJA;;41;20030401;08:30:00;20030401;12:30:00",
            $"{Id()};12;UNITAPAJOS - MILHO;C77;41;20030402;14:00:00;20030403;20:00:00",
            $"{Id()};13;UNITAPAJOS - MILHO;;41;20030405;08:15:00;;",
            $"{Id()};14;UNITAPAJOS - SOJA;;41;20030325;09:00:00;20030325;11:00:00",
        ]);

        const string q = "terminalId=unitapajos&de=2003-04-01&ate=2003-04-10";
        var a = await gestor.GetFromJsonAsync<ResultadoAnalise>($"/api/v1/marcacoes/analise?{q}", Json.Opcoes);
        Assert.Equal(10, a!.Dias);
        Assert.Equal(new DateOnly(2003, 3, 22), a.AnteriorDe);
        Assert.Equal(new DateOnly(2003, 3, 31), a.AnteriorAte);
        Assert.Equal(4, a.Atual.Entradas);
        Assert.Equal(3, a.Atual.Saidas);
        Assert.Equal(3, a.Atual.Concluidas);
        Assert.Equal(1, a.Atual.Pendentes);
        Assert.Equal(4, a.Atual.EsperaMediana);
        Assert.Equal(a.LimiteEsperaLongaHoras < 30 ? 1 : 0, a.Atual.EsperasLongas); // só a espera de 30 h pode passar do limite
        Assert.Equal(1, a.Anterior!.Entradas);
        Assert.Equal("dia", a.Agrupamento);
        Assert.Equal(10, a.Serie.Count);
        Assert.Equal(2, a.Serie[0].Entradas);
        Assert.Equal(3, a.Qualidade.SemCodigoConvenio);
        Assert.Equal(1, a.Qualidade.SemLiberacao);
        Assert.Equal(1.0 / 10 * 3, a.PorHora[8].EntradasPorDia, 6);

        // Drill-down: os mesmos filtros + a fatia clicada devolvem exatamente os registros contados.
        async Task<int> Total(string extra) =>
            (await gestor.GetFromJsonAsync<Pagina<MarcacaoDto>>($"/api/v1/marcacoes?{q}&{extra}", Json.Opcoes))!.Total;
        Assert.Equal(3, await Total("hora=8"));
        Assert.Equal(2, await Total("diaSemana=2")); // terça (01/04)
        Assert.Equal(1, await Total("diaSemana=6")); // sábado (05/04)
        Assert.Equal(3, await Total("semCodigoConvenio=true"));
        Assert.Equal(1, await Total("semLiberacao=true"));
        var milho = a.Dimensoes.Single(d => d.Dimensao == "convenio").Grupos.Single(g => g.Rotulo == "UNITAPAJOS - MILHO");
        Assert.Equal(milho.Entradas, await Total("convenioExato=UNITAPAJOS%20-%20MILHO"));
        Assert.Equal(a.Atual.EsperasLongas, await Total($"esperaAcimaHoras={a.LimiteEsperaLongaHoras}"));

        Assert.Equal(HttpStatusCode.BadRequest, (await gestor.GetAsync("/api/v1/marcacoes/analise?de=2003-04-10&ate=2003-04-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await gestor.GetAsync("/api/v1/marcacoes?hora=24")).StatusCode);
    }
}
