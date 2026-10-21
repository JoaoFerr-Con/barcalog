using System.Net.Http.Json;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Enums;

namespace BarcaLog.IntegrationTests;

[Collection(ColecaoApi.Nome)]
public class PortariaTests(ApiFixture api)
{
    [SkippableFact]
    public async Task Carreta_cadastrada_nao_entra_na_fila_ate_registrar_presenca()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var operador = await api.ClienteAsync("Operador");
        var (_, veiculo) = await NegativacaoIntegracaoTests.CriarFrotaAsync(operador);

        // Cadastro não é chegada.
        Assert.Equal(StatusPortaria.ForaDoPorto, veiculo.StatusPortaria);
        var fila = await operador.GetFromJsonAsync<FilaVirtualDto>("/api/v1/portaria/fila-virtual", Json.Opcoes);
        Assert.DoesNotContain(fila!.Fila, i => i.VeiculoId == veiculo.Id);

        // Presença registrada pela portaria: entra na fila, com o tempo típico do terminal.
        var r = await operador.PatchAsJsonAsync($"/api/v1/veiculos/{veiculo.Id}/status-portaria", new { statusPortaria = "NoPatio" });
        r.EnsureSuccessStatusCode();
        fila = await operador.GetFromJsonAsync<FilaVirtualDto>("/api/v1/portaria/fila-virtual", Json.Opcoes);
        var item = Assert.Single(fila!.Fila, i => i.VeiculoId == veiculo.Id);
        Assert.Equal(StatusPortaria.NoPatio, item.StatusPortaria);
        Assert.Contains(fila.TempoTipico, t => t.TerminalId == "tgpm");

        // Saída: volta para fora do porto e sai da fila.
        (await operador.PatchAsJsonAsync($"/api/v1/veiculos/{veiculo.Id}/status-portaria", new { statusPortaria = "ForaDoPorto" })).EnsureSuccessStatusCode();
        fila = await operador.GetFromJsonAsync<FilaVirtualDto>("/api/v1/portaria/fila-virtual", Json.Opcoes);
        Assert.DoesNotContain(fila!.Fila, i => i.VeiculoId == veiculo.Id);
    }

    [SkippableFact]
    public async Task Endpoints_com_constantes_nao_validadas_avisam_que_estao_descontinuados()
    {
        Skip.IfNot(api.BancoDisponivel, api.MotivoIndisponivel);
        var auditor = await api.ClienteAsync("Auditor");
        var r = await auditor.GetAsync("/api/v1/marcacoes/business-case");
        r.EnsureSuccessStatusCode(); // continua respondendo (compatibilidade)
        Assert.Equal("true", r.Headers.GetValues("Deprecation").Single());
        var atual = await auditor.GetAsync("/api/v1/indicadores/frescor");
        Assert.False(atual.Headers.Contains("Deprecation"));
    }
}
