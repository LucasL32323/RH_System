using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Seguranca;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Autenticacao;

public sealed record AlterarSenhaDto(string? SenhaAtual, string? NovaSenha);

public sealed record SolicitarRecuperacaoDto(string? Email);

public sealed record RedefinirSenhaDto(string? Token, string? NovaSenha);

/// <summary>Endereço da tela de redefinição, usado no link do e-mail (configuração App:UrlRedefinicaoSenha).</summary>
public sealed record OpcoesRecuperacaoSenha(string UrlRedefinicaoSenha);

/// <summary>RF04: o usuário logado troca a própria senha informando a atual.</summary>
public sealed class AlterarSenha
{
    private readonly IUsuarioRepositorio _usuarios;
    private readonly IHashSenha _hash;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;

    public AlterarSenha(IUsuarioRepositorio usuarios, IHashSenha hash, IAuditoria auditoria, IUnidadeDeTrabalho unidade)
    {
        _usuarios = usuarios;
        _hash = hash;
        _auditoria = auditoria;
        _unidade = unidade;
    }

    public async Task<Resultado> ExecutarAsync(Solicitante solicitante, AlterarSenhaDto? dto, CancellationToken cancelamento = default)
    {
        if (dto is null || string.IsNullOrEmpty(dto.SenhaAtual))
            return Resultado.Falha("Informe a senha atual.");

        var usuario = await _usuarios.ObterPorIdAsync(solicitante.IdUsuario, cancelamento);
        if (usuario is null || !usuario.Ativo)
            return Resultado.Falha(TipoErro.NaoAutenticado, "Sessão inválida. Entre novamente.");

        // 400 e não 401: o token é válido; um 401 faria o app deslogar o usuário por um simples erro de digitação.
        if (!_hash.Verificar(dto.SenhaAtual, usuario.Senha))
        {
            _auditoria.Registrar(usuario.Id, "AlterarSenhaFalha", "USUARIO", usuario.Id, "Senha atual incorreta.");
            await _unidade.SalvarAsync(cancelamento);
            return Resultado.Falha("A senha atual está incorreta.");
        }

        var erro = PoliticaSenha.Validar(dto.NovaSenha);
        if (erro is not null) return Resultado.Falha(erro);

        if (dto.NovaSenha == dto.SenhaAtual)
            return Resultado.Falha("A nova senha deve ser diferente da atual.");

        usuario.Senha = _hash.Gerar(dto.NovaSenha!);
        _auditoria.Registrar(usuario.Id, "AlterarSenha", "USUARIO", usuario.Id, null);
        await _unidade.SalvarAsync(cancelamento);
        return Resultado.Ok();
    }
}

/// <summary>
/// RF03 "Esqueci minha senha": envia por e-mail um link com token assinado, válido por 30 minutos e de uso único.
/// A solicitação responde sempre a mesma coisa, exista ou não o e-mail (não revela quem tem conta).
/// </summary>
public sealed class RecuperarSenha
{
    public const string MensagemSolicitacao = "Se o e-mail estiver cadastrado, você receberá as instruções em instantes.";
    public const string MensagemLinkInvalido = "O link é inválido ou expirou. Solicite a recuperação de senha novamente.";

    private readonly IUsuarioRepositorio _usuarios;
    private readonly IHashSenha _hash;
    private readonly IServicoToken _tokens;
    private readonly IEnviadorEmail _email;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly OpcoesRecuperacaoSenha _opcoes;

    public RecuperarSenha(
        IUsuarioRepositorio usuarios,
        IHashSenha hash,
        IServicoToken tokens,
        IEnviadorEmail email,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        OpcoesRecuperacaoSenha opcoes)
    {
        _usuarios = usuarios;
        _hash = hash;
        _tokens = tokens;
        _email = email;
        _auditoria = auditoria;
        _unidade = unidade;
        _opcoes = opcoes;
    }

    /// <summary>Passo 1: pede o link. Só falha se o e-mail estiver em formato inválido.</summary>
    public async Task<Resultado> SolicitarAsync(SolicitarRecuperacaoDto? dto, CancellationToken cancelamento = default)
    {
        if (dto is null || !EmailUsuario.TentarNormalizar(dto.Email, out var email))
            return Resultado.Falha("Informe um e-mail válido.");

        var usuario = await _usuarios.ObterPorEmailAsync(email, cancelamento);
        var funcionario = usuario?.Funcionario;
        if (usuario is null || !usuario.Ativo || funcionario is null || !funcionario.Ativo)
            return Resultado.Ok(); // silêncio proposital

        var token = _tokens.GerarRedefinicaoSenha(usuario.Id, ImpressaoSenha.De(usuario.Senha));
        var link = $"{_opcoes.UrlRedefinicaoSenha}?token={Uri.EscapeDataString(token)}";

        await _email.EnviarAsync(
            usuario.Email,
            "HR-Auto — redefinição de senha",
            $"Olá, {funcionario.Nome}.\n\n" +
            "Recebemos um pedido para redefinir a senha da sua conta no HR-Auto. " +
            $"Use o link abaixo em até 30 minutos:\n\n{link}\n\n" +
            "Se você não fez este pedido, ignore esta mensagem: sua senha continua a mesma.",
            cancelamento);

        _auditoria.Registrar(usuario.Id, "SolicitarRecuperacaoSenha", "USUARIO", usuario.Id, null);
        await _unidade.SalvarAsync(cancelamento);
        return Resultado.Ok();
    }

    /// <summary>Passo 2: define a nova senha com o token recebido por e-mail.</summary>
    public async Task<Resultado> RedefinirAsync(RedefinirSenhaDto? dto, CancellationToken cancelamento = default)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Token))
            return Resultado.Falha(MensagemLinkInvalido);

        if (!_tokens.TentarLerRedefinicaoSenha(dto.Token, out var idUsuario, out var impressao))
            return Resultado.Falha(MensagemLinkInvalido);

        var usuario = await _usuarios.ObterPorIdAsync(idUsuario, cancelamento);
        if (usuario is null || !usuario.Ativo || usuario.Funcionario is not { Ativo: true }
            || !ImpressaoSenha.Confere(usuario.Senha, impressao))
            return Resultado.Falha(MensagemLinkInvalido); // inclui o link já usado: a senha mudou, a impressão não confere

        var erro = PoliticaSenha.Validar(dto.NovaSenha);
        if (erro is not null) return Resultado.Falha(erro);

        usuario.Senha = _hash.Gerar(dto.NovaSenha!);
        _auditoria.Registrar(usuario.Id, "RedefinirSenha", "USUARIO", usuario.Id, null);
        await _unidade.SalvarAsync(cancelamento);
        return Resultado.Ok();
    }
}
