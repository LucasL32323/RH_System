using System.Security.Cryptography;
using System.Text;

namespace HRAuto.Application.Autenticacao;

/// <summary>Nomes das claims do JWT, compartilhados entre quem emite (Infrastructure) e quem lê (Api).</summary>
public static class NomesClaims
{
    public const string Usuario = "sub";
    public const string Funcionario = "fid";
    public const string Codigo = "codigo";
    public const string Nome = "name";
    public const string Email = "email";
    public const string Perfil = "role";

    // Somente no token de redefinição de senha.
    public const string Finalidade = "fin";
    public const string ImpressaoSenha = "ph";
    public const string FinalidadeRedefinicaoSenha = "redefinir-senha";
}

/// <summary>
/// "Impressão" da senha atual: 16 caracteres derivados do hash BCrypt gravado.
/// Vai dentro do token de redefinição; assim que a senha muda, o hash muda e o link antigo deixa de valer,
/// o que dá uso único ao token sem precisar de uma tabela (o modelo físico não tem).
/// </summary>
public static class ImpressaoSenha
{
    public static string De(string hashSenha)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashSenha ?? string.Empty));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
    }

    /// <summary>Comparação em tempo constante.</summary>
    public static bool Confere(string hashSenhaAtual, string impressaoDoToken) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(De(hashSenhaAtual)),
            Encoding.ASCII.GetBytes(impressaoDoToken ?? string.Empty));
}
