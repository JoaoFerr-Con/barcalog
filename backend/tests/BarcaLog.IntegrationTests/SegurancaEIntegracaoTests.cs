using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using BarcaLog.Api.Infra;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BarcaLog.IntegrationTests;

[Collection(ColecaoApi.Nome)]
public class SegurancaEIntegracaoTests(ApiFixture api)
{
    // ---------- Autorização por papel ----------

    [SkippableFact]
    public async Task Sem_token_retorna_401_e_papeis_nao_ultrapassam_o_que_podem()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var anonimo = api.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/transportadoras")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/auditoria")).StatusCode);

        var auditor = await api.ClienteAsync("Auditor");
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/api/v1/auditoria")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PostAsJsonAsync("/api/v1/transportadoras", new { nome = "Não Pode", cnpj = Dados.Cnpj() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PostAsJsonAsync("/api/v1/ocorrencias", new { nivel = "N1", placa = "NGL-3021", transportadoraId = 1, descricao = "xxxx" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/usuarios")).StatusCode);

        var operador = await api.ClienteAsync("Operador");
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.DeleteAsync("/api/v1/veiculos/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PostAsync("/api/v1/marcacoes/importar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.GetAsync("/api/v1/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.GetAsync("/api/v1/condutores/1/dados-pessoais")).StatusCode);
    }

    [SkippableFact]
    public async Task Listagem_de_usuarios_nao_expoe_hash_nem_segredo()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        var corpo = await gestor.GetStringAsync("/api/v1/usuarios");
        Assert.DoesNotContain("senhaHash", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mfaSegredo", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AQAAAA", corpo); // prefixo de hash PBKDF2 do Identity
    }

    // ---------- Integração por API Key ----------

    [SkippableFact]
    public async Task Integracao_por_api_key_grava_marcacao_e_valida_eventos()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var sistema = api.Factory.CreateClient();
        var id = $"T{Random.Shared.NextInt64(10_000_000, 99_999_999)}";
        object[] eventos =
        [
            new { tipo = "Marcacao", movimentoId = id, terminalId = "hidrovias", senha = "000001", convenio = "HIDROVIAS - SOJA", operador = "HIDROVIAS", carga = "Soja", ciclo = 1, dataMarcacao = "2026-08-01T06:00:00" },
            new { tipo = "Liberacao", movimentoId = id, dataLiberacao = "2026-08-02T08:30:00" },
            new { tipo = "Marcacao", movimentoId = id + "F", terminalId = "tgpm", senha = "1", convenio = "C", operador = "O", carga = "Soja", dataMarcacao = "2099-01-01T00:00:00" }
        ];

        Assert.Equal(HttpStatusCode.Unauthorized, (await sistema.PostAsJsonAsync("/api/v1/integracao/eventos", eventos)).StatusCode);
        sistema.DefaultRequestHeaders.Add("X-Api-Key", "chave-errada");
        Assert.Equal(HttpStatusCode.Unauthorized, (await sistema.PostAsJsonAsync("/api/v1/integracao/eventos", eventos)).StatusCode);

        sistema.DefaultRequestHeaders.Remove("X-Api-Key");
        sistema.DefaultRequestHeaders.Add("X-Api-Key", ApiFixture.ApiKey);
        var resposta = await sistema.PostAsJsonAsync("/api/v1/integracao/eventos", eventos);
        resposta.EnsureSuccessStatusCode();
        var resultado = await resposta.Content.ReadFromJsonAsync<ResultadoIntegracaoDto>(Json.Opcoes);
        Assert.Equal(1, resultado!.Criados);
        var erro = Assert.Single(resultado.Erros); // data no futuro recusada
        Assert.Equal(2, erro.Indice);

        var espera = await api.NoBancoAsync(db => db.Marcacoes.Where(m => m.MovimentoId == id).Select(m => m.EsperaHoras).SingleAsync());
        Assert.Equal(26.5, espera);
        var usuario = await api.ClienteAsync("Operador");
        Assert.Equal("D1", (await usuario.GetFromJsonAsync<MarcacaoDto>($"/api/v1/marcacoes/{id}", Json.Opcoes))!.Janela);

        // API Key não abre endpoints de usuário.
        Assert.Equal(HttpStatusCode.Unauthorized, (await sistema.GetAsync("/api/v1/transportadoras")).StatusCode);
        var logs = await usuario.GetFromJsonAsync<Pagina<LogAuditoriaDto>>("/api/v1/auditoria?autor=Integração", Json.Opcoes);
        Assert.Contains(logs!.Itens, l => l.Autor == "Integração: Testes");
    }

    // ---------- Idempotência e concorrência ----------

    [SkippableFact]
    public async Task Mesma_Idempotency_Key_nao_cria_ocorrencia_duplicada()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Operador");
        var (t, v) = await NegativacaoIntegracaoTests.CriarFrotaAsync(cliente);
        var chave = Guid.NewGuid().ToString();
        var corpo = new { nivel = "N2", placa = v.Placa, transportadoraId = t.Id, descricao = "Clique duplo no botão salvar" };

        HttpRequestMessage Req(object body) => new(HttpMethod.Post, "/api/v1/ocorrencias")
        {
            Content = JsonContent.Create(body),
            Headers = { { IdempotenteAttribute.Cabecalho, chave } }
        };
        var r1 = await cliente.SendAsync(Req(corpo));
        var r2 = await cliente.SendAsync(Req(corpo));

        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        Assert.True(r2.Headers.Contains("Idempotent-Replayed"));
        var o1 = await r1.Content.ReadFromJsonAsync<OcorrenciaRegistradaDto>(Json.Opcoes);
        var o2 = await r2.Content.ReadFromJsonAsync<OcorrenciaRegistradaDto>(Json.Opcoes);
        Assert.Equal(o1!.Ocorrencia.Id, o2!.Ocorrencia.Id);
        Assert.Equal(1, await api.NoBancoAsync(db => db.Ocorrencias.CountAsync(o => o.Placa == v.Placa)));

        // Mesma chave, conteúdo diferente = erro do cliente.
        var r3 = await cliente.SendAsync(Req(new { nivel = "N3", placa = v.Placa, transportadoraId = t.Id, descricao = "Outra coisa" }));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r3.StatusCode);
    }

    [SkippableFact]
    public async Task Duas_aprovacoes_simultaneas_da_mesma_contestacao_so_uma_vence()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Operador");
        var (t, v) = await NegativacaoIntegracaoTests.CriarFrotaAsync(cliente);
        var oc = await NegativacaoIntegracaoTests.RegistrarN3Async(cliente, v.Placa, t.Id);
        var ct = await (await cliente.PostAsJsonAsync("/api/v1/contestacoes", new { ocorrenciaId = oc.Id, justificativa = "Justificativa com laudo anexo." }))
            .Content.ReadFromJsonAsync<ContestacaoDto>(Json.Opcoes);

        var clientes = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => api.ClienteAsync("Operador")));
        var respostas = await Task.WhenAll(clientes.Select((c, i) =>
            c.PostAsJsonAsync($"/api/v1/contestacoes/{ct!.Id}/{(i % 2 == 0 ? "aprovar" : "rejeitar")}", new { respostaOperador = $"resp {i}" })));

        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(respostas.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity }));
        Assert.DoesNotContain(respostas, r => (int)r.StatusCode >= 500);

        var final = await cliente.GetFromJsonAsync<ContestacaoDto>($"/api/v1/contestacoes/{ct!.Id}", Json.Opcoes);
        var veiculo = await cliente.GetFromJsonAsync<VeiculoDto>($"/api/v1/veiculos/{v.Id}", Json.Opcoes);
        // Estado coerente com a resposta vencedora.
        Assert.Equal(final!.Status == StatusContestacao.Aprovada ? StatusNegativacao.Regular : StatusNegativacao.Negativada, veiculo!.StatusNegativacao);
    }

    [SkippableFact]
    public async Task Contestacoes_simultaneas_da_mesma_ocorrencia_so_uma_e_aberta()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Operador");
        var (t, v) = await NegativacaoIntegracaoTests.CriarFrotaAsync(cliente);
        var oc = await NegativacaoIntegracaoTests.RegistrarN3Async(cliente, v.Placa, t.Id);

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            cliente.PostAsJsonAsync("/api/v1/contestacoes", new { ocorrenciaId = oc.Id, justificativa = "Pedido enviado várias vezes." })));

        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.DoesNotContain(respostas, r => (int)r.StatusCode >= 500);
        Assert.Equal(1, await api.NoBancoAsync(db => db.Contestacoes.CountAsync(c => c.OcorrenciaId == oc.Id)));
    }

    // ---------- Validação de entrada ----------

    [SkippableTheory]
    [InlineData("{\"nome\":\"X Transportes\",\"cnpj\":\"11.111.111/1111-11\"}")]      // CNPJ com DV errado
    [InlineData("{\"nome\":\"X Transportes\",\"cnpj\":\"12.345.678/0001-00\"}")]
    [InlineData("{\"nome\":\"\",\"cnpj\":\"12.ABC.345/01DE-35\"}")]                    // nome vazio
    [InlineData("{\"nome\":\"X\\u0000Y Transportes\",\"cnpj\":\"12.ABC.345/01DE-35\"}")] // caractere de controle
    [InlineData("[1,2,3]")]
    [InlineData("{not json")]
    public async Task Entrada_invalida_em_transportadora_retorna_400(string corpo)
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Operador");
        var r = await cliente.PostAsync("/api/v1/transportadoras", new StringContent(corpo, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("System.", texto); // nada de tipo .NET na resposta
        Assert.DoesNotContain("   at ", texto);  // nem stack trace
    }

    [SkippableFact]
    public async Task CNPJ_alfanumerico_valido_e_aceito()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Operador");
        var r = await cliente.PostAsJsonAsync("/api/v1/transportadoras", new { nome = $"Alfa {Dados.Proximo()}", cnpj = "12abc34501de35" });
        if (r.StatusCode == HttpStatusCode.UnprocessableEntity) return; // já cadastrado por execução anterior do mesmo banco
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.Equal("12.ABC.345/01DE-35", (await r.Content.ReadFromJsonAsync<TransportadoraDto>(Json.Opcoes))!.Cnpj);
    }

    [SkippableTheory]
    [InlineData("{\"nivel\":7,\"placa\":\"NGL-3021\",\"transportadoraId\":3,\"descricao\":\"enum numérico\"}")]
    [InlineData("{\"nivel\":\"N9\",\"placa\":\"NGL-3021\",\"transportadoraId\":3,\"descricao\":\"enum inexistente\"}")]
    [InlineData("{\"nivel\":\"N1\",\"placa\":\"'; DROP TABLE Veiculos;--\",\"transportadoraId\":3,\"descricao\":\"sql injection\"}")]
    [InlineData("{\"nivel\":\"N1\",\"placa\":\"<script>\",\"transportadoraId\":3,\"descricao\":\"xss\"}")]
    public async Task Ocorrencia_com_enum_ou_placa_invalida_retorna_400(string corpo)
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Operador");
        var r = await cliente.PostAsync("/api/v1/ocorrencias", new StringContent(corpo, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.True(await api.NoBancoAsync(db => db.Veiculos.AnyAsync())); // a tabela continua lá
    }

    [SkippableFact]
    public async Task Busca_com_texto_malicioso_e_tratada_como_texto()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Auditor");
        var r = await cliente.GetAsync("/api/v1/auditoria?texto=" + Uri.EscapeDataString("%' OR 1=1; DROP TABLE LogsAuditoria;--"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var pagina = await r.Content.ReadFromJsonAsync<Pagina<LogAuditoriaDto>>(Json.Opcoes);
        Assert.Empty(pagina!.Itens);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/api/v1/marcacoes?de=2026-05-10&ate=2026-05-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/api/v1/auditoria?texto=ab")).StatusCode);
    }

    [SkippableFact]
    public async Task Corpo_acima_do_limite_retorna_413()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Operador");
        var enorme = "{\"nome\":\"" + new string('a', 1_100_000) + "\",\"cnpj\":\"x\"}";
        var r = await cliente.PostAsync("/api/v1/transportadoras", new StringContent(enorme, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.StatusCode);
    }

    [SkippableFact]
    public async Task Agendamento_duplicado_ou_no_passado_e_recusado()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Operador");
        var (t, v) = await NegativacaoIntegracaoTests.CriarFrotaAsync(cliente);
        var amanha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)).ToString("yyyy-MM-dd");
        var corpo = new { data = amanha, hora = "07:00", placa = v.Placa, transportadoraId = t.Id, terminalId = "tgpm", carga = "Soja" };

        Assert.Equal(HttpStatusCode.Created, (await cliente.PostAsJsonAsync("/api/v1/agendamentos", corpo)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await cliente.PostAsJsonAsync("/api/v1/agendamentos", corpo)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await cliente.PostAsJsonAsync("/api/v1/agendamentos", corpo with { })).StatusCode);
        var passado = new { data = "2020-01-01", hora = "07:00", placa = v.Placa, transportadoraId = t.Id, terminalId = "tgpm", carga = "Soja" };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await cliente.PostAsJsonAsync("/api/v1/agendamentos", passado)).StatusCode);
    }

    // ---------- Upload ----------

    [SkippableFact]
    public async Task Upload_valida_extensao_tipo_e_conteudo_registro_a_registro()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");

        async Task<HttpResponseMessage> Enviar(string nome, string tipo, string conteudo)
        {
            var form = new MultipartFormDataContent();
            var arquivo = new StringContent(conteudo, Encoding.UTF8);
            arquivo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
            form.Add(arquivo, "arquivo", nome);
            return await gestor.PostAsync("/api/v1/marcacoes/importar/tgpm", form);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar("dados.exe", "application/json", "[]")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar("dados.json", "text/html", "[]")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar("dados.json", "application/json", "<html>")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar("../../etc/passwd.json", "application/json", "")).StatusCode);

        var id = $"U{Random.Shared.Next(10_000_000, 99_999_999)}";
        var json = $$"""
        [
          {"id":"{{id}}","senha":"1","convenio":"TGPM - SOJA","operador":"TGPM","carga":"Soja","ciclo":1,"marcadoEm":"2026-06-01T08:00:00","liberadoEm":"2026-06-01T10:00:00"},
          {"id":"{{id}}X","senha":"1","convenio":"{{new string('c', 500)}}","operador":"TGPM","carga":"Soja","ciclo":1,"marcadoEm":"2026-06-01T08:00:00","liberadoEm":"2026-06-01T10:00:00"},
          {"id":"{{id}}Y","senha":"1","convenio":"TGPM","operador":"TGPM","carga":"Soja","ciclo":1,"marcadoEm":"2026-06-01T08:00:00","liberadoEm":"2026-05-01T10:00:00"}
        ]
        """;
        var r = await Enviar("../../../teste.json", "application/json", json);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var resultado = await r.Content.ReadFromJsonAsync<ResultadoImportacaoDto>(Json.Opcoes);
        Assert.Equal(1, resultado!.Inseridos);
        Assert.Equal(2, resultado.Rejeitados);
        Assert.Equal(2, resultado.Erros.Count);
        Assert.Equal("teste.json", resultado.Arquivo); // caminho descartado

        // Reenviar o mesmo arquivo não duplica nada.
        var de_novo = await (await Enviar("teste.json", "application/json", json)).Content.ReadFromJsonAsync<ResultadoImportacaoDto>(Json.Opcoes);
        Assert.Equal(0, de_novo!.Inseridos);
        Assert.Equal(1, de_novo.IgnoradosJaExistentes);
    }

    // ---------- Headers, erros, observabilidade ----------

    [SkippableFact]
    public async Task Respostas_tem_headers_de_seguranca_e_request_id_sem_vazar_servidor()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var c = api.Factory.CreateClient();
        var r = await c.GetAsync("/api/v1/transportadoras");

        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'none'", r.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(r.Headers.Contains("X-Request-Id"));
        Assert.False(r.Headers.Contains("Server"));
        Assert.False(r.Headers.Contains("X-Powered-By"));

        var inexistente = await (await api.ClienteAsync("Auditor")).GetAsync("/api/v1/veiculos/999999");
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal("application/problem+json", inexistente.Content.Headers.ContentType!.MediaType);
        var problema = System.Text.Json.JsonDocument.Parse(await inexistente.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(inexistente.Headers.GetValues("X-Request-Id").Single(), problema.GetProperty("traceId").GetString());

        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health/ready")).StatusCode);
    }

    [SkippableFact]
    public async Task CORS_so_libera_a_origem_configurada()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var factory = api.CriarFactory(new Dictionary<string, string?> { ["Cors:Origens:0"] = "https://barcalog.vercel.app" });
        var c = factory.CreateClient();

        async Task<string?> Preflight(string origem)
        {
            var req = new HttpRequestMessage(HttpMethod.Options, "/api/v1/ocorrencias");
            req.Headers.Add("Origin", origem);
            req.Headers.Add("Access-Control-Request-Method", "POST");
            var r = await c.SendAsync(req);
            return r.Headers.TryGetValues("Access-Control-Allow-Origin", out var v) ? v.Single() : null;
        }

        Assert.Equal("https://barcalog.vercel.app", await Preflight("https://barcalog.vercel.app"));
        Assert.Null(await Preflight("https://site-malicioso.example"));
    }

    // ---------- LGPD ----------

    [SkippableFact]
    public async Task Anonimizar_condutor_remove_o_nome_sem_copiar_pro_log()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        var (t, v) = await NegativacaoIntegracaoTests.CriarFrotaAsync(gestor);
        var nome = $"Maria Exemplo {Dados.Proximo()}";
        var condutor = await (await gestor.PostAsJsonAsync("/api/v1/condutores", new { nome, transportadoraId = t.Id, placaVinculada = v.Placa }))
            .Content.ReadFromJsonAsync<CondutorDto>(Json.Opcoes);
        var amanha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd");
        await gestor.PostAsJsonAsync("/api/v1/agendamentos", new { data = amanha, hora = "09:00", placa = v.Placa, transportadoraId = t.Id, terminalId = "tgpm", carga = "Soja", motorista = nome });

        var exportado = await gestor.GetFromJsonAsync<DadosPessoaisCondutorDto>($"/api/v1/condutores/{condutor!.Id}/dados-pessoais", Json.Opcoes);
        Assert.Equal(nome, exportado!.Condutor.Nome);
        Assert.Single(exportado.Agendamentos);

        var anon = await (await gestor.PostAsync($"/api/v1/condutores/{condutor.Id}/anonimizar", null)).Content.ReadFromJsonAsync<CondutorDto>(Json.Opcoes);
        Assert.StartsWith("Condutor anonimizado", anon!.Nome);
        Assert.Null(anon.PlacaVinculada);
        Assert.False(await api.NoBancoAsync(db => db.Agendamentos.AnyAsync(a => a.Motorista == nome)));

        var log = await api.NoBancoAsync(db => db.LogsAuditoria.OrderByDescending(l => l.Id).FirstAsync(l => l.Acao == "Condutor anonimizado (LGPD)"));
        Assert.DoesNotContain(nome, log.Detalhes);
    }

    // ---------- Configuração de produção ----------

    [Fact]
    public void Producao_nao_sobe_com_segredos_de_desenvolvimento()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:BarcaLog"] = "Server=x;Database=y",
            ["Jwt:Chave"] = "DEV-ONLY-barcalog-chave-jwt-local-troque-em-producao-0123456789",
            ["Seguranca:ChaveCriptografia"] = ValidacaoConfiguracao.ChaveCriptografiaDev,
            ["Seguranca:ExigirMfaParaGestor"] = "false",
            ["Integracao:ApiKeys:0:Sistema"] = "x",
            ["Integracao:ApiKeys:0:ChaveSha256"] = ValidacaoConfiguracao.HashApiKeyDev,
            ["Seed:Usuarios:0:Email"] = "a@b.c",
            ["Seed:DadosExemplo"] = "true",
            ["Cors:Origens:0"] = "http://localhost:5173"
        }).Build();

        var erros = ValidacaoConfiguracao.Validar(config, ehProducao: true);

        Assert.Equal(7, erros.Count);
    }

    [Fact]
    public void Producao_com_configuracao_correta_passa()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:BarcaLog"] = "Server=x;Database=y",
            ["Jwt:Chave"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)),
            ["Seguranca:ChaveCriptografia"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            ["Cors:Origens:0"] = "https://barcalog.vercel.app"
        }).Build();

        Assert.Empty(ValidacaoConfiguracao.Validar(config, ehProducao: true));
    }
}
