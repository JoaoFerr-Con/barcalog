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

    public sealed record ResumoAlertasDto(int Ativos, IReadOnlyDictionary<TipoAlerta, int> PorTipo, DateTime? UltimaAvaliacao);

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
        ITerminalRepositorio terminais,
        IAgendamentoRepositorio agendamentos,
        IUnitOfWork uow,
        IContextoAuditoria auditoria,
        IUsuarioAtual usuario,
        RelogioOperacional relogio)
    {
        private static readonly SemaphoreSlim Avaliacao = new(1, 1);
        public const string AutorSistema = "Sistema (regras de alerta)";

        public async Task<int> AvaliarAsync(CancellationToken ct = default)
        {
            await Avaliacao.WaitAsync(ct);
            try
            {
                var entrada = await MontarEntradaAsync(ct);
                var candidatos = RegrasAlerta.Avaliar(entrada);
                var agora = relogio.AgoraUtc;
                var existentes = await alertas.ObterPorChavesAsync(candidatos.Select(c => c.Chave), ct);
                var mudancas = 0;
                foreach (var c in candidatos)
                {
                    if (!existentes.TryGetValue(c.Chave, out var a))
                    {
                        a = new Alerta { Chave = c.Chave, IdentificadoEm = agora };
                        Copiar(c, a);
                        a.AtualizadoEm = agora;
                        a.Eventos.Add(new EventoAlerta { Quando = agora, Autor = AutorSistema, Acao = "Gerado", Texto = c.Evidencia });
                        alertas.Adicionar(a);
                        mudancas++;
                        continue;
                    }
                    var antes = (a.Titulo, a.Evidencia, a.ValorObservado, a.Limite, a.Prioridade, a.Ativo);
                    Copiar(c, a);
                    if (!a.Ativo)
                    {
                        a.Ativo = true;
                        a.Eventos.Add(new EventoAlerta { Quando = agora, Autor = AutorSistema, Acao = "Voltou a ocorrer", Texto = c.Evidencia });
                    }
                    if (antes != (a.Titulo, a.Evidencia, a.ValorObservado, a.Limite, a.Prioridade, a.Ativo)) { a.AtualizadoEm = agora; mudancas++; }
                }
                var chaves = candidatos.Select(c => c.Chave).ToHashSet();
                foreach (var a in await alertas.ListarAtivosAsync(ct))
                {
                    if (chaves.Contains(a.Chave)) continue;
                    a.Ativo = false;
                    a.AtualizadoEm = agora;
                    a.Eventos.Add(new EventoAlerta { Quando = agora, Autor = AutorSistema, Acao = "Deixou de ocorrer", Texto = "A condição não aparece mais nos dados da última avaliação." });
                    mudancas++;
                }
                if (mudancas > 0)
                {
                    auditoria.DefinirAcao("Alertas reavaliados", $"{candidatos.Count} condição(ões) ativa(s); {mudancas} alerta(s) criado(s) ou alterado(s).");
                    await uow.SalvarAsync(ct);
                }
                return mudancas;
            }
            finally
            {
                Avaliacao.Release();
            }
        }

        private static void Copiar(AlertaCandidato c, Alerta a)
        {
            a.Tipo = c.Tipo; a.Regra = c.Regra; a.Prioridade = c.Prioridade; a.TerminalId = c.TerminalId; a.Periodo = c.Periodo;
            a.Titulo = c.Titulo; a.Indicador = c.Indicador; a.ValorObservado = c.ValorObservado; a.Limite = c.Limite;
            a.Evidencia = c.Evidencia; a.Origem = c.Origem; a.AcaoRecomendada = c.AcaoRecomendada;
        }

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
            var porTipo = await alertas.ContarAbertosPorTipoAsync(ct);
            return new ResumoAlertasDto(porTipo.Values.Sum(), porTipo, await alertas.UltimaAtualizacaoAsync(ct));
        }

        public async Task<AlertaDto> ObterAsync(int id, CancellationToken ct = default)
        {
            var a = await alertas.ObterAsync(id, ct) ?? throw new NaoEncontradoException($"Alerta {id} não encontrado.");
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
            var a = await alertas.ObterParaAlterarAsync(id, ct) ?? throw new NaoEncontradoException($"Alerta {id} não encontrado.");
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
