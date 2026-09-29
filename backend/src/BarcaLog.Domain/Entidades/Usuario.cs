using BarcaLog.Domain.Enums;

namespace BarcaLog.Domain.Entidades;

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string SenhaHash { get; set; } = null!;
    public PapelUsuario Papel { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Obrigatório (e só permitido) para o papel Transportadora — define o escopo de dados do Portal.</summary>
    public int? TransportadoraId { get; set; }

    /// <summary>Tentativas erradas seguidas; zera no login bem-sucedido.</summary>
    public int FalhasLoginConsecutivas { get; set; }

    /// <summary>Conta bloqueada por força bruta até este instante (UTC).</summary>
    public DateTime? BloqueadoAte { get; set; }

    /// <summary>
    /// Vai no JWT (claim "ver"). Incrementar invalida todos os tokens já
    /// emitidos: logout, troca/redefinição de senha, desativação, MFA.
    /// </summary>
    public int VersaoToken { get; set; } = 1;

    /// <summary>Senha provisória: o token só serve pra trocar a senha.</summary>
    public bool DeveTrocarSenha { get; set; }

    /// <summary>Segredo TOTP cifrado (AES-GCM). Presente com MfaAtivo=false = cadastro pendente.</summary>
    public string? MfaSegredoCifrado { get; set; }
    public bool MfaAtivo { get; set; }

    /// <summary>Último passo TOTP aceito — impede reutilizar o mesmo código.</summary>
    public long? MfaUltimoPassoUsado { get; set; }

    public DateTime? UltimoLoginEm { get; set; }

    public bool EstaBloqueado(DateTime agoraUtc) => BloqueadoAte is { } ate && ate > agoraUtc;

    public void Desbloquear()
    {
        FalhasLoginConsecutivas = 0;
        BloqueadoAte = null;
    }

    public void RevogarTokens() => VersaoToken++;

    public void DefinirSenha(string novoHash, bool provisoria)
    {
        SenhaHash = novoHash;
        DeveTrocarSenha = provisoria;
        Desbloquear();
        RevogarTokens();
    }

    public void RemoverMfa()
    {
        MfaAtivo = false;
        MfaSegredoCifrado = null;
        MfaUltimoPassoUsado = null;
        RevogarTokens();
    }
}
