using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Application.Servicos;

/// <summary>
/// Cadastro de convênios (código → nome, operador, carga). Criado pelas
/// importações; o Gestor confirma ou corrige operador e carga. A correção vale
/// para os próximos movimentos — os já gravados não são reescritos aqui.
/// </summary>
public class ConvenioServico(IConvenioRepositorio convenios, IUnitOfWork uow, IContextoAuditoria auditoria)
{
    public async Task<List<ConvenioDto>> ListarAsync(CancellationToken ct = default) =>
        (await convenios.ListarAsync(ct)).Select(c => new ConvenioDto(c.Codigo, c.Nome, c.Operador, c.Carga, c.Deduzido)).ToList();

    public async Task<ConvenioDto> AtualizarAsync(string codigo, AtualizarConvenioRequest req, CancellationToken ct = default)
    {
        var c = await convenios.ObterAsync(codigo, ct) ?? throw new NaoEncontradoException($"Convênio {codigo} não encontrado.");
        c.Operador = req.Operador.Trim().ToUpperInvariant();
        c.Carga = req.Carga.Trim();
        c.Deduzido = false;
        auditoria.DefinirAcao("Convênio revisado", $"{c.Codigo} · {c.Nome}: operador {c.Operador}, carga {c.Carga}");
        await uow.SalvarAsync(ct);
        return new ConvenioDto(c.Codigo, c.Nome, c.Operador, c.Carga, c.Deduzido);
    }
}
