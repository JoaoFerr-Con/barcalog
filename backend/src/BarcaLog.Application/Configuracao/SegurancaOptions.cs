namespace BarcaLog.Application.Configuracao;

public class SegurancaOptions
{
    public const string Secao = "Seguranca";

    /// <summary>Falhas seguidas até bloquear a conta.</summary>
    public int MaxTentativasLogin { get; set; } = 5;

    /// <summary>Duração do bloqueio por força bruta.</summary>
    public int BloqueioMinutos { get; set; } = 15;

    /// <summary>Gestor sem MFA ativo só consegue configurar o MFA (recomendado em produção).</summary>
    public bool ExigirMfaParaGestor { get; set; } = true;

    /// <summary>Nome exibido no app autenticador.</summary>
    public string EmissorMfa { get; set; } = "BarcaLog";

    /// <summary>
    /// Chave AES-256 (Base64, 32 bytes) que cifra os segredos TOTP no banco.
    /// Em produção: variável de ambiente Seguranca__ChaveCriptografia.
    /// </summary>
    public string ChaveCriptografia { get; set; } = string.Empty;
}
