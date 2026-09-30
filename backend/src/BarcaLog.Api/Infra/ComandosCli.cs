using BarcaLog.Application.Dtos;
using BarcaLog.Application.Servicos;
using BarcaLog.Domain.Enums;
using BarcaLog.Infrastructure.Persistencia;
using BarcaLog.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace BarcaLog.Api.Infra;

/// <summary>
/// Tarefas administrativas sem subir o servidor HTTP:
///   dotnet run -- migrar
///   dotnet run -- seed
///   dotnet run -- importar-marcacoes [diretorio]
///   dotnet run -- criar-usuario &lt;email&gt; &lt;nome&gt; &lt;Operador|Gestor|Auditor&gt;   (senha via env BARCALOG_SENHA)
///   dotnet run -- gerar-api-key &lt;sistema&gt;     (imprime a chave UMA vez + o hash pra config)
///   dotnet run -- gerar-chave                     (chave aleatória pra Jwt:Chave / Seguranca:ChaveCriptografia)
/// </summary>
public static class ComandosCli
{
    private static readonly string[] Comandos = ["migrar", "seed", "importar-marcacoes", "criar-usuario", "gerar-api-key", "gerar-chave"];

    /// <summary>Comandos que só geram texto e não precisam de configuração completa.</summary>
    private static readonly string[] SemServidor = ["gerar-api-key", "gerar-chave"];

    public static bool EhComando(string[] args) => args.Length > 0 && Comandos.Contains(args[0]);

    public static bool EhComandoSemServidor(string[] args) => args.Length > 0 && SemServidor.Contains(args[0]);

    public static async Task<int> ExecutarAsync(WebApplication app, string[] args)
    {
        if (args[0] == "gerar-chave")
        {
            Console.WriteLine(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
            return 0;
        }
        if (args[0] == "gerar-api-key")
        {
            var sistema = args.Length > 1 ? args[1] : "Sistema";
            var chave = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Console.WriteLine($"Chave (entregue ao sistema '{sistema}' por canal seguro; não é mostrada de novo):\n  {chave}");
            Console.WriteLine($"Configuração da API (só o hash):\n  Integracao__ApiKeys__N__Sistema={sistema}\n  Integracao__ApiKeys__N__ChaveSha256={Seguranca.ApiKeyAuthenticationHandler.Hash(chave)}");
            return 0;
        }

        await using var escopo = app.Services.CreateAsyncScope();
        var sp = escopo.ServiceProvider;
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("BarcaLog.Cli");
        try
        {
            switch (args[0])
            {
                case "migrar":
                    await sp.GetRequiredService<BarcaLogDbContext>().Database.MigrateAsync();
                    log.LogInformation("Migrations aplicadas.");
                    break;
                case "seed":
                    await sp.GetRequiredService<DbSeeder>().SemearDadosExemploAsync();
                    break;
                case "importar-marcacoes":
                    var dir = args.Length > 1
                        ? Path.GetFullPath(args[1])
                        : Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, app.Configuration["Seed:DiretorioDatasets"] ?? "../../../src/data/datasets"));
                    foreach (var r in await sp.GetRequiredService<ImportacaoMarcacoesServico>().ImportarDiretorioAsync(dir))
                        log.LogInformation("{Arquivo}: {Lidos} lidos, {Inseridos} inseridos, {Ignorados} já existiam", r.Arquivo, r.Lidos, r.Inseridos, r.IgnoradosJaExistentes);
                    break;
                case "criar-usuario":
                    if (args.Length < 4 || !Enum.TryParse<PapelUsuario>(args[3], true, out var papel))
                    {
                        log.LogError("Uso: criar-usuario <email> <nome> <Operador|Gestor|Auditor>  (senha em BARCALOG_SENHA)");
                        return 2;
                    }
                    var senha = Environment.GetEnvironmentVariable("BARCALOG_SENHA");
                    if (string.IsNullOrWhiteSpace(senha) || senha.Length < 8)
                    {
                        log.LogError("Defina a senha (mín. 8 caracteres) na variável de ambiente BARCALOG_SENHA.");
                        return 2;
                    }
                    // Criado pelo administrador do servidor: senha definitiva (Gestor ainda precisa cadastrar MFA no 1º login).
                    var u = await sp.GetRequiredService<UsuarioServico>().CriarAsync(new CriarUsuarioRequest { Email = args[1], Nome = args[2], Papel = papel, Senha = senha }, senhaProvisoria: false);
                    log.LogInformation("Usuário {Email} criado com papel {Papel}.", u.Email, u.Papel);
                    break;
            }
            return 0;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Falha ao executar {Comando}", args[0]);
            return 1;
        }
    }
}
