using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Application.Servicos;

public class MarcacaoServico(IMarcacaoRepositorio marcacoes, IImportacaoRepositorio importacoes)
{
    public async Task<Pagina<MarcacaoDto>> ListarAsync(FiltroMarcacoes filtro, CancellationToken ct = default) =>
        (await marcacoes.ListarAsync(filtro, ct)).Mapear(m => m.ParaDto());

    public async Task<MarcacaoDto> ObterAsync(string movimentoId, CancellationToken ct = default) =>
        (await marcacoes.ObterAsync(movimentoId, ct) ?? throw new NaoEncontradoException($"Movimento {movimentoId} não encontrado.")).ParaDto();

    public async Task<MarcacaoDetalheDto> DetalharAsync(string movimentoId, CancellationToken ct = default)
    {
        var m = await marcacoes.ObterAsync(movimentoId, ct) ?? throw new NaoEncontradoException($"Movimento {movimentoId} não encontrado.");
        var lote = m.LoteImportacaoId is { } id ? await importacoes.ObterLoteAsync(id, ct) : null;
        var juntos = m.DataLiberacao is { } lib ? await marcacoes.ContarLiberadosEmAsync(m.TerminalId, lib, ct) - 1 : 0;
        return new MarcacaoDetalheDto(m.ParaDto(), lote?.Arquivo, lote?.ConcluidoEm, lote?.ConcluidoPor, Math.Max(0, juntos));
    }
}
