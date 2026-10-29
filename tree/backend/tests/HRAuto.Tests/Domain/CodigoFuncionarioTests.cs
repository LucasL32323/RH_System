using HRAuto.Domain.Common;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Tests.Domain;

public class CodigoFuncionarioTests
{
    [Theory]
    [InlineData(1, "FUNC-0001")]
    [InlineData(42, "FUNC-0042")]
    [InlineData(9999, "FUNC-9999")]
    [InlineData(12345, "FUNC-12345")] // acima de 4 dígitos o código simplesmente cresce
    public void Formatar_GeraCodigoComMinimoDeQuatroDigitos(int id, string esperado)
    {
        Assert.Equal(esperado, CodigoFuncionario.Formatar(id));
        Assert.Equal(esperado, new CodigoFuncionario(id).ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Formatar_IdInvalido_LancaDominioException(int id)
    {
        Assert.Throws<DominioException>(() => CodigoFuncionario.Formatar(id));
        Assert.Throws<DominioException>(() => new CodigoFuncionario(id));
    }

    [Theory]
    [InlineData("FUNC-0001", 1)]
    [InlineData("func-0007", 7)]       // aceita minúsculas
    [InlineData("  FUNC-0123  ", 123)] // ignora espaços nas pontas
    [InlineData("FUNC-12345", 12345)]
    public void TentarConverter_CodigoValido_RetornaId(string texto, int idEsperado)
    {
        var ok = CodigoFuncionario.TentarConverter(texto, out var codigo);

        Assert.True(ok);
        Assert.Equal(idEsperado, codigo.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Maria")]        // busca é pelo código, não pelo nome
    [InlineData("FUNC-1")]       // menos de 4 dígitos
    [InlineData("FUNC-0000")]    // id zero não existe
    [InlineData("FUNC-ABCD")]
    [InlineData("FUNC0001")]
    [InlineData("XYZ-0001")]
    public void TentarConverter_CodigoInvalido_RetornaFalse(string? texto)
    {
        var ok = CodigoFuncionario.TentarConverter(texto, out var codigo);

        Assert.False(ok);
        Assert.Equal(default(CodigoFuncionario), codigo);
    }

    [Fact]
    public void IdaEVolta_FormatarEConverterSaoInversos()
    {
        var texto = CodigoFuncionario.Formatar(318);

        Assert.True(CodigoFuncionario.TentarConverter(texto, out var codigo));
        Assert.Equal(318, codigo.Id);
    }
}
