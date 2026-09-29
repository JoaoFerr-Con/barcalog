using System.Net.Http.Headers;
using System.Net.Http.Json;
using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Dtos;
using BarcaLog.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace BarcaLog.IntegrationTests;

/// <summary>
/// Sobe a API inteira (WebApplicationFactory) contra um SQL Server REAL, num
/// banco descartável criado pelas migrations e apagado no fim.
///
/// Servidor: variável de ambiente BARCALOG_TESTES_SQL (connection string sem
/// Database). Sem ela, tenta o LocalDB no Windows. Se não houver SQL Server
/// acessível, os testes são pulados (Skip) com a mensagem explicando o porquê.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string SenhaUsuarios = "Teste@12345-Seguro";
    public const string ApiKey = "chave-integracao-testes-0001";

    private readonly string? _servidor = Environment.GetEnvironmentVariable("BARCALOG_TESTES_SQL")
        ?? (OperatingSystem.IsWindows() ? @"Server=(localdb)\mssqllocaldb;Trusted_Connection=True;TrustServerCertificate=True" : null);
    private readonly string _banco = $"BarcaLog_Testes_{Guid.NewGuid():N}";
    private readonly List<WebApplicationFactory<Program>> _factories = [];
    private WebApplicationFactory<Program>? _factory;

    public bool BancoDisponivel { get; private set; }
    public string MotivoIndisponivel { get; private set; } = "";

    public WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException(MotivoIndisponivel);

    private string ConnectionString => new SqlConnectionStringBuilder(_servidor) { InitialCatalog = _banco }.ConnectionString;

    public async Task InitializeAsync()
    {
        if (_servidor is null)
        {
            MotivoIndisponivel = "Defina BARCALOG_TESTES_SQL (ex.: Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True).";
            return;
        }
        try
        {
            await using var conexao = new SqlConnection(new SqlConnectionStringBuilder(_servidor) { InitialCatalog = "master", ConnectTimeout = 5 }.ConnectionString);
            await conexao.OpenAsync();
        }
        catch (Exception ex)
        {
            MotivoIndisponivel = $"SQL Server indisponível ({ex.Message}).";
            return;
        }
        _factory = CriarFactory();
        _ = _factory.Server; // força o startup (migrations + seed)
        BancoDisponivel = true;
    }

    /// <summary>Nova instância da API sobre o MESMO banco, com configurações extras (ex.: limite de login baixo).</summary>
    public WebApplicationFactory<Program> CriarFactory(IDictionary<string, string?>? extras = null, string ambiente = "Testes")
    {
        var f = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment(ambiente);
            // UseSetting entra na configuração antes do Program.cs ler Jwt/ConnectionStrings.
            b.UseSetting("ConnectionStrings:BarcaLog", ConnectionString);
            b.UseSetting("Jwt:Chave", "chave-jwt-somente-para-testes-de-integracao-0123456789");
            b.UseSetting("Seguranca:ChaveCriptografia", Convert.ToBase64String("chave-aes-somente-testes-32bytes"u8.ToArray()));
            b.UseSetting("Seguranca:ExigirMfaParaGestor", "false");
            b.UseSetting("Integracao:ApiKeys:0:Sistema", "Testes");
            b.UseSetting("Integracao:ApiKeys:0:ChaveSha256", ApiKeyAuthenticationHandler.Hash(ApiKey));
            b.UseSetting("LimitesRequisicao:LoginPorMinutoPorIp", "100000");
            b.UseSetting("LimitesRequisicao:GeralPorMinuto", "100000");
            b.UseSetting("LimitesRequisicao:IntegracaoPorMinuto", "100000");
            b.UseSetting("LimitesRequisicao:ImportacaoPorHora", "1000");
            b.UseSetting("Operacao:IntervaloMinimoRecargaSegundos", "0");
            b.UseSetting("Seed:AplicarMigrations", "true");
            b.UseSetting("Seed:DadosExemplo", "true");
            b.UseSetting("Seed:ImportarMarcacoesSeVazio", "false");
            string[] papeis = ["Operador", "Gestor", "Auditor"];
            for (var i = 0; i < papeis.Length; i++)
            {
                b.UseSetting($"Seed:Usuarios:{i}:Nome", $"{papeis[i]} Testes");
                b.UseSetting($"Seed:Usuarios:{i}:Email", $"{papeis[i].ToLowerInvariant()}@testes.local");
                b.UseSetting($"Seed:Usuarios:{i}:Senha", SenhaUsuarios);
                b.UseSetting($"Seed:Usuarios:{i}:Papel", papeis[i]);
            }
            foreach (var (k, v) in extras ?? new Dictionary<string, string?>()) b.UseSetting(k, v);
        });
        _factories.Add(f);
        return f;
    }

    public Task<HttpClient> ClienteAsync(string papel) => ClienteAsync(Factory, $"{papel.ToLowerInvariant()}@testes.local", SenhaUsuarios);

    public static async Task<HttpClient> ClienteAsync(WebApplicationFactory<Program> factory, string email, string senha, string? codigoMfa = null)
    {
        var cliente = factory.CreateClient();
        var login = await LoginAsync(cliente, email, senha, codigoMfa);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        return cliente;
    }

    public static async Task<LoginRespostaDto> LoginAsync(HttpClient cliente, string email, string senha, string? codigoMfa = null)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, senha, codigoMfa });
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<LoginRespostaDto>(Json.Opcoes))!;
    }

    /// <summary>Acesso direto ao banco pra conferir o que foi realmente persistido.</summary>
    public async Task<T> NoBancoAsync<T>(Func<BarcaLogDbContext, Task<T>> consulta)
    {
        await using var escopo = Factory.Services.CreateAsyncScope();
        return await consulta(escopo.ServiceProvider.GetRequiredService<BarcaLogDbContext>());
    }

    public async Task DisposeAsync()
    {
        if (_factory is null) return;
        foreach (var f in _factories) await f.DisposeAsync();
        SqlConnection.ClearAllPools();
        await using var conexao = new SqlConnection(new SqlConnectionStringBuilder(_servidor) { InitialCatalog = "master" }.ConnectionString);
        await conexao.OpenAsync();
        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = $"IF DB_ID('{_banco}') IS NOT NULL BEGIN ALTER DATABASE [{_banco}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_banco}]; END";
        await cmd.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Nome)]
public class ColecaoApi : ICollectionFixture<ApiFixture>
{
    public const string Nome = "API + SQL Server";
}

public static class Json
{
    public static readonly System.Text.Json.JsonSerializerOptions Opcoes = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(), new BarcaLog.Api.Infra.ConversorTimeOnly() }
    };
}

/// <summary>Geradores de dados de teste válidos e únicos.</summary>
public static class Dados
{
    private static int _seq = Random.Shared.Next(100, 900) * 1000;

    public static int Proximo() => Interlocked.Increment(ref _seq);

    /// <summary>CNPJ numérico válido e único.</summary>
    public static string Cnpj()
    {
        var baseNum = $"{Proximo():D8}0001";
        return BarcaLog.Domain.Regras.Cnpj.Normalizar(baseNum + BarcaLog.Domain.Regras.Cnpj.CalcularDigitos(baseNum));
    }

    /// <summary>Placa padrão antigo única (ex.: TST-1234).</summary>
    public static string Placa()
    {
        var n = Proximo();
        var letras = new string([(char)('A' + n / 10000 % 26), (char)('A' + n / 260000 % 26), 'Z']);
        return $"{letras}-{n % 10000:D4}";
    }
}
