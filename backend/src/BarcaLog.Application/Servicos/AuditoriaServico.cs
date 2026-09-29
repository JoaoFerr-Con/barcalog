using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;

namespace BarcaLog.Application.Servicos;

public class AuditoriaServico(IAuditoriaRepositorio repositorio)
{
    public async Task<Pagina<LogAuditoriaDto>> BuscarAsync(FiltroAuditoria filtro, CancellationToken ct = default) =>
        (await repositorio.BuscarAsync(filtro, ct)).Mapear(l => l.ParaDto());
}
