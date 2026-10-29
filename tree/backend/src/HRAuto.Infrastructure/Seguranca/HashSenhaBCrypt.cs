using BCryptNet = BCrypt.Net.BCrypt;
using HRAuto.Application.Abstractions;

namespace HRAuto.Infrastructure.Seguranca;

/// <summary>
/// Hash de senha com BCrypt (RNF05). O salt vai embutido no hash; o custo (work factor) é 11, acima do mínimo 10.
/// </summary>
public sealed class HashSenhaBCrypt : IHashSenha
{
    public const int CustoMinimo = 10;
    private readonly int _custo;

    public HashSenhaBCrypt() : this(11) { }

    public HashSenhaBCrypt(int custo)
    {
        if (custo < CustoMinimo)
            throw new ArgumentOutOfRangeException(nameof(custo), custo, $"O custo do BCrypt deve ser pelo menos {CustoMinimo} (RNF05).");
        _custo = custo;
    }

    public string Gerar(string senha)
    {
        if (string.IsNullOrEmpty(senha))
            throw new ArgumentException("A senha é obrigatória.", nameof(senha));
        return BCryptNet.HashPassword(senha, _custo);
    }

    public bool Verificar(string senha, string hash)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(hash)) return false;
        try
        {
            return BCryptNet.Verify(senha, hash);
        }
        catch (Exception)
        {
            return false; // hash em formato inválido: falha fechada, nunca autentica
        }
    }
}
