using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Application.Servicos;

public class MarcacaoServico(IMarcacaoRepositorio marcacoes)
{
    public async Task<Pagina<MarcacaoDto>> ListarAsync(FiltroMarcacoes filtro, CancellationToken ct = default) =>
        (await marcacoes.ListarAsync(filtro, ct)).Mapear(m => m.ParaDto());

    public async Task<MarcacaoDto> ObterAsync(string movimentoId, CancellationToken ct = default) =>
        (await marcacoes.ObterAsync(movimentoId, ct) ?? throw new NaoEncontradoException($"Movimento {movimentoId} não encontrado.")).ParaDto();
}
