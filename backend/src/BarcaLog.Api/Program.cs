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
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Camadas ----------
builder.Services.Configure<OperacaoOptions>(config.GetSection(OperacaoOptions.Secao));
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(config);
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtualHttp>();

// ---------- Autenticação: JWT (usuários) + API Key (sistemas) ----------
var jwt = config.GetSection(JwtOptions.Secao).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Chave) || jwt.Chave.Length < 32)
    throw new InvalidOperationException("Jwt:Chave não configurada (mínimo 32 caracteres). Em produção use a variável de ambiente Jwt__Chave.");
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Chave)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.Esquema, null);

builder.Services.AddAuthorization(o =>
{
    string[] todosPapeis = Enum.GetNames<PapelUsuario>();
    o.AddPolicy(Politicas.Leitura, p => p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireRole(todosPapeis));
    o.AddPolicy(Politicas.Escrita, p => p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireRole(nameof(PapelUsuario.Operador), nameof(PapelUsuario.Gestor)));
    o.AddPolicy(Politicas.Gestao, p => p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireRole(nameof(PapelUsuario.Gestor)));
    o.AddPolicy(Politicas.Integracao, p => p.AddAuthenticationSchemes(ApiKeyAuthenticationHandler.Esquema)
        .RequireClaim(ApiKeyAuthenticationHandler.ClaimSistema));
});

// ---------- MVC / erros ----------
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.Converters.Add(new ConversorTimeOnly());
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorExcecoes>();
builder.Services.AddHealthChecks();

// ---------- CORS (Vite dev server + Vercel) ----------
const string PoliticaCors = "Frontend";
var origens = config.GetSection("Cors:Origens").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy(PoliticaCors, p => p
    .WithOrigins(origens)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BarcaLog API",
        Version = "v1",
        Description = "Backend do BarcaLog — gestão logística do Porto de Barcarena (PA). " +
                      "Usuários: POST /api/auth/login e use o token no botão Authorize (Bearer). " +
                      "Sistemas integrados: cabeçalho X-Api-Key em /api/integracao."
    });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Token obtido em POST /api/auth/login."
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

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment() || config.GetValue<bool>("Swagger:Habilitado"))
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "BarcaLog API v1");
        o.DocumentTitle = "BarcaLog API";
    });
}

if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors(PoliticaCors);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

await app.RunAsync();
return 0;

/// <summary>Exposto para WebApplicationFactory nos testes de integração.</summary>
public partial class Program;
