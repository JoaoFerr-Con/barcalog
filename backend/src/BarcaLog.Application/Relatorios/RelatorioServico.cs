using System.Security.Cryptography;
using System.Text.Json;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;
using BarcaLog.Application.Servicos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Excecoes;
using BarcaLog.Domain.Regras;
using Microsoft.Extensions.Options;

namespace BarcaLog.Application.Relatorios;

/// <summary>Gera os arquivos (implementado na infraestrutura: PDF com PDFsharp, Excel com ClosedXML).</summary>
public interface IRenderizadorRelatorio
{
    byte[] Pdf(DocumentoRelatorio documento);

    /// <remarks>
    /// <para>movimentos: Movimentos marcados no período (aba de dados detalhados).</para>
    /// </remarks>
    byte[] Excel(DocumentoRelatorio documento, IReadOnlyList<RegistroAnalise> movimentos);
}

public interface IRelatorioGeradoRepositorio
{
    Task AdicionarAsync(RelatorioGerado relatorio, CancellationToken ct = default);
    Task<RelatorioGerado?> ObterAsync(int id, CancellationToken ct = default);
    /// <remarks>
    /// <para>terminalId: Com valor: só relatórios desse terminal (usuário vinculado).</para>
    /// </remarks>
    Task<(List<RelatorioGerado> Itens, int Total)> ListarAsync(string? terminalId, int pagina, int tamanho, CancellationToken ct = default);
}

public sealed record TipoRelatorioDto(string Id, string Titulo, string Descricao);
public sealed record TerminalRelatorioDto(string Id, string Nome, DateOnly PrimeiraMarcacao, DateOnly UltimaMarcacao);
public sealed record MesDisponivelDto(string Chave, string Rotulo, DateOnly De, DateOnly Ate, bool Completo);

/// <summary>O que a central de relatórios oferece para o usuário (já no escopo dele).</summary>
/// <remarks>
/// <para>TerminalVinculado: Preenchido quando o usuário só pode ver um terminal.</para>
/// <para>ReferenciaDiariaCarretas: Referência provisória de carretas/dia por terminal.</para>
/// </remarks>
public sealed record OpcoesRelatorioDto(
    IReadOnlyList<TipoRelatorioDto> Tipos,
    IReadOnlyList<TerminalRelatorioDto> Terminais,
    IReadOnlyList<string> Operadores,
    IReadOnlyList<string> Cargas,
    IReadOnlyList<MesDisponivelDto> Meses,
    DateOnly? PadraoDe,
    DateOnly? PadraoAte,
    string? TerminalVinculado,
    int ReferenciaDiariaCarretas);

public sealed record RelatorioGeradoDto(int Id, string Codigo, string Tipo, string Titulo, DateOnly De, DateOnly Ate, string Periodo, string Abrangencia,
    string Formato, string GeradoPor, DateTime GeradoEm, long TamanhoBytes, string HashSha256);

public sealed record ArquivoRelatorio(byte[] Conteudo, string NomeArquivo, string TipoConteudo, string Codigo);

/// <summary>
/// Central de relatórios: monta o documento (prévia), emite PDF/Excel e mantém o histórico.
/// Os números vêm da Análise 360° (mesmo cálculo da tela) e de <see cref="CalculosRelatorio"/>;
/// o escopo do usuário (terminal vinculado) vale para tudo.
/// </summary>
public class RelatorioServico(
    IMarcacaoRepositorio marcacoes,
    ITerminalRepositorio terminais,
    IRelatorioGeradoRepositorio historico,
    IUnitOfWork uow,
    IEscopoUsuario escopo,
    IUsuarioAtual usuario,
    IContextoAuditoria auditoria,
    IRenderizadorRelatorio renderizador,
    IOptionsMonitor<RelatoriosOptions> opcoes,
    RelogioOperacional relogio)
{
    private static readonly DateTime InicioHistorico = new(2000, 1, 1);
    private static readonly DateTime FimHistorico = new(2100, 1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<OpcoesRelatorioDto> OpcoesAsync(CancellationToken ct = default)
    {
        var terminalId = escopo.RestringirTerminal(null);
        var todos = await marcacoes.ListarParaAnaliseAsync(new FiltroAnaliseMovimentacoes { TerminalId = terminalId }, InicioHistorico, FimHistorico, ct);
        var cobertura = CalculosRelatorio.Cobertura(todos);
        var meses = Meses(todos, cobertura);
        var padrao = meses.LastOrDefault(m => m.Completo) ?? meses.LastOrDefault();
        var cmp = StringComparer.Create(Fmt.Br, true);
        return new OpcoesRelatorioDto(
            FiltroRelatorio.Tipos.Select(t => new TipoRelatorioDto(t, MontadorRelatorio.Titulos[t], MontadorRelatorio.Descricoes[t])).ToList(),
            cobertura.Select(c => new TerminalRelatorioDto(c.TerminalId, c.Terminal, c.Primeira, c.Ultima)).ToList(),
            todos.Select(r => r.Operador).Distinct().Order(cmp).ToList(),
            todos.Select(r => r.Carga).Distinct().Order(cmp).ToList(),
            meses, padrao?.De, padrao?.Ate, escopo.TerminalVinculado, opcoes.CurrentValue.ReferenciaDiariaCarretas);
    }

    /// <summary>Documento completo para a prévia (sem código e sem registro no histórico).</summary>
    public async Task<DocumentoRelatorio> MontarAsync(FiltroRelatorio filtro, CancellationToken ct = default) =>
        (await MontarComDadosAsync(filtro, ct)).Documento;

    /// <summary>Gera o arquivo, registra no histórico (com o resumo SHA-256) e devolve o conteúdo.</summary>
    public async Task<ArquivoRelatorio> EmitirAsync(FiltroRelatorio filtro, string formato, CancellationToken ct = default)
    {
        formato = formato.ToLowerInvariant();
        if (formato is not ("pdf" or "xlsx")) throw new RegraNegocioException("Formato inválido: use pdf ou xlsx.");
        var (doc, movimentos, filtroEfetivo) = await MontarComDadosAsync(filtro, ct);
        if (doc.SemDados) throw new RegraNegocioException("Não há movimentações no período e recorte escolhidos — não há o que exportar.");

        var codigo = $"RL-{doc.GeradoEm:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3))}";
        doc = doc with { Codigo = codigo };
        var conteudo = formato == "pdf" ? renderizador.Pdf(doc) : renderizador.Excel(doc, movimentos);

        auditoria.DefinirAcao("Relatório emitido", $"{doc.Titulo} · {doc.PeriodoRotulo} · {doc.Abrangencia} · {formato.ToUpperInvariant()} · {codigo}");
        await historico.AdicionarAsync(new RelatorioGerado
        {
            Codigo = codigo,
            Tipo = doc.Tipo,
            Titulo = Cortar(doc.Titulo, RelatorioGerado.Tamanho.Titulo),
            De = doc.De,
            Ate = doc.Ate,
            TerminalId = filtroEfetivo.TerminalId,
            Abrangencia = Cortar(doc.Abrangencia, RelatorioGerado.Tamanho.Abrangencia),
            Filtros = Cortar(JsonSerializer.Serialize(filtroEfetivo, Json), RelatorioGerado.Tamanho.Filtros),
            Formato = formato,
            GeradoPor = Cortar(usuario.Nome ?? usuario.Autor, RelatorioGerado.Tamanho.Autor),
            GeradoEm = relogio.AgoraUtc,
            TamanhoBytes = conteudo.LongLength,
            HashSha256 = Convert.ToHexString(SHA256.HashData(conteudo)).ToLowerInvariant()
        }, ct);
        await uow.SalvarAsync(ct);

        var nome = $"barcalog_{doc.Tipo}_{(doc.De.Day == 1 && doc.Ate == doc.De.AddMonths(1).AddDays(-1) ? doc.De.ToString("yyyy-MM") : $"{doc.De:yyyy-MM-dd}_a_{doc.Ate:yyyy-MM-dd}")}" +
                   $"_{filtroEfetivo.TerminalId ?? "todos"}_{codigo}.{formato}";
        return new ArquivoRelatorio(conteudo, nome,
            formato == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", codigo);
    }

    public async Task<Pagina<RelatorioGeradoDto>> HistoricoAsync(int pagina, int tamanho, CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 100);
        var (itens, total) = await historico.ListarAsync(escopo.TerminalVinculado, pagina, tamanho, ct);
        return new Pagina<RelatorioGeradoDto>(itens.Select(r => new RelatorioGeradoDto(r.Id, r.Codigo, r.Tipo, r.Titulo, r.De, r.Ate, Fmt.Periodo(r.De, r.Ate),
            r.Abrangencia, r.Formato, r.GeradoPor, r.GeradoEm, r.TamanhoBytes, r.HashSha256)).ToList(), total, pagina, tamanho);
    }

    /// <summary>Emite de novo com os mesmos filtros (os números refletem os dados de hoje e o código é novo).</summary>
    public async Task<ArquivoRelatorio> ReemitirAsync(int id, string? formato, CancellationToken ct = default)
    {
        var r = await historico.ObterAsync(id, ct) ?? throw new NaoEncontradoException("Relatório não encontrado no histórico.");
        if (!escopo.PodeVerTerminal(r.TerminalId)) throw new NaoEncontradoException("Relatório não encontrado no histórico.");
        var filtro = JsonSerializer.Deserialize<FiltroRelatorio>(r.Filtros, Json) ?? throw new RegraNegocioException("Filtros do relatório ilegíveis.");
        return await EmitirAsync(filtro, formato ?? r.Formato, ct);
    }

    // ------------------------------------------------------------------

    private async Task<(DocumentoRelatorio Documento, IReadOnlyList<RegistroAnalise> Movimentos, FiltroRelatorio Filtro)> MontarComDadosAsync(FiltroRelatorio f, CancellationToken ct)
    {
        var terminalId = escopo.RestringirTerminal(string.IsNullOrWhiteSpace(f.TerminalId) ? null : f.TerminalId);
        var todos = await marcacoes.ListarParaAnaliseAsync(new FiltroAnaliseMovimentacoes { TerminalId = terminalId }, InicioHistorico, FimHistorico, ct);
        var cobertura = CalculosRelatorio.Cobertura(todos);

        DateOnly de, ate;
        if (f.De is { } d && f.Ate is { } a) (de, ate) = (d, a);
        else
        {
            var meses = Meses(todos, cobertura);
            var padrao = meses.LastOrDefault(m => m.Completo) ?? meses.LastOrDefault()
                ?? throw new NaoEncontradoException("Não há movimentações registradas para gerar relatórios.");
            (de, ate) = (padrao.De, padrao.Ate);
        }

        var operador = string.IsNullOrWhiteSpace(f.Operador) ? null : f.Operador.Trim();
        var carga = string.IsNullOrWhiteSpace(f.Carga) ? null : f.Carga.Trim();
        var filtrados = todos.Where(r => (operador is null || r.Operador == operador) && (carga is null || r.Carga == carga)).ToList();

        var parametros = await terminais.ObterParametrosAsync(false, ct);
        var listaTerminais = await terminais.ListarAsync(ct);
        var dias = ate.DayNumber - de.DayNumber + 1;
        var (ade, _) = AnaliseMovimentacoes.PeriodoAnterior(de, ate);
        DateTime ini = ade.ToDateTime(TimeOnly.MinValue), fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var paraAnalise = filtrados.Where(r => (r.DataMarcacao >= ini && r.DataMarcacao < fim) || (r.DataLiberacao >= ini && r.DataLiberacao < fim)).ToList();
        var agrupar = f.Tipo == "operacional" && dias <= 62 ? "dia" : null;
        var analise = AnaliseMovimentacoes.Calcular(paraAnalise, de, ate, agrupar, parametros.LimiteEsperaLongaHoras, RegrasImportacao.JanelaRemarcacaoMinutos);
        DateTime iniPeriodo = de.ToDateTime(TimeOnly.MinValue);
        var entradas = filtrados.Where(r => r.DataMarcacao >= iniPeriodo && r.DataMarcacao < fim).ToList();

        var o = opcoes.CurrentValue;
        var capacidades = listaTerminais.ToDictionary(t => t.Id, t => t.CapacidadeDiariaCarretas);
        (int, bool) Referencia(string id) => capacidades.TryGetValue(id, out var c) && c is int cap && cap > 0 ? (cap, true) : (o.ReferenciaDiariaCarretas, false);

        var nomeTerminal = terminalId is null ? null : listaTerminais.FirstOrDefault(t => t.Id == terminalId)?.Nome ?? terminalId;
        var abrangencia = (nomeTerminal ?? "Todos os terminais") + (operador is null ? "" : $" · Operador {operador}") + (carga is null ? "" : $" · Carga {carga}");
        var filtrosRotulados = new List<ItemRotulado>
        {
            new("Período", Fmt.Periodo(de, ate)),
            new("Terminal", nomeTerminal ?? "Todos os terminais")
        };
        if (operador is not null) filtrosRotulados.Add(new("Operador", operador));
        if (carga is not null) filtrosRotulados.Add(new("Carga", carga));
        filtrosRotulados.Add(new("Detalhamento", f.Detalhamento == "resumido" ? "Resumido" : "Completo"));

        var doc = MontadorRelatorio.Montar(new EntradaMontagem(f, abrangencia, filtrosRotulados, analise, entradas,
            CalculosRelatorio.HistoricoMensal(filtrados, ate, cobertura), cobertura, Referencia, o.ReferenciaDiariaCarretas,
            parametros.LimiteEsperaMedianaHoras, parametros.MetaLiberadas24hPct, o.ContextoExterno, relogio.AgoraLocalPorto, o.Organizacao));

        var efetivo = new FiltroRelatorio { Tipo = f.Tipo, De = de, Ate = ate, TerminalId = terminalId, Operador = operador, Carga = carga, Detalhamento = f.Detalhamento };
        return (doc, entradas, efetivo);
    }

    private static List<MesDisponivelDto> Meses(IReadOnlyList<RegistroAnalise> regs, IReadOnlyList<CoberturaTerminal> cobertura) =>
        regs.Select(r => r.DataMarcacao.ToString("yyyy-MM", Fmt.Br)).Distinct().Order(StringComparer.Ordinal)
            .Select(k =>
            {
                var de = DateOnly.ParseExact(k + "-01", "yyyy-MM-dd", Fmt.Br);
                return new MesDisponivelDto(k, CalculosRelatorio.RotuloMes(k), de, de.AddMonths(1).AddDays(-1), CalculosRelatorio.MesCompleto(k, cobertura));
            }).ToList();

    private static string Cortar(string s, int max) => s.Length <= max ? s : s[..max];
}
