using HRAuto.Application.Autenticacao;
using HRAuto.Application.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

/// <summary>RF01, RF08, RF45, RNF05: login com e-mail e senha, sempre com a mesma mensagem para qualquer credencial inválida.</summary>
public class AutenticarUsuarioTests
{
    private static (Cenario Cenario, Usuario Usuario) CenarioComAna(PerfilAcesso perfil = PerfilAcesso.Colaborador)
    {
        var cenario = new Cenario();
        var ana = cenario.NovoFuncionario(7, "Ana Souza");
        var usuario = cenario.NovoUsuario(ana, "ana@hrauto.local", perfil: perfil);
        return (cenario, usuario);
    }

    [Fact]
    public async Task Login_Valido_DevolveTokenCodigoEPerfil_EAtualizaUltimoLogin()
    {
        var (cenario, usuario) = CenarioComAna(PerfilAcesso.RH);

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto("ana@hrauto.local", Cenario.SenhaPadrao));

        Assert.True(r.Sucesso);
        Assert.Equal($"jwt-{usuario.Id}-RH", r.Valor!.Token);
        Assert.Equal("FUNC-0007", r.Valor.Usuario.Codigo);
        Assert.Equal("RH", r.Valor.Usuario.Perfil);
        Assert.Equal("Ana Souza", r.Valor.Usuario.Nome);
        Assert.Equal(new DateTime(2026, 10, 21, 8, 0, 0), usuario.UltimoLogin);
        Assert.Equal("Login", cenario.Banco.Logs.Single().Acao);
        Assert.Equal(usuario.Id, cenario.Banco.Logs.Single().IdUsuario);
    }

    [Fact]
    public async Task Login_EmailEmMaiusculasEComEspacos_EhAceito()
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto("  ANA@HrAuto.Local ", Cenario.SenhaPadrao));

        Assert.True(r.Sucesso);
    }

    [Fact]
    public async Task Login_SenhaErrada_EhNaoAutenticado_ComMensagemGenerica_EFicaNaAuditoria()
    {
        var (cenario, usuario) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto("ana@hrauto.local", "outra-senha"));

        Assert.False(r.Sucesso);
        Assert.Equal(TipoErro.NaoAutenticado, r.TipoDoErro);
        Assert.Equal(AutenticarUsuario.MensagemCredenciaisInvalidas, r.Erro);
        Assert.Null(usuario.UltimoLogin);
        Assert.Equal("LoginFalha", cenario.Banco.Logs.Single().Acao);
    }

    [Fact]
    public async Task Login_EmailInexistente_TemAMesmaMensagemDaSenhaErrada()
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto("ninguem@hrauto.local", Cenario.SenhaPadrao));

        Assert.Equal(TipoErro.NaoAutenticado, r.TipoDoErro);
        Assert.Equal(AutenticarUsuario.MensagemCredenciaisInvalidas, r.Erro);
        Assert.Empty(cenario.Banco.Logs); // sem usuário não há id_usuario para gravar no log
    }

    [Fact]
    public async Task Login_UsuarioInativo_ComSenhaCorreta_EhNegado_SemRevelarOMotivo()
    {
        var cenario = new Cenario();
        var bia = cenario.NovoFuncionario(8, "Bia Lima");
        cenario.NovoUsuario(bia, "bia@hrauto.local", ativo: false);

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto("bia@hrauto.local", Cenario.SenhaPadrao));

        Assert.Equal(TipoErro.NaoAutenticado, r.TipoDoErro);
        Assert.Equal(AutenticarUsuario.MensagemCredenciaisInvalidas, r.Erro);
        Assert.Equal("LoginNegado", cenario.Banco.Logs.Single().Acao);
    }

    [Fact]
    public async Task Login_FuncionarioDesligado_EhNegado()
    {
        var cenario = new Cenario();
        var caio = cenario.NovoFuncionario(9, "Caio Dias", ativo: false);
        cenario.NovoUsuario(caio, "caio@hrauto.local");

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto("caio@hrauto.local", Cenario.SenhaPadrao));

        Assert.Equal(TipoErro.NaoAutenticado, r.TipoDoErro);
    }

    [Theory]
    [InlineData(null, "x")]
    [InlineData("", "x")]
    [InlineData("ana@hrauto.local", null)]
    [InlineData("ana@hrauto.local", "")]
    public async Task Login_CamposVazios_SaoErroDeValidacao(string? email, string? senha)
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto(email, senha));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Fact]
    public async Task Login_CorpoNulo_EhErroDeValidacao()
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(null);

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Fact]
    public async Task Login_EmailMalFormado_EhCredencialInvalida()
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.Autenticar().ExecutarAsync(new CredenciaisDto("isto-nao-e-email", Cenario.SenhaPadrao));

        Assert.Equal(TipoErro.NaoAutenticado, r.TipoDoErro);
    }

    [Fact]
    public async Task MeusDados_DevolveCabecalhoDoDashboard()
    {
        var cenario = new Cenario();
        var gestor = cenario.NovoFuncionario(2, "Gil Prado");
        cenario.NovoFuncionario(3, "Eva Rocha", idGestor: gestor.Id);
        var usuario = cenario.NovoUsuario(gestor, "gil@hrauto.local", perfil: PerfilAcesso.Colaborador);

        var r = await cenario.MeusDados().ExecutarAsync(cenario.Solicitante(usuario));

        Assert.True(r.Sucesso);
        Assert.Equal("FUNC-0002", r.Valor!.Codigo);
        Assert.Equal("Analista", r.Valor.Cargo);
        Assert.Equal("Tecnologia", r.Valor.Departamento);
        Assert.Equal("Colaborador", r.Valor.Perfil);
        Assert.True(r.Valor.EhGestor);
    }

    [Fact]
    public async Task MeusDados_QuemNaoLideraNinguem_NaoEhGestor()
    {
        var (cenario, usuario) = CenarioComAna();

        var r = await cenario.MeusDados().ExecutarAsync(cenario.Solicitante(usuario));

        Assert.False(r.Valor!.EhGestor);
    }

    [Fact]
    public async Task Logout_FicaNaAuditoria()
    {
        var (cenario, usuario) = CenarioComAna();

        var r = await new EncerrarSessao(new AuditoriaFake(cenario.Banco, cenario.Relogio), new UnidadeDeTrabalhoFake(cenario.Banco))
            .ExecutarAsync(cenario.Solicitante(usuario));

        Assert.True(r.Sucesso);
        Assert.Equal("Logout", cenario.Banco.Logs.Single().Acao);
    }
}
