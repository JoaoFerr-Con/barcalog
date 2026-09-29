using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Application.Servicos;

public class UsuarioServico(
    IUsuarioRepositorio usuarios,
    IHashSenha hash,
    IGeradorToken tokens,
    IUnitOfWork uow,
    IContextoAuditoria auditoria)
{
    /// <summary>Devolve null quando e-mail/senha não conferem (a API responde 401 sem dizer qual dos dois).</summary>
    public async Task<LoginRespostaDto?> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObterPorEmailAsync(NormalizarEmail(req.Email), ct);
        if (usuario is null || !usuario.Ativo || !hash.Verificar(usuario.SenhaHash, req.Senha)) return null;
        var token = tokens.Gerar(usuario);
        return new LoginRespostaDto(token.Token, token.ExpiraEmUtc, usuario.ParaDto());
    }

    public async Task<UsuarioDto> ObterAsync(int id, CancellationToken ct = default) =>
        (await usuarios.ObterAsync(id, ct) ?? throw new NaoEncontradoException($"Usuário {id} não encontrado.")).ParaDto();

    public async Task<UsuarioDto> CriarAsync(CriarUsuarioRequest req, CancellationToken ct = default)
    {
        var email = NormalizarEmail(req.Email);
        if (await usuarios.ObterPorEmailAsync(email, ct) is not null)
            throw new RegraNegocioException($"Já existe usuário com o e-mail {email}.");
        var usuario = new Usuario
        {
            Nome = req.Nome.Trim(),
            Email = email,
            SenhaHash = hash.Gerar(req.Senha),
            Papel = req.Papel,
            Ativo = true
        };
        usuarios.Adicionar(usuario);
        auditoria.DefinirAcao("Usuário criado", $"{usuario.Nome} <{email}> — {usuario.Papel}");
        await uow.SalvarAsync(ct);
        return usuario.ParaDto();
    }

    public static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();
}
