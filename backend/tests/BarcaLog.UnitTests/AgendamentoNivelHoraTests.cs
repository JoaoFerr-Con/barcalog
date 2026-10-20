using BarcaLog.Application.Servicos;

namespace BarcaLog.UnitTests;

public class AgendamentoNivelHoraTests
{
    [Theory]
    [InlineData(5, null, "sem-capacidade")]
    [InlineData(5, 4, "acima")]
    [InlineData(4, 4, "no-limite")]
    [InlineData(3, 4, "normal")]
    public void Nivel_depende_so_da_capacidade_cadastrada(int total, int? capacidade, string esperado) =>
        Assert.Equal(esperado, AgendamentoServico.NivelHora(total, capacidade));
}
