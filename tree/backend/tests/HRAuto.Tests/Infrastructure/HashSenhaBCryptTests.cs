using HRAuto.Infrastructure.Seguranca;

namespace HRAuto.Tests.Infrastructure;

/// <summary>RNF05: senha com hash BCrypt, custo mínimo 10, nunca em texto puro.</summary>
public class HashSenhaBCryptTests
{
    private readonly HashSenhaBCrypt _hash = new();

    [Fact]
    public void Gerar_NaoGuardaTextoPuro_EUsaCustoMinimoDez()
    {
        var hash = _hash.Gerar("Senha@123");

        Assert.NotEqual("Senha@123", hash);
        Assert.DoesNotContain("Senha@123", hash);
        Assert.Matches(@"^\$2[aby]\$(1[0-9]|[2-3][0-9])\$", hash); // $2a$11$... => custo >= 10
    }

    [Fact]
    public void Gerar_DuasVezesMesmaSenha_ProduzHashesDiferentes_PorCausaDoSalt()
    {
        Assert.NotEqual(_hash.Gerar("Senha@123"), _hash.Gerar("Senha@123"));
    }

    [Fact]
    public void Verificar_SenhaCorreta_RetornaTrue_ErradaRetornaFalse()
    {
        var hash = _hash.Gerar("Senha@123");

        Assert.True(_hash.Verificar("Senha@123", hash));
        Assert.False(_hash.Verificar("senha@123", hash));
        Assert.False(_hash.Verificar("", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("isto-nao-e-um-hash")]
    public void Verificar_HashInvalido_RetornaFalse_SemLancarExcecao(string hashInvalido)
    {
        Assert.False(_hash.Verificar("Senha@123", hashInvalido));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public void Construtor_CustoAbaixoDoMinimo_Rejeita(int custo)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HashSenhaBCrypt(custo));
    }
}
