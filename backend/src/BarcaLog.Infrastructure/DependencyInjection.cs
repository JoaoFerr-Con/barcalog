using BarcaLog.Application.Abstracoes;
using BarcaLog.Infrastructure.Auditoria;
using BarcaLog.Infrastructure.Persistencia;
using BarcaLog.Infrastructure.Persistencia.Repositorios;
using BarcaLog.Infrastructure.Seed;
using BarcaLog.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BarcaLog.Infrastructure;

public static class DependencyInjection
{
    public const string NomeConnectionString = "BarcaLog";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(NomeConnectionString);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"Connection string '{NomeConnectionString}' não configurada. Defina ConnectionStrings:{NomeConnectionString} " +
                $"(appsettings.{{Ambiente}}.json, user-secrets ou variável de ambiente ConnectionStrings__{NomeConnectionString}).");

        services.AddScoped<IContextoAuditoria, ContextoAuditoria>();
        services.AddScoped<AuditoriaInterceptor>();
        services.AddDbContext<BarcaLogDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(BarcaLogDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(3);
                sql.CommandTimeout(30);
            });
            options.AddInterceptors(sp.GetRequiredService<AuditoriaInterceptor>());
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BarcaLogDbContext>());

        services.AddScoped<ITerminalRepositorio, TerminalRepositorio>();
        services.AddScoped<IMarcacaoRepositorio, MarcacaoRepositorio>();
        services.AddScoped<ITransportadoraRepositorio, TransportadoraRepositorio>();
        services.AddScoped<IVeiculoRepositorio, VeiculoRepositorio>();
        services.AddScoped<ICondutorRepositorio, CondutorRepositorio>();
        services.AddScoped<IOcorrenciaRepositorio, OcorrenciaRepositorio>();
        services.AddScoped<IContestacaoRepositorio, ContestacaoRepositorio>();
        services.AddScoped<IAuditoriaRepositorio, AuditoriaRepositorio>();
        services.AddScoped<IAgendamentoRepositorio, AgendamentoRepositorio>();
        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();

        services.AddSingleton<IHashSenha, HashSenha>();
        services.AddSingleton<IProtetorSegredos, ProtetorSegredosAesGcm>();
        services.AddSingleton<IValidadorSessao, ValidadorSessao>();
        services.AddScoped<Idempotencia.ServicoIdempotencia>();
        services.AddHostedService<Idempotencia.LimpezaIdempotencia>();
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.Secao));
        services.AddScoped<DbSeeder>();
        return services;
    }
}
