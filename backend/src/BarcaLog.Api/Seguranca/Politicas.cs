namespace BarcaLog.Api.Seguranca;

/// <summary>
/// Leitura: qualquer usuário autenticado (Operador, Gestor, Auditor).
/// Escrita: Operador ou Gestor. Gestao: só Gestor (remoções, importação, usuários).
/// Integracao: somente API Key.
/// </summary>
public static class Politicas
{
    public const string Leitura = "Leitura";
    public const string Escrita = "Escrita";
    public const string Gestao = "Gestao";
    public const string Integracao = "Integracao";
}
