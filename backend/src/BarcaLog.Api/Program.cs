using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using BarcaLog.Api.Infra;
using BarcaLog.Api.Seguranca;
using BarcaLog.Application;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Configuracao;
using BarcaLog.Domain.Enums;
using BarcaLog.Infrastructure;
using BarcaLog.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Trava de configuração (antes de qualquer coisa) ----------
var errosConfig = ValidacaoConfiguracao.Validar(config, builder.Environment.IsProduction());
if (errosConfig.Count > 0 && !ComandosCli.EhComandoSemServidor(args))
    throw new InvalidOperationException("Configuração insegura ou incompleta:\n - " + string.Join("\n - ", errosConfig));

// ---------- Logs: JSON estruturado fora de Development ----------
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.UseUtcTimestamp = true;
        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    });
}

// ---------- Kestrel: limites e sem header "Server" ----------
builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;
    k.Limits.MaxRequestBodySize = LimiteCorpo.LimitePadrao; // 1 MB; importação sobe esse limite só no próprio endpoint
    k.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
});

// ---------- Camadas ----------
builder.Services.Configure<OperacaoOptions>(config.GetSection(OperacaoOptions.Secao));
builder.Services.Configure<SegurancaOptions>(config.GetSection(SegurancaOptions.Secao));
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(config);
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtualHttp>();

// ---------- Autenticação: JWT (usuários) + API Key (sistemas) ----------
var jwt = config.GetSection(JwtOptions.Secao).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.Configure<JwtOptions>(config.GetSection(JwtOptions.Secao));
builder.Services.Configure<IntegracaoOptions>(config.GetSection(IntegracaoOptions.Secao));
builder.Services.AddSingleton<IGeradorToken, GeradorTokenJwt>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Emissor,
            ValidateAudience = true,
            ValidAudience = jwt.Audiencia,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Chave.Length > 0 ? jwt.Chave : "chave-ausente-a-api-nao-sobe-assim")),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], // impede "alg": "none" / troca de algoritmo
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = ClaimTypes.Role
        };
        o.Events = new JwtBearerEvents
        {
            // Revogação: usuário desativado, logout, troca de senha/papel/MFA → token antigo morre.
            OnTokenValidated = async ctx =>
            {
                var p = ctx.Principal!;
                if (!int.TryParse(p.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ||
                    !int.TryParse(p.FindFirstValue(ClaimsBarcaLog.Versao), out var versao) ||
                    !await ctx.HttpContext.RequestServices.GetRequiredService<IValidadorSessao>().SessaoValidaAsync(id, versao, ctx.HttpContext.RequestAborted))
                {
                    ctx.Fail("Sessão encerrada.");
                }
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.Esquema, null);

builder.Services.AddAuthorization(o =>
{
    string[] todosPapeis = Enum.GetNames<PapelUsuario>();
    static bool SemRestricao(AuthorizationHandlerContext c) => !c.User.HasClaim(x => x.Type == ClaimsBarcaLog.Restricao);
    o.AddPolicy(Politicas.Autenticado, p => p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireRole(todosPapeis));
    o.AddPolicy(Politicas.Leitura, p => p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireRole(todosPapeis).RequireAssertion(SemRestricao));
    o.AddPolicy(Politicas.Escrita, p => p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireRole(nameof(PapelUsuario.Operador), nameof(PapelUsuario.Gestor)).RequireAssertion(SemRestricao));
    o.AddPolicy(Politicas.Gestao, p => p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireRole(nameof(PapelUsuario.Gestor)).RequireAssertion(SemRestricao));
    o.AddPolicy(Politicas.Integracao, p => p.AddAuthenticationSchemes(ApiKeyAuthenticationHandler.Esquema)
        .RequireClaim(ApiKeyAuthenticationHandler.ClaimSistema));
    // Endpoint sem [Authorize] explícito também exige login (seguro por padrão).
    o.FallbackPolicy = o.GetPolicy(Politicas.Leitura);
});

// ---------- Proteção contra abuso ----------
builder.Services.AddLimitesRequisicao(config);
builder.Services.AddRequestTimeouts(o =>
{
    o.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(30), TimeoutStatusCode = StatusCodes.Status504GatewayTimeout };
    o.AddPolicy(LimitesRequisicao.TimeoutImportacao, TimeSpan.FromMinutes(5));
});

// ---------- Proxy reverso (IP real para rate limit/log) ----------
if (config.GetValue<bool>("Proxy:Habilitado"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = 1;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
        // Só confia no X-Forwarded-For vindo das redes do proxy (senão qualquer um forja o IP).
        foreach (var rede in config.GetSection("Proxy:RedesConfiaveis").Get<string[]>() ?? [])
        {
            var partes = rede.Split('/');
            o.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse(partes[0]), int.Parse(partes[1])));
        }
    });
}

// ---------- MVC / erros ----------
builder.Services
    .AddControllers(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.ProducesAttribute("application/json")))
    .AddJsonOptions(o =>
    {
        // Enum só por nome: "nivel": 7 é rejeitado em vez de gravar valor inexistente.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        o.JsonSerializerOptions.Converters.Add(new ConversorTimeOnly());
        o.JsonSerializerOptions.MaxDepth = 16;
        // Erro de JSON não expõe nomes de tipos .NET na resposta.
        o.AllowInputFormatterExceptionMessages = false;
    });
// Todo erro leva o traceId (mesmo valor do X-Request-Id e do log) pra correlacionar sem expor detalhes.
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<TratadorExcecoes>();
builder.Services.AddHealthChecks().AddCheck<VerificacaoBanco>("banco", tags: ["ready"]);

// ---------- CORS (Vite dev server + Vercel) ----------
const string PoliticaCors = "Frontend";
var origens = config.GetSection("Cors:Origens").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy(PoliticaCors, p => p
    .WithOrigins(origens)
    .WithHeaders("Authorization", "Content-Type", IdempotenteAttribute.Cabecalho, LogRequisicoes.Cabecalho)
    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
    .WithExposedHeaders(LogRequisicoes.Cabecalho, "Retry-After", "Idempotent-Replayed", "Location")
    .SetPreflightMaxAge(TimeSpan.FromHours(1))));

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BarcaLog API",
        Version = "v1",
        Description = "Backend do BarcaLog — gestão logística do Porto de Barcarena (PA). " +
                      "Usuários: POST /api/v1/auth/login e use o token no botão Authorize (Bearer). " +
                      "Sistemas integrados: cabeçalho X-Api-Key em /api/v1/integracao. " +
                      "Operações que criam/alteram estado aceitam o cabeçalho Idempotency-Key."
    });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Token obtido em POST /api/v1/auth/login."
    });
    o.AddSecurityDefinition(ApiKeyAuthenticationHandler.Esquema, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = ApiKeyAuthenticationHandler.Cabecalho,
        Description = "Chave de integração sistema-a-sistema."
    });
    o.OperationFilter<FiltroSegurancaSwagger>();
    o.MapType<TimeOnly>(() => new OpenApiSchema { Type = "string", Format = "time", Example = new Microsoft.OpenApi.Any.OpenApiString("08:00") });
    o.SupportNonNullableReferenceTypes();
    o.CustomSchemaIds(t => t.FullName?.Replace("+", ".").Replace("BarcaLog.Application.", ""));
    foreach (var assembly in new[] { Assembly.GetExecutingAssembly(), typeof(BarcaLog.Infrastructure.DependencyInjection).Assembly, typeof(BarcaLog.Application.DependencyInjection).Assembly })
    {
        var xml = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
        if (File.Exists(xml)) o.IncludeXmlComments(xml, includeControllerXmlComments: true);
    }
});

var app = builder.Build();

// ---------- Comandos de linha (dotnet run -- <comando>) ----------
if (ComandosCli.EhComando(args))
    return await ComandosCli.ExecutarAsync(app, args);

// ---------- Migrations / seed conforme configuração do ambiente ----------
await using (var escopo = app.Services.CreateAsyncScope())
{
    await escopo.ServiceProvider.GetRequiredService<DbSeeder>().ExecutarAsync(app.Environment.ContentRootPath);
}

if (config.GetValue<bool>("Proxy:Habilitado")) app.UseForwardedHeaders();
app.UseMiddleware<LogRequisicoes>();
app.UseMiddleware<CabecalhosSeguranca>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment() || config.GetValue<bool>("Swagger:Habilitado"))
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "BarcaLog API v1");
        o.DocumentTitle = "BarcaLog API";
    });
}

app.UseRouting();
app.Use(LogRequisicoes.GuardarRota);
app.UseMiddleware<LimiteCorpo>();
app.UseCors(PoliticaCors);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseRequestTimeouts();

app.MapControllers();
// Liveness: o processo responde. Readiness: consegue falar com o banco. Sem detalhes na resposta.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
if (app.Environment.IsDevelopment())
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription().AllowAnonymous();

await app.RunAsync();
return 0;

/// <summary>Exposto para WebApplicationFactory nos testes de integração.</summary>
public partial class Program;
