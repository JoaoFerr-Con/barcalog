using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BarcaLog.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BarcaLog.Api.Infra;

/// <summary>
/// Gera o dicionário de dados a partir da implementação real:
///   * modelo do EF Core (tabelas, colunas, tipos, nulos, padrões, chaves, índices, CHECKs);
///   * enums do domínio;
///   * endpoints do ApiExplorer (rota, verbo, política, parâmetros, validações, respostas)
///     e os contratos (DTOs) que eles usam;
///   * comentários XML do código como descrição técnica.
/// O que não dá para extrair do código (nome amigável, origem, responsável, sensibilidade,
/// telas consumidoras) vem de docs/dicionario-de-dados/curadoria.json, mantido por pessoas;
/// o que não estiver lá sai como "a validar".
///
///   dotnet run -- dicionario              (regrava docs/dicionario-de-dados/gerado)
///   dotnet run -- dicionario --verificar  (falha se o gerado no disco estiver desatualizado — usado no CI)
/// </summary>
public static class DicionarioDados
{
    private const string AValidar = "a validar";
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // ------------------------------------------------------------------ modelo extraído

    public sealed record Coluna(string Nome, string Tipo, bool Nulo, int? Tamanho, string? Padrao, string? Calculada,
        string? Geracao, string? Fuso, string[]? Dominio, string? Descricao);
    public sealed record ChaveEstrangeira(string[] Colunas, string TabelaReferenciada, string[] ColunasReferenciadas,
        string AoExcluir, bool Obrigatoria, bool Unica, string? Navegacao);
    public sealed record Indice(string Nome, string[] Colunas, bool Unico, string? Filtro);
    public sealed record Restricao(string Nome, string Sql);
    public sealed record Tabela(string Nome, string Entidade, string? Descricao, string[] ChavePrimaria, int? LinhasIniciais,
        List<Coluna> Colunas, List<ChaveEstrangeira> ChavesEstrangeiras, List<Indice> Indices, List<Restricao> Restricoes);
    public sealed record ValorEnum(string Nome, long Numero, string? Descricao);
    public sealed record EnumDominio(string Nome, bool Flags, string? Descricao, List<ValorEnum> Valores, string[] UsadoEm);
    public sealed record Parametro(string Nome, string Origem, string Tipo, bool Obrigatorio, string? Regras, string? Descricao);
    public sealed record Resposta(int Status, string? Tipo);
    public sealed record Endpoint(string Metodo, string Rota, string Controlador, string Acao, string? Descricao, string Acesso,
        string? CorpoTipo, List<Parametro> Parametros, List<Resposta> Respostas, bool Idempotente, string? LimiteTaxa,
        string? Descontinuado, bool Paginado);
    public sealed record CampoContrato(string Nome, string Tipo, bool Nulo, string? Regras, string? Descricao);
    public sealed record Contrato(string Nome, string? Descricao, List<CampoContrato> Campos);
    public sealed record Esquema(string UltimaMigration, List<Tabela> Tabelas, List<EnumDominio> Enums, List<Endpoint> Endpoints, List<Contrato> Contratos);

    // ------------------------------------------------------------------ curadoria (humana)

    public sealed class CuradoriaCampo
    {
        public string? NomeAmigavel { get; set; }
        public string? Origem { get; set; }
        public string? Sensibilidade { get; set; }
        public string? Observacao { get; set; }
    }

    public sealed class CuradoriaTabela
    {
        public string? NomeLogico { get; set; }
        public string? Finalidade { get; set; }
        public string? Origem { get; set; }
        public string? Responsavel { get; set; }
        public string[]? Consumidores { get; set; }
        public string? Retencao { get; set; }
        public string? Situacao { get; set; }
        public Dictionary<string, CuradoriaCampo>? Campos { get; set; }
    }

    public sealed class Curadoria
    {
        public Dictionary<string, CuradoriaTabela> Tabelas { get; set; } = [];
    }

    // ------------------------------------------------------------------ execução

    public static async Task<int> ExecutarAsync(IServiceProvider sp, string diretorio, bool verificar, ILogger log)
    {
        var db = sp.GetRequiredService<BarcaLogDbContext>();
        var api = sp.GetRequiredService<IApiDescriptionGroupCollectionProvider>();
        var xml = DocXml.Carregar(AppContext.BaseDirectory);
        var esquema = Extrair(db, api, xml);

        var caminhoCuradoria = Path.Combine(diretorio, "curadoria.json");
        var curadoria = File.Exists(caminhoCuradoria)
            ? JsonSerializer.Deserialize<Curadoria>(await File.ReadAllTextAsync(caminhoCuradoria), new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip }) ?? new()
            : new Curadoria();

        var problemas = ConferirCuradoria(esquema, curadoria);
        var arquivos = Gerar(esquema, curadoria);
        var saida = Path.Combine(diretorio, "gerado");

        if (verificar)
        {
            foreach (var (nome, conteudo) in arquivos)
            {
                var caminho = Path.Combine(saida, nome);
                if (!File.Exists(caminho)) problemas.Add($"gerado/{nome} não existe.");
                else if (Normalizar(await File.ReadAllTextAsync(caminho)) != Normalizar(conteudo))
                    problemas.Add($"gerado/{nome} está desatualizado em relação ao código.");
            }
            if (problemas.Count == 0)
            {
                log.LogInformation("Dicionário de dados em dia: {Tabelas} tabelas, {Enums} enums, {Endpoints} endpoints.", esquema.Tabelas.Count, esquema.Enums.Count, esquema.Endpoints.Count);
                return 0;
            }
            foreach (var p in problemas) log.LogError("{Problema}", p);
            log.LogError("Rode: dotnet run --project src/BarcaLog.Api -- dicionario (e complete docs/dicionario-de-dados/curadoria.json).");
            return 1;
        }

        Directory.CreateDirectory(saida);
        foreach (var (nome, conteudo) in arquivos)
            await File.WriteAllTextAsync(Path.Combine(saida, nome), conteudo, new UTF8Encoding(nome.EndsWith(".csv")));
        foreach (var p in problemas) log.LogWarning("{Problema}", p);
        log.LogInformation("Dicionário gerado em {Dir}: {Tabelas} tabelas, {Enums} enums, {Endpoints} endpoints, {Contratos} contratos.",
            saida, esquema.Tabelas.Count, esquema.Enums.Count, esquema.Endpoints.Count, esquema.Contratos.Count);
        return problemas.Count == 0 ? 0 : 1;
    }

    private static string Normalizar(string s) => s.Replace("\r\n", "\n").TrimStart('\uFEFF').TrimEnd();

    private static List<string> ConferirCuradoria(Esquema e, Curadoria c)
    {
        var problemas = new List<string>();
        var tabelas = e.Tabelas.ToDictionary(t => t.Nome);
        foreach (var t in e.Tabelas.Where(t => !c.Tabelas.ContainsKey(t.Nome)))
            problemas.Add($"Tabela {t.Nome} sem entrada em curadoria.json (finalidade, origem, responsável).");
        foreach (var (nome, ct) in c.Tabelas)
        {
            if (!tabelas.TryGetValue(nome, out var t)) { problemas.Add($"curadoria.json cita a tabela {nome}, que não existe no modelo."); continue; }
            foreach (var campo in (ct.Campos ?? []).Keys.Where(k => t.Colunas.All(col => col.Nome != k)))
                problemas.Add($"curadoria.json cita {nome}.{campo}, que não existe no modelo.");
        }
        return problemas;
    }

    // ------------------------------------------------------------------ extração

    public static Esquema Extrair(BarcaLogDbContext db, IApiDescriptionGroupCollectionProvider api, DocXml xml)
    {
        var modelo = db.GetService<IDesignTimeModel>().Model;
        var migrations = db.Database.GetMigrations().ToList();
        var tabelas = new List<Tabela>();
        var usoEnum = new Dictionary<Type, SortedSet<string>>();

        foreach (var ent in modelo.GetEntityTypes().Where(t => t.GetTableName() is not null && !t.IsOwned()).OrderBy(t => t.GetTableName(), StringComparer.Ordinal))
        {
            var nomeTabela = ent.GetTableName()!;
            var loja = StoreObjectIdentifier.Table(nomeTabela, ent.GetSchema());
            var colunas = new List<Coluna>();
            foreach (var p in ent.GetProperties())
            {
                var nomeCol = p.GetColumnName(loja) ?? p.Name;
                var tipoClr = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
                string[]? dominio = null;
                if (tipoClr.IsEnum)
                {
                    dominio = tipoClr.IsDefined(typeof(FlagsAttribute), false)
                        ? [$"{tipoClr.Name} (combinação de flags gravada como número)"]
                        : Enum.GetNames(tipoClr);
                    (usoEnum.TryGetValue(tipoClr, out var u) ? u : usoEnum[tipoClr] = new SortedSet<string>(StringComparer.Ordinal)).Add($"{nomeTabela}.{nomeCol}");
                }
                string? geracao = null;
                if (p.IsConcurrencyToken && p.ValueGenerated == ValueGenerated.OnAddOrUpdate) geracao = "rowversion (concorrência otimista)";
                else if (p.GetValueGenerationStrategy(loja) == Microsoft.EntityFrameworkCore.Metadata.SqlServerValueGenerationStrategy.IdentityColumn) geracao = "identidade (gerado pelo banco)";
                else if (p.GetComputedColumnSql(loja) is not null) geracao = p.GetIsStored(loja) == true ? "calculada e persistida" : "calculada";
                string? fuso = tipoClr == typeof(DateTime)
                    ? p.GetValueConverter() is not null ? "UTC" : "sem fuso no modelo"
                    : null;
                var padrao = p.GetDefaultValueSql(loja) ?? (p.TryGetDefaultValue(loja, out var v) && v is not null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null);
                colunas.Add(new Coluna(nomeCol, p.GetColumnType(loja) ?? "?", p.IsColumnNullable(loja), p.GetMaxLength(loja),
                    padrao, p.GetComputedColumnSql(loja), geracao, fuso, dominio,
                    p.PropertyInfo is { } pi ? xml.Propriedade(pi) : null));
            }

            var pk = ent.FindPrimaryKey()?.Properties.Select(p => p.GetColumnName(loja) ?? p.Name).ToArray() ?? [];
            var fks = ent.GetForeignKeys().Select(fk => new ChaveEstrangeira(
                    fk.Properties.Select(p => p.GetColumnName(loja) ?? p.Name).ToArray(),
                    fk.PrincipalEntityType.GetTableName() ?? fk.PrincipalEntityType.ShortName(),
                    fk.PrincipalKey.Properties.Select(p => p.GetColumnName(StoreObjectIdentifier.Table(fk.PrincipalEntityType.GetTableName()!, fk.PrincipalEntityType.GetSchema())) ?? p.Name).ToArray(),
                    fk.DeleteBehavior switch
                    {
                        DeleteBehavior.Cascade => "CASCADE",
                        DeleteBehavior.SetNull => "SET NULL",
                        DeleteBehavior.ClientSetNull => "NO ACTION (EF anula na memória)",
                        _ => "NO ACTION (impede excluir com vínculos)"
                    },
                    fk.IsRequired, fk.IsUnique, fk.DependentToPrincipal?.Name))
                .OrderBy(f => string.Join(",", f.Colunas), StringComparer.Ordinal).ToList();
            var indices = ent.GetIndexes().Select(i => new Indice(i.GetDatabaseName(loja) ?? i.Name ?? "?",
                    i.Properties.Select(p => p.GetColumnName(loja) ?? p.Name).ToArray(), i.IsUnique, i.GetFilter(loja)))
                .OrderBy(i => i.Nome, StringComparer.Ordinal).ToList();
            var checks = ent.GetCheckConstraints().Select(c => new Restricao(c.Name ?? "?", c.Sql)).OrderBy(c => c.Nome, StringComparer.Ordinal).ToList();
            var semente = ent.GetSeedData().Count();
            tabelas.Add(new Tabela(nomeTabela, ent.ClrType.Name, xml.Tipo(ent.ClrType), pk, semente == 0 ? null : semente, colunas, fks, indices, checks));
        }

        var (endpoints, tiposContrato) = ExtrairApi(api, xml);
        foreach (var tipo in tiposContrato.SelectMany(t => t.GetProperties()).Select(p => Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType).Where(t => t.IsEnum))
            if (!usoEnum.ContainsKey(tipo)) usoEnum[tipo] = new SortedSet<string>(StringComparer.Ordinal);

        var dominioEnums = typeof(Domain.Enums.PapelUsuario).Assembly.GetTypes().Where(t => t.IsEnum && t.IsPublic);
        foreach (var t in dominioEnums) if (!usoEnum.ContainsKey(t)) usoEnum[t] = new SortedSet<string>(StringComparer.Ordinal);
        var enums = usoEnum.Keys.OrderBy(t => t.Name, StringComparer.Ordinal).Select(t => new EnumDominio(t.Name, t.IsDefined(typeof(FlagsAttribute), false), xml.Tipo(t),
            Enum.GetValues(t).Cast<object>().Select(v => new ValorEnum(v.ToString()!, Convert.ToInt64(v, CultureInfo.InvariantCulture), xml.Campo(t, v.ToString()!))).ToList(),
            usoEnum[t].ToArray())).ToList();

        var contratos = tiposContrato.OrderBy(NomeTipo, StringComparer.Ordinal).Select(t => new Contrato(NomeTipo(t), xml.Tipo(t),
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0)
                .Select(p => new CampoContrato(Camel(p.Name), NomeTipo(p.PropertyType), PodeSerNulo(p), Regras(p), xml.Propriedade(p))).ToList())).ToList();

        return new Esquema(migrations.LastOrDefault() ?? "(nenhuma)", tabelas, enums, endpoints, contratos);
    }

    private static (List<Endpoint>, HashSet<Type>) ExtrairApi(IApiDescriptionGroupCollectionProvider api, DocXml xml)
    {
        var endpoints = new List<Endpoint>();
        var contratos = new HashSet<Type>();
        foreach (var d in api.ApiDescriptionGroups.Items.SelectMany(g => g.Items))
        {
            if (d.ActionDescriptor is not ControllerActionDescriptor a) continue;
            var m = a.MethodInfo;
            var c = a.ControllerTypeInfo;
            var acesso = Acesso(m, c);
            var parametros = new List<Parametro>();
            string? corpo = null;
            foreach (var p in d.ParameterDescriptions)
            {
                var origem = p.Source.Id switch
                {
                    "Path" => "rota", "Query" => "consulta", "Body" => "corpo", "Form" or "FormFile" => "formulário", "Header" => "cabeçalho", _ => p.Source.Id.ToLowerInvariant()
                };
                if (origem == "corpo" && p.Type is not null)
                {
                    corpo = NomeTipo(p.Type);
                    RegistrarContrato(p.Type, contratos);
                    continue;
                }
                var meta = p.ModelMetadata;
                PropertyInfo? prop = meta?.ContainerType?.GetProperty(meta.PropertyName ?? "");
                var regras = prop is not null ? Regras(prop) : null;
                var descricao = prop is not null ? xml.Propriedade(prop) : null;
                if (meta?.ContainerType is null && p.ParameterDescriptor is ControllerParameterDescriptor cp)
                {
                    descricao = xml.ParametroMetodo(m, cp.ParameterInfo.Name!);
                    regras = RegrasDe(cp.ParameterInfo.GetCustomAttributes(true));
                }
                if (p.RouteInfo?.Constraints?.Any() == true)
                    regras = Juntar(regras, string.Join(", ", p.RouteInfo.Constraints.Select(x => x.GetType().Name.Replace("RouteConstraint", "").ToLowerInvariant())));
                parametros.Add(new Parametro(Camel(p.Name), origem, NomeTipo(p.Type ?? typeof(string)), p.IsRequired || origem == "rota", regras, descricao));
                if (p.Type is { IsEnum: true }) RegistrarContrato(p.Type, contratos);
            }
            var respostas = d.SupportedResponseTypes.Select(r => new Resposta(r.StatusCode, r.Type is null || r.Type == typeof(void) ? null : NomeTipo(r.Type)))
                .GroupBy(r => r.Status).Select(g => g.First()).OrderBy(r => r.Status).ToList();
            foreach (var r in d.SupportedResponseTypes.Where(r => r.Type is not null)) RegistrarContrato(r.Type!, contratos);
            var retorno = TipoRetorno(m.ReturnType);
            if (retorno is not null) RegistrarContrato(retorno, contratos);
            var idempotente = m.GetCustomAttributes(true).Any(x => x.GetType().Name == "IdempotenteAttribute");
            var limite = m.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName ?? c.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName;
            var desc = m.GetCustomAttribute<DescontinuadoAttribute>();
            endpoints.Add(new Endpoint(d.HttpMethod ?? "?", "/" + (d.RelativePath ?? ""), c.Name.Replace("Controller", ""), m.Name,
                xml.Metodo(m), acesso, corpo, parametros, respostas, idempotente, limite,
                desc is null ? null : desc.Motivo + (desc.Substituto is null ? "" : $" Use {desc.Substituto}."),
                retorno is { IsGenericType: true } && retorno.GetGenericTypeDefinition().Name.StartsWith("Pagina")));
        }
        return (endpoints.OrderBy(e => e.Rota, StringComparer.Ordinal).ThenBy(e => e.Metodo, StringComparer.Ordinal).ToList(), contratos);
    }

    private static string Acesso(MethodInfo m, Type c)
    {
        if (m.GetCustomAttribute<AllowAnonymousAttribute>() is not null) return "público";
        var doMetodo = m.GetCustomAttributes<AuthorizeAttribute>().ToList();
        var autorizacao = doMetodo.Count > 0 ? doMetodo : c.GetCustomAttributes<AuthorizeAttribute>().ToList();
        if (c.GetCustomAttribute<AllowAnonymousAttribute>() is not null && doMetodo.Count == 0) return "público";
        // Sem [Authorize]: vale a política padrão da API (FallbackPolicy = Leitura).
        if (autorizacao.Count == 0) return "Leitura (política padrão)";
        return string.Join(" + ", autorizacao.Select(x => x.Policy ?? (x.Roles is { } r ? $"papéis {r}" : "autenticado")));
    }

    private static Type? TipoRetorno(Type t)
    {
        if (t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(Task<>) || t.GetGenericTypeDefinition() == typeof(ValueTask<>))) t = t.GetGenericArguments()[0];
        else if (t == typeof(Task) || t == typeof(void)) return null;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ActionResult<>)) t = t.GetGenericArguments()[0];
        if (typeof(IActionResult).IsAssignableFrom(t)) return null;
        return t;
    }

    private static void RegistrarContrato(Type t, HashSet<Type> tipos)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        if (t.IsArray) { RegistrarContrato(t.GetElementType()!, tipos); return; }
        if (t.IsGenericType)
        {
            foreach (var g in t.GetGenericArguments()) RegistrarContrato(g, tipos);
            if (!(t.Namespace ?? "").StartsWith("BarcaLog")) return;
        }
        if (!(t.Namespace ?? "").StartsWith("BarcaLog") || t.IsEnum || !tipos.Add(t)) return;
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance)) RegistrarContrato(p.PropertyType, tipos);
    }

    private static readonly NullabilityInfoContext Nulidade = new();

    private static bool PodeSerNulo(PropertyInfo p) =>
        Nullable.GetUnderlyingType(p.PropertyType) is not null || (!p.PropertyType.IsValueType && Nulidade.Create(p).ReadState != NullabilityState.NotNull);

    private static string? Regras(PropertyInfo p) => RegrasDe(p.GetCustomAttributes(true));

    private static string? RegrasDe(object[] atributos)
    {
        var r = new List<string>();
        foreach (var a in atributos)
        {
            switch (a)
            {
                case RequiredAttribute: r.Add("obrigatório"); break;
                case StringLengthAttribute s: r.Add(s.MinimumLength > 0 ? $"{s.MinimumLength} a {s.MaximumLength} caracteres" : $"até {s.MaximumLength} caracteres"); break;
                case MaxLengthAttribute s: r.Add($"até {s.Length} itens/caracteres"); break;
                case MinLengthAttribute s: r.Add($"mínimo {s.Length}"); break;
                case RangeAttribute g: r.Add($"de {Convert.ToString(g.Minimum, CultureInfo.InvariantCulture)} a {Convert.ToString(g.Maximum, CultureInfo.InvariantCulture)}"); break;
                case RegularExpressionAttribute x: r.Add($"formato `{x.Pattern}`"); break;
                case EmailAddressAttribute: r.Add("e-mail"); break;
                case ValidationAttribute v: r.Add(v.GetType().Name.Replace("Attribute", "")); break;
            }
        }
        return r.Count == 0 ? null : string.Join("; ", r);
    }

    private static string? Juntar(string? a, string b) => string.IsNullOrEmpty(a) ? b : $"{a}; {b}";

    private static string Camel(string nome) => string.IsNullOrEmpty(nome) ? nome : char.ToLowerInvariant(nome[0]) + nome[1..];

    public static string NomeTipo(Type t)
    {
        if (Nullable.GetUnderlyingType(t) is { } n) return NomeTipo(n) + "?";
        if (t.IsArray) return $"lista de {NomeTipo(t.GetElementType()!)}";
        if (t.IsGenericType)
        {
            var g = t.GetGenericTypeDefinition();
            var args = t.GetGenericArguments();
            if (g == typeof(List<>) || g == typeof(IReadOnlyList<>) || g == typeof(IEnumerable<>) || g == typeof(IList<>) || g == typeof(ICollection<>) || g == typeof(IReadOnlyCollection<>) || g == typeof(HashSet<>))
                return $"lista de {NomeTipo(args[0])}";
            if (g == typeof(Dictionary<,>) || g == typeof(IReadOnlyDictionary<,>) || g == typeof(IDictionary<,>))
                return $"mapa {NomeTipo(args[0])} → {NomeTipo(args[1])}";
            return $"{t.Name[..t.Name.IndexOf('`')]}<{string.Join(", ", args.Select(NomeTipo))}>";
        }
        return t switch
        {
            _ when t == typeof(string) => "texto",
            _ when t == typeof(int) => "inteiro",
            _ when t == typeof(long) => "inteiro longo",
            _ when t == typeof(double) || t == typeof(decimal) || t == typeof(float) => "número",
            _ when t == typeof(bool) => "booleano",
            _ when t == typeof(DateTime) => "data e hora",
            _ when t == typeof(DateOnly) => "data",
            _ when t == typeof(TimeOnly) => "hora",
            _ when t == typeof(Guid) => "guid",
            _ when t == typeof(object) => "objeto",
            _ when t.Name == "IFormFile" => "arquivo",
            _ => t.DeclaringType is { } dt && (dt.Namespace ?? "").StartsWith("BarcaLog") ? $"{dt.Name}.{t.Name}" : t.Name
        };
    }

    // ------------------------------------------------------------------ geração dos arquivos

    private static List<(string, string)> Gerar(Esquema e, Curadoria c) =>
    [
        ("esquema.json", JsonSerializer.Serialize(e, Json) + "\n"),
        ("tabelas-e-campos.md", TabelasMd(e, c)),
        ("relacionamentos.md", RelacionamentosMd(e)),
        ("enums.md", EnumsMd(e)),
        ("api.md", ApiMd(e)),
        ("campos.csv", Csv(e, c))
    ];

    private const string Aviso = "> Arquivo **gerado** por `dotnet run --project backend/src/BarcaLog.Api -- dicionario` a partir do código\n" +
                                 "> (modelo do EF Core, enums, ApiExplorer e comentários XML) e de `docs/dicionario-de-dados/curadoria.json`.\n" +
                                 "> Não edite à mão: o CI recusa o arquivo se ele divergir do código.\n";

    private static string Md(string? s) => string.IsNullOrWhiteSpace(s) ? "" : s.Replace("|", "\\|").Replace("\n", " ");

    private static string TabelasMd(Esquema e, Curadoria c)
    {
        var sb = new StringBuilder();
        sb.Append("# Tabelas e campos\n\n").Append(Aviso).Append('\n');
        sb.Append($"Modelo na migration `{e.UltimaMigration}` · {e.Tabelas.Count} tabelas · {e.Tabelas.Sum(t => t.Colunas.Count)} colunas.\n\n");
        sb.Append("Legenda: **Nulo** = aceita NULL no banco. **Fuso**: *UTC* = gravado e lido em UTC; *sem fuso no modelo* = o banco guarda a data/hora como recebida (ver descrição). ");
        sb.Append("Textos marcados *a validar* ainda não foram confirmados com o negócio.\n\n");
        sb.Append("## Índice\n\n| Tabela | Nome lógico | Finalidade | Situação |\n|---|---|---|---|\n");
        foreach (var t in e.Tabelas)
        {
            var ct = c.Tabelas.GetValueOrDefault(t.Nome);
            sb.Append($"| [{t.Nome}](#{t.Nome.ToLowerInvariant()}) | {Md(ct?.NomeLogico ?? AValidar)} | {Md(ct?.Finalidade ?? t.Descricao ?? AValidar)} | {Md(ct?.Situacao ?? AValidar)} |\n");
        }
        foreach (var t in e.Tabelas)
        {
            var ct = c.Tabelas.GetValueOrDefault(t.Nome);
            sb.Append($"\n## {t.Nome}\n\n");
            sb.Append($"- **Nome lógico:** {ct?.NomeLogico ?? AValidar}\n");
            sb.Append($"- **Entidade (código):** `{t.Entidade}`\n");
            if (t.Descricao is not null) sb.Append($"- **Descrição (código):** {t.Descricao}\n");
            sb.Append($"- **Finalidade:** {ct?.Finalidade ?? AValidar}\n");
            sb.Append($"- **Origem dos registros:** {ct?.Origem ?? AValidar}\n");
            sb.Append($"- **Quem cria/atualiza:** {ct?.Responsavel ?? AValidar}\n");
            sb.Append($"- **Consumida por:** {(ct?.Consumidores is { Length: > 0 } cons ? string.Join("; ", cons) : AValidar)}\n");
            sb.Append($"- **Retenção/histórico:** {ct?.Retencao ?? AValidar}\n");
            sb.Append($"- **Chave primária:** {string.Join(", ", t.ChavePrimaria.Select(x => $"`{x}`"))}\n");
            if (t.LinhasIniciais is { } li) sb.Append($"- **Dados iniciais (migration):** {li} linha(s)\n");
            sb.Append("\n| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |\n|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (var col in t.Colunas)
            {
                var cc = ct?.Campos?.GetValueOrDefault(col.Nome);
                var chave = new List<string>();
                if (t.ChavePrimaria.Contains(col.Nome)) chave.Add("PK");
                foreach (var fk in t.ChavesEstrangeiras.Where(f => f.Colunas.Contains(col.Nome))) chave.Add($"FK → {fk.TabelaReferenciada}");
                foreach (var ix in t.Indices.Where(i => i.Colunas.Length == 1 && i.Colunas[0] == col.Nome && i.Unico)) chave.Add("único");
                var regra = new List<string>();
                if (col.Dominio is { } d) regra.Add(string.Join(", ", d));
                if (col.Geracao is { } g) regra.Add(g);
                if (col.Calculada is { } calc) regra.Add($"`{calc}`");
                if (col.Fuso is { } f) regra.Add($"fuso: {f}");
                foreach (var ck in t.Restricoes.Where(r => r.Sql.Contains($"[{col.Nome}]") && !r.Nome.EndsWith($"_{col.Nome}") ))
                    regra.Add($"{ck.Nome}");
                var descricao = col.Descricao ?? "";
                if (cc?.Observacao is { } obs) descricao = string.IsNullOrEmpty(descricao) ? obs : $"{descricao} — {obs}";
                sb.Append($"| `{col.Nome}` | {Md(cc?.NomeAmigavel ?? Humanizar(col.Nome))} | {col.Tipo} | {(col.Nulo ? "sim" : "não")} | {Md(col.Padrao)} | {string.Join(", ", chave)} | {Md(string.Join("; ", regra))} | {Md(cc?.Origem ?? ct?.Origem ?? AValidar)} | {Md(cc?.Sensibilidade)} | {Md(string.IsNullOrEmpty(descricao) ? AValidar : descricao)} |\n");
            }
            if (t.Indices.Count > 0)
            {
                sb.Append("\n**Índices**\n\n| Nome | Colunas | Único | Filtro |\n|---|---|---|---|\n");
                foreach (var i in t.Indices) sb.Append($"| `{i.Nome}` | {string.Join(", ", i.Colunas)} | {(i.Unico ? "sim" : "não")} | {Md(i.Filtro)} |\n");
            }
            if (t.Restricoes.Count > 0)
            {
                sb.Append("\n**Restrições (CHECK)**\n\n| Nome | Regra |\n|---|---|\n");
                foreach (var r in t.Restricoes) sb.Append($"| `{r.Nome}` | `{Md(r.Sql)}` |\n");
            }
        }
        return sb.ToString();
    }

    private static string RelacionamentosMd(Esquema e)
    {
        var sb = new StringBuilder();
        sb.Append("# Relacionamentos\n\n").Append(Aviso).Append('\n');
        sb.Append("Relacionamentos **explícitos** (chaves estrangeiras no banco). Associações indiretas, sem FK, estão em `../modelo-relacional.md`.\n\n");
        sb.Append("```mermaid\nerDiagram\n");
        foreach (var t in e.Tabelas)
            foreach (var fk in t.ChavesEstrangeiras)
            {
                var lado = fk.Unica ? (fk.Obrigatoria ? "||--||" : "|o--||") : (fk.Obrigatoria ? "}o--||" : "}o--o|");
                sb.Append($"    {t.Nome} {lado} {fk.TabelaReferenciada} : \"{string.Join(",", fk.Colunas)}\"\n");
            }
        sb.Append("```\n\n| Origem (N) | Campo(s) | Destino (1) | Cardinalidade | Obrigatório | Ao excluir o destino |\n|---|---|---|---|---|---|\n");
        foreach (var t in e.Tabelas)
            foreach (var fk in t.ChavesEstrangeiras)
                sb.Append($"| {t.Nome} | {string.Join(", ", fk.Colunas)} | {fk.TabelaReferenciada}.{string.Join(", ", fk.ColunasReferenciadas)} | {(fk.Unica ? "1:1" : "N:1")} | {(fk.Obrigatoria ? "sim" : "não")} | {fk.AoExcluir} |\n");
        return sb.ToString();
    }

    private static string EnumsMd(Esquema e)
    {
        var sb = new StringBuilder();
        sb.Append("# Enums e domínios\n\n").Append(Aviso).Append('\n');
        sb.Append("No banco, enums são gravados **como texto** (nome do valor), com `CHECK` gerando a lista permitida — exceto `[Flags]`, gravados como número. ");
        sb.Append("Transições permitidas e efeitos nos indicadores estão em `../regras-de-negocio.md` (não são deduzidas do nome).\n");
        foreach (var en in e.Enums)
        {
            sb.Append($"\n## {en.Nome}{(en.Flags ? " (flags)" : "")}\n\n");
            if (en.Descricao is not null) sb.Append(en.Descricao).Append("\n\n");
            sb.Append($"Usado em: {(en.UsadoEm.Length > 0 ? string.Join(", ", en.UsadoEm.Select(u => $"`{u}`")) : "contratos da API (sem coluna própria)")}\n\n");
            sb.Append("| Valor | Número | Significado (código) |\n|---|---|---|\n");
            foreach (var v in en.Valores) sb.Append($"| `{v.Nome}` | {v.Numero} | {Md(v.Descricao ?? AValidar)} |\n");
        }
        return sb.ToString();
    }

    private static string ApiMd(Esquema e)
    {
        var sb = new StringBuilder();
        sb.Append("# API — endpoints e contratos\n\n").Append(Aviso).Append('\n');
        sb.Append($"{e.Endpoints.Count} endpoints · {e.Contratos.Count} contratos. Erros e políticas de acesso comuns a todos: `../api.md`.\n\n");
        sb.Append("Acesso: `Leitura` = Operador, Gestor ou Auditor · `Escrita` = Operador ou Gestor · `Gestao` = Gestor · `Autenticado` = qualquer token válido (inclusive restrito) · `Portal` = usuário de transportadora · `Integracao` = `X-Api-Key`.\n\n");
        foreach (var grupo in e.Endpoints.GroupBy(x => x.Controlador).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            sb.Append($"\n## {grupo.Key}\n\n| Método | Rota | Acesso | Descrição |\n|---|---|---|---|\n");
            foreach (var ep in grupo) sb.Append($"| {ep.Metodo} | `{ep.Rota}` | {ep.Acesso} | {Md(ep.Descricao ?? AValidar)}{(ep.Descontinuado is null ? "" : " **(descontinuado)**")} |\n");
            foreach (var ep in grupo)
            {
                sb.Append($"\n### {ep.Metodo} `{ep.Rota}`\n\n");
                if (ep.Descricao is not null) sb.Append(ep.Descricao).Append("\n\n");
                sb.Append($"- Acesso: **{ep.Acesso}**\n");
                if (ep.CorpoTipo is not null) sb.Append($"- Corpo: `{ep.CorpoTipo}`\n");
                if (ep.Paginado) sb.Append("- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`\n");
                if (ep.Idempotente) sb.Append("- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)\n");
                if (ep.LimiteTaxa is not null) sb.Append($"- Limite de taxa: `{ep.LimiteTaxa}`\n");
                if (ep.Descontinuado is not null) sb.Append($"- **Descontinuado:** {ep.Descontinuado}\n");
                sb.Append($"- Respostas: {(ep.Respostas.Count == 0 ? "200" : string.Join(", ", ep.Respostas.Select(r => r.Tipo is null ? $"{r.Status}" : $"{r.Status} `{r.Tipo}`")))}\n");
                if (ep.Parametros.Count > 0)
                {
                    sb.Append("\n| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |\n|---|---|---|---|---|---|\n");
                    foreach (var p in ep.Parametros) sb.Append($"| `{p.Nome}` | {p.Origem} | {Md(p.Tipo)} | {(p.Obrigatorio ? "sim" : "não")} | {Md(p.Regras)} | {Md(p.Descricao)} |\n");
                }
            }
        }
        sb.Append("\n## Contratos (DTOs)\n\nNomes dos campos como trafegam em JSON (camelCase). Enums trafegam pelo nome.\n");
        foreach (var ct in e.Contratos)
        {
            sb.Append($"\n### {ct.Nome}\n\n");
            if (ct.Descricao is not null) sb.Append(ct.Descricao).Append("\n\n");
            if (ct.Campos.Count == 0) { sb.Append("(sem campos públicos)\n"); continue; }
            sb.Append("| Campo | Tipo | Nulo | Regras | Descrição |\n|---|---|---|---|---|\n");
            foreach (var f in ct.Campos) sb.Append($"| `{f.Nome}` | {Md(f.Tipo)} | {(f.Nulo ? "sim" : "não")} | {Md(f.Regras)} | {Md(f.Descricao)} |\n");
        }
        return sb.ToString();
    }

    private static string Csv(Esquema e, Curadoria c)
    {
        static string Cel(string? s)
        {
            s ??= "";
            if (s.Length > 0 && "=+-@".Contains(s[0])) s = "'" + s;
            return s.IndexOfAny([';', '"', '\n']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }
        var sb = new StringBuilder();
        sb.Append("Tabela;Nome lógico da tabela;Campo;Nome amigável;Descrição;Tipo;Tamanho;Obrigatório;Padrão;Chave;Domínio;Regra de validação;Origem;Sensibilidade;Observações\n");
        foreach (var t in e.Tabelas)
        {
            var ct = c.Tabelas.GetValueOrDefault(t.Nome);
            foreach (var col in t.Colunas)
            {
                var cc = ct?.Campos?.GetValueOrDefault(col.Nome);
                var chave = new List<string>();
                if (t.ChavePrimaria.Contains(col.Nome)) chave.Add("PK");
                foreach (var fk in t.ChavesEstrangeiras.Where(f => f.Colunas.Contains(col.Nome))) chave.Add($"FK {fk.TabelaReferenciada}");
                if (t.Indices.Any(i => i.Colunas.Length == 1 && i.Colunas[0] == col.Nome && i.Unico)) chave.Add("único");
                var regra = string.Join(" | ", new[] { col.Geracao, col.Calculada, col.Fuso is null ? null : $"fuso: {col.Fuso}" }
                    .Concat(t.Restricoes.Where(r => r.Sql.Contains($"[{col.Nome}]") && !r.Nome.EndsWith($"_{col.Nome}")).Select(r => r.Sql)).Where(x => x is not null));
                sb.Append(string.Join(';',
                    Cel(t.Nome), Cel(ct?.NomeLogico ?? AValidar), Cel(col.Nome), Cel(cc?.NomeAmigavel ?? Humanizar(col.Nome)),
                    Cel(col.Descricao ?? AValidar), Cel(col.Tipo), Cel(col.Tamanho?.ToString(CultureInfo.InvariantCulture)),
                    col.Nulo ? "não" : "sim", Cel(col.Padrao), Cel(string.Join(" ", chave)), Cel(col.Dominio is null ? null : string.Join(", ", col.Dominio)),
                    Cel(regra), Cel(cc?.Origem ?? ct?.Origem ?? AValidar), Cel(cc?.Sensibilidade), Cel(cc?.Observacao))).Append('\n');
            }
        }
        return sb.ToString();
    }

    /// <summary>"DataMarcacao" → "Data marcacao" (sem acentos: o nome técnico não tem). Nome amigável de verdade vem da curadoria.</summary>
    private static string Humanizar(string nome)
    {
        var partes = Regex.Split(nome, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])");
        return string.Join(" ", partes.Select((p, i) => i == 0 ? p : p.Length > 1 && p.All(char.IsUpper) ? p : p.ToLower(Br)));
    }

    // ------------------------------------------------------------------ comentários XML

    /// <summary>Resumos dos comentários XML (///) dos assemblies do BarcaLog.</summary>
    public sealed class DocXml
    {
        private readonly Dictionary<string, XElement> _membros = new(StringComparer.Ordinal);

        public static DocXml Carregar(string diretorio)
        {
            var d = new DocXml();
            foreach (var arq in Directory.GetFiles(diretorio, "BarcaLog.*.xml").OrderBy(x => x, StringComparer.Ordinal))
            {
                try
                {
                    foreach (var m in XDocument.Load(arq).Descendants("member"))
                        if (m.Attribute("name")?.Value is { } n) d._membros[n] = m;
                }
                catch (System.Xml.XmlException) { /* arquivo que não é doc XML */ }
            }
            return d;
        }

        private static string NomeTipoXml(Type t) => (t.FullName ?? t.Name).Replace('+', '.');

        public string? Tipo(Type t) => Resumo(_membros.GetValueOrDefault("T:" + NomeTipoXml(t))?.Element("summary"));

        public string? Campo(Type t, string nome) => Resumo(_membros.GetValueOrDefault($"F:{NomeTipoXml(t)}.{nome}")?.Element("summary"));

        public string? Propriedade(PropertyInfo p)
        {
            var tipo = p.DeclaringType!;
            if (Resumo(_membros.GetValueOrDefault($"P:{NomeTipoXml(tipo)}.{p.Name}")?.Element("summary")) is { } r) return r;
            // record posicional: documentado como <param name="X"> no tipo
            return Resumo(_membros.GetValueOrDefault("T:" + NomeTipoXml(tipo))?.Elements("param").FirstOrDefault(x => x.Attribute("name")?.Value == p.Name));
        }

        private XElement? MembroMetodo(MethodInfo m)
        {
            var prefixo = $"M:{NomeTipoXml(m.DeclaringType!)}.{m.Name}";
            var n = m.GetParameters().Length;
            return _membros.Where(kv => kv.Key == prefixo || kv.Key.StartsWith(prefixo + "(", StringComparison.Ordinal))
                .Where(kv => (kv.Key.Contains('(') ? kv.Key[(kv.Key.IndexOf('(') + 1)..].Split(',').Length : 0) == n || n == 0 && !kv.Key.Contains('('))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Value).FirstOrDefault();
        }

        public string? Metodo(MethodInfo m) => Resumo(MembroMetodo(m)?.Element("summary"));

        public string? ParametroMetodo(MethodInfo m, string nome) =>
            Resumo(MembroMetodo(m)?.Elements("param").FirstOrDefault(x => x.Attribute("name")?.Value == nome));

        private static string? Resumo(XElement? e)
        {
            if (e is null) return null;
            var sb = new StringBuilder();
            foreach (var n in e.Nodes())
            {
                switch (n)
                {
                    case XText t: sb.Append(t.Value); break;
                    case XElement x when x.Name == "see" || x.Name == "seealso":
                        var cref = x.Attribute("cref")?.Value ?? x.Attribute("langword")?.Value ?? "";
                        cref = Regex.Replace(cref, @"^[A-Z]:", "");
                        cref = Regex.Replace(cref, @"\(.*\)$", "");
                        sb.Append(cref[(cref.LastIndexOf('.') + 1)..]);
                        break;
                    case XElement x when x.Name == "paramref" || x.Name == "typeparamref": sb.Append(x.Attribute("name")?.Value); break;
                    case XElement x: sb.Append(x.Value); break;
                }
            }
            var texto = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
            return texto.Length == 0 ? null : texto;
        }
    }
}
