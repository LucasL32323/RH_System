using System.Security.Cryptography;

namespace HRAuto.Domain.Seguranca;

/// <summary>
/// Senha aleatória para contas criadas pelo RH (RF06). Ninguém a conhece nem a recebe: ela só existe para a conta
/// nunca ficar sem senha; o funcionário define a própria senha pelo link de convite (mesmo fluxo do "esqueci minha senha").
/// Atende a <see cref="PoliticaSenha"/> e usa gerador criptográfico.
/// </summary>
public static class GeradorSenhaTemporaria
{
    private const string Maiusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Minusculas = "abcdefghijkmnpqrstuvwxyz";
    private const string Numeros = "23456789";
    private const string Simbolos = "!@#$%&*?";
    public const int Tamanho = 24;

    public static string Gerar()
    {
        var todos = Maiusculas + Minusculas + Numeros + Simbolos;
        var caracteres = new List<char>(Tamanho)
        {
            Sortear(Maiusculas), Sortear(Minusculas), Sortear(Numeros), Sortear(Simbolos)
        };
        while (caracteres.Count < Tamanho) caracteres.Add(Sortear(todos));

        // Embaralha (Fisher-Yates) para os quatro primeiros não ficarem sempre no começo.
        for (var i = caracteres.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
        }
        return new string(caracteres.ToArray());
    }

    private static char Sortear(string alfabeto) => alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
}
