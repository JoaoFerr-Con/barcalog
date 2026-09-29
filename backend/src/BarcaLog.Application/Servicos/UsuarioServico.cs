using System.Security.Cryptography;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Configuracao;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Seguranca;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Excecoes;
using BarcaLog.Domain.Regras;
using Microsoft.Extensions.Options;

namespace BarcaLog.Application.Servicos;

/// <summary>Login, sessão, senha e MFA do próprio usuário.</summary>
public class AutenticacaoServico(
    IUsuarioRepositorio usuarios,
    IHashSenha hash,
    IGeradorToken tokens,
    IProtetorSegredos protetor,
    IValidadorSessao sessoes,
    IUnitOfWork uow,
    IContextoAuditoria auditoria,
    RelogioOperacional relogio,
    IOptions<SegurancaOptions> opcoes)
{
    public const string RestricaoTrocarSenha = "trocar-senha";
    public const string RestricaoConfigurarMfa = "configurar-mfa";

    // Hash de uma senha qualquer: verificado quando o e-mail não existe, pra que
    // a resposta demore o mesmo tanto e não revele quais e-mails têm conta.
    private static string? _hashFicticio;

    private SegurancaOptions O => opcoes.Value;

    /// <summary>
    /// Sempre a mesma resposta pra e-mail inexistente, senha errada, conta
    /// inativa ou bloqueada — só o MFA pendente é sinalizado, e só depois da
    /// senha certa.
    /// </summary>
    public async Task<ResultadoLogin> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var agora = relogio.AgoraUtc;
        var usuario = await usuarios.ObterPorEmailAsync(UsuarioServico.NormalizarEmail(req.Email), ct);
        if (usuario is null || !usuario.Ativo || usuario.EstaBloqueado(agora))
        {
            hash.Verificar(_hashFicticio ??= hash.Gerar(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))), req.Senha);
            return new ResultadoLogin(TipoResultadoLogin.CredenciaisInvalidas);
        }

        var verificacao = hash.Verificar(usuario.SenhaHash, req.Senha);
        if (verificacao == ResultadoVerificacaoSenha.Falha)
        {
            await RegistrarFalhaAsync(usuario, agora, "senha incorreta", ct);
            return new ResultadoLogin(TipoResultadoLogin.CredenciaisInvalidas);
        }

        if (usuario.MfaAtivo)
        {
            if (string.IsNullOrWhiteSpace(req.CodigoMfa)) return new ResultadoLogin(TipoResultadoLogin.MfaRequerido);
            var passo = Totp.Verificar(protetor.Decifrar(usuario.MfaSegredoCifrado!), req.CodigoMfa, relogio.AgoraUtcOffset, usuario.MfaUltimoPassoUsado);
            // ConsumirPasso é atômico: dois logins simultâneos com o mesmo código → só um passa.
            if (passo is null || !await usuarios.ConsumirPassoMfaAsync(usuario.Id, passo.Value, ct))
            {
                await RegistrarFalhaAsync(usuario, agora, "código MFA inválido ou já usado", ct);
                return new ResultadoLogin(TipoResultadoLogin.CredenciaisInvalidas);
            }
        }

        await usuarios.RegistrarLoginAsync(usuario.Id, agora,
            verificacao == ResultadoVerificacaoSenha.SucessoPrecisaRehash ? hash.Gerar(req.Senha) : null, ct);
        usuario.UltimoLoginEm = agora;

        var restricao = Restricao(usuario);
        var token = tokens.Gerar(usuario, restricao);
        return new ResultadoLogin(TipoResultadoLogin.Sucesso, new LoginRespostaDto(token.Token, token.ExpiraEmUtc, restricao, UsuarioServico.ParaDto(usuario, agora)));
    }

    /// <summary>Encerra TODAS as sessões do usuário (incrementa VersaoToken).</summary>
    public async Task LogoutAsync(int usuarioId, CancellationToken ct = default)
    {
        var usuario = await ObterAsync(usuarioId, ct);
        usuario.RevogarTokens();
        auditoria.DefinirAcao("Logout (sessões encerradas)", usuario.Email);
        await uow.SalvarAsync(ct);
        sessoes.Invalidar(usuario.Id);
    }

    public async Task TrocarSenhaAsync(int usuarioId, TrocarSenhaRequest req, CancellationToken ct = default)
    {
        var usuario = await ObterAsync(usuarioId, ct);
        await ExigirSenhaAtualAsync(usuario, req.SenhaAtual, ct);
        if (req.NovaSenha == req.SenhaAtual) throw new RegraNegocioException("A nova senha precisa ser diferente da atual.");
        ValidarPolitica(req.NovaSenha, usuario);
        usuario.DefinirSenha(hash.Gerar(req.NovaSenha), provisoria: false);
        auditoria.DefinirAcao("Senha alterada pelo próprio usuário", usuario.Email);
        await uow.SalvarAsync(ct);
        sessoes.Invalidar(usuario.Id);
    }

    /// <summary>Gera um segredo novo (pendente até <see cref="AtivarMfaAsync"/>). Exige a senha atual.</summary>
    public async Task<ConfiguracaoMfaDto> ConfigurarMfaAsync(int usuarioId, string senhaAtual, CancellationToken ct = default)
    {
        var usuario = await ObterAsync(usuarioId, ct);
        await ExigirSenhaAtualAsync(usuario, senhaAtual, ct);
        if (usuario.MfaAtivo) throw new RegraNegocioException("MFA já está ativo. Desative antes de cadastrar outro dispositivo.");
        var segredo = Totp.GerarSegredo();
        usuario.MfaSegredoCifrado = protetor.Cifrar(segredo);
        usuario.MfaUltimoPassoUsado = null;
        auditoria.DefinirAcao("Cadastro de MFA iniciado", usuario.Email, ocultarValores: true);
        await uow.SalvarAsync(ct);
        return new ConfiguracaoMfaDto(segredo, Totp.UriConfiguracao(O.EmissorMfa, usuario.Email, segredo));
    }

    /// <summary>Confirma o cadastro com um código válido. Tokens antigos (inclusive o restrito) deixam de valer.</summary>
    public async Task AtivarMfaAsync(int usuarioId, string codigo, CancellationToken ct = default)
    {
        var usuario = await ObterAsync(usuarioId, ct);
        if (usuario.MfaAtivo) throw new RegraNegocioException("MFA já está ativo.");
        if (usuario.MfaSegredoCifrado is null) throw new RegraNegocioException("Inicie o cadastro do MFA antes de ativar.");
        var passo = Totp.Verificar(protetor.Decifrar(usuario.MfaSegredoCifrado), codigo, relogio.AgoraUtcOffset, null)
            ?? throw new RegraNegocioException("Código inválido. Confira o horário do celular e tente de novo.");
        usuario.MfaAtivo = true;
        usuario.MfaUltimoPassoUsado = passo;
        usuario.RevogarTokens();
        auditoria.DefinirAcao("MFA ativado", usuario.Email, ocultarValores: true);
        await uow.SalvarAsync(ct);
        sessoes.Invalidar(usuario.Id);
    }

    public async Task DesativarMfaAsync(int usuarioId, DesativarMfaRequest req, CancellationToken ct = default)
    {
        var usuario = await ObterAsync(usuarioId, ct);
        if (!usuario.MfaAtivo) throw new RegraNegocioException("MFA não está ativo.");
        if (usuario.Papel == PapelUsuario.Gestor && O.ExigirMfaParaGestor)
            throw new RegraNegocioException("MFA é obrigatório para Gestor. Peça a outro Gestor para redefinir seu MFA se trocou de celular.");
        await ExigirSenhaAtualAsync(usuario, req.SenhaAtual, ct);
        if (Totp.Verificar(protetor.Decifrar(usuario.MfaSegredoCifrado!), req.Codigo, relogio.AgoraUtcOffset, usuario.MfaUltimoPassoUsado) is null)
            throw new RegraNegocioException("Código MFA inválido.");
        usuario.RemoverMfa();
        auditoria.DefinirAcao("MFA desativado pelo próprio usuário", usuario.Email, ocultarValores: true);
        await uow.SalvarAsync(ct);
        sessoes.Invalidar(usuario.Id);
    }

    public async Task<UsuarioDto> ObterPerfilAsync(int usuarioId, CancellationToken ct = default) =>
        UsuarioServico.ParaDto(await ObterAsync(usuarioId, ct), relogio.AgoraUtc);

    public string? Restricao(Usuario u) =>
        u.DeveTrocarSenha ? RestricaoTrocarSenha
        : u.Papel == PapelUsuario.Gestor && O.ExigirMfaParaGestor && !u.MfaAtivo ? RestricaoConfigurarMfa
        : null;

    private async Task ExigirSenhaAtualAsync(Usuario usuario, string senha, CancellationToken ct)
    {
        if (hash.Verificar(usuario.SenhaHash, senha) != ResultadoVerificacaoSenha.Falha) return;
        // Conta como tentativa de força bruta, igual ao login.
        await RegistrarFalhaAsync(usuario, relogio.AgoraUtc, "senha atual incorreta", ct);
        throw new RegraNegocioException("Senha atual incorreta.");
    }

    private Task RegistrarFalhaAsync(Usuario usuario, DateTime agora, string motivo, CancellationToken ct) =>
        usuarios.RegistrarFalhaLoginAsync(usuario.Id, agora, O.MaxTentativasLogin, TimeSpan.FromMinutes(O.BloqueioMinutos), motivo, ct);

    private void ValidarPolitica(string senha, Usuario usuario)
    {
        var erros = PoliticaSenha.Validar(senha, usuario.Email, usuario.Nome);
        if (erros.Count > 0) throw new RegraNegocioException(string.Join(" ", erros));
    }

    private async Task<Usuario> ObterAsync(int id, CancellationToken ct) =>
        await usuarios.ObterAsync(id, ct) ?? throw new NaoEncontradoException("Usuário não encontrado.");
}

/// <summary>Administração de usuários (somente Gestor).</summary>
public class UsuarioServico(
    IUsuarioRepositorio usuarios,
    IHashSenha hash,
    IValidadorSessao sessoes,
    IUnitOfWork uow,
    IContextoAuditoria auditoria,
    IUsuarioAtual usuarioAtual,
    RelogioOperacional relogio)
{
    public static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();

    public static UsuarioDto ParaDto(Usuario u, DateTime agoraUtc) =>
        new(u.Id, u.Nome, u.Email, u.Papel, u.Ativo, u.MfaAtivo, u.DeveTrocarSenha, u.EstaBloqueado(agoraUtc), u.UltimoLoginEm);

    public async Task<Pagina<UsuarioDto>> ListarAsync(FiltroUsuarios filtro, CancellationToken ct = default)
    {
        var agora = relogio.AgoraUtc;
        return (await usuarios.ListarAsync(filtro, ct)).Mapear(u => ParaDto(u, agora));
    }

    public async Task<UsuarioDto> ObterAsync(int id, CancellationToken ct = default) =>
        ParaDto(await ObterEntidadeAsync(id, ct), relogio.AgoraUtc);

    /// <summary>Senha inicial é provisória: o usuário é obrigado a trocar no primeiro login.</summary>
    public async Task<UsuarioDto> CriarAsync(CriarUsuarioRequest req, bool senhaProvisoria = true, CancellationToken ct = default)
    {
        var email = NormalizarEmail(req.Email);
        var nome = req.Nome.Trim();
        var erros = PoliticaSenha.Validar(req.Senha, email, nome);
        if (erros.Count > 0) throw new RegraNegocioException(string.Join(" ", erros));
        if (await usuarios.ObterPorEmailAsync(email, ct) is not null)
            throw new RegraNegocioException("Já existe usuário com esse e-mail.");
        var usuario = new Usuario { Nome = nome, Email = email, SenhaHash = hash.Gerar(req.Senha), Papel = req.Papel, Ativo = true, DeveTrocarSenha = senhaProvisoria };
        usuarios.Adicionar(usuario);
        auditoria.DefinirAcao("Usuário criado", $"{nome} <{email}> — {usuario.Papel}");
        await uow.SalvarAsync(ct);
        return ParaDto(usuario, relogio.AgoraUtc);
    }

    public async Task<UsuarioDto> AlterarPapelAsync(int id, PapelUsuario papel, CancellationToken ct = default)
    {
        var usuario = await ObterEntidadeAsync(id, ct);
        if (usuario.Papel == papel) return ParaDto(usuario, relogio.AgoraUtc);
        await GarantirQueNaoRemoveUltimoGestorAsync(usuario, ct);
        usuario.Papel = papel;
        usuario.RevogarTokens(); // o papel vai no token: força novo login
        auditoria.DefinirAcao("Papel de usuário alterado", $"{usuario.Email} → {papel}");
        await uow.SalvarAsync(ct);
        sessoes.Invalidar(id);
        return ParaDto(usuario, relogio.AgoraUtc);
    }

    public async Task<UsuarioDto> DesativarAsync(int id, CancellationToken ct = default)
    {
        var usuario = await ObterEntidadeAsync(id, ct);
        await GarantirQueNaoRemoveUltimoGestorAsync(usuario, ct);
        usuario.Ativo = false;
        usuario.RevogarTokens();
        auditoria.DefinirAcao("Usuário desativado", usuario.Email);
        await uow.SalvarAsync(ct);
        sessoes.Invalidar(id);
        return ParaDto(usuario, relogio.AgoraUtc);
    }

    public async Task<UsuarioDto> ReativarAsync(int id, CancellationToken ct = default)
    {
        var usuario = await ObterEntidadeAsync(id, ct);
        usuario.Ativo = true;
        usuario.Desbloquear();
        auditoria.DefinirAcao("Usuário reativado/desbloqueado", usuario.Email);
        await uow.SalvarAsync(ct);
        return ParaDto(usuario, relogio.AgoraUtc);
    }

    /// <summary>
    /// Recuperação de senha sem e-mail: o Gestor gera uma senha provisória
    /// aleatória (mostrada uma vez) e o usuário é obrigado a trocá-la.
    /// </summary>
    public async Task<SenhaProvisoriaDto> RedefinirSenhaAsync(int id, CancellationToken ct = default)
    {
        var usuario = await ObterEntidadeAsync(id, ct);
        var provisoria = GerarSenhaAleatoria();
        usuario.DefinirSenha(hash.Gerar(provisoria), provisoria: true);
        auditoria.DefinirAcao("Senha redefinida pelo Gestor (provisória)", usuario.Email);
        await uow.SalvarAsync(ct);
        sessoes.Invalidar(id);
        return new SenhaProvisoriaDto(provisoria);
    }

    /// <summary>Remove o MFA de alguém que perdeu o celular (a pessoa cadastra de novo no próximo login).</summary>
    public async Task<UsuarioDto> RedefinirMfaAsync(int id, CancellationToken ct = default)
    {
        var usuario = await ObterEntidadeAsync(id, ct);
        usuario.RemoverMfa();
        auditoria.DefinirAcao("MFA redefinido pelo Gestor", usuario.Email, ocultarValores: true);
        await uow.SalvarAsync(ct);
        sessoes.Invalidar(id);
        return ParaDto(usuario, relogio.AgoraUtc);
    }

    /// <summary>Evita ficar sem nenhum Gestor ativo (ninguém conseguiria mais administrar).</summary>
    private async Task GarantirQueNaoRemoveUltimoGestorAsync(Usuario alvo, CancellationToken ct)
    {
        if (alvo.Papel != PapelUsuario.Gestor || !alvo.Ativo) return;
        if (await usuarios.ContarGestoresAtivosAsync(ct) <= 1)
            throw new RegraNegocioException("Não é possível remover o último Gestor ativo.");
        if (usuarioAtual.UsuarioId == alvo.Id)
            throw new RegraNegocioException("Você não pode rebaixar ou desativar a sua própria conta de Gestor.");
    }

    private static string GerarSenhaAleatoria()
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%*";
        return new string(Enumerable.Range(0, 20).Select(_ => alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)]).ToArray());
    }

    private async Task<Usuario> ObterEntidadeAsync(int id, CancellationToken ct) =>
        await usuarios.ObterAsync(id, ct) ?? throw new NaoEncontradoException($"Usuário {id} não encontrado.");
}
