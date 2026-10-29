using HRAuto.Domain.Enums;
using HRAuto.Domain.Seguranca;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Tests.Domain;

/// <summary>RF01, RF03, RF04, RNF05: política de senha, e-mail de login e perfis.</summary>
public class SegurancaTests
{
    [Theory]
    [InlineData("Admin@123")]
    [InlineData("Senha#Forte9")]
    [InlineData("Ação@2026a")]
    public void Senha_QueAtendeAPolitica_EhAceita(string senha)
    {
        Assert.Null(PoliticaSenha.Validar(senha));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Senha_Vazia_TemMensagemPropria(string? senha)
    {
        Assert.Equal("A senha é obrigatória.", PoliticaSenha.Validar(senha));
    }

    [Theory]
    [InlineData("Ab1@")]            // curta
    [InlineData("semmaiuscula1@")]
    [InlineData("SEMMINUSCULA1@")]
    [InlineData("SemNumero@@")]
    [InlineData("SemSimbolo123")]
    [InlineData("Com Espaco 1A")]   // espaço não conta como símbolo
    public void Senha_Fraca_DevolveOsRequisitos(string senha)
    {
        Assert.Equal(PoliticaSenha.MensagemRequisitos, PoliticaSenha.Validar(senha));
    }

    [Fact]
    public void Senha_Acima_De72Bytes_EhRecusada_PorqueOBCryptIgnoraOResto()
    {
        var oitentaCaracteres = "Aa1@" + new string('x', 76);
        var limite = "Aa1@" + new string('x', 68); // exatamente 72 bytes

        Assert.Contains("muito longa", PoliticaSenha.Validar(oitentaCaracteres)!);
        Assert.Null(PoliticaSenha.Validar(limite));
    }

    [Theory]
    [InlineData("ana@hrauto.local", "ana@hrauto.local")]
    [InlineData("  ANA@HrAuto.Local ", "ana@hrauto.local")]
    public void Email_ValidoEhNormalizado(string digitado, string esperado)
    {
        Assert.True(EmailUsuario.TentarNormalizar(digitado, out var email));
        Assert.Equal(esperado, email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("semarroba")]
    [InlineData("@semusuario.com")]
    [InlineData("sem@dominio")]
    [InlineData("dois@@arrobas.com")]
    [InlineData("com espaco@x.com")]
    public void Email_Invalido_EhRecusado(string? digitado)
    {
        Assert.False(EmailUsuario.TentarNormalizar(digitado, out var email));
        Assert.Equal(string.Empty, email);
    }

    [Fact]
    public void Email_AcimaDe254Caracteres_EhRecusado()
    {
        var longo = new string('a', 250) + "@x.com";

        Assert.False(EmailUsuario.TentarNormalizar(longo, out _));
    }

    [Theory]
    [InlineData("Colaborador", PerfilAcesso.Colaborador, false)]
    [InlineData("RH", PerfilAcesso.RH, true)]
    [InlineData("Admin", PerfilAcesso.Admin, true)]
    public void Perfil_LidoDoToken_EhConvertido(string nome, PerfilAcesso esperado, bool rhOuAdmin)
    {
        Assert.True(PerfilAcessoExtensoes.TentarLer(nome, out var perfil));
        Assert.Equal(esperado, perfil);
        Assert.Equal(rhOuAdmin, perfil.EhRhOuAdmin());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Gestor")] // gestor não é perfil: é quem aparece como id_gestor de outros
    [InlineData("admin")]  // diferencia maiúsculas: o token só é emitido pelo próprio servidor
    public void Perfil_Desconhecido_EhRecusado(string? nome)
    {
        Assert.False(PerfilAcessoExtensoes.TentarLer(nome, out _));
    }
}
