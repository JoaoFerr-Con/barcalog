using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Servicos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;
using BarcaLog.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarcaLog.Infrastructure.Seed;

/// <summary>
/// Popula o banco com o MESMO cadastro de exemplo do frontend
/// (SEED de src/data/negativacaoStore.js e SEED_AGENDAMENTOS de
/// src/pages/Agendamentos.jsx), pra manter a continuidade visual do que já
/// foi testado. Horários relativos ao "agora", como no JS.
/// </summary>
public class DbSeeder(
    BarcaLogDbContext db,
    IContextoAuditoria auditoria,
    IHashSenha hash,
    ImportacaoMarcacoesServico importacao,
    RelogioOperacional relogio,
    IOptions<SeedOptions> opcoes,
    ILogger<DbSeeder> logger)
{
    public async Task ExecutarAsync(string contentRoot, CancellationToken ct = default)
    {
        var o = opcoes.Value;
        if (o.AplicarMigrations)
        {
            logger.LogInformation("Aplicando migrations pendentes…");
            await db.Database.MigrateAsync(ct);
        }
        if (o.Usuarios.Count > 0) await SemearUsuariosAsync(o.Usuarios, ct);
        if (o.DadosExemplo) await SemearDadosExemploAsync(ct);
        if (o.ImportarMarcacoesSeVazio && !await db.Marcacoes.AnyAsync(ct))
        {
            var dir = Path.GetFullPath(Path.Combine(contentRoot, o.DiretorioDatasets));
            logger.LogInformation("Importando marcações reais de {Diretorio}…", dir);
            foreach (var r in await importacao.ImportarDiretorioAsync(dir, ct))
                logger.LogInformation("{Arquivo}: {Inseridos} inseridos de {Lidos} lidos", r.Arquivo, r.Inseridos, r.Lidos);
        }
    }

    private async Task SemearUsuariosAsync(IEnumerable<UsuarioSeed> usuarios, CancellationToken ct)
    {
        if (await db.Usuarios.AnyAsync(ct)) return;
        foreach (var u in usuarios)
        {
            db.Usuarios.Add(new Usuario
            {
                Nome = u.Nome,
                Email = UsuarioServico.NormalizarEmail(u.Email),
                SenhaHash = hash.Gerar(u.Senha),
                Papel = u.Papel,
                Ativo = true,
                DeveTrocarSenha = false // usuários de desenvolvimento; em produção não há seed de usuários
            });
        }
        auditoria.DefinirAcao("Usuários iniciais criados (seed)");
        await db.SaveChangesAsync(ct);
    }

    public async Task SemearDadosExemploAsync(CancellationToken ct = default)
    {
        if (await db.Transportadoras.AnyAsync(ct)) return;
        var agora = relogio.AgoraUtc;
        DateTime Ha(TimeSpan t) => agora - t;

        var rota = new Transportadora { Nome = "Rota Amazônia Cargas", Cnpj = "07.123.456/0001-98" };
        var agro = new Transportadora { Nome = "AgroTransportes Sul", Cnpj = "09.234.567/0001-05" };
        var norte = new Transportadora { Nome = "Norte Grãos Logística", Cnpj = "11.345.678/0001-22" };
        var transnorte = new Transportadora { Nome = "TransNorte Cargas", Cnpj = "13.456.789/0001-31" };
        var barcarena = new Transportadora { Nome = "Barcarena Transportes", Cnpj = "15.567.890/0001-95" };
        db.Transportadoras.AddRange(rota, agro, norte, transnorte, barcarena);

        Veiculo V(string placa, Transportadora t, string modelo, StatusPortaria sp, TimeSpan ha, string terminal, StatusNegativacao sn = StatusNegativacao.Regular) =>
            new() { Placa = placa, Transportadora = t, Modelo = modelo, StatusPortaria = sp, StatusPortariaDesde = Ha(ha), TerminalId = terminal, StatusNegativacao = sn };

        db.Veiculos.AddRange(
            V("ENM-1001", rota, "Carreta graneleira", StatusPortaria.NoPorto, TimeSpan.FromMinutes(40), "unitapajos"),
            V("NGL-3021", norte, "Carreta graneleira", StatusPortaria.NoPatio, TimeSpan.FromHours(3), "unitapajos", StatusNegativacao.Negativada),
            V("ATS-4410", agro, "Bitrem graneleiro", StatusPortaria.Aguardando, TimeSpan.FromMinutes(90), "tgpm"),
            V("TNC-2290", transnorte, "Carreta graneleira", StatusPortaria.DescargaFinalizada, TimeSpan.FromHours(6), "hidrovias"),
            V("BCT-7715", barcarena, "Rodotrem", StatusPortaria.NoPatio, TimeSpan.FromHours(5), "hidrovias"),
            V("RAC-5502", rota, "Carreta graneleira", StatusPortaria.Aguardando, TimeSpan.FromMinutes(20), "tgpm"),
            V("ATS-9091", agro, "Carreta graneleira", StatusPortaria.NoPatio, TimeSpan.FromHours(2), "unitapajos"));

        var jose = new Condutor { Nome = "José Ribeiro", Transportadora = norte, PlacaVinculada = "NGL-3021", StatusNegativacao = StatusNegativacao.Negativada };
        var marcos = new Condutor { Nome = "Marcos Andrade", Transportadora = agro, PlacaVinculada = "ATS-4410", StatusNegativacao = StatusNegativacao.Regular };
        var elias = new Condutor { Nome = "Elias Farias", Transportadora = rota, PlacaVinculada = "ENM-1001", StatusNegativacao = StatusNegativacao.Regular };
        db.Condutores.AddRange(jose, marcos, elias);

        var oc1 = new Ocorrencia
        {
            Nivel = NivelOcorrencia.N3,
            Placa = "NGL-3021",
            Transportadora = norte,
            Condutor = jose,
            Descricao = "Carga liberada fora da janela D0-D3 sem autorização, com divergência de peso na pesagem.",
            Local = "Pátio de Triagem",
            Responsavel = "Fiscal — Admin Teste",
            Status = StatusOcorrencia.Contestada,
            CriadoEm = Ha(TimeSpan.FromDays(5))
        };
        var oc2 = new Ocorrencia
        {
            Nivel = NivelOcorrencia.N2,
            Placa = "ATS-4410",
            Transportadora = agro,
            Condutor = marcos,
            Descricao = "Atraso de 2h40 além da janela D1, sem comunicação prévia à triagem.",
            Local = "Pátio de Triagem",
            Responsavel = "Fiscal — Admin Teste",
            Status = StatusOcorrencia.Ativa,
            CriadoEm = Ha(TimeSpan.FromDays(2))
        };
        db.Ocorrencias.AddRange(oc1, oc2);

        db.Contestacoes.Add(new Contestacao
        {
            Ocorrencia = oc1,
            Transportadora = norte,
            Justificativa = "O atraso foi causado por pane mecânica documentada. Anexamos o laudo da oficina e o boletim de ocorrência do guincho.",
            Status = StatusContestacao.Pendente,
            CriadoEm = Ha(TimeSpan.FromDays(2))
        });

        var hoje = relogio.HojeLocalPorto;
        Agendamento A(string hora, string placa, Transportadora t, string terminal, string carga, string motorista, JanelaConformidade j, StatusAgendamento s) =>
            new() { Data = hoje, Hora = TimeOnly.Parse(hora), Placa = placa, Transportadora = t, TerminalId = terminal, Carga = carga, Motorista = motorista, JanelaConformidade = j, Status = s };

        db.Agendamentos.AddRange(
            A("08:00", "ENM-1001", rota, "unitapajos", "Milho", "Elias Farias", JanelaConformidade.D0, StatusAgendamento.Finalizado),
            A("09:00", "NGL-3021", norte, "unitapajos", "Soja", "José Ribeiro", JanelaConformidade.D1, StatusAgendamento.EmOperacao),
            A("10:00", "ATS-4410", agro, "tgpm", "Soja", "Marcos Andrade", JanelaConformidade.D0, StatusAgendamento.AguardandoEntrada),
            A("10:00", "RAC-5502", rota, "tgpm", "Milho", "Paulo Souza", JanelaConformidade.D0, StatusAgendamento.Confirmado),
            A("11:00", "BCT-7715", barcarena, "hidrovias", "Soja Segregado", "Carla Lima", JanelaConformidade.D2, StatusAgendamento.ACaminho),
            A("11:00", "TNC-2290", transnorte, "hidrovias", "Caçamba", "Rui Nogueira", JanelaConformidade.D3, StatusAgendamento.Atrasado),
            A("12:00", "ATS-9091", agro, "unitapajos", "Soja", "Fábio Melo", JanelaConformidade.D0, StatusAgendamento.Agendado));

        auditoria.DefinirAcao("Carga inicial do cadastro de exemplo (seed)", "Mesmos dados de exemplo do negativacaoStore.js / Agendamentos.jsx");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Cadastro de exemplo criado.");
    }
}
