using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Application.Servicos;

public class MarcacaoServico(IMarcacaoRepositorio marcacoes, IImportacaoRepositorio importacoes, IEscopoUsuario escopo)
{
    public async Task<Pagina<MarcacaoDto>> ListarAsync(FiltroMarcacoes filtro, CancellationToken ct = default) =>
        (await marcacoes.ListarAsync(filtro, ct)).Mapear(m => m.ParaDto());

    public async Task<MarcacaoDto> ObterAsync(string movimentoId, CancellationToken ct = default) =>
        (await ObterNoEscopoAsync(movimentoId, ct)).ParaDto();

    /// <summary>Fora do escopo do usuário = "não encontrado" (não revela que o movimento existe em outro terminal).</summary>
    private async Task<Domain.Entidades.Marcacao> ObterNoEscopoAsync(string movimentoId, CancellationToken ct)
    {
        var m = await marcacoes.ObterAsync(movimentoId, ct);
        return m is not null && escopo.PodeVerTerminal(m.TerminalId) ? m : throw new NaoEncontradoException($"Movimento {movimentoId} não encontrado.");
    }

    public async Task<MarcacaoDetalheDto> DetalharAsync(string movimentoId, CancellationToken ct = default)
    {
        var m = await ObterNoEscopoAsync(movimentoId, ct);
        var lote = m.LoteImportacaoId is { } id ? await importacoes.ObterLoteAsync(id, ct) : null;
        var juntos = m.DataLiberacao is { } lib ? await marcacoes.ContarLiberadosEmAsync(m.TerminalId, lib, ct) - 1 : 0;
        return new MarcacaoDetalheDto(m.ParaDto(), lote?.Arquivo, lote?.ConcluidoEm, lote?.ConcluidoPor, Math.Max(0, juntos));
    }
}
