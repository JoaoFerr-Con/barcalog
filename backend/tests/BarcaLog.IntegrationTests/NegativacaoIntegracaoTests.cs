using System.Net;
using System.Net.Http.Json;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BarcaLog.IntegrationTests;

[Collection(ColecaoApi.Nome)]
public class NegativacaoIntegracaoTests(ApiFixture api)
{
    private static int _seq;

    /// <summary>Cria transportadora + veículo novos (placa única) pra cada teste não depender dos outros.</summary>
    private static async Task<(TransportadoraDto T, VeiculoDto V)> CriarFrotaAsync(HttpClient cliente)
    {
        var n = Interlocked.Increment(ref _seq) * 10_000 + Random.Shared.Next(0, 9999);
        var criada = await cliente.PostAsJsonAsync("/api/transportadoras",
            new { nome = $"Transportes Teste {n}", cnpj = $"{n / 1_000_000 % 100:D2}.{n / 1000 % 1000:D3}.{n % 1000:D3}/0001-{Random.Shared.Next(0, 99):D2}" });
        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        var t = await criada.Content.ReadFromJsonAsync<TransportadoraDto>(Json.Opcoes);
        var resposta = await cliente.PostAsJsonAsync("/api/veiculos",
            new { placa = $"T{n % 100:D2}-{n / 100 % 10000:D4}", transportadoraId = t!.Id, modelo = "Carreta graneleira", terminalId = "tgpm" });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (t, (await resposta.Content.ReadFromJsonAsync<VeiculoDto>(Json.Opcoes))!);
    }

    [SkippableFact]
    public async Task Registrar_ocorrencia_N3_negativa_o_veiculo_no_banco()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Operador");
        var (transportadora, veiculo) = await CriarFrotaAsync(cliente);
        Assert.Equal(StatusNegativacao.Regular, veiculo.StatusNegativacao);

        var resposta = await cliente.PostAsJsonAsync("/api/ocorrencias", new
        {
            nivel = "N3",
            placa = veiculo.Placa.ToLowerInvariant(), // placa é normalizada
            transportadoraId = transportadora.Id,
            descricao = "Carga liberada fora da janela sem autorização",
            local = "Pátio de Triagem"
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var registrada = await resposta.Content.ReadFromJsonAsync<OcorrenciaRegistradaDto>(Json.Opcoes);
        Assert.True(registrada!.VeiculoBloqueado);
        Assert.Equal(NivelOcorrencia.N3, registrada.Ocorrencia.Nivel);

        // Pela API…
        var depois = await cliente.GetFromJsonAsync<VeiculoDto>($"/api/veiculos/{veiculo.Id}", Json.Opcoes);
        Assert.Equal(StatusNegativacao.Negativada, depois!.StatusNegativacao);

        // …e direto no SQL Server.
        var statusNoBanco = await api.NoBancoAsync(db => db.Veiculos.Where(v => v.Id == veiculo.Id).Select(v => v.StatusNegativacao).SingleAsync());
        Assert.Equal(StatusNegativacao.Negativada, statusNoBanco);

        // Status da transportadora é derivado da frota.
        var t = await cliente.GetFromJsonAsync<TransportadoraDto>($"/api/transportadoras/{transportadora.Id}", Json.Opcoes);
        Assert.Equal(StatusNegativacao.Negativada, t!.Status);
        Assert.Equal(1, t.CarretasNegativadas);
        Assert.False(t.AptaParaOperar);

        // Auditoria automática, com o usuário autenticado como autor.
        var logs = await cliente.GetFromJsonAsync<Pagina<LogAuditoriaDto>>($"/api/auditoria?texto={veiculo.Placa}", Json.Opcoes);
        Assert.Contains(logs!.Itens, l => l.Acao == "Ocorrência N3 registrada (bloqueio automático)" && l.Autor.Contains("operador@testes.local"));

        // Carreta negativada não pode ser agendada.
        var agendamento = await cliente.PostAsJsonAsync("/api/agendamentos",
            new { hora = "10:00", placa = veiculo.Placa, transportadoraId = transportadora.Id, terminalId = "tgpm", carga = "Soja" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, agendamento.StatusCode);
    }

    [SkippableTheory]
    [InlineData("N1")]
    [InlineData("N2")]
    public async Task Ocorrencias_N1_e_N2_nao_negativam(string nivel)
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Operador");
        var (transportadora, veiculo) = await CriarFrotaAsync(cliente);

        var resposta = await cliente.PostAsJsonAsync("/api/ocorrencias",
            new { nivel, placa = veiculo.Placa, transportadoraId = transportadora.Id, descricao = "Atraso sem comunicação prévia" });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var depois = await cliente.GetFromJsonAsync<VeiculoDto>($"/api/veiculos/{veiculo.Id}", Json.Opcoes);
        Assert.Equal(StatusNegativacao.Regular, depois!.StatusNegativacao);
    }

    [SkippableFact]
    public async Task Aprovar_contestacao_regulariza_so_o_veiculo_da_ocorrencia()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Operador");
        var (transportadora, veiculo) = await CriarFrotaAsync(cliente);
        var outro = await (await cliente.PostAsJsonAsync("/api/veiculos",
            new { placa = $"OUT-{Random.Shared.Next(1000, 9999)}", transportadoraId = transportadora.Id, terminalId = "tgpm" })).Content.ReadFromJsonAsync<VeiculoDto>(Json.Opcoes);

        async Task<OcorrenciaDto> N3(string placa) =>
            (await (await cliente.PostAsJsonAsync("/api/ocorrencias", new { nivel = "N3", placa, transportadoraId = transportadora.Id, descricao = "Divergência de peso" }))
                .Content.ReadFromJsonAsync<OcorrenciaRegistradaDto>(Json.Opcoes))!.Ocorrencia;
        var ocorrencia = await N3(veiculo.Placa);
        await N3(outro!.Placa);

        var contestacao = await (await cliente.PostAsJsonAsync("/api/contestacoes",
            new { ocorrenciaId = ocorrencia.Id, justificativa = "Pane mecânica documentada com laudo." })).Content.ReadFromJsonAsync<ContestacaoDto>(Json.Opcoes);
        Assert.Equal(StatusContestacao.Pendente, contestacao!.Status);
        Assert.Equal(StatusOcorrencia.Contestada, (await cliente.GetFromJsonAsync<OcorrenciaDto>($"/api/ocorrencias/{ocorrencia.Id}", Json.Opcoes))!.Status);

        var aprovada = await (await cliente.PostAsJsonAsync($"/api/contestacoes/{contestacao.Id}/aprovar", new { respostaOperador = "Laudo aceito" }))
            .Content.ReadFromJsonAsync<ContestacaoDto>(Json.Opcoes);

        Assert.Equal(StatusContestacao.Aprovada, aprovada!.Status);
        Assert.Equal(StatusNegativacao.Regular, (await cliente.GetFromJsonAsync<VeiculoDto>($"/api/veiculos/{veiculo.Id}", Json.Opcoes))!.StatusNegativacao);
        Assert.Equal(StatusNegativacao.Negativada, (await cliente.GetFromJsonAsync<VeiculoDto>($"/api/veiculos/{outro.Id}", Json.Opcoes))!.StatusNegativacao);
        Assert.Equal(StatusOcorrencia.Resolvida, (await cliente.GetFromJsonAsync<OcorrenciaDto>($"/api/ocorrencias/{ocorrencia.Id}", Json.Opcoes))!.Status);
        // Ainda tem outra carreta negativada → transportadora continua negativada.
        Assert.Equal(StatusNegativacao.Negativada, (await cliente.GetFromJsonAsync<TransportadoraDto>($"/api/transportadoras/{transportadora.Id}", Json.Opcoes))!.Status);
    }

    [SkippableFact]
    public async Task Seed_reproduz_o_cadastro_de_exemplo_do_frontend()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Auditor");

        var transportadoras = await cliente.GetFromJsonAsync<List<TransportadoraDto>>("/api/transportadoras", Json.Opcoes);
        var norte = Assert.Single(transportadoras!, t => t.Nome == "Norte Grãos Logística");
        Assert.Equal(StatusNegativacao.Negativada, norte.Status);
        var placa = await cliente.GetFromJsonAsync<VeiculoDto>("/api/veiculos/placa/NGL-3021", Json.Opcoes);
        Assert.Equal(StatusNegativacao.Negativada, placa!.StatusNegativacao);
        var pendentes = await cliente.GetFromJsonAsync<List<ContestacaoDto>>("/api/contestacoes?status=Pendente", Json.Opcoes);
        Assert.Contains(pendentes!, c => c.Placa == "NGL-3021");
    }
}
