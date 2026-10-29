using HRAuto.Domain.Enums;

namespace HRAuto.Application.Abstractions;

/// <summary>Dados que vão dentro do token de acesso.</summary>
public sealed record DadosToken(int IdUsuario, int IdFuncionario, string Codigo, string Nome, string Email, PerfilAcesso Perfil);

/// <summary>Token de acesso (JWT) e o instante em que ele expira.</summary>
public sealed record TokenAcesso(string Token, DateTimeOffset ExpiraEm);

/// <summary>
/// Emissão e leitura de tokens (RF01, RF03). A implementação (JWT HS256) fica na Infrastructure.
/// O token de redefinição de senha usa chave, público e finalidade diferentes do token de acesso:
/// ele nunca é aceito como credencial de login.
/// </summary>
public interface IServicoToken
{
    TokenAcesso GerarAcesso(DadosToken dados);

    /// <summary>Token de uso único para o link "Esqueci minha senha", válido por 30 minutos (RF03).</summary>
    /// <param name="impressaoSenha">Marca derivada do hash da senha atual; quando a senha muda, o token deixa de valer.</param>
    string GerarRedefinicaoSenha(int idUsuario, string impressaoSenha);

    /// <summary>Valida assinatura, público, finalidade e validade. Retorna false para qualquer problema.</summary>
    bool TentarLerRedefinicaoSenha(string token, out int idUsuario, out string impressaoSenha);
}

/// <summary>Envio de e-mail (RF03). Implementações não devem lançar exceção nem registrar o corpo da mensagem em produção.</summary>
public interface IEnviadorEmail
{
    Task EnviarAsync(string destinatario, string assunto, string corpo, CancellationToken cancelamento = default);
}
