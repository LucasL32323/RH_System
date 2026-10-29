using HRAuto.Domain.Calendario;
using HRAuto.Domain.Common;
using HRAuto.Domain.Enums;
using static HRAuto.Tests.Domain.Apoio;

namespace HRAuto.Tests.Domain;

/// <summary>RF28: cadastro de feriados nacionais, estaduais e municipais.</summary>
public class RegrasFeriadoTests
{
    [Fact]
    public void Nacional_Valido_NormalizaNomeETipo()
    {
        var feriado = RegrasFeriado.Criar(D("2026-12-25"), "  Natal  ", "nacional", null, null);

        Assert.Equal(D("2026-12-25"), feriado.Data);
        Assert.Equal("Natal", feriado.Nome);
        Assert.Equal(TiposFeriado.Nacional, feriado.Tipo);
        Assert.Null(feriado.Cidade);
        Assert.Null(feriado.Estado);
    }

    [Fact]
    public void Estadual_GravaEstadoEmMaiusculas()
    {
        var feriado = RegrasFeriado.Criar(D("2026-07-09"), "Revolução Constitucionalista", "ESTADUAL", "  ", " sp ");

        Assert.Equal(TiposFeriado.Estadual, feriado.Tipo);
        Assert.Equal("SP", feriado.Estado);
        Assert.Null(feriado.Cidade);
    }

    [Fact]
    public void Municipal_GuardaCidadeSemEspacosNasPontas()
    {
        var feriado = RegrasFeriado.Criar(D("2026-01-26"), "Aniversário de Santos", "Municipal", " Santos ", "sp");

        Assert.Equal("Santos", feriado.Cidade);
        Assert.Equal("SP", feriado.Estado);
    }

    [Theory]
    [InlineData(null, "SP")]
    [InlineData("", "SP")]
    [InlineData("Santos", null)]
    [InlineData("Santos", " ")]
    public void Municipal_SemCidadeOuEstado_Recusa(string? cidade, string? estado)
    {
        var erro = Assert.Throws<DominioException>(() =>
            RegrasFeriado.Criar(D("2026-01-26"), "Aniversário", "Municipal", cidade, estado));
        Assert.Contains("cidade e o estado", erro.Message);
    }

    [Fact]
    public void Estadual_SemEstado_Recusa()
    {
        var erro = Assert.Throws<DominioException>(() => RegrasFeriado.Criar(D("2026-07-09"), "Data estadual", "Estadual", null, null));
        Assert.Contains("estado", erro.Message);
    }

    [Fact]
    public void Estadual_ComCidade_Recusa()
    {
        var erro = Assert.Throws<DominioException>(() => RegrasFeriado.Criar(D("2026-07-09"), "Data estadual", "Estadual", "Santos", "SP"));
        Assert.Contains("não tem cidade", erro.Message);
    }

    [Theory]
    [InlineData("Santos", null)]
    [InlineData(null, "SP")]
    public void Nacional_ComCidadeOuEstado_Recusa(string? cidade, string? estado)
    {
        var erro = Assert.Throws<DominioException>(() => RegrasFeriado.Criar(D("2026-12-25"), "Natal", "Nacional", cidade, estado));
        Assert.Contains("nacional", erro.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Regional")]
    [InlineData("1")]
    public void TipoInvalido_Recusa(string? tipo)
    {
        var erro = Assert.Throws<DominioException>(() => RegrasFeriado.Criar(D("2026-12-25"), "Natal", tipo, null, null));
        Assert.Contains("Tipo inválido", erro.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("ab")]
    public void NomeCurto_Recusa(string? nome)
    {
        Assert.Throws<DominioException>(() => RegrasFeriado.Criar(D("2026-12-25"), nome, "Nacional", null, null));
    }

    [Fact]
    public void NomeNoLimite_Aceita_EMaisQueOLimite_Recusa()
    {
        var noLimite = new string('a', RegrasFeriado.TamanhoMaximoNome);
        Assert.Equal(RegrasFeriado.TamanhoMaximoNome, RegrasFeriado.Criar(D("2026-12-25"), noLimite, "Nacional", null, null).Nome.Length);

        Assert.Throws<DominioException>(() =>
            RegrasFeriado.Criar(D("2026-12-25"), noLimite + "a", "Nacional", null, null));
    }

    [Theory]
    [InlineData("SPP")]
    [InlineData("S")]
    [InlineData("S1")]
    [InlineData("12")]
    public void EstadoInvalido_Recusa(string estado)
    {
        var erro = Assert.Throws<DominioException>(() => RegrasFeriado.Criar(D("2026-07-09"), "Data estadual", "Estadual", null, estado));
        Assert.Contains("Estado inválido", erro.Message);
    }

    [Theory]
    [InlineData("nacional", "Nacional")]
    [InlineData(" Estadual ", "Estadual")]
    [InlineData("MUNICIPAL", "Municipal")]
    [InlineData("outro", null)]
    [InlineData(null, null)]
    public void NormalizarTipo_ReconheceOsTresTipos(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, RegrasFeriado.NormalizarTipo(entrada));
    }
}
