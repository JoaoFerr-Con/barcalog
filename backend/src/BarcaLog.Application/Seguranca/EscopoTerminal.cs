using BarcaLog.Application.Abstracoes;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Application.Seguranca;

/// <summary>Regras puras do escopo por terminal (testadas sem banco).</summary>
public static class EscopoTerminal
{
    public static string? Restringir(string? vinculado, string? pedido)
    {
        pedido = string.IsNullOrWhiteSpace(pedido) ? null : pedido.Trim();
        if (vinculado is null) return pedido;
        // "todos"/"todas" = sem filtro em algumas telas; para quem é vinculado, vira o próprio terminal.
        if (pedido is null || pedido is "todos" or "todas" || string.Equals(pedido, vinculado, StringComparison.OrdinalIgnoreCase)) return vinculado;
        throw new AcessoNegadoException("Seu usuário está vinculado a outro terminal. Peça ao Gestor para alterar o vínculo, se necessário.");
    }

    /// <summary>
    /// Usuário vinculado só vê registros do próprio terminal; registros gerais (todos os
    /// terminais somados) também ficam fora, porque misturam dados de outros terminais.
    /// </summary>
    public static bool PodeVer(string? vinculado, string? terminalDoRegistro) =>
        vinculado is null || string.Equals(vinculado, terminalDoRegistro, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Sem usuário (tarefas em segundo plano, CLI): sem restrição. A API substitui pelo escopo do token.</summary>
public sealed class EscopoSemRestricao : IEscopoUsuario
{
    public string? TerminalVinculado => null;
}
