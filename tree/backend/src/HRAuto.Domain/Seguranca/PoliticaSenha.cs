using System.Text;

namespace HRAuto.Domain.Seguranca;

/// <summary>
/// Política de senha (RF03, RF04, RNF05): mínimo de 8 caracteres com maiúscula, minúscula, número e símbolo.
/// O máximo é de 72 bytes porque o BCrypt ignora o que passar disso: aceitar mais daria a falsa
/// impressão de que a parte final da senha é conferida.
/// </summary>
public static class PoliticaSenha
{
    public const int TamanhoMinimo = 8;
    public const int TamanhoMaximoBytes = 72;

    public const string MensagemRequisitos =
        "A senha deve ter no mínimo 8 caracteres, com letra maiúscula, letra minúscula, número e símbolo.";

    /// <summary>Devolve a mensagem de erro, ou nulo quando a senha atende à política.</summary>
    public static string? Validar(string? senha)
    {
        if (string.IsNullOrEmpty(senha)) return "A senha é obrigatória.";
        if (Encoding.UTF8.GetByteCount(senha) > TamanhoMaximoBytes)
            return $"A senha é muito longa (máximo de {TamanhoMaximoBytes} bytes).";

        var atende = senha.Length >= TamanhoMinimo
                     && senha.Any(char.IsUpper)
                     && senha.Any(char.IsLower)
                     && senha.Any(char.IsDigit)
                     && senha.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));

        return atende ? null : MensagemRequisitos;
    }
}
