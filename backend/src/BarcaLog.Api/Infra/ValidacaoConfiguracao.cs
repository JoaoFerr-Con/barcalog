using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Configuracao;
using BarcaLog.Infrastructure.Seed;

namespace BarcaLog.Api.Infra;

/// <summary>
/// Trava de segurança na subida: fora de Development a API se recusa a
/// iniciar com chave de desenvolvimento, seed de usuários/dados de exemplo,
/// CORS sem HTTPS ou sem connection string. Melhor não subir do que subir
/// inseguro sem ninguém perceber.
/// </summary>
public static class ValidacaoConfiguracao
{
    /// <summary>Marcador obrigatório de todo segredo de desenvolvimento versionado no repositório.</summary>
    public const string MarcadorDev = "DEV-ONLY";

    public static IReadOnlyList<string> Validar(IConfiguration config, bool ehProducao)
    {
        var erros = new List<string>();
        var jwt = config.GetSection(JwtOptions.Secao).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.Chave) || jwt.Chave.Length < 32)
            erros.Add("Jwt:Chave ausente ou curta (mínimo 32 caracteres aleatórios).");

        var seg = config.GetSection(SegurancaOptions.Secao).Get<SegurancaOptions>() ?? new SegurancaOptions();
        if (string.IsNullOrWhiteSpace(seg.ChaveCriptografia))
            erros.Add("Seguranca:ChaveCriptografia ausente (Base64 de 32 bytes; gere com 'dotnet run -- gerar-chave').");

        if (string.IsNullOrWhiteSpace(config.GetConnectionString(BarcaLog.Infrastructure.DependencyInjection.NomeConnectionString)))
            erros.Add("ConnectionStrings:BarcaLog ausente.");

        if (!ehProducao) return erros;

        if (jwt.Chave.Contains(MarcadorDev, StringComparison.OrdinalIgnoreCase)) erros.Add("Jwt:Chave é a chave de desenvolvimento.");
        if (seg.ChaveCriptografia == ChaveCriptografiaDev) erros.Add("Seguranca:ChaveCriptografia é a chave de desenvolvimento.");
        if (!seg.ExigirMfaParaGestor) erros.Add("Seguranca:ExigirMfaParaGestor deve ser true em produção.");

        var integracao = config.GetSection(IntegracaoOptions.Secao).Get<IntegracaoOptions>() ?? new IntegracaoOptions();
        if (integracao.ApiKeys.Any(k => k.ChaveSha256?.ToLowerInvariant() == HashApiKeyDev))
            erros.Add("Integracao:ApiKeys contém a chave de desenvolvimento.");

        var seed = config.GetSection(SeedOptions.Secao).Get<SeedOptions>() ?? new SeedOptions();
        if (seed.Usuarios.Count > 0) erros.Add("Seed:Usuarios deve ficar vazio em produção (crie usuários com 'criar-usuario').");
        if (seed.DadosExemplo) erros.Add("Seed:DadosExemplo deve ser false em produção.");

        var origens = config.GetSection("Cors:Origens").Get<string[]>() ?? [];
        if (origens.Any(o => o == "*" || !o.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            erros.Add("Cors:Origens em produção só aceita origens https:// explícitas.");
        return erros;
    }

    /// <summary>Valores do appsettings.Development.json (públicos no Git — por isso proibidos em produção).</summary>
    public const string ChaveCriptografiaDev = "REVWLU9OTFktYmFyY2Fsb2ctYWVzLTI1Ni1rZXktMDE=";
    public const string HashApiKeyDev = "8843a1b07d93955dd1f613ad5669a2e7dd122b123cb62245b65527e62dcf1f5b";
}
