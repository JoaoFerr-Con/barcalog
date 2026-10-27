using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;

namespace BarcaLog.IntegrationTests;

/// <summary>
/// Central de relatórios: prévia com os mesmos números da Análise 360°, emissão em PDF e Excel,
/// histórico, período sem dados e escopo por terminal. Usa o ano de 2003 (isolado dos demais testes).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class RelatoriosTests(ApiFixture api)
{
    private const string Periodo = "de=2003-05-01&ate=2003-05-31";

    private static async Task ImportarAsync(HttpClient gestor, string terminal, IEnumerable<string> linhas)
    {
        var csv = "COD_MOVIMENTO;SENHA;CONVENIO;COD_CONVENIO;CICLO;DATA_MARCACAO;HORA_MARCACAO;DATA_LIBERACAO;HORA_LIBERACAO\r\n" + string.Join("\r\n", linhas);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(terminal), "terminalId");
        var arquivo = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(arquivo, "arquivo", $"relatorio_{Guid.NewGuid():N}.csv");
        var r = await gestor.PostAsync("/api/v1/importacoes", form);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var lote = await r.Content.ReadFromJsonAsync<LoteImportacaoDto>(Json.Opcoes);
        Assert.True((await gestor.PostAsync($"/api/v1/importacoes/{lote!.Id}/confirmar", null)).IsSuccessStatusCode);
    }

    private static string Id() => $"RL{Dados.Proximo()}";

    private async Task<HttpClient> PrepararAsync()
    {
        var gestor = await api.ClienteAsync("Gestor");
        // Idempotente entre execuções: só importa se maio/2003 ainda não tem movimentos.
        var existente = await gestor.GetFromJsonAsync<Pagina<MarcacaoDto>>($"/api/v1/marcacoes?{Periodo}", Json.Opcoes);
        if (existente!.Total > 0) return gestor;
        await ImportarAsync(gestor, "tgpm", [
            $"{Id()};1;TGPM - SOJA;;41;20030505;06:00:00;20030505;08:00:00",
            $"{Id()};2;TGPM - SOJA;;41;20030506;07:00:00;20030507;09:00:00",
            $"{Id()};3;TGPM - MILHO;;41;20030520;10:00:00;20030524;12:00:00",
            $"{Id()};4;TGPM - SOJA;;41;20030420;10:00:00;20030420;12:00:00"
        ]);
        await ImportarAsync(gestor, "hidrovias", [
            $"{Id()};5;HIDROVIAS - SOJA;;41;20030510;09:00:00;20030510;21:00:00"
        ]);
        return gestor;
    }

    [SkippableFact]
    public async Task Previa_tem_os_numeros_da_analise_360_e_exporta_pdf_e_excel_com_historico()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await PrepararAsync();

        var opcoes = await gestor.GetFromJsonAsync<JsonElement>("/api/v1/relatorios/opcoes", Json.Opcoes);
        Assert.Equal(3, opcoes.GetProperty("tipos").GetArrayLength());
        Assert.Equal(1000, opcoes.GetProperty("referenciaDiariaCarretas").GetInt32());

        var analise = await gestor.GetFromJsonAsync<ResultadoAnalise>($"/api/v1/marcacoes/analise?{Periodo}", Json.Opcoes);
        foreach (var tipo in new[] { "gerencial", "operacional", "analise360" })
        {
            var doc = await gestor.GetFromJsonAsync<JsonElement>($"/api/v1/relatorios/previa?tipo={tipo}&{Periodo}", Json.Opcoes);
            Assert.False(doc.GetProperty("semDados").GetBoolean());
            Assert.Equal("Maio de 2003", doc.GetProperty("periodoRotulo").GetString());
            var resumo = doc.GetProperty("secoes")[0];
            Assert.Equal("resumo", resumo.GetProperty("id").GetString());
            var kpis = resumo.GetProperty("blocos").EnumerateArray().First(b => b.GetProperty("tipo").GetString() == "indicadores").GetProperty("itens");
            var entradas = kpis.EnumerateArray().First(i => i.GetProperty("rotulo").GetString() == "Entradas de carretas").GetProperty("valor").GetString();
            Assert.Equal(analise!.Atual.Entradas.ToString(), entradas);
            Assert.Equal(4, analise.Atual.Entradas);
            Assert.True(doc.GetProperty("secoes").GetArrayLength() >= 7);
        }

        var corpo = new { tipo = "analise360", de = "2003-05-01", ate = "2003-05-31" };
        var pdf = await gestor.PostAsJsonAsync("/api/v1/relatorios/exportar?formato=pdf", corpo);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        var bytesPdf = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytesPdf, 0, 4));
        var codigo = pdf.Headers.GetValues("X-Relatorio-Codigo").Single();
        Assert.StartsWith("RL-", codigo);

        var xlsx = await gestor.PostAsJsonAsync("/api/v1/relatorios/exportar?formato=xlsx", corpo);
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        var bytesXlsx = await xlsx.Content.ReadAsByteArrayAsync();
        Assert.Equal("PK", Encoding.ASCII.GetString(bytesXlsx, 0, 2));

        var hist = await gestor.GetFromJsonAsync<JsonElement>("/api/v1/relatorios/historico?tamanho=100", Json.Opcoes);
        var item = hist.GetProperty("itens").EnumerateArray().First(i => i.GetProperty("codigo").GetString() == codigo);
        Assert.Equal("pdf", item.GetProperty("formato").GetString());
        Assert.Equal(bytesPdf.LongLength, item.GetProperty("tamanhoBytes").GetInt64());
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytesPdf)).ToLowerInvariant(), item.GetProperty("hashSha256").GetString());

        var de_novo = await gestor.PostAsync($"/api/v1/relatorios/historico/{item.GetProperty("id").GetInt32()}/reemitir", null);
        Assert.Equal(HttpStatusCode.OK, de_novo.StatusCode);
        Assert.NotEqual(codigo, de_novo.Headers.GetValues("X-Relatorio-Codigo").Single());

        // A emissão fica na auditoria.
        var auditoria = await gestor.GetFromJsonAsync<JsonElement>("/api/v1/auditoria?tamanhoPagina=50", Json.Opcoes);
        Assert.Contains(auditoria.GetProperty("itens").EnumerateArray(), l => (l.GetProperty("detalhes").GetString() ?? "").Contains(codigo));
    }

    [SkippableFact]
    public async Task Periodo_sem_dados_nao_gera_conclusoes_nem_arquivo()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await PrepararAsync();
        var doc = await gestor.GetFromJsonAsync<JsonElement>("/api/v1/relatorios/previa?tipo=gerencial&de=1999-01-01&ate=1999-01-31", Json.Opcoes);
        Assert.True(doc.GetProperty("semDados").GetBoolean());
        Assert.DoesNotContain(doc.GetProperty("secoes").EnumerateArray(), s => s.GetProperty("id").GetString() == "conclusoes");
        var r = await gestor.PostAsJsonAsync("/api/v1/relatorios/exportar", new { tipo = "gerencial", de = "1999-01-01", ate = "1999-01-31" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await gestor.GetAsync("/api/v1/relatorios/previa?tipo=outro")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await gestor.GetAsync("/api/v1/relatorios/previa?tipo=gerencial&de=2003-05-31&ate=2003-05-01")).StatusCode);
    }

    [SkippableFact]
    public async Task Usuario_vinculado_so_gera_relatorio_do_proprio_terminal()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await PrepararAsync();
        var email = $"relatorio{Dados.Proximo()}@testes.local";
        var criado = await gestor.PostAsJsonAsync("/api/v1/usuarios", new { nome = "Pessoa Terminal", email, senha = "Provisoria-Inicial#99", papel = "Operador" });
        var u = await criado.Content.ReadFromJsonAsync<UsuarioDto>(Json.Opcoes);
        Assert.True((await gestor.PutAsJsonAsync($"/api/v1/usuarios/{u!.Id}/terminal", new { terminalId = "tgpm" })).IsSuccessStatusCode);
        var temp = await ApiFixture.ClienteAsync(api.Factory, email, "Provisoria-Inicial#99");
        await temp.PostAsJsonAsync("/api/v1/auth/trocar-senha", new { senhaAtual = "Provisoria-Inicial#99", novaSenha = "Porto-Barcarena#2026r" });
        var op = await ApiFixture.ClienteAsync(api.Factory, email, "Porto-Barcarena#2026r");

        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync($"/api/v1/relatorios/previa?tipo=gerencial&{Periodo}&terminalId=hidrovias")).StatusCode);
        var doc = await op.GetFromJsonAsync<JsonElement>($"/api/v1/relatorios/previa?tipo=gerencial&{Periodo}", Json.Opcoes);
        Assert.Equal("TGPM", doc.GetProperty("abrangencia").GetString());
        var opcoes = await op.GetFromJsonAsync<JsonElement>("/api/v1/relatorios/opcoes", Json.Opcoes);
        Assert.Equal("tgpm", opcoes.GetProperty("terminalVinculado").GetString());
        Assert.All(opcoes.GetProperty("terminais").EnumerateArray(), t => Assert.Equal("tgpm", t.GetProperty("id").GetString()));

        var r = await op.PostAsJsonAsync("/api/v1/relatorios/exportar?formato=pdf", new { tipo = "operacional", de = "2003-05-01", ate = "2003-05-31" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var hist = await op.GetFromJsonAsync<JsonElement>("/api/v1/relatorios/historico?tamanho=100", Json.Opcoes);
        Assert.All(hist.GetProperty("itens").EnumerateArray(), i => Assert.Equal("TGPM", i.GetProperty("abrangencia").GetString()));
    }
}
