using BarcaLog.Domain.Enums;

namespace BarcaLog.Infrastructure.Seed;

public class SeedOptions
{
    public const string Secao = "Seed";

    /// <summary>Aplica as migrations pendentes ao subir a API.</summary>
    public bool AplicarMigrations { get; set; }

    /// <summary>Popula o cadastro de exemplo (mesmos dados do negativacaoStore.js) se o banco estiver vazio.</summary>
    public bool DadosExemplo { get; set; }

    /// <summary>Importa os JSON reais de <see cref="DiretorioDatasets"/> se a tabela de marcações estiver vazia.</summary>
    public bool ImportarMarcacoesSeVazio { get; set; }

    /// <summary>Pasta com unitapajos.json / tgpm.json / hidrovias.json (relativa ao content root da API).</summary>
    public string DiretorioDatasets { get; set; } = "../../../src/data/datasets";

    /// <summary>Usuários criados se ainda não existir nenhum (apenas pra desenvolvimento).</summary>
    public List<UsuarioSeed> Usuarios { get; set; } = [];
}

public class UsuarioSeed
{
    public string Nome { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Senha { get; set; } = null!;
    public PapelUsuario Papel { get; set; }
}
