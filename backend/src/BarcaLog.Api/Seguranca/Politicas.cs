namespace BarcaLog.Api.Seguranca;

/// <summary>
/// Autenticado: qualquer JWT válido, INCLUSIVE restrito (só pra perfil, logout,
/// troca de senha e cadastro de MFA) e de qualquer papel.
/// Leitura: papéis INTERNOS (Operador, Gestor, Auditor), token sem restrição.
/// Escrita: Operador ou Gestor. Gestao: só Gestor.
/// Portal: só papel Transportadora, com a claim da transportadora (escopo dos dados).
/// Integracao: somente API Key.
/// </summary>
public static class Politicas
{
    public const string Autenticado = "Autenticado";
    public const string Leitura = "Leitura";
    public const string Escrita = "Escrita";
    public const string Gestao = "Gestao";
    public const string Portal = "Portal";
    public const string Integracao = "Integracao";
}
