using HRAuto.Application.Autenticacao;
using HRAuto.Application.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

/// <summary>
/// RF01, RF08, RNF05: login pelo código do funcionário (FUNC-0001) além do e-mail.
/// A busca é pelo código, nunca pelo nome, e toda credencial inválida recebe a mesma resposta.
/// </summary>
public class LoginPorCodigoTests
{
    private static (Cenario Cenario, Usuario Usuario) CenarioComAna(PerfilAcesso perfil = PerfilAcesso.Colaborador)
    {
        var cenario = new Cenario();
        var ana = cenario.NovoFuncionario(7, "Ana Souza");
        return (cenario, cenario.NovoUsuario(ana, "ana@hrauto.local", perfil: perfil));
    }

    [Fact]
    public async Task Login_PeloCodigo_DevolveOMesmoUsuarioDoLoginPorEmail_EAuditaOCanal()
    {
        var (cenario, usuario) = CenarioComAna(PerfilAcesso.RH);

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto(null, Cenario.SenhaPadrao, "FUNC-0007"));

        Assert.True(r.Sucesso);
        Assert.Equal($"jwt-{usuario.Id}-RH", r.Valor!.Token);
        Assert.Equal("FUNC-0007", r.Valor.Usuario.Codigo);
        Assert.Equal("ana@hrauto.local", r.Valor.Usuario.Email);
        Assert.Equal(new DateTime(2026, 10, 21, 8, 0, 0), usuario.UltimoLogin);

        var log = cenario.Banco.Logs.Single();
        Assert.Equal("Login", log.Acao);
        Assert.Equal("Entrada pelo código FUNC.", log.Descricao);
    }

    [Fact]
    public async Task Login_PorEmail_ContinuaSemDescricaoDeCodigo()
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto("ana@hrauto.local", Cenario.SenhaPadrao));

        Assert.True(r.Sucesso);
        Assert.Null(cenario.Banco.Logs.Single().Descricao);
    }

    [Theory]
    [InlineData("func-0007")]
    [InlineData("  FUNC-0007  ")]
    [InlineData("Func-0007")]
    public async Task Login_Codigo_AceitaMinusculasEEspacosNasPontas(string digitado)
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto(null, Cenario.SenhaPadrao, digitado));

        Assert.True(r.Sucesso);
    }

    [Fact]
    public async Task Login_CodigoNoCampoEmailAntigo_TambemFunciona()
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto("FUNC-0007", Cenario.SenhaPadrao));

        Assert.True(r.Sucesso);
    }

    [Fact]
    public async Task Login_QuandoVemLoginEEmail_ValeOLogin()
    {
        var (cenario, _) = CenarioComAna();
        var bia = cenario.NovoFuncionario(8, "Bia Lima");
        cenario.NovoUsuario(bia, "bia@hrauto.local");

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto("ana@hrauto.local", Cenario.SenhaPadrao, "FUNC-0008"));

        Assert.True(r.Sucesso);
        Assert.Equal("FUNC-0008", r.Valor!.Usuario.Codigo);
    }

    [Fact]
    public async Task Login_Codigo_SenhaErrada_EhNaoAutenticado_ComMensagemGenerica_EFicaNaAuditoria()
    {
        var (cenario, usuario) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto(null, "outra-senha", "FUNC-0007"));

        Assert.Equal(TipoErro.NaoAutenticado, r.TipoDoErro);
        Assert.Equal(AutenticarUsuario.MensagemCredenciaisInvalidas, r.Erro);
        Assert.Null(usuario.UltimoLogin);
        Assert.Equal("LoginFalha", cenario.Banco.Logs.Single().Acao);
    }

    [Fact]
    public async Task Login_CodigoInexistente_TemAMesmaMensagemDaSenhaErrada_SemRegistrarNada()
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto(null, Cenario.SenhaPadrao, "FUNC-0999"));

        Assert.Equal(TipoErro.NaoAutenticado, r.TipoDoErro);
        Assert.Equal(AutenticarUsuario.MensagemCredenciaisInvalidas, r.Erro);
        Assert.Empty(cenario.Banco.Logs);
    }

    [Fact]
    public async Task Login_FuncionarioSemAcesso_EhCredencialInvalida()
    {
        var cenario = new Cenario();
        cenario.NovoFuncionario(9, "Caio Dias"); // existe, mas o RH ainda não criou o login

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto(null, Cenario.SenhaPadrao, "FUNC-0009"));

        Assert.Equal(TipoErro.NaoAutenticado, r.TipoDoErro);
        Assert.Equal(AutenticarUsuario.MensagemCredenciaisInvalidas, r.Erro);
    }

    [Theory]
    [InlineData("FUNC-7")]       // faltam zeros: o formato tem 4 dígitos
    [InlineData("FUNC-0000")]    // não existe funcionário 0
    [InlineData("FUNC-00071")]
    [InlineData("0007")]
    [InlineData("Ana Souza")]    // a busca é pelo código, não pelo nome
    [InlineData("ana")]
    public async Task Login_IdentificadorForaDoFormato_EhCredencialInvalida_ENaoPesquisaPeloNome(string digitado)
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto(null, Cenario.SenhaPadrao, digitado));

        Assert.Equal(TipoErro.NaoAutenticado, r.TipoDoErro);
        Assert.Equal(AutenticarUsuario.MensagemCredenciaisInvalidas, r.Erro);
        Assert.Empty(cenario.Banco.Logs);
    }

    [Fact]
    public async Task Login_Codigo_UsuarioInativo_ComSenhaCorreta_EhNegado_SemRevelarOMotivo()
    {
        var cenario = new Cenario();
        var bia = cenario.NovoFuncionario(8, "Bia Lima");
        cenario.NovoUsuario(bia, "bia@hrauto.local", ativo: false);

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto(null, Cenario.SenhaPadrao, "FUNC-0008"));

        Assert.Equal(TipoErro.NaoAutenticado, r.TipoDoErro);
        Assert.Equal(AutenticarUsuario.MensagemCredenciaisInvalidas, r.Erro);
        Assert.Equal("LoginNegado", cenario.Banco.Logs.Single().Acao);
    }

    [Fact]
    public async Task Login_Codigo_FuncionarioDesligado_EhNegado()
    {
        var cenario = new Cenario();
        var caio = cenario.NovoFuncionario(9, "Caio Dias", ativo: false);
        cenario.NovoUsuario(caio, "caio@hrauto.local");

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto(null, Cenario.SenhaPadrao, "FUNC-0009"));

        Assert.Equal(TipoErro.NaoAutenticado, r.TipoDoErro);
    }

    [Theory]
    [InlineData(null, "x", null)]
    [InlineData("", "x", "")]
    [InlineData(null, "x", "   ")]
    [InlineData(null, "", "FUNC-0007")]
    [InlineData(null, null, "FUNC-0007")]
    public async Task Login_CamposVazios_SaoErroDeValidacao(string? email, string? senha, string? login)
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto(email, senha, login));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Equal(AutenticarUsuario.MensagemCamposObrigatorios, r.Erro);
    }
}
