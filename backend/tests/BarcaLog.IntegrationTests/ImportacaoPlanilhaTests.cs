using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security;
using System.Text;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BarcaLog.IntegrationTests;

/// <summary>
/// Importação de planilhas de ponta a ponta contra o SQL Server: análise sem
/// gravar, conflitos com decisão obrigatória, confirmação transacional,
/// idempotência, complementos, rejeições com motivo e permissões.
/// Os arquivos são sintéticos (gerados aqui) — o repositório não guarda dados reais.
/// </summary>
[Collection(ColecaoApi.Nome)]
public class ImportacaoPlanilhaTests(ApiFixture api)
{
    private static readonly string[] Cabecalho = ["COD_MOVIMENTO", "SENHA", "CONVENIO", "COD_CONVENIO", "CICLO", "DATA_MARCACAO", "HORA_MARCACAO", "DATA_LIBERACAO", "HORA_LIBERACAO"];

    private static string Id() => $"T{Dados.Proximo()}";

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient c, string terminal, byte[] conteudo, string nome)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(terminal), "terminalId");
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue(nome.EndsWith(".csv") ? "text/csv" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(arquivo, "arquivo", nome);
        return await c.PostAsync("/api/v1/importacoes", form);
    }

    private static async Task<LoteImportacaoDto> AnalisarAsync(HttpClient c, string terminal, byte[] conteudo, string nome)
    {
        var r = await EnviarAsync(c, terminal, conteudo, nome);
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<LoteImportacaoDto>(Json.Opcoes))!;
    }

    private static async Task<LoteImportacaoDto> ConfirmarAsync(HttpClient c, int lote)
    {
        var r = await c.PostAsync($"/api/v1/importacoes/{lote}/confirmar", null);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<LoteImportacaoDto>(Json.Opcoes))!;
    }

    private static byte[] Csv(IEnumerable<string[]> linhas, char sep = ';') =>
        Encoding.UTF8.GetBytes(string.Join("\r\n", new[] { Cabecalho }.Concat(linhas).Select(l => string.Join(sep, l))));

    [SkippableFact]
    public async Task Csv_analisa_sem_gravar_confirma_e_reimportar_nao_duplica()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        string a = Id(), b = Id(), c = Id(), cod = $"C{Dados.Proximo()}";
        var arquivo = Csv([
            [a, "000001", "TGPM - SOJA", cod, "41", "20260801", "06:10:00", "20260801", "10:00:00"],
            [b, "000002", "TGPM - SOJA", cod, "41", "01/08/2026", "06:20", "20260805", "12:00:00"],   // espera > 72 h
            [c, "000003", "TGPM - SOJA", cod, "41", "2026-08-01", "06:30:00", "", ""],               // ainda sem liberação
            [a, "000004", "TGPM - SOJA", cod, "41", "20260801", "07:00:00", "20260801", "09:00:00"], // repetido no arquivo
            [Id(), "000005", "TGPM - SOJA", cod, "41", "20261399", "07:00:00", "", ""],              // data inválida
            [Id(), "000006", "TGPM - SOJA", cod, "41", "20260801", "08:00:00", "20260801", "07:00:00"], // liberação antes da marcação
        ]);

        var lote = await AnalisarAsync(gestor, "tgpm", arquivo, "TGPM_agosto.csv");
        Assert.Equal(StatusLoteImportacao.Analisado, lote.Status);
        Assert.Equal(6, lote.Linhas);
        Assert.Equal(3, lote.Novos);
        Assert.Equal(3, lote.Rejeitados);
        Assert.Equal(1, lote.Avisos.EsperaAcima72h);
        Assert.Equal(1, lote.Avisos.SemLiberacao);
        Assert.Equal(3, lote.Avisos.ConvenioNovo);
        Assert.Contains(lote.ConveniosNovos, cv => cv.Codigo == cod && cv.Operador == "TGPM" && cv.Carga == "Soja" && cv.Deduzido);
        Assert.Equal(new DateTime(2026, 8, 1, 6, 10, 0), lote.MarcacaoInicial);

        // Análise não grava nada em Marcacoes.
        Assert.Equal(0, await api.NoBancoAsync(db => db.Marcacoes.CountAsync(m => m.MovimentoId == a || m.MovimentoId == b || m.MovimentoId == c)));

        var rejeitadas = await gestor.GetFromJsonAsync<Pagina<LinhaImportacaoDto>>($"/api/v1/importacoes/{lote.Id}/linhas?classificacao=Rejeitado", Json.Opcoes);
        Assert.Collection(rejeitadas!.Itens.OrderBy(l => l.NumeroLinha),
            l => { Assert.Equal(5, l.NumeroLinha); Assert.Contains("repetido no arquivo (primeira ocorrência na linha 2)", l.Motivo); },
            l => { Assert.Equal(6, l.NumeroLinha); Assert.Contains("data \"20261399\" não reconhecida", l.Motivo); },
            l => { Assert.Equal(7, l.NumeroLinha); Assert.Equal("Liberação anterior à marcação.", l.Motivo); });

        var confirmado = await ConfirmarAsync(gestor, lote.Id);
        Assert.Equal(StatusLoteImportacao.Confirmado, confirmado.Status);
        Assert.Equal(3, confirmado.Inseridos);
        Assert.Equal(0, confirmado.Ignorados);
        Assert.Contains("gestor@testes.local", confirmado.ConcluidoPor);

        var gravados = await api.NoBancoAsync(db => db.Marcacoes.Where(m => m.MovimentoId == a || m.MovimentoId == b || m.MovimentoId == c).OrderBy(m => m.LinhaOrigem).ToListAsync());
        Assert.Equal(3, gravados.Count);
        Assert.All(gravados, m =>
        {
            Assert.Equal(OrigemMarcacao.Planilha, m.Origem);
            Assert.Equal(lote.Id, m.LoteImportacaoId);
            Assert.Equal("TGPM", m.Operador);
            Assert.Equal("Soja", m.Carga);
            Assert.Equal(cod, m.CodConvenio);
        });
        Assert.Equal([2, 3, 4], gravados.Select(m => m.LinhaOrigem!.Value));
        Assert.Equal(3.83, (double)gravados[0].EsperaHoras!.Value, 2); // coluna computada no SQL Server
        Assert.Null(gravados[2].DataLiberacao);
        Assert.NotNull(await api.NoBancoAsync(db => db.Convenios.SingleOrDefaultAsync(cv => cv.Codigo == cod)));

        // Mesmo arquivo de novo: nada novo, e a API avisa que ele já foi confirmado.
        var de_novo = await AnalisarAsync(gestor, "tgpm", arquivo, "TGPM_agosto.csv");
        Assert.Equal(0, de_novo.Novos);
        Assert.Equal(3, de_novo.Iguais);
        Assert.Equal(3, de_novo.Rejeitados);
        Assert.NotNull(de_novo.MesmoArquivoConfirmadoEm);
        var r2 = await ConfirmarAsync(gestor, de_novo.Id);
        Assert.Equal(0, r2.Inseridos);
        Assert.Equal(3, await api.NoBancoAsync(db => db.Marcacoes.CountAsync(m => m.MovimentoId == a || m.MovimentoId == b || m.MovimentoId == c)));

        // Auditoria registra análise e confirmação.
        var logs = await gestor.GetFromJsonAsync<Pagina<LogAuditoriaDto>>("/api/v1/auditoria?texto=TGPM_agosto.csv&tamanhoPagina=20", Json.Opcoes);
        Assert.Contains(logs!.Itens, l => l.Acao == "Importação de planilha confirmada" && l.Detalhes.Contains("3 movimentos inseridos"));
        Assert.Contains(logs.Itens, l => l.Acao == "Planilha analisada");
    }

    [SkippableFact]
    public async Task Xlsx_conflito_exige_decisao_e_preserva_o_historico()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        string a = Id(), b = Id(), cod = $"C{Dados.Proximo()}";
        string[] Linha(string id, string convenio) => [id, "000010", convenio, cod, "12", "20260704", "08:25:35", "20260704", "17:00:00"];

        var base1 = await AnalisarAsync(gestor, "unitapajos", Xlsx([Linha(a, "UNITAPAJOS BUNGE SOJA SEGREGADO"), Linha(b, "UNITAPAJOS BUNGE SOJA SEGREGADO")]), "unitapajos_julho.xlsx");
        Assert.Equal(2, base1.Novos);
        await ConfirmarAsync(gestor, base1.Id);

        // A origem renomeou o convênio (mesmo código).
        var lote = await AnalisarAsync(gestor, "unitapajos", Xlsx([Linha(a, "UNITAPAJOS BUNGE SORGO"), Linha(b, "UNITAPAJOS BUNGE SORGO")]), "unitapajos_julho_v2.xlsx");
        Assert.Equal(2, lote.Conflitantes);
        Assert.Equal(2, lote.ConflitosPendentes);

        var bloqueado = await gestor.PostAsync($"/api/v1/importacoes/{lote.Id}/confirmar", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bloqueado.StatusCode);
        Assert.Contains("2 conflito(s) sem decisão", await bloqueado.Content.ReadAsStringAsync());

        var conflitos = await gestor.GetFromJsonAsync<Pagina<LinhaImportacaoDto>>($"/api/v1/importacoes/{lote.Id}/linhas?classificacao=Conflito", Json.Opcoes);
        Assert.All(conflitos!.Itens, l => Assert.Equal("Convênio: UNITAPAJOS BUNGE SOJA SEGREGADO → UNITAPAJOS BUNGE SORGO", l.Motivo));
        var primeiro = conflitos.Itens.Single(l => l.MovimentoId == a);

        // Um mantido, o outro aplicado.
        var d1 = await gestor.PostAsJsonAsync($"/api/v1/importacoes/{lote.Id}/conflitos", new { decisao = "Manter", linhas = new[] { primeiro.Id } });
        Assert.Equal(1, (await d1.Content.ReadFromJsonAsync<ResultadoDecisaoDto>(Json.Opcoes))!.AindaPendentes);
        var d2 = await gestor.PostAsJsonAsync($"/api/v1/importacoes/{lote.Id}/conflitos", new { decisao = "Aplicar" });
        Assert.Equal(0, (await d2.Content.ReadFromJsonAsync<ResultadoDecisaoDto>(Json.Opcoes))!.AindaPendentes);

        var confirmado = await ConfirmarAsync(gestor, lote.Id);
        Assert.Equal(1, confirmado.Atualizados);
        Assert.Equal(1, confirmado.Ignorados);

        var convenios = await api.NoBancoAsync(db => db.Marcacoes.Where(m => m.MovimentoId == a || m.MovimentoId == b).ToDictionaryAsync(m => m.MovimentoId, m => m.Convenio));
        Assert.Equal("UNITAPAJOS BUNGE SOJA SEGREGADO", convenios[a]);
        Assert.Equal("UNITAPAJOS BUNGE SORGO", convenios[b]);

        // O que mudou continua consultável depois da confirmação.
        var historico = await gestor.GetFromJsonAsync<Pagina<LinhaImportacaoDto>>($"/api/v1/importacoes/{lote.Id}/linhas?classificacao=Conflito", Json.Opcoes);
        Assert.Equal(2, historico!.Total);
        Assert.Contains(historico.Itens, l => l.MovimentoId == b && l.Decisao == DecisaoConflitoImportacao.Aplicar);
    }

    [SkippableFact]
    public async Task Liberacao_que_chega_depois_completa_o_movimento_aberto()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        var id = Id();
        var primeiro = await AnalisarAsync(gestor, "hidrovias", Csv([[id, "000020", "HIDROVIAS - SOJA", "", "17", "20260901", "10:00:00", "", ""]], ','), "hidro_parcial.csv");
        Assert.Equal(1, primeiro.Avisos.SemLiberacao);
        await ConfirmarAsync(gestor, primeiro.Id);

        var segundo = await AnalisarAsync(gestor, "hidrovias", Csv([[id, "000020", "HIDROVIAS - SOJA", "", "17", "20260901", "10:00:00", "20260901", "15:30:00"]], ','), "hidro_completo.csv");
        Assert.Equal(1, segundo.Complementares);
        Assert.Equal(0, segundo.Conflitantes);
        var confirmado = await ConfirmarAsync(gestor, segundo.Id);
        Assert.Equal(1, confirmado.Atualizados);

        var m = await api.NoBancoAsync(db => db.Marcacoes.SingleAsync(x => x.MovimentoId == id));
        Assert.Equal(new DateTime(2026, 9, 1, 15, 30, 0), m.DataLiberacao);
        Assert.Equal(5.5, (double)m.EsperaHoras!.Value, 2);
        Assert.Equal("HIDROVIAS", m.Operador); // nome já conhecido → mesma regra dos dados originais
    }

    [SkippableFact]
    public async Task Rejeicoes_de_arquivo_e_de_linha_explicam_o_motivo()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");

        var semColuna = await EnviarAsync(gestor, "tgpm", Encoding.UTF8.GetBytes("COD_MOVIMENTO;CONVENIO\r\nX1;TGPM - SOJA"), "incompleto.csv");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, semColuna.StatusCode);
        Assert.Contains("Colunas obrigatórias ausentes: SENHA, DATA_MARCACAO, HORA_MARCACAO", await semColuna.Content.ReadAsStringAsync());

        var naoXlsx = await EnviarAsync(gestor, "tgpm", Encoding.UTF8.GetBytes("não sou planilha"), "falso.xlsx");
        Assert.Equal(HttpStatusCode.BadRequest, naoXlsx.StatusCode);

        var formato = await EnviarAsync(gestor, "tgpm", [1, 2, 3], "dados.json");
        Assert.Equal(HttpStatusCode.BadRequest, formato.StatusCode);

        var terminal = await EnviarAsync(gestor, "nao-existe", Csv([[Id(), "1", "X", "", "1", "20260101", "10:00:00", "", ""]]), "a.csv");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, terminal.StatusCode);

        // Identificador gravado como número no Excel perde zeros à esquerda: rejeitado, nunca "corrigido" em silêncio.
        var lote = await AnalisarAsync(gestor, "tgpm", Xlsx([["591898", "000001", "TGPM - SOJA", "000010", "41", "20260801", "06:10:00", "", ""]], idNumerico: true), "numerico.xlsx");
        Assert.Equal(1, lote.Rejeitados);
        var linha = (await gestor.GetFromJsonAsync<Pagina<LinhaImportacaoDto>>($"/api/v1/importacoes/{lote.Id}/linhas", Json.Opcoes))!.Itens.Single();
        Assert.Contains("gravado como número", linha.Motivo);

        // Relatório CSV: rejeições com motivo, abre no Excel.
        var relatorio = await gestor.GetAsync($"/api/v1/importacoes/{lote.Id}/relatorio");
        Assert.Equal("text/csv", relatorio.Content.Headers.ContentType?.MediaType);
        var texto = await relatorio.Content.ReadAsStringAsync();
        Assert.Contains("Rejeitada", texto);
        Assert.Contains("gravado como número", texto);
    }

    [SkippableFact]
    public async Task Cancelar_descarta_a_analise()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        var id = Id();
        var lote = await AnalisarAsync(gestor, "tgpm", Csv([[id, "000030", "TGPM - MILHO", "", "25", "20260802", "03:00:00", "20260802", "07:00:00"]]), "cancelar.csv");
        var r = await gestor.PostAsync($"/api/v1/importacoes/{lote.Id}/cancelar", null);
        Assert.Equal(StatusLoteImportacao.Cancelado, (await r.Content.ReadFromJsonAsync<LoteImportacaoDto>(Json.Opcoes))!.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await gestor.PostAsync($"/api/v1/importacoes/{lote.Id}/confirmar", null)).StatusCode);
        Assert.Equal(0, await api.NoBancoAsync(db => db.Marcacoes.CountAsync(m => m.MovimentoId == id)));
        Assert.Equal(0, await api.NoBancoAsync(db => db.LinhasImportacao.CountAsync(l => l.LoteId == lote.Id)));
    }

    [SkippableFact]
    public async Task Analise_desatualizada_nao_pode_ser_confirmada()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        var id = Id();
        var antiga = await AnalisarAsync(gestor, "hidrovias", Csv([[id, "000050", "HIDROVIAS - SOJA", "", "17", "20260803", "10:00:00", "20260803", "12:00:00"]]), "antiga.csv");
        var nova = await AnalisarAsync(gestor, "hidrovias", Csv([[id, "000050", "HIDROVIAS - SOJA", "", "17", "20260803", "10:00:00", "20260803", "12:00:00"]]), "nova.csv");
        await ConfirmarAsync(gestor, nova.Id);

        var r = await gestor.PostAsync($"/api/v1/importacoes/{antiga.Id}/confirmar", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        Assert.Contains("Análise desatualizada", await r.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task So_gestor_importa_mas_todos_os_papeis_internos_consultam()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var operador = await api.ClienteAsync("Operador");
        var auditor = await api.ClienteAsync("Auditor");
        var arquivo = Csv([[Id(), "000040", "TGPM - SOJA", "", "41", "20260801", "06:10:00", "", ""]]);
        Assert.Equal(HttpStatusCode.Forbidden, (await EnviarAsync(operador, "tgpm", arquivo, "x.csv")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PostAsync("/api/v1/importacoes/1/confirmar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/api/v1/importacoes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await operador.GetAsync("/api/v1/convenios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PutAsJsonAsync("/api/v1/convenios/000010", new { operador = "X", carga = "Y" })).StatusCode);
    }

    /// <summary>XLSX mínimo e válido (uma aba, células de texto inline). Com idNumerico, a 1ª coluna vira célula numérica.</summary>
    private static byte[] Xlsx(IEnumerable<string[]> linhas, bool idNumerico = false)
    {
        static string Col(int i) => ((char)('A' + i)).ToString();
        var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        var todas = new[] { Cabecalho }.Concat(linhas).ToList();
        for (var r = 0; r < todas.Count; r++)
        {
            sb.Append($"<row r=\"{r + 1}\">");
            for (var c = 0; c < todas[r].Length; c++)
            {
                var v = todas[r][c];
                if (v.Length == 0) continue;
                var refe = $"{Col(c)}{r + 1}";
                if (idNumerico && r > 0 && c == 0) sb.Append($"<c r=\"{refe}\"><v>{v}</v></c>");
                else sb.Append($"<c r=\"{refe}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(v)}</t></is></c>");
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData></worksheet>");
        var partes = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = """<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""",
            ["_rels/.rels"] = """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""",
            ["xl/workbook.xml"] = """<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="sheet1" sheetId="1" r:id="rId1"/></sheets></workbook>""",
            ["xl/_rels/workbook.xml.rels"] = """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""",
            ["xl/worksheets/sheet1.xml"] = sb.ToString(),
        };
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (nome, xml) in partes)
            {
                using var w = new StreamWriter(zip.CreateEntry(nome).Open(), new UTF8Encoding(false));
                w.Write(xml);
            }
        }
        return ms.ToArray();
    }
}

[Collection(ColecaoApi.Nome)]
public class MovimentacoesTests(ApiFixture api)
{
    [SkippableFact]
    public async Task Filtros_ordenacao_e_rastreabilidade_do_movimento()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        string a = $"V{Dados.Proximo()}", b = $"V{Dados.Proximo()}", c = $"V{Dados.Proximo()}";
        var csv = "COD_MOVIMENTO;SENHA;CONVENIO;COD_CONVENIO;CICLO;DATA_MARCACAO;HORA_MARCACAO;DATA_LIBERACAO;HORA_LIBERACAO\r\n" +
                  $"{a};S1;HIDROVIAS - MILHO;;9;20020105;08:00:00;20020109;10:00:00\r\n" +   // 98 h
                  $"{b};S2;HIDROVIAS - MILHO;;9;20020105;09:00:00;20020109;10:00:00\r\n" +   // 97 h, liberada junto
                  $"{c};S3;HIDROVIAS - MILHO;;9;20020106;09:00:00;;\r\n";                    // aberta
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("hidrovias"), "terminalId");
        form.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "arquivo", "rastreio_2002.csv");
        var lote = await (await gestor.PostAsync("/api/v1/importacoes", form)).Content.ReadFromJsonAsync<LoteImportacaoDto>(Json.Opcoes);
        Assert.True((await gestor.PostAsync($"/api/v1/importacoes/{lote!.Id}/confirmar", null)).IsSuccessStatusCode);

        var periodo = "terminalId=hidrovias&de=2002-01-01&ate=2002-01-31";
        var longas = await gestor.GetFromJsonAsync<Pagina<MarcacaoDto>>($"/api/v1/marcacoes?{periodo}&esperaAcimaHoras=72&ordenacao=espera_desc", Json.Opcoes);
        Assert.Equal([a, b], longas!.Itens.Select(m => m.MovimentoId));
        var abertas = await gestor.GetFromJsonAsync<Pagina<MarcacaoDto>>($"/api/v1/marcacoes?{periodo}&semLiberacao=true", Json.Opcoes);
        Assert.Equal([c], abertas!.Itens.Select(m => m.MovimentoId));
        var busca = await gestor.GetFromJsonAsync<Pagina<MarcacaoDto>>($"/api/v1/marcacoes?busca=S2&{periodo}", Json.Opcoes);
        Assert.Equal([b], busca!.Itens.Select(m => m.MovimentoId));
        Assert.Equal(HttpStatusCode.BadRequest, (await gestor.GetAsync("/api/v1/marcacoes?ordenacao=qualquer")).StatusCode);

        var detalhe = await gestor.GetFromJsonAsync<MarcacaoDetalheDto>($"/api/v1/marcacoes/{a}/detalhe", Json.Opcoes);
        Assert.Equal(OrigemMarcacao.Planilha, detalhe!.Movimento.Origem);
        Assert.Equal("rastreio_2002.csv", detalhe.ArquivoOrigem);
        Assert.Equal(2, detalhe.Movimento.LinhaOrigem);
        Assert.Equal(1, detalhe.LiberadosNoMesmoInstante);
    }
}

[Collection(ColecaoApi.Nome)]
public class AmbienteTests(ApiFixture api)
{
    [SkippableFact]
    public async Task Avisa_quando_o_banco_tem_o_cadastro_ficticio_de_exemplo()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var auditor = await api.ClienteAsync("Auditor");
        var r = await auditor.GetFromJsonAsync<BarcaLog.Api.Controllers.AmbienteDto>("/api/v1/ambiente", Json.Opcoes);
        Assert.True(r!.DadosExemplo); // a fixture liga Seed:DadosExemplo
    }
}
