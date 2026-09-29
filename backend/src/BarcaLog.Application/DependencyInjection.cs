using BarcaLog.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;

namespace BarcaLog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RelogioOperacional>();
        services.AddSingleton<CacheMarcacoes>();
        services.AddScoped<TransportadoraServico>();
        services.AddScoped<VeiculoServico>();
        services.AddScoped<CondutorServico>();
        services.AddScoped<OcorrenciaServico>();
        services.AddScoped<ContestacaoServico>();
        services.AddScoped<AuditoriaServico>();
        services.AddScoped<AgendamentoServico>();
        services.AddScoped<PortariaServico>();
        services.AddScoped<MetricasServico>();
        services.AddScoped<MarcacaoServico>();
        services.AddScoped<ImportacaoMarcacoesServico>();
        services.AddScoped<IntegracaoServico>();
        services.AddScoped<UsuarioServico>();
        services.AddScoped<AutenticacaoServico>();
        return services;
    }
}
