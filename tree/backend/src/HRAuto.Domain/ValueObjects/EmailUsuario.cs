using System.Text.RegularExpressions;

namespace HRAuto.Domain.ValueObjects;

/// <summary>
/// E-mail de login (RF01). É sempre guardado e comparado em minúsculas e sem espaços nas pontas;
/// o banco também compara sem diferenciar maiúsculas (índice único ux_usuario_email).
/// </summary>
public static class EmailUsuario
{
    public const int TamanhoMaximo = 254;

    // Validação propositalmente simples (algo@dominio.ext): quem confirma o e-mail de verdade é o envio da mensagem.
    private static readonly Regex Padrao =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Normaliza o texto digitado. Retorna false se estiver vazio, longo demais ou fora do formato.</summary>
    public static bool TentarNormalizar(string? texto, out string email)
    {
        email = string.Empty;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var limpo = texto.Trim().ToLowerInvariant();
        if (limpo.Length > TamanhoMaximo || !Padrao.IsMatch(limpo)) return false;

        email = limpo;
        return true;
    }
}
