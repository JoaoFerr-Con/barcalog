using System.Net;
using System.Net.Http.Json;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BarcaLog.IntegrationTests;

[Collection(ColecaoApi.Nome)]
public class NegativacaoIntegracaoTests(ApiFixture api)
{
    /// <summary>Cria transportadora + veículo novos pra cada teste não depender dos outros.</summary>
    public static async Task<(TransportadoraDto T, VeiculoDto V)> CriarFrotaAsync(HttpClient cliente)
    {
        var criada = await cliente.PostAsJsonAsync("/api/v1/transportadoras", new { nome = $"Transportes Teste {Dados.Proximo()}", cnpj = Dados.Cnpj() });
        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        var t = await criada.Content.ReadFromJsonAsync<TransportadoraDto>(Json.Opcoes);
        var resposta = await cliente.PostAsJsonAsync("/api/v1/veiculos",
            new { placa = Dados.Placa(), transportadoraId = t!.Id, modelo = "Carreta graneleira", terminalId = "tgpm" });
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

        var resposta = await cliente.PostAsJsonAsync("/api/v1/ocorrencias", new
        {
            nivel = "N3",
            placa = veiculo.Placa.ToLowerInvariant().Replace("-", ""), // placa é normalizada
            transportadoraId = transportadora.Id,
            descricao = "Carga liberada fora da janela sem autorização",
            local = "Pátio de Triagem"
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var registrada = await resposta.Content.ReadFromJsonAsync<OcorrenciaRegistradaDto>(Json.Opcoes);
        Assert.True(registrada!.VeiculoBloqueado);
        Assert.Equal(NivelOcorrencia.N3, registrada.Ocorrencia.Nivel);

        var depois = await cliente.GetFromJsonAsync<VeiculoDto>($"/api/v1/veiculos/{veiculo.Id}", Json.Opcoes);
        Assert.Equal(StatusNegativacao.Negativada, depois!.StatusNegativacao);

        var statusNoBanco = await api.NoBancoAsync(db => db.Veiculos.Where(v => v.Id == veiculo.Id).Select(v => v.StatusNegativacao).SingleAsync());
        Assert.Equal(StatusNegativacao.Negativada, statusNoBanco);

        var t = await cliente.GetFromJsonAsync<TransportadoraDto>($"/api/v1/transportadoras/{transportadora.Id}", Json.Opcoes);
        Assert.Equal(StatusNegativacao.Negativada, t!.Status);
        Assert.Equal(1, t.CarretasNegativadas);
        Assert.False(t.AptaParaOperar);

        var logs = await cliente.GetFromJsonAsync<Pagina<LogAuditoriaDto>>($"/api/v1/auditoria?texto={veiculo.Placa}", Json.Opcoes);
        Assert.Contains(logs!.Itens, l => l.Acao == "Ocorrência N3 registrada (bloqueio automático)" && l.Autor.Contains("operador@testes.local"));

        var agendamento = await cliente.PostAsJsonAsync("/api/v1/agendamentos",
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

        var resposta = await cliente.PostAsJsonAsync("/api/v1/ocorrencias",
            new { nivel, placa = veiculo.Placa, transportadoraId = transportadora.Id, descricao = "Atraso sem comunicação prévia" });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var depois = await cliente.GetFromJsonAsync<VeiculoDto>($"/api/v1/veiculos/{veiculo.Id}", Json.Opcoes);
        Assert.Equal(StatusNegativacao.Regular, depois!.StatusNegativacao);
    }

    [SkippableFact]
    public async Task Aprovar_contestacao_regulariza_so_o_veiculo_da_ocorrencia()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Operador");
        var (transportadora, veiculo) = await CriarFrotaAsync(cliente);
        var outro = await (await cliente.PostAsJsonAsync("/api/v1/veiculos",
            new { placa = Dados.Placa(), transportadoraId = transportadora.Id, terminalId = "tgpm" })).Content.ReadFromJsonAsync<VeiculoDto>(Json.Opcoes);

        var ocorrencia = await RegistrarN3Async(cliente, veiculo.Placa, transportadora.Id);
        await RegistrarN3Async(cliente, outro!.Placa, transportadora.Id);

        var contestacao = await (await cliente.PostAsJsonAsync("/api/v1/contestacoes",
            new { ocorrenciaId = ocorrencia.Id, justificativa = "Pane mecânica documentada com laudo." })).Content.ReadFromJsonAsync<ContestacaoDto>(Json.Opcoes);
        Assert.Equal(StatusContestacao.Pendente, contestacao!.Status);
        Assert.Equal(StatusOcorrencia.Contestada, (await cliente.GetFromJsonAsync<OcorrenciaDto>($"/api/v1/ocorrencias/{ocorrencia.Id}", Json.Opcoes))!.Status);

        var aprovada = await (await cliente.PostAsJsonAsync($"/api/v1/contestacoes/{contestacao.Id}/aprovar", new { respostaOperador = "Laudo aceito" }))
            .Content.ReadFromJsonAsync<ContestacaoDto>(Json.Opcoes);

        Assert.Equal(StatusContestacao.Aprovada, aprovada!.Status);
        Assert.Equal(StatusNegativacao.Regular, (await cliente.GetFromJsonAsync<VeiculoDto>($"/api/v1/veiculos/{veiculo.Id}", Json.Opcoes))!.StatusNegativacao);
        Assert.Equal(StatusNegativacao.Negativada, (await cliente.GetFromJsonAsync<VeiculoDto>($"/api/v1/veiculos/{outro.Id}", Json.Opcoes))!.StatusNegativacao);
        Assert.Equal(StatusOcorrencia.Resolvida, (await cliente.GetFromJsonAsync<OcorrenciaDto>($"/api/v1/ocorrencias/{ocorrencia.Id}", Json.Opcoes))!.Status);
        Assert.Equal(StatusNegativacao.Negativada, (await cliente.GetFromJsonAsync<TransportadoraDto>($"/api/v1/transportadoras/{transportadora.Id}", Json.Opcoes))!.Status);
    }

    public static async Task<OcorrenciaDto> RegistrarN3Async(HttpClient cliente, string placa, int transportadoraId)
    {
        var r = await cliente.PostAsJsonAsync("/api/v1/ocorrencias", new { nivel = "N3", placa, transportadoraId, descricao = "Divergência de peso" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<OcorrenciaRegistradaDto>(Json.Opcoes))!.Ocorrencia;
    }

    [SkippableFact]
    public async Task Seed_reproduz_o_cadastro_de_exemplo_do_frontend()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var cliente = await api.ClienteAsync("Auditor");

        var transportadoras = await cliente.GetFromJsonAsync<List<TransportadoraDto>>("/api/v1/transportadoras", Json.Opcoes);
        var norte = Assert.Single(transportadoras!, t => t.Nome == "Norte Grãos Logística");
        Assert.Equal(StatusNegativacao.Negativada, norte.Status);
        Assert.True(BarcaLog.Domain.Regras.Cnpj.EhValido(norte.Cnpj));
        var placa = await cliente.GetFromJsonAsync<VeiculoDto>("/api/v1/veiculos/placa/NGL-3021", Json.Opcoes);
        Assert.Equal(StatusNegativacao.Negativada, placa!.StatusNegativacao);
        var pendentes = await cliente.GetFromJsonAsync<Pagina<ContestacaoDto>>("/api/v1/contestacoes?status=Pendente", Json.Opcoes);
        Assert.Contains(pendentes!.Itens, c => c.Placa == "NGL-3021");
    }
}
