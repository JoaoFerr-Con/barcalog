using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Excecoes;
using BarcaLog.Domain.Regras;

namespace BarcaLog.Application.Servicos;

/// <summary>
/// Importação de marcações/liberações a partir das planilhas dos terminais
/// (XLSX ou CSV), em duas etapas:
///
/// 1. <see cref="AnalisarAsync"/>: lê o arquivo, valida linha a linha e compara
///    com o banco. Classifica cada linha em novo / igual / complementar /
///    conflitante / rejeitado e grava só o lote e as linhas que precisam de
///    revisão. NADA é gravado em Marcacoes.
/// 2. <see cref="ConfirmarAsync"/>: exige decisão em todos os conflitos e grava
///    tudo numa única transação.
///
/// Chave de duplicidade: COD_MOVIMENTO. Reimportar o mesmo arquivo não cria nada.
/// </summary>
public class ImportacaoPlanilhaServico(
    ILeitorPlanilha leitor,
    IImportacaoRepositorio importacoes,
    IConvenioRepositorio convenios,
    IMarcacaoRepositorio marcacoes,
    ITerminalRepositorio terminais,
    IUnitOfWork uow,
    IContextoAuditoria auditoria,
    IUsuarioAtual usuario,
    MetricasServico metricas,
    RelogioOperacional relogio,
    IEscopoUsuario escopo)
{
    public const long TamanhoMaximoBytes = 50_000_000;
    private const int LoteConsulta = 5000;
    private static readonly SemaphoreSlim ConfirmacaoExclusiva = new(1, 1);

    /// <summary>Campos do BarcaLog e os nomes de coluna aceitos para cada um (já normalizados).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ColunasAceitas = new Dictionary<string, string[]>
    {
        ["COD_MOVIMENTO"] = ["COD_MOVIMENTO", "CODIGO_MOVIMENTO", "MOVIMENTO", "ID_MOVIMENTO"],
        ["SENHA"] = ["SENHA"],
        ["CONVENIO"] = ["CONVENIO", "NOME_CONVENIO"],
        ["COD_CONVENIO"] = ["COD_CONVENIO", "CODIGO_CONVENIO"],
        ["CICLO"] = ["CICLO"],
        ["DATA_MARCACAO"] = ["DATA_MARCACAO", "DT_MARCACAO"],
        ["HORA_MARCACAO"] = ["HORA_MARCACAO", "HR_MARCACAO"],
        ["DATA_LIBERACAO"] = ["DATA_LIBERACAO", "DT_LIBERACAO"],
        ["HORA_LIBERACAO"] = ["HORA_LIBERACAO", "HR_LIBERACAO"],
    };
    public static readonly string[] ColunasObrigatorias = ["COD_MOVIMENTO", "SENHA", "CONVENIO", "DATA_MARCACAO", "HORA_MARCACAO"];

    private sealed class Lida
    {
        public required int Linha;
        public required string MovimentoId;
        public string? Senha, Convenio, CodConvenio, Erro;
        public int Ciclo;
        public DateTime? Marcacao, Liberacao;
        public AvisoImportacao Avisos;
    }

    // ===================== 1. Análise =====================

    public async Task<LoteImportacaoDto> AnalisarAsync(string terminalId, Stream conteudo, string nomeArquivo, long tamanho, CancellationToken ct = default)
    {
        var terminal = await terminais.ObterAsync(terminalId, ct) ?? throw new RegraNegocioException($"Terminal '{terminalId}' não existe.");
        nomeArquivo = Path.GetFileName(nomeArquivo);
        var formato = Path.GetExtension(nomeArquivo).ToLowerInvariant() switch
        {
            ".xlsx" => FormatoArquivoImportacao.Xlsx,
            ".csv" => FormatoArquivoImportacao.Csv,
            _ => throw new RegraNegocioException("Envie um arquivo .xlsx ou .csv.")
        };
        if (tamanho <= 0 || tamanho > TamanhoMaximoBytes) throw new RegraNegocioException($"Arquivo vazio ou maior que {TamanhoMaximoBytes / 1_000_000} MB.");

        // Cópia em memória: precisamos do hash e de ler o conteúdo; o arquivo nunca vai para o disco.
        using var memoria = new MemoryStream();
        await conteudo.CopyToAsync(memoria, ct);
        var hash = Convert.ToHexString(SHA256.HashData(memoria.ToArray())).ToLowerInvariant();
        memoria.Position = 0;

        List<Lida> lidas;
        try
        {
            lidas = LerLinhas(memoria, formato);
        }
        catch (RegraNegocioException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new RegraNegocioException($"Não foi possível ler o arquivo como {formato.ToString().ToUpperInvariant()}: confira se ele não está corrompido ou protegido por senha.");
        }
        if (lidas.Count == 0) throw new RegraNegocioException("O arquivo não tem linhas de dados.");

        MarcarRepetidasNoArquivo(lidas);
        var validas = lidas.Where(l => l.Erro is null).ToList();
        MarcarAvisos(validas);

        // Convênios: cadastro atual, mapeamento histórico por nome e os que ainda não existem.
        var cadastro = (await convenios.ListarAsync(ct)).ToDictionary(c => c.Codigo, StringComparer.Ordinal);
        var porNome = await convenios.OperadorCargaPorNomeAsync(ct);
        var novosConvenios = validas
            .Where(l => l.CodConvenio is not null && !cadastro.ContainsKey(l.CodConvenio))
            .GroupBy(l => l.CodConvenio!)
            .Select(g =>
            {
                var nome = g.GroupBy(l => l.Convenio!).OrderByDescending(n => n.Count()).First().Key;
                var (op, carga) = porNome.TryGetValue(nome, out var h) ? h : RegrasImportacao.DeduzirOperadorCarga(nome);
                return new ConvenioDto(g.Key, nome, op, carga, true);
            })
            .OrderBy(c => c.Codigo, StringComparer.Ordinal)
            .ToList();
        var novosPorCodigo = novosConvenios.ToDictionary(c => c.Codigo, StringComparer.Ordinal);
        foreach (var l in validas.Where(l => l.CodConvenio is not null && novosPorCodigo.ContainsKey(l.CodConvenio)))
            l.Avisos |= AvisoImportacao.ConvenioNovo;

        (string Operador, string Carga) OperadorCarga(Lida l)
        {
            if (l.CodConvenio is { } cod)
            {
                if (cadastro.TryGetValue(cod, out var c)) return (c.Operador, c.Carga);
                if (novosPorCodigo.TryGetValue(cod, out var n)) return (n.Operador, n.Carga);
            }
            return porNome.TryGetValue(l.Convenio!, out var h) ? h : RegrasImportacao.DeduzirOperadorCarga(l.Convenio!);
        }

        // Comparação com o banco, em blocos.
        var lote = new LoteImportacao
        {
            Arquivo = nomeArquivo, HashSha256 = hash, TamanhoBytes = tamanho, Formato = formato, TerminalId = terminal.Id,
            Status = StatusLoteImportacao.Analisado, CriadoPor = usuario.Autor, CriadoEm = relogio.AgoraUtc,
            Linhas = lidas.Count,
            MarcacaoInicial = validas.Count == 0 ? null : validas.Min(l => l.Marcacao),
            MarcacaoFinal = validas.Count == 0 ? null : validas.Max(l => l.Marcacao),
            ConveniosNovos = novosConvenios.Count == 0 ? null : JsonSerializer.Serialize(novosConvenios),
        };
        var revisao = new List<LinhaImportacao>();
        foreach (var bloco in validas.Chunk(LoteConsulta))
        {
            var existentes = await marcacoes.ObterDadosAsync(bloco.Select(l => l.MovimentoId), ct);
            foreach (var l in bloco)
            {
                var dados = new DadosMovimento(terminal.Id, l.Senha!, l.Convenio!, l.CodConvenio, l.Ciclo, l.Marcacao!.Value, l.Liberacao);
                ClassificacaoLinhaImportacao classe;
                string? motivo = null;
                if (!existentes.TryGetValue(l.MovimentoId, out var atual))
                {
                    classe = ClassificacaoLinhaImportacao.Novo;
                    lote.Novos++;
                }
                else if (RegrasImportacao.Comparar(atual, dados) is { } comp)
                {
                    classe = comp.Classificacao;
                    motivo = comp.Descrever();
                    if (classe == ClassificacaoLinhaImportacao.Conflito) lote.Conflitantes++; else lote.Complementares++;
                }
                else
                {
                    lote.Iguais++;
                    continue; // igual: só contado
                }
                var (op, carga) = OperadorCarga(l);
                revisao.Add(new LinhaImportacao
                {
                    NumeroLinha = l.Linha, Classificacao = classe, Motivo = motivo,
                    Decisao = classe == ClassificacaoLinhaImportacao.Conflito ? DecisaoConflitoImportacao.Pendente : DecisaoConflitoImportacao.NaoSeAplica,
                    // Avisos só fazem sentido para o que vai entrar ou mudar.
                    Avisos = l.Avisos,
                    MovimentoId = l.MovimentoId, Senha = l.Senha, Convenio = l.Convenio, CodConvenio = l.CodConvenio,
                    Operador = op, Carga = carga, Ciclo = l.Ciclo, DataMarcacao = l.Marcacao, DataLiberacao = l.Liberacao
                });
            }
        }
        foreach (var l in lidas.Where(l => l.Erro is not null))
        {
            lote.Rejeitados++;
            revisao.Add(new LinhaImportacao
            {
                NumeroLinha = l.Linha, Classificacao = ClassificacaoLinhaImportacao.Rejeitado, Motivo = l.Erro,
                MovimentoId = Truncar(l.MovimentoId, 32) ?? "", Senha = Truncar(l.Senha, 32), Convenio = Truncar(l.Convenio, 100),
                CodConvenio = Truncar(l.CodConvenio, 32), DataMarcacao = l.Marcacao, DataLiberacao = l.Liberacao
            });
        }
        var comAviso = revisao.Where(r => r.Classificacao != ClassificacaoLinhaImportacao.Rejeitado && r.Avisos != AvisoImportacao.Nenhum).ToList();
        lote.ComAvisos = comAviso.Count;
        lote.AvisosEspera72h = comAviso.Count(r => r.Avisos.HasFlag(AvisoImportacao.EsperaAcima72h));
        lote.AvisosSemLiberacao = comAviso.Count(r => r.Avisos.HasFlag(AvisoImportacao.SemLiberacao));
        lote.AvisosRemarcacao = comAviso.Count(r => r.Avisos.HasFlag(AvisoImportacao.PossivelRemarcacao));
        lote.AvisosConvenioNovo = comAviso.Count(r => r.Avisos.HasFlag(AvisoImportacao.ConvenioNovo));

        await uow.ExecutarEmTransacaoAsync(async t =>
        {
            importacoes.AdicionarLote(lote);
            auditoria.DefinirAcao("Planilha analisada", $"{terminal.Nome} ({nomeArquivo}) — {lote.Linhas} linhas: {lote.Novos} novas, {lote.Iguais} iguais, {lote.Complementares} a completar, {lote.Conflitantes} conflitantes, {lote.Rejeitados} rejeitadas. Nada gravado em marcações.");
            await uow.SalvarAsync(t);
            foreach (var r in revisao) r.LoteId = lote.Id;
            await importacoes.GravarLinhasAsync(revisao, t);
        }, ct);

        return await MontarDtoAsync(lote, terminal.Nome, ct);
    }

    private List<Lida> LerLinhas(Stream memoria, FormatoArquivoImportacao formato)
    {
        var (colunas, linhas) = leitor.Ler(memoria, formato);
        var mapa = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (campo, aceitas) in ColunasAceitas)
        {
            var coluna = aceitas.FirstOrDefault(colunas.Contains);
            if (coluna is not null) mapa[campo] = coluna;
        }
        var faltando = ColunasObrigatorias.Where(c => !mapa.ContainsKey(c)).ToList();
        if (faltando.Count > 0)
            throw new RegraNegocioException($"Colunas obrigatórias ausentes: {string.Join(", ", faltando)}. Colunas encontradas: {string.Join(", ", colunas)}.");

        object? Valor(LinhaPlanilha l, string campo) => mapa.TryGetValue(campo, out var c) && l.Valores.TryGetValue(c, out var v) ? v : null;
        var agora = relogio.AgoraLocalPorto;
        var resultado = new List<Lida>();
        foreach (var l in linhas)
        {
            var id = TextoId(Valor(l, "COD_MOVIMENTO"), out var idNumerico);
            var lida = new Lida { Linha = l.Numero, MovimentoId = id ?? "" };
            resultado.Add(lida);
            lida.Senha = TextoId(Valor(l, "SENHA"), out var senhaNumerica);
            lida.Convenio = Texto(Valor(l, "CONVENIO"));
            lida.CodConvenio = TextoId(Valor(l, "COD_CONVENIO"), out var codNumerico);
            if (idNumerico || senhaNumerica || codNumerico)
            {
                lida.Erro = "Identificador gravado como número na planilha (zeros à esquerda podem ter sido perdidos). Formate COD_MOVIMENTO, SENHA e COD_CONVENIO como texto.";
                continue;
            }
            if (id is null || id.Length > 32 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) { lida.Erro = "COD_MOVIMENTO ausente ou inválido."; continue; }
            if (lida.Senha is null || lida.Senha.Length > 32) { lida.Erro = "SENHA ausente ou com mais de 32 caracteres."; continue; }
            if (lida.Convenio is null || lida.Convenio.Length > 100) { lida.Erro = "CONVENIO ausente ou com mais de 100 caracteres."; continue; }
            if (lida.CodConvenio is { Length: > 32 }) { lida.Erro = "COD_CONVENIO com mais de 32 caracteres."; continue; }
            var cicloTexto = Texto(Valor(l, "CICLO"));
            if (cicloTexto is null) lida.Ciclo = 0;
            else if (!int.TryParse(cicloTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out lida.Ciclo) || lida.Ciclo < 0)
            { lida.Erro = $"CICLO inválido: \"{cicloTexto}\"."; continue; }

            lida.Marcacao = DataHora(Valor(l, "DATA_MARCACAO"), Valor(l, "HORA_MARCACAO"), out var erroM);
            if (lida.Marcacao is null) { lida.Erro = $"Marcação inválida: {erroM}"; continue; }
            var dataLib = Valor(l, "DATA_LIBERACAO");
            var horaLib = Valor(l, "HORA_LIBERACAO");
            if (Texto(dataLib) is not null || Texto(horaLib) is not null)
            {
                lida.Liberacao = DataHora(dataLib, horaLib, out var erroL);
                if (lida.Liberacao is null) { lida.Erro = $"Liberação inválida: {erroL}"; continue; }
            }
            if (lida.Marcacao.Value.Year < 2000 || lida.Marcacao > agora.AddHours(2)) { lida.Erro = "Marcação fora da faixa aceita (2000 até agora)."; continue; }
            if (lida.Liberacao is { } lib)
            {
                if (lib < lida.Marcacao) { lida.Erro = "Liberação anterior à marcação."; continue; }
                if (lib > agora.AddHours(2)) { lida.Erro = "Liberação no futuro."; continue; }
            }
        }
        return resultado;
    }

    private static void MarcarRepetidasNoArquivo(List<Lida> lidas)
    {
        var primeira = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var l in lidas.Where(l => l.Erro is null))
        {
            if (primeira.TryGetValue(l.MovimentoId, out var linha)) l.Erro = $"COD_MOVIMENTO repetido no arquivo (primeira ocorrência na linha {linha}).";
            else primeira[l.MovimentoId] = l.Linha;
        }
    }

    private static void MarcarAvisos(List<Lida> validas)
    {
        foreach (var l in validas)
        {
            if (l.Liberacao is null) l.Avisos |= AvisoImportacao.SemLiberacao;
            else if ((l.Liberacao.Value - l.Marcacao!.Value).TotalHours > RegrasImportacao.LimiteEsperaAvisoHoras) l.Avisos |= AvisoImportacao.EsperaAcima72h;
        }
        // Remarcação provável: mesma senha, convênio e liberação; marcada de novo em poucos minutos.
        foreach (var grupo in validas.Where(l => l.Liberacao is not null).GroupBy(l => (l.Senha, l.Convenio, l.Liberacao)))
        {
            Lida? anterior = null;
            foreach (var l in grupo.OrderBy(l => l.Marcacao))
            {
                if (anterior is not null && (l.Marcacao!.Value - anterior.Marcacao!.Value).TotalMinutes <= RegrasImportacao.JanelaRemarcacaoMinutos)
                    l.Avisos |= AvisoImportacao.PossivelRemarcacao;
                anterior = l;
            }
        }
    }

    // ===================== 2. Revisão =====================

    public async Task<LoteImportacaoDto> ObterAsync(int id, CancellationToken ct = default)
    {
        var lote = await ObterLoteAsync(id, ct);
        return await MontarDtoAsync(lote, null, ct);
    }

    public async Task<Pagina<LoteImportacaoDto>> ListarAsync(FiltroLotesImportacao filtro, CancellationToken ct = default)
    {
        var pagina = await importacoes.ListarLotesAsync(filtro, ct);
        var itens = new List<LoteImportacaoDto>();
        foreach (var l in pagina.Itens) itens.Add(await MontarDtoAsync(l, l.Terminal?.Nome, ct, verificarMesmoArquivo: false));
        return new Pagina<LoteImportacaoDto>(itens, pagina.Total, pagina.NumeroPagina, pagina.TamanhoPagina);
    }

    public async Task<Pagina<LinhaImportacaoDto>> ListarLinhasAsync(int loteId, FiltroLinhasImportacao filtro, CancellationToken ct = default)
    {
        await ObterLoteAsync(loteId, ct);
        return (await importacoes.ListarLinhasAsync(loteId, filtro, ct)).Mapear(ParaDto);
    }

    public async Task<ResultadoDecisaoDto> DecidirConflitosAsync(int loteId, DecidirConflitosRequest req, CancellationToken ct = default)
    {
        if (req.Decisao is not (DecisaoConflitoImportacao.Aplicar or DecisaoConflitoImportacao.Manter))
            throw new RegraNegocioException("Decisão deve ser Aplicar ou Manter.");
        var lote = await ObterLoteAsync(loteId, ct);
        if (lote.Status != StatusLoteImportacao.Analisado) throw new RegraNegocioException($"A importação {loteId} já está {Rotulo(lote.Status)}.");
        var decididas = 0;
        await uow.ExecutarEmTransacaoAsync(async t =>
        {
            decididas = await importacoes.DecidirConflitosAsync(loteId, req.Linhas is { Count: > 0 } ? req.Linhas : null, req.Decisao, t);
            var alvo = req.Linhas is { Count: > 0 } ? $"{decididas} linha(s) escolhida(s)" : $"todos os {decididas} conflito(s) pendentes";
            await importacoes.RegistrarEventoAsync(usuario.Autor, "Conflitos de importação decididos",
                $"Importação {loteId} ({lote.Arquivo}): {(req.Decisao == DecisaoConflitoImportacao.Aplicar ? "aplicar os valores da planilha" : "manter os valores atuais")} em {alvo}.", relogio.AgoraUtc, t);
        }, ct);
        return new ResultadoDecisaoDto(decididas, await importacoes.ContarConflitosPendentesAsync(loteId, ct));
    }

    public async IAsyncEnumerable<LinhaImportacaoDto> RelatorioAsync(int loteId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await ObterLoteAsync(loteId, ct);
        await foreach (var l in importacoes.ListarLinhasParaRelatorioAsync(loteId, ct)) yield return ParaDto(l);
    }

    // ===================== 3. Confirmação =====================

    public async Task<LoteImportacaoDto> ConfirmarAsync(int loteId, CancellationToken ct = default)
    {
        if (!await ConfirmacaoExclusiva.WaitAsync(TimeSpan.Zero, ct))
            throw new RegraNegocioException("Já existe uma importação sendo gravada. Tente de novo em alguns instantes.");
        try
        {
            var lote = await ObterLoteAsync(loteId, ct);
            if (lote.Status != StatusLoteImportacao.Analisado) throw new RegraNegocioException($"A importação {loteId} já está {Rotulo(lote.Status)}.");
            // A comparação foi feita contra o banco daquele momento; se outra importação do terminal
            // foi gravada depois, os conflitos e complementos podem estar desatualizados.
            if (await importacoes.ConfirmadoDepoisAsync(lote.TerminalId, lote.CriadoEm, lote.Id, ct) is { } outra)
                throw new RegraNegocioException($"Análise desatualizada: a importação {outra.Id} ({outra.Arquivo}) foi confirmada depois desta análise. Cancele esta e envie o arquivo de novo.");
            var pendentes = await importacoes.ContarConflitosPendentesAsync(loteId, ct);
            if (pendentes > 0) throw new RegraNegocioException($"Ainda há {pendentes} conflito(s) sem decisão. Escolha aplicar ou manter antes de confirmar.");

            await uow.ExecutarEmTransacaoAsync(async t =>
            {
                // Cadastra os convênios que apareceram pela primeira vez (só os que ainda faltam).
                foreach (var c in LerConveniosNovos(lote))
                {
                    if (await convenios.ObterAsync(c.Codigo, t) is not null) continue;
                    convenios.Adicionar(new Convenio { Codigo = c.Codigo, Nome = c.Nome, Operador = c.Operador, Carga = c.Carga, Deduzido = true, CriadoEm = relogio.AgoraUtc });
                }
                await uow.SalvarAsync(t);

                var r = await importacoes.AplicarAsync(lote, t);
                lote.Inseridos = r.Inseridos;
                lote.Atualizados = r.Complementados + r.ConflitosAplicados;
                lote.Ignorados = lote.Novos + lote.Complementares + lote.Conflitantes - lote.Inseridos - lote.Atualizados;
                lote.Status = StatusLoteImportacao.Confirmado;
                lote.ConcluidoPor = usuario.Autor;
                lote.ConcluidoEm = relogio.AgoraUtc;
                auditoria.DefinirAcao("Importação de planilha confirmada",
                    $"Importação {lote.Id} — {lote.Arquivo}: {r.Inseridos} movimentos inseridos, {r.Complementados} completados, {r.ConflitosAplicados} conflitos aplicados, {lote.Ignorados} ignorados, {lote.Rejeitados} linhas rejeitadas.");
                await uow.SalvarAsync(t);
                // Novos e complementos ficam rastreáveis pelo próprio movimento; guardamos só rejeitados e conflitos.
                await importacoes.RemoverLinhasAsync(lote.Id, [ClassificacaoLinhaImportacao.Novo, ClassificacaoLinhaImportacao.Complementar], t);
            }, ct);
            metricas.InvalidarCache();
            return await MontarDtoAsync(lote, null, ct);
        }
        finally
        {
            ConfirmacaoExclusiva.Release();
        }
    }

    public async Task<LoteImportacaoDto> CancelarAsync(int loteId, CancellationToken ct = default)
    {
        var lote = await ObterLoteAsync(loteId, ct);
        if (lote.Status != StatusLoteImportacao.Analisado) throw new RegraNegocioException($"A importação {loteId} já está {Rotulo(lote.Status)}.");
        await uow.ExecutarEmTransacaoAsync(async t =>
        {
            lote.Status = StatusLoteImportacao.Cancelado;
            lote.ConcluidoPor = usuario.Autor;
            lote.ConcluidoEm = relogio.AgoraUtc;
            auditoria.DefinirAcao("Importação cancelada", $"Importação {lote.Id} — {lote.Arquivo}. Nada foi gravado em marcações.");
            await uow.SalvarAsync(t);
            await importacoes.RemoverLinhasAsync(lote.Id, Enum.GetValues<ClassificacaoLinhaImportacao>(), t);
        }, ct);
        return await MontarDtoAsync(lote, null, ct);
    }

    // ===================== Apoio =====================

    private async Task<LoteImportacao> ObterLoteAsync(int id, CancellationToken ct) =>
        await importacoes.ObterLoteAsync(id, ct) is { } lote && escopo.PodeVerTerminal(lote.TerminalId)
            ? lote
            : throw new NaoEncontradoException($"Importação {id} não encontrada.");

    private static string Rotulo(StatusLoteImportacao s) => s switch
    {
        StatusLoteImportacao.Confirmado => "confirmada",
        StatusLoteImportacao.Cancelado => "cancelada",
        _ => "em análise"
    };

    private static List<ConvenioDto> LerConveniosNovos(LoteImportacao lote) =>
        string.IsNullOrEmpty(lote.ConveniosNovos) ? [] : JsonSerializer.Deserialize<List<ConvenioDto>>(lote.ConveniosNovos) ?? [];

    private async Task<LoteImportacaoDto> MontarDtoAsync(LoteImportacao l, string? nomeTerminal, CancellationToken ct, bool verificarMesmoArquivo = true)
    {
        nomeTerminal ??= l.Terminal?.Nome ?? (await terminais.ObterAsync(l.TerminalId, ct))?.Nome;
        var pendentes = l.Status == StatusLoteImportacao.Analisado && l.Conflitantes > 0 ? await importacoes.ContarConflitosPendentesAsync(l.Id, ct) : 0;
        var mesmo = verificarMesmoArquivo ? await importacoes.ConfirmadoComMesmoHashAsync(l.HashSha256, l.TerminalId, l.Id, ct) : null;
        return new LoteImportacaoDto(
            l.Id, l.Arquivo, l.Formato, l.TamanhoBytes, l.TerminalId, nomeTerminal, l.Status, l.CriadoPor, l.CriadoEm, l.ConcluidoPor, l.ConcluidoEm,
            l.MarcacaoInicial, l.MarcacaoFinal, l.Linhas, l.Novos, l.Iguais, l.Complementares, l.Conflitantes, pendentes, l.Rejeitados, l.ComAvisos,
            new AvisosImportacaoDto(l.AvisosEspera72h, l.AvisosSemLiberacao, l.AvisosRemarcacao, l.AvisosConvenioNovo),
            LerConveniosNovos(l), l.Inseridos, l.Atualizados, l.Ignorados, mesmo);
    }

    private static LinhaImportacaoDto ParaDto(LinhaImportacao l) => new(
        l.Id, l.NumeroLinha, l.Classificacao, l.Decisao, l.Motivo, DescreverAvisos(l.Avisos), l.MovimentoId, l.Senha, l.Convenio, l.CodConvenio,
        l.Ciclo, l.DataMarcacao, l.DataLiberacao,
        l.DataMarcacao is { } m && l.DataLiberacao is { } lib ? JanelaPermanencia.EsperaEmHoras(m, lib) : null);

    public static IReadOnlyList<string> DescreverAvisos(AvisoImportacao a)
    {
        var lista = new List<string>();
        if (a.HasFlag(AvisoImportacao.EsperaAcima72h)) lista.Add("Espera acima de 72 h");
        if (a.HasFlag(AvisoImportacao.SemLiberacao)) lista.Add("Sem liberação");
        if (a.HasFlag(AvisoImportacao.PossivelRemarcacao)) lista.Add("Possível remarcação");
        if (a.HasFlag(AvisoImportacao.ConvenioNovo)) lista.Add("Convênio novo");
        return lista;
    }

    private static string? Truncar(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];

    // ---------- Conversão de células ----------

    private static string? Texto(object? v) => v switch
    {
        null => null,
        string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
        double d => d.ToString("0.###############", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        _ => v.ToString() is { } t && !string.IsNullOrWhiteSpace(t) ? t.Trim() : null
    };

    /// <summary>Identificadores precisam vir como texto: célula numérica perde zeros à esquerda.</summary>
    private static string? TextoId(object? v, out bool numerico)
    {
        numerico = v is double or int or long or decimal;
        return Texto(v);
    }

    private static readonly string[] FormatosData = ["yyyyMMdd", "dd/MM/yyyy", "yyyy-MM-dd", "d/M/yyyy", "dd-MM-yyyy"];
    private static readonly string[] FormatosHora = [@"hh\:mm\:ss", @"h\:mm\:ss", @"hh\:mm", @"h\:mm", "hhmmss"];

    /// <summary>Junta DATA + HORA (texto, data do Excel ou número de série). Sem fuso: horário local do porto.</summary>
    public static DateTime? DataHora(object? data, object? hora, out string erro)
    {
        erro = "";
        DateOnly? d = data switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            double serial when serial is > 20000 and < 80000 => DateOnly.FromDateTime(DateTime.FromOADate(serial)),
            _ => Texto(data) is { } s && DateTime.TryParseExact(s, FormatosData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var p) ? DateOnly.FromDateTime(p) : null
        };
        if (d is null) { erro = Texto(data) is { } bruto ? $"data \"{bruto}\" não reconhecida (use AAAAMMDD ou DD/MM/AAAA)." : "data ausente."; return null; }

        TimeOnly? h = hora switch
        {
            DateTime dt => TimeOnly.FromDateTime(dt),
            TimeSpan ts when ts >= TimeSpan.Zero && ts < TimeSpan.FromDays(1) => TimeOnly.FromTimeSpan(ts),
            double frac when frac is >= 0 and < 1 => TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(Math.Min(86399, Math.Round(frac * 86400)))),
            _ => Texto(hora) is { } s && TimeSpan.TryParseExact(s, FormatosHora, CultureInfo.InvariantCulture, out var ts) && ts < TimeSpan.FromDays(1) ? TimeOnly.FromTimeSpan(ts) : null
        };
        if (h is null) { erro = Texto(hora) is { } bruto ? $"hora \"{bruto}\" não reconhecida (use HH:MM:SS)." : "hora ausente."; return null; }
        return d.Value.ToDateTime(h.Value);
    }
}
