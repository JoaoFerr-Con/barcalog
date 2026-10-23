using System.ComponentModel.DataAnnotations;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Application.Dtos
{
    public sealed record EventoAlertaDto(DateTime Quando, string Autor, string Acao, string? Texto);

    public sealed record AlertaDto(
        int Id, TipoAlerta Tipo, string Regra, PrioridadeAlerta Prioridade, string? TerminalId, string? Terminal, string Periodo,
        string Titulo, string Indicador, string ValorObservado, string Limite, string Evidencia, string Origem, string? AcaoRecomendada,
        EstadoAlerta Estado, bool Ativo, DateTime IdentificadoEm, DateTime AtualizadoEm, string? Responsavel,
        IReadOnlyList<EventoAlertaDto>? Eventos);

    /// <summary>Contagem de abertos; última avaliação bem-sucedida e a última tentativa (que pode ter falhado).</summary>
    public sealed record ResumoAlertasDto(int Ativos, IReadOnlyDictionary<TipoAlerta, int> PorTipo, DateTime? UltimaAvaliacao, ResultadoAvaliacaoDto? UltimaTentativa);

    /// <summary>
    /// Resultado de uma avaliação das regras. Situacao: concluida | falhou | em-andamento.
    /// Comparacao (da confiabilidade com a avaliação bem-sucedida anterior): melhorou | igual | piorou | primeira.
    /// </summary>
    public sealed record ResultadoAvaliacaoDto(
        int? Id, string Situacao, DateTime Inicio, DateTime? Fim, OrigemAvaliacao Origem, string Autor, string? Erro,
        DateTime? DadosAte, DateTime? UltimaImportacaoEm, int Registros, bool DadosNovos,
        int Criados, int Alterados, int Encerrados, int Ativos,
        string? Confiabilidade, string? ConfiabilidadeAnterior, string Comparacao, string Resumo,
        IReadOnlyList<MotivoConfiabilidade> Motivos);

    public class FiltroAvaliacoes : FiltroPaginado;

    public class FiltroAlertas : FiltroPaginado
    {
        public TipoAlerta? Tipo { get; set; }
        public EstadoAlerta? Estado { get; set; }
        [StringLength(32)] public string? TerminalId { get; set; }
        /// <summary>Padrão: só os que estão ocorrendo (Ativo) e ainda não foram resolvidos/descartados.</summary>
        public bool SomenteAbertos { get; set; } = true;
    }

    public class TextoAlertaRequest
    {
        [StringLength(1000), TextoSimples] public string? Texto { get; set; }
    }

    /// <summary>
    /// Estimativa das próximas horas pelo padrão histórico (não é previsão por modelo).
    /// Geral = terminal filtrado ou todos somados; Terminais = cada terminal.
    /// </summary>
    public sealed record ProximasHorasDto(DateTime GeradoEm, int Horas, double LimiteRisco, EstimativaTerminal Geral, IReadOnlyList<EstimativaTerminal> Terminais);
}

namespace BarcaLog.Application.Servicos
{
    /// <summary>
    /// Avalia as regras de alerta e guarda cada alerta com estado e histórico.
    /// A avaliação roda periodicamente, depois de cada importação e sob demanda;
    /// um alerta já resolvido/descartado não é reaberto pela mesma condição.
    /// </summary>
    public class AlertasServico(
        IAlertaRepositorio alertas,
        MetricasServico metricas,
        IMarcacaoRepositorio marcacoes,
        IImportacaoRepositorio importacoes,
        ITerminalRepositorio terminais,
        IAgendamentoRepositorio agendamentos,
        IUnitOfWork uow,
        IContextoAuditoria auditoria,
        IUsuarioAtual usuario,
        RelogioOperacional relogio,
        IEscopoUsuario escopo)
    {
        private static readonly SemaphoreSlim Avaliacao = new(1, 1);
        public const string AutorSistema = "Sistema (regras de alerta)";

        /// <summary>
        /// Avalia as regras de alerta e registra a avaliação (data, autor, dados usados, o que mudou,
        /// confiabilidade). Uma por vez: com outra em andamento, a manual responde "em-andamento" sem
        /// esperar; a automática e a da importação aguardam. Falha também fica registrada.
        /// </summary>
        public async Task<ResultadoAvaliacaoDto> AvaliarAsync(OrigemAvaliacao origem, CancellationToken ct = default)
        {
            var inicio = relogio.AgoraUtc;
            if (origem == OrigemAvaliacao.Manual)
            {
                if (!await Avaliacao.WaitAsync(0, ct))
                    return new ResultadoAvaliacaoDto(null, "em-andamento", inicio, null, origem, usuario.Autor, null, null, null, 0, false, 0, 0, 0, 0,
                        null, null, "igual", "Já existe uma avaliação em andamento. Aguarde alguns segundos e atualize a página.", []);
            }
            else await Avaliacao.WaitAsync(ct);
            try
            {
                var anterior = await alertas.UltimaAvaliacaoAsync(true, ct);
                var entrada = await MontarEntradaAsync(ct);
                var dadosAte = entrada.UltimaMarcacaoPorTerminal.Values.Max();
                var ultimaImportacao = (await importacoes.UltimaConfirmadaPorTerminalAsync(ct)).Values.Select(v => (DateTime?)v.Quando).Max();
                var registros = entrada.Liberadas.Count + (await marcacoes.ContarSemLiberacaoAsync(null, null, ct)).Values.Sum();
                var dadosNovos = anterior is null || anterior.DadosAte != dadosAte || anterior.UltimaImportacaoEm != ultimaImportacao || anterior.Registros != registros;
                var (geral, _) = EstimativaJanelas.Calcular(entrada.Liberadas,
                    entrada.Terminais.Select(t => new TerminalEstimativa(t.Id, t.Nome, entrada.CapacidadeHoraria?.GetValueOrDefault(t.Id), entrada.UltimaMarcacaoPorTerminal.GetValueOrDefault(t.Id))).ToList(),
                    entrada.AgoraLocalPorto, entrada.Parametros.LimiteRazaoChegadaLiberacao, entrada.Parametros.DiasSemDadosAlerta);

                var (criados, alterados, encerrados) = await AplicarAsync(RegrasAlerta.Avaliar(entrada), ct);
                if (criados + alterados + encerrados > 0)
                {
                    auditoria.DefinirAcao("Alertas reavaliados", $"{criados} novo(s), {alterados} alterado(s), {encerrados} encerrado(s).");
                    await uow.SalvarAsync(ct);
                }
                // Contado depois de gravar: abertos = ativos e ainda não resolvidos/descartados.
                var ativos = (await alertas.ContarAbertosPorTipoAsync(null, ct)).Values.Sum();
                var registro = new AvaliacaoAlertas
                {
                    Inicio = inicio, Fim = relogio.AgoraUtc, Origem = origem, Autor = usuario.Autor, Sucesso = true,
                    DadosAte = dadosAte, UltimaImportacaoEm = ultimaImportacao, Registros = registros, DadosNovos = dadosNovos,
                    Criados = criados, Alterados = alterados, Encerrados = encerrados, Ativos = ativos, Confiabilidade = geral.Confiabilidade
                };
                alertas.AdicionarAvaliacao(registro);
                await uow.SalvarAsync(ct);
                return ParaResultado(registro, anterior?.Confiabilidade, geral.Motivos);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                uow.DescartarAlteracoes();
                var falha = new AvaliacaoAlertas
                {
                    Inicio = inicio, Fim = relogio.AgoraUtc, Origem = origem, Autor = usuario.Autor, Sucesso = false,
                    Erro = "A avaliação não foi concluída por um erro interno. Os alertas anteriores foram mantidos; tente de novo e, se persistir, avise o suporte."
                };
                alertas.AdicionarAvaliacao(falha);
                try { await uow.SalvarAsync(ct); } catch (Exception) when (!ct.IsCancellationRequested) { uow.DescartarAlteracoes(); }
                if (origem != OrigemAvaliacao.Manual) throw;
                return ParaResultado(falha, null, []) with { Situacao = "falhou" };
            }
            finally
            {
                Avaliacao.Release();
            }
        }

        private async Task<(int Criados, int Alterados, int Encerrados)> AplicarAsync(List<AlertaCandidato> candidatos, CancellationToken ct)
        {
            var agora = relogio.AgoraUtc;
            var existentes = await alertas.ObterPorChavesAsync(candidatos.Select(c => c.Chave), ct);
            int criados = 0, alterados = 0, encerrados = 0;
            foreach (var c in candidatos)
            {
                if (!existentes.TryGetValue(c.Chave, out var a))
                {
                    a = new Alerta { Chave = c.Chave, IdentificadoEm = agora };
                    Copiar(c, a);
                    a.AtualizadoEm = agora;
                    a.Eventos.Add(new EventoAlerta { Quando = agora, Autor = AutorSistema, Acao = "Gerado", Texto = Caber(c.Evidencia, 1000) });
                    alertas.Adicionar(a);
                    criados++;
                    continue;
                }
                var antes = (a.Titulo, a.Evidencia, a.ValorObservado, a.Limite, a.Prioridade, a.Ativo);
                Copiar(c, a);
                if (!a.Ativo)
                {
                    a.Ativo = true;
                    a.Eventos.Add(new EventoAlerta { Quando = agora, Autor = AutorSistema, Acao = "Voltou a ocorrer", Texto = Caber(c.Evidencia, 1000) });
                }
                if (antes != (a.Titulo, a.Evidencia, a.ValorObservado, a.Limite, a.Prioridade, a.Ativo)) { a.AtualizadoEm = agora; alterados++; }
            }
            var chaves = candidatos.Select(c => c.Chave).ToHashSet();
            foreach (var a in await alertas.ListarAtivosAsync(ct))
            {
                if (chaves.Contains(a.Chave)) continue;
                a.Ativo = false;
                a.AtualizadoEm = agora;
                a.Eventos.Add(new EventoAlerta { Quando = agora, Autor = AutorSistema, Acao = "Deixou de ocorrer", Texto = "A condição não aparece mais nos dados da última avaliação." });
                encerrados++;
            }
            return (criados, alterados, encerrados);
        }

        public static ResultadoAvaliacaoDto ParaResultado(AvaliacaoAlertas a, string? confAnterior, IReadOnlyList<MotivoConfiabilidade> motivos)
        {
            var comparacao = !a.Sucesso || a.Confiabilidade is null ? "igual"
                : confAnterior is null ? "primeira"
                : Metricas.Confiabilidade.Nivel(a.Confiabilidade) > Metricas.Confiabilidade.Nivel(confAnterior) ? "melhorou"
                : Metricas.Confiabilidade.Nivel(a.Confiabilidade) < Metricas.Confiabilidade.Nivel(confAnterior) ? "piorou" : "igual";
            var resumo = !a.Sucesso ? a.Erro ?? "A avaliação falhou."
                : (a.DadosNovos ? "Dados novos desde a última avaliação. " : "Nenhum dado novo desde a última avaliação. ")
                  + (a.Criados + a.Alterados + a.Encerrados == 0 ? "Nenhum alerta mudou."
                      : $"{a.Criados} alerta(s) novo(s), {a.Alterados} alterado(s), {a.Encerrados} encerrado(s).");
            return new ResultadoAvaliacaoDto(a.Id, a.Sucesso ? "concluida" : "falhou", a.Inicio, a.Fim, a.Origem, a.Autor, a.Erro,
                a.DadosAte, a.UltimaImportacaoEm, a.Registros, a.DadosNovos, a.Criados, a.Alterados, a.Encerrados, a.Ativos,
                a.Confiabilidade, confAnterior, comparacao, resumo, motivos);
        }

        public async Task<Pagina<ResultadoAvaliacaoDto>> ListarAvaliacoesAsync(FiltroAvaliacoes filtro, CancellationToken ct = default) =>
            (await alertas.ListarAvaliacoesAsync(filtro, ct)).Mapear(a => ParaResultado(a, null, []) with { Comparacao = a.Sucesso ? "—" : "igual" });

        /// <summary>
        /// Copia o candidato para o registro, ajustando cada texto ao tamanho da coluna: um
        /// texto longo demais não pode derrubar a avaliação inteira (foi o que aconteceu em
        /// 20/10 com o limite do alerta de previsão, e a avaliação parou de rodar).
        /// </summary>
        public static void Copiar(AlertaCandidato c, Alerta a)
        {
            a.Tipo = c.Tipo; a.Regra = Caber(c.Regra, Alerta.Tamanho.Regra)!; a.Prioridade = c.Prioridade; a.TerminalId = c.TerminalId;
            a.Periodo = Caber(c.Periodo, Alerta.Tamanho.Periodo)!; a.Titulo = Caber(c.Titulo, Alerta.Tamanho.Titulo)!;
            a.Indicador = Caber(c.Indicador, Alerta.Tamanho.Indicador)!; a.ValorObservado = Caber(c.ValorObservado, Alerta.Tamanho.ValorObservado)!;
            a.Limite = Caber(c.Limite, Alerta.Tamanho.Limite)!; a.Evidencia = Caber(c.Evidencia, Alerta.Tamanho.Evidencia)!;
            a.Origem = Caber(c.Origem, Alerta.Tamanho.Origem)!; a.AcaoRecomendada = Caber(c.AcaoRecomendada, Alerta.Tamanho.AcaoRecomendada);
        }

        public static string? Caber(string? texto, int maximo) =>
            texto is null || texto.Length <= maximo ? texto : texto[..(maximo - 1)].TrimEnd() + "…";

        private async Task<EntradaAlertas> MontarEntradaAsync(CancellationToken ct)
        {
            var parametros = await terminais.ObterParametrosAsync(false, ct);
            var lista = await terminais.ListarAsync(ct);
            var agoraLocal = relogio.AgoraLocalPorto;
            var ultimos = await marcacoes.UltimosEventosPorTerminalAsync(ct);
            var abertas = await marcacoes.ListarAbertasMarcadasAntesDeAsync(agoraLocal.AddHours(-parametros.LimiteEsperaLongaHoras), ct);
            var atrasados = (await agendamentos.ListarDoDiaAsync(DateOnly.FromDateTime(agoraLocal), null, ct))
                .Where(a => a.Status == StatusAgendamento.Atrasado).Select(a => (a.Placa, a.TerminalId, a.Hora)).ToList();
            return new EntradaAlertas(
                await metricas.TodosLiberadosAsync(ct),
                lista.Select(t => (t.Id, t.Nome)).ToList(),
                parametros,
                lista.ToDictionary(t => t.Id, t => ultimos.TryGetValue(t.Id, out var u) ? u.UltimaMarcacao : null),
                abertas,
                atrasados,
                agoraLocal,
                lista.ToDictionary(t => t.Id, t => t.CapacidadeHoraria));
        }

        public async Task<ProximasHorasDto?> ProximasHorasAsync(string? terminalId, CancellationToken ct = default)
        {
            var parametros = await terminais.ObterParametrosAsync(false, ct);
            var lista = await terminais.ListarAsync(ct);
            if (!string.IsNullOrWhiteSpace(terminalId) && lista.All(t => t.Id != terminalId)) return null;
            var ultimos = await marcacoes.UltimosEventosPorTerminalAsync(ct);
            var registros = await metricas.TodosLiberadosAsync(ct);
            var agora = relogio.AgoraLocalPorto;
            var entrada = lista.Select(t => new TerminalEstimativa(t.Id, t.Nome, t.CapacidadeHoraria,
                ultimos.TryGetValue(t.Id, out var u) ? u.UltimaMarcacao : null)).ToList();
            var (geral, porTerminal) = EstimativaJanelas.Calcular(registros, entrada, agora, parametros.LimiteRazaoChegadaLiberacao, parametros.DiasSemDadosAlerta);
            if (!string.IsNullOrWhiteSpace(terminalId)) geral = porTerminal.First(t => t.TerminalId == terminalId);
            return new ProximasHorasDto(agora, EstimativaJanelas.HorasHorizonte, parametros.LimiteRazaoChegadaLiberacao, geral, porTerminal);
        }

        public async Task<Pagina<AlertaDto>> ListarAsync(FiltroAlertas f, CancellationToken ct = default)
        {
            var nomes = (await terminais.ListarAsync(ct)).ToDictionary(t => t.Id, t => t.Nome);
            return (await alertas.ListarAsync(f, ct)).Mapear(a => ParaDto(a, nomes, false));
        }

        public async Task<ResumoAlertasDto> ResumoAsync(CancellationToken ct = default)
        {
            var porTipo = await alertas.ContarAbertosPorTipoAsync(escopo.TerminalVinculado, ct);
            var sucesso = await alertas.UltimaAvaliacaoAsync(true, ct);
            var tentativa = await alertas.UltimaAvaliacaoAsync(false, ct);
            return new ResumoAlertasDto(porTipo.Values.Sum(), porTipo, sucesso?.Inicio,
                tentativa is null ? null : ParaResultado(tentativa, null, []));
        }

        public async Task<AlertaDto> ObterAsync(int id, CancellationToken ct = default)
        {
            var a = await alertas.ObterAsync(id, ct);
            if (a is null || !escopo.PodeVerTerminal(a.TerminalId)) throw new NaoEncontradoException($"Alerta {id} não encontrado.");
            var nomes = (await terminais.ListarAsync(ct)).ToDictionary(t => t.Id, t => t.Nome);
            return ParaDto(a, nomes, true);
        }

        public Task<AlertaDto> AssumirAsync(int id, string? texto, CancellationToken ct = default) =>
            AgirAsync(id, "Assumido", texto, false, a =>
            {
                if (a.Estado is EstadoAlerta.Resolvido or EstadoAlerta.Descartado) throw new RegraNegocioException("Alerta encerrado: reabra antes de assumir.");
                a.Estado = EstadoAlerta.EmAnalise;
                a.Responsavel = usuario.Autor;
            }, ct);

        public Task<AlertaDto> ComentarAsync(int id, string? texto, CancellationToken ct = default) =>
            AgirAsync(id, "Ação registrada", texto, true, _ => { }, ct);

        public Task<AlertaDto> ResolverAsync(int id, string? texto, CancellationToken ct = default) =>
            AgirAsync(id, "Resolvido", texto, true, a => a.Estado = EstadoAlerta.Resolvido, ct);

        public Task<AlertaDto> DescartarAsync(int id, string? texto, CancellationToken ct = default) =>
            AgirAsync(id, "Descartado", texto, true, a => a.Estado = EstadoAlerta.Descartado, ct);

        public Task<AlertaDto> ReabrirAsync(int id, string? texto, CancellationToken ct = default) =>
            AgirAsync(id, "Reaberto", texto, false, a =>
            {
                if (a.Estado is not (EstadoAlerta.Resolvido or EstadoAlerta.Descartado)) throw new RegraNegocioException("Só alertas resolvidos ou descartados podem ser reabertos.");
                a.Estado = EstadoAlerta.Novo;
                a.Responsavel = null;
            }, ct);

        private async Task<AlertaDto> AgirAsync(int id, string acao, string? texto, bool textoObrigatorio, Action<Alerta> mudar, CancellationToken ct)
        {
            texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
            if (textoObrigatorio && texto is null)
                throw new RegraNegocioException(acao == "Descartado" ? "Informe a justificativa do descarte." : "Descreva o que foi feito.");
            var a = await alertas.ObterParaAlterarAsync(id, ct);
            if (a is null || !escopo.PodeVerTerminal(a.TerminalId)) throw new NaoEncontradoException($"Alerta {id} não encontrado.");
            mudar(a);
            a.AtualizadoEm = relogio.AgoraUtc;
            a.Eventos.Add(new EventoAlerta { Quando = a.AtualizadoEm, Autor = usuario.Autor, Acao = acao, Texto = texto });
            auditoria.DefinirAcao($"Alerta {acao.ToLowerInvariant()}", $"#{a.Id} {a.Titulo} ({a.Periodo})" + (texto is null ? "" : $": {texto}"));
            await uow.SalvarAsync(ct);
            return await ObterAsync(id, ct);
        }

        private static AlertaDto ParaDto(Alerta a, IReadOnlyDictionary<string, string> nomes, bool comEventos) => new(
            a.Id, a.Tipo, a.Regra, a.Prioridade, a.TerminalId, a.TerminalId is null ? null : nomes.GetValueOrDefault(a.TerminalId, a.TerminalId), a.Periodo,
            a.Titulo, a.Indicador, a.ValorObservado, a.Limite, a.Evidencia, a.Origem, a.AcaoRecomendada,
            a.Estado, a.Ativo, a.IdentificadoEm, a.AtualizadoEm, a.Responsavel,
            comEventos ? a.Eventos.OrderBy(e => e.Quando).ThenBy(e => e.Id).Select(e => new EventoAlertaDto(e.Quando, e.Autor, e.Acao, e.Texto)).ToList() : null);
    }
}
