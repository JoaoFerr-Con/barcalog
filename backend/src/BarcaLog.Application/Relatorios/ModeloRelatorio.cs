using System.Text.Json.Serialization;

namespace BarcaLog.Application.Relatorios;

/// <summary>
/// Documento de relatório já composto: a MESMA estrutura alimenta a prévia na tela,
/// o PDF e o Excel, por isso nenhum número muda entre a visualização e a exportação.
/// Textos e valores chegam formatados (pt-BR); tabelas e gráficos trazem também os
/// valores numéricos brutos para o Excel.
/// </summary>
public sealed record DocumentoRelatorio(
    string Tipo,
    string Titulo,
    string Subtitulo,
    DateOnly De,
    DateOnly Ate,
    string PeriodoRotulo,
    string Abrangencia,
    IReadOnlyList<ItemRotulado> Filtros,
    DateTime GeradoEm,
    string? Organizacao,
    string FonteDados,
    bool SemDados,
    IReadOnlyList<SecaoRelatorio> Secoes)
{
    /// <summary>Preenchido quando o documento é emitido (registrado no histórico); a prévia não tem código.</summary>
    public string? Codigo { get; init; }
}

public sealed record ItemRotulado(string Rotulo, string Valor);

/// <summary>Uma seção numerada do relatório.</summary>
public sealed record SecaoRelatorio(string Id, string Titulo, IReadOnlyList<BlocoRelatorio> Blocos);

/// <summary>Um bloco de conteúdo. O campo "tipo" no JSON diz qual é.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(BlocoParagrafo), "paragrafo")]
[JsonDerivedType(typeof(BlocoIndicadores), "indicadores")]
[JsonDerivedType(typeof(BlocoTabela), "tabela")]
[JsonDerivedType(typeof(BlocoGrafico), "grafico")]
[JsonDerivedType(typeof(BlocoLista), "lista")]
[JsonDerivedType(typeof(BlocoQuadro), "quadro")]
public abstract record BlocoRelatorio;

/// <summary>Texto corrido. Estilo: normal | destaque (resumo executivo) | nota (letra menor, cor suave).</summary>
public sealed record BlocoParagrafo(string Texto, string Estilo = "normal") : BlocoRelatorio;

/// <summary>Indicadores em blocos compactos.</summary>
public sealed record BlocoIndicadores(IReadOnlyList<Indicador> Itens) : BlocoRelatorio;

/// <remarks>
/// <para>Rotulo: Nome do indicador.</para>
/// <para>Valor: Valor formatado.</para>
/// <para>Comparacao: Texto de comparação com o período anterior (ou explicação curta).</para>
/// <para>Sentido: melhor | pior | neutro | null — só quando a comparação tem leitura clara.</para>
/// </remarks>
public sealed record Indicador(string Rotulo, string Valor, string? Comparacao, string? Sentido);

/// <remarks>
/// <para>Titulo: Título da tabela (também nome da aba no Excel).</para>
/// <para>Colunas: Cabeçalhos.</para>
/// <para>Linhas: Células formatadas.</para>
/// <para>Valores: Mesmas células como número (null = texto); usado no Excel.</para>
/// <para>Nota: Explicação curta sob a tabela.</para>
/// <para>LinhasDestacadas: Índices de linhas a destacar (ex.: total, pico).</para>
/// </remarks>
public sealed record BlocoTabela(
    string Titulo,
    IReadOnlyList<ColunaTabela> Colunas,
    IReadOnlyList<IReadOnlyList<string>> Linhas,
    IReadOnlyList<IReadOnlyList<double?>> Valores,
    string? Nota,
    IReadOnlyList<int> LinhasDestacadas) : BlocoRelatorio;

/// <remarks>
/// <para>Alinhamento: esquerda | direita.</para>
/// <para>Largura: Peso relativo da largura.</para>
/// <para>Formato: Formato numérico no Excel (ex.: "#,##0", "0.0%", "[h]:mm"); null = texto.</para>
/// </remarks>
public sealed record ColunaTabela(string Titulo, string Alinhamento = "esquerda", double Largura = 1, string? Formato = null);

/// <summary>Gráfico simples. Tipo: barras | linhas | barrasEmpilhadas.</summary>
/// <remarks>
/// <para>Unidade: Unidade do eixo vertical, por extenso (ex.: "carretas por dia").</para>
/// </remarks>
public sealed record BlocoGrafico(
    string Titulo,
    string TipoGrafico,
    IReadOnlyList<string> Categorias,
    IReadOnlyList<SerieGrafico> Series,
    string Unidade,
    string? Nota,
    IReadOnlyList<int> CategoriasDestacadas) : BlocoRelatorio;

/// <remarks>
/// <para>Estimado: Valores estimados (desenhados de forma diferente dos observados).</para>
/// <para>Cor: Nome da cor na paleta do documento: marca | ambar | unitapajos | tgpm | hidrovias | neutra | estimado.</para>
/// </remarks>
public sealed record SerieGrafico(string Nome, IReadOnlyList<double?> Valores, bool Estimado, string Cor);

/// <summary>Lista com marcadores.</summary>
public sealed record BlocoLista(IReadOnlyList<string> Itens) : BlocoRelatorio;

/// <summary>
/// Quadro com título e itens rotulados — usado para separar "dado observado",
/// "contexto externo", "interpretação" e "limitação", e para as fontes.
/// Tom: informativo | atencao | limitacao.
/// </summary>
public sealed record BlocoQuadro(string Titulo, IReadOnlyList<ItemRotulado> Itens, string Tom = "informativo") : BlocoRelatorio;
