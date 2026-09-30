using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BarcaLog.IntegrationTests;

/// <summary>Tentativas de quebrar login, sessão, senha e MFA.</summary>
[Collection(ColecaoApi.Nome)]
public class AutenticacaoTests(ApiFixture api)
{
    private const string SenhaForte = "Porto-Barcarena#2026x";

    /// <summary>Cria usuário pela API (senha provisória) e já troca a senha, devolvendo o e-mail.</summary>
    private async Task<string> CriarUsuarioAsync(string papel = "Operador", bool trocarSenha = true)
    {
        var gestor = await api.ClienteAsync("Gestor");
        var email = $"u{Dados.Proximo()}@testes.local";
        var r = await gestor.PostAsJsonAsync("/api/v1/usuarios", new { nome = "Fulana de Tal", email, senha = "Provisoria-Inicial#99", papel });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        if (!trocarSenha) return email;
        var cliente = await ApiFixture.ClienteAsync(api.Factory, email, "Provisoria-Inicial#99");
        var troca = await cliente.PostAsJsonAsync("/api/v1/auth/trocar-senha", new { senhaAtual = "Provisoria-Inicial#99", novaSenha = SenhaForte });
        Assert.Equal(HttpStatusCode.NoContent, troca.StatusCode);
        return email;
    }

    private static Task<HttpResponseMessage> Login(HttpClient c, string email, string senha, string? codigo = null) =>
        c.PostAsJsonAsync("/api/v1/auth/login", new { email, senha, codigoMfa = codigo });

    [SkippableFact]
    public async Task Resposta_de_login_nao_revela_se_o_email_existe()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var c = api.Factory.CreateClient();
        var inexistente = await Login(c, "ninguem@testes.local", "Qualquer-Senha#123");
        var senhaErrada = await Login(c, "auditor@testes.local", "Senha-Errada#12345");
        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, senhaErrada.StatusCode);
        Assert.Equal(await Titulo(inexistente), await Titulo(senhaErrada));
    }

    [SkippableFact]
    public async Task Cinco_falhas_bloqueiam_a_conta_mesmo_com_a_senha_certa_depois()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var email = await CriarUsuarioAsync();
        var c = api.Factory.CreateClient();
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await Login(c, email, "Senha-Errada#12345")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(c, email, SenhaForte)).StatusCode);
        var bloqueadoAte = await api.NoBancoAsync(db => db.Usuarios.Where(u => u.Email == email).Select(u => u.BloqueadoAte).SingleAsync());
        Assert.NotNull(bloqueadoAte);

        var logs = await (await api.ClienteAsync("Auditor")).GetFromJsonAsync<Pagina<LogAuditoriaDto>>($"/api/v1/auditoria?texto={email}", Json.Opcoes);
        Assert.Contains(logs!.Itens, l => l.Acao == "Conta bloqueada por excesso de tentativas");
        Assert.DoesNotContain(logs.Itens, l => l.Detalhes.Contains("Senha-Errada") || l.Detalhes.Contains("AQAAAA")); // nem senha nem hash no log

        // Gestor desbloqueia.
        var gestor = await api.ClienteAsync("Gestor");
        var id = await api.NoBancoAsync(db => db.Usuarios.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        Assert.Equal(HttpStatusCode.OK, (await gestor.PostAsync($"/api/v1/usuarios/{id}/reativar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(c, email, SenhaForte)).StatusCode);
    }

    [SkippableFact]
    public async Task Senha_provisoria_gera_token_restrito_que_so_troca_a_senha()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var email = await CriarUsuarioAsync(trocarSenha: false);
        var c = api.Factory.CreateClient();
        var login = await ApiFixture.LoginAsync(c, email, "Provisoria-Inicial#99");
        Assert.Equal("trocar-senha", login.Restricao);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/transportadoras")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/auth/me")).StatusCode);

        // Senha fraca é recusada pela política.
        var fraca = await c.PostAsJsonAsync("/api/v1/auth/trocar-senha", new { senhaAtual = "Provisoria-Inicial#99", novaSenha = "123456789012" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, fraca.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync("/api/v1/auth/trocar-senha", new { senhaAtual = "Provisoria-Inicial#99", novaSenha = SenhaForte })).StatusCode);
        // O token antigo (restrito) morreu junto.
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/auth/me")).StatusCode);

        var novo = await ApiFixture.LoginAsync(api.Factory.CreateClient(), email, SenhaForte);
        Assert.Null(novo.Restricao);
    }

    [SkippableFact]
    public async Task Logout_revoga_o_token_imediatamente()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var email = await CriarUsuarioAsync();
        var c = await ApiFixture.ClienteAsync(api.Factory, email, SenhaForte);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/transportadoras")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/v1/auth/logout", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/transportadoras")).StatusCode);
    }

    [SkippableFact]
    public async Task Desativar_usuario_derruba_a_sessao_dele()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var email = await CriarUsuarioAsync();
        var vitima = await ApiFixture.ClienteAsync(api.Factory, email, SenhaForte);
        var gestor = await api.ClienteAsync("Gestor");
        var id = await api.NoBancoAsync(db => db.Usuarios.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

        Assert.Equal(HttpStatusCode.OK, (await gestor.PostAsync($"/api/v1/usuarios/{id}/desativar", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await vitima.GetAsync("/api/v1/transportadoras")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(api.Factory.CreateClient(), email, SenhaForte)).StatusCode);
    }

    [SkippableFact]
    public async Task Gestor_nao_pode_se_desativar_nem_deixar_o_sistema_sem_gestor()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var gestor = await api.ClienteAsync("Gestor");
        var me = await gestor.GetFromJsonAsync<UsuarioDto>("/api/v1/auth/me", Json.Opcoes);

        var r = await gestor.PostAsync($"/api/v1/usuarios/{me!.Id}/desativar", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }

    [SkippableFact]
    public async Task Troca_de_papel_exige_novo_login_e_nao_escala_privilegio()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var email = await CriarUsuarioAsync("Auditor");
        var auditor = await ApiFixture.ClienteAsync(api.Factory, email, SenhaForte);
        var id = await api.NoBancoAsync(db => db.Usuarios.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

        // Auditor não consegue se promover.
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PutAsJsonAsync($"/api/v1/usuarios/{id}/papel", new { papel = "Gestor" })).StatusCode);

        var gestor = await api.ClienteAsync("Gestor");
        Assert.Equal(HttpStatusCode.OK, (await gestor.PutAsJsonAsync($"/api/v1/usuarios/{id}/papel", new { papel = "Operador" })).StatusCode);
        // Token com o papel antigo não vale mais.
        Assert.Equal(HttpStatusCode.Unauthorized, (await auditor.GetAsync("/api/v1/transportadoras")).StatusCode);
    }

    [SkippableFact]
    public async Task MFA_exige_codigo_e_nao_aceita_o_mesmo_codigo_duas_vezes()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var email = await CriarUsuarioAsync();
        var c = await ApiFixture.ClienteAsync(api.Factory, email, SenhaForte);

        var config = await (await c.PostAsJsonAsync("/api/v1/auth/mfa/configurar", new { senhaAtual = SenhaForte })).Content.ReadFromJsonAsync<ConfiguracaoMfaDto>(Json.Opcoes);
        Assert.StartsWith("otpauth://totp/", config!.UriOtpauth);
        // Segredo não fica em claro no banco.
        var cifrado = await api.NoBancoAsync(db => db.Usuarios.Where(u => u.Email == email).Select(u => u.MfaSegredoCifrado).SingleAsync());
        Assert.StartsWith("v1:", cifrado);
        Assert.DoesNotContain(config.Segredo, cifrado);

        var codigoErrado = await c.PostAsJsonAsync("/api/v1/auth/mfa/ativar", new { codigo = "000000" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, codigoErrado.StatusCode);

        var segredo = Base32.Decodificar(config.Segredo);
        var passoAtual = Totp.PassoAtual(DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync("/api/v1/auth/mfa/ativar", new { codigo = Totp.Calcular(segredo, passoAtual) })).StatusCode);

        var anon = api.Factory.CreateClient();
        var semCodigo = await Login(anon, email, SenhaForte);
        Assert.Equal(HttpStatusCode.Unauthorized, semCodigo.StatusCode);
        Assert.Equal("mfa_requerido", (await Corpo(semCodigo)).GetProperty("codigo").GetString());

        // Código do passo seguinte (o do passo atual já foi consumido na ativação → replay recusado).
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(anon, email, SenhaForte, Totp.Calcular(segredo, passoAtual))).StatusCode);
        var proximo = Totp.Calcular(segredo, passoAtual + 1);
        Assert.Equal(HttpStatusCode.OK, (await Login(anon, email, SenhaForte, proximo)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(anon, email, SenhaForte, proximo)).StatusCode); // replay
    }

    [SkippableFact]
    public async Task Gestor_sem_MFA_so_consegue_configurar_o_MFA_quando_obrigatorio()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var factory = api.CriarFactory(new Dictionary<string, string?> { ["Seguranca:ExigirMfaParaGestor"] = "true" });
        var login = await ApiFixture.LoginAsync(factory.CreateClient(), "gestor@testes.local", ApiFixture.SenhaUsuarios);
        Assert.Equal("configurar-mfa", login.Restricao);

        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [SkippableFact]
    public async Task Excesso_de_tentativas_de_login_por_IP_recebe_429()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var factory = api.CriarFactory(new Dictionary<string, string?> { ["LimitesRequisicao:LoginPorMinutoPorIp"] = "3" });
        var c = factory.CreateClient();
        for (var i = 0; i < 3; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await Login(c, "x@testes.local", "Qualquer-Senha#1")).StatusCode);

        var bloqueada = await Login(c, "x@testes.local", "Qualquer-Senha#1");

        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueada.StatusCode);
        Assert.True(bloqueada.Headers.Contains("Retry-After"));
    }

    [SkippableFact]
    public async Task Token_adulterado_ou_com_alg_none_e_recusado()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var login = await ApiFixture.LoginAsync(api.Factory.CreateClient(), "auditor@testes.local", ApiFixture.SenhaUsuarios);
        var partes = login.Token.Split('.');
        var payload = System.Text.Encoding.UTF8.GetString(Base64Url(partes[1])).Replace("\"Auditor\"", "\"Gestor\"");
        var adulterado = $"{partes[0]}.{ToBase64Url(payload)}.{partes[2]}";
        var semAssinatura = $"{ToBase64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}")}.{ToBase64Url(payload)}.";

        foreach (var token in new[] { adulterado, semAssinatura })
        {
            var c = api.Factory.CreateClient();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/usuarios")).StatusCode);
        }
    }

    private static byte[] Base64Url(string s) => Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/').PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    private static string ToBase64Url(string s) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static async Task<JsonElement> Corpo(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
    private static async Task<string?> Titulo(HttpResponseMessage r) => (await Corpo(r)).GetProperty("title").GetString();
}
