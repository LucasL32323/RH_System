using HRAuto.Application.Abstractions;
using HRAuto.Application.Autenticacao;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Seguranca;

namespace HRAuto.Application.Cadastro;

/// <summary>
/// Convite de primeiro acesso: o RH cria a conta com uma senha temporária aleatória que ninguém conhece e o
/// funcionário define a própria senha pelo link (mesmo token do "Esqueci minha senha": 30 minutos, uso único).
/// Se o link expirar, o funcionário usa "Esqueci minha senha" ou o RH reenvia o convite.
/// </summary>
public sealed class EnviadorDeConvite
{
    private readonly IServicoToken _tokens;
    private readonly IEnviadorEmail _email;
    private readonly OpcoesRecuperacaoSenha _opcoes;

    public EnviadorDeConvite(IServicoToken tokens, IEnviadorEmail email, OpcoesRecuperacaoSenha opcoes)
    {
        _tokens = tokens;
        _email = email;
        _opcoes = opcoes;
    }

    /// <summary>Envia o convite. Chamar só depois de gravar o usuário (o token depende do hash da senha gravada).</summary>
    public async Task EnviarAsync(Usuario usuario, string nomeFuncionario, CancellationToken cancelamento = default)
    {
        var token = _tokens.GerarRedefinicaoSenha(usuario.Id, ImpressaoSenha.De(usuario.Senha));
        var link = $"{_opcoes.UrlRedefinicaoSenha}?token={Uri.EscapeDataString(token)}";

        await _email.EnviarAsync(
            usuario.Email,
            "HR-Auto — seu acesso foi criado",
            $"Olá, {nomeFuncionario}.\n\n" +
            "O RH criou seu acesso ao HR-Auto. Defina sua senha pelo link abaixo em até 30 minutos:\n\n" +
            $"{link}\n\n" +
            "Se o link expirar, use \"Esqueci minha senha\" na tela de login com este e-mail.",
            cancelamento);
    }
}
