using HRAuto.Application.Cadastro;
using HRAuto.Application.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

/// <summary>RF12 (desligamento e reativação) e RF06/RF07 (acessos e perfis).</summary>
public class SituacaoAcessoTests
{
    // ---------- Desligar ----------

    [Fact]
    public async Task Desligar_Rh_GravaDataInativaFuncionarioEBloqueiaOAcesso()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AlterarSituacao().DesligarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0005", new DesligarFuncionarioDto(null));

        Assert.True(r.Sucesso, r.Erro);
        Assert.False(e.Bruno.Ativo);
        Assert.Equal(new DateOnly(2026, 10, 21), e.Bruno.DataDesligamento);
        Assert.False(e.UsuarioBruno.Ativo);
        Assert.Equal("DesligarFuncionario", c.Banco.Logs.Single().Acao);
    }

    [Fact]
    public async Task Desligar_ComDataInformada_UsaAData()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AlterarSituacao().DesligarAsync(
            c.Solicitante(e.UsuarioAdmin), "FUNC-0005", new DesligarFuncionarioDto(new DateOnly(2026, 10, 15)));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(new DateOnly(2026, 10, 15), e.Bruno.DataDesligamento);
    }

    [Fact]
    public async Task Desligar_DataFutura_EhRecusada_ENadaMuda()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AlterarSituacao().DesligarAsync(
            c.Solicitante(e.UsuarioRh), "FUNC-0005", new DesligarFuncionarioDto(new DateOnly(2026, 11, 30)));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.True(e.Bruno.Ativo);
        Assert.True(e.UsuarioBruno.Ativo);
        Assert.Empty(c.Banco.Logs);
    }

    [Fact]
    public async Task Desligar_JaDesligado_EhRecusado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        e.Bruno.Ativo = false;

        var r = await c.AlterarSituacao().DesligarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0005", null);

        Assert.False(r.Sucesso);
    }

    [Fact]
    public async Task Desligar_NinguemDesligaASiMesmo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AlterarSituacao().DesligarAsync(c.Solicitante(e.UsuarioAdmin), "FUNC-0001", null);

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
        Assert.True(e.Admin.Ativo);
    }

    [Fact]
    public async Task Desligar_RhNaoDesligaRhNemAdmin()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var outroRh = c.NovoFuncionario(20, "Rui RH");
        c.NovoUsuario(outroRh, "rui@hrauto.local", perfil: PerfilAcesso.RH);

        var contraAdmin = await c.AlterarSituacao().DesligarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0001", null);
        var contraRh = await c.AlterarSituacao().DesligarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0020", null);

        Assert.Equal(TipoErro.Proibido, contraAdmin.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, contraRh.TipoDoErro);
    }

    [Fact]
    public async Task Desligar_GestorComEquipeAtiva_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AlterarSituacao().DesligarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0003", null);

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.Contains("2 funcionário(s)", r.Erro!);
        Assert.True(e.Gestor.Ativo);
    }

    [Fact]
    public async Task Desligar_GestorSemEquipeAtiva_Funciona()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        e.Ana.Ativo = false;
        e.Bruno.Ativo = false;

        var r = await c.AlterarSituacao().DesligarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0003", null);

        Assert.True(r.Sucesso, r.Erro);
    }

    [Fact]
    public async Task Desligar_UltimoAdminAtivo_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var segundo = c.NovoFuncionario(30, "Beto Admin");
        c.NovoUsuario(segundo, "beto@hrauto.local", perfil: PerfilAcesso.Admin);
        // Alice (token ainda válido por até 8 h) foi bloqueada: Beto passou a ser o único Admin ativo.
        e.UsuarioAdmin.Ativo = false;

        var r = await c.AlterarSituacao().DesligarAsync(c.Solicitante(e.UsuarioAdmin), "FUNC-0030", null);

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.Contains("último administrador", r.Erro!);
    }

    [Fact]
    public async Task Desligar_ColaboradorEhProibido()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AlterarSituacao().DesligarAsync(c.Solicitante(e.UsuarioGestor), "FUNC-0005", null);

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }

    [Theory]
    [InlineData("FUNC-0999", TipoErro.NaoEncontrado)]
    [InlineData("5", TipoErro.Validacao)]
    public async Task Desligar_CodigoInexistenteOuInvalido(string codigo, TipoErro esperado)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AlterarSituacao().DesligarAsync(c.Solicitante(e.UsuarioRh), codigo, null);

        Assert.Equal(esperado, r.TipoDoErro);
    }

    // ---------- Reativar ----------

    [Fact]
    public async Task Reativar_VoltaOFuncionario_MasNaoOAcesso()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        await c.AlterarSituacao().DesligarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0005", null);

        var r = await c.AlterarSituacao().ReativarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0005");

        Assert.True(r.Sucesso, r.Erro);
        Assert.True(e.Bruno.Ativo);
        Assert.Null(e.Bruno.DataDesligamento);
        Assert.False(e.UsuarioBruno.Ativo); // o RH reativa o login em separado
        Assert.Contains(c.Banco.Logs, l => l.Acao == "ReativarFuncionario");
    }

    [Fact]
    public async Task Reativar_FuncionarioAtivo_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AlterarSituacao().ReativarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0005");

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
    }

    // ---------- Criar acesso ----------

    private static Funcionario SemAcesso(Cenario c) => c.NovoFuncionario(20, "Davi Novo");

    [Fact]
    public async Task CriarAcesso_RhCriaColaborador_EnviaConviteParaOEmailDoCadastro()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var davi = SemAcesso(c);

        var r = await c.Acesso().CriarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0020", new CriarAcessoDto(null));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("Colaborador", r.Valor!.Perfil);
        Assert.True(r.Valor.ConviteEnviado);
        var usuario = c.Banco.Usuarios.Single(u => u.IdFuncionario == davi.Id);
        Assert.Equal(davi.Email, usuario.Email);
        Assert.True(usuario.Ativo);
        Assert.Equal(davi.Email, Assert.Single(c.Email.Enviados).Para);
        Assert.Equal("CriarAcesso", c.Banco.Logs.Single().Acao);
    }

    [Fact]
    public async Task CriarAcesso_RhNaoConcedeRh_MasAdminConcede()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        SemAcesso(c);

        var rh = await c.Acesso().CriarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0020", new CriarAcessoDto("RH"));
        var admin = await c.Acesso().CriarAsync(c.Solicitante(e.UsuarioAdmin), "FUNC-0020", new CriarAcessoDto("RH"));

        Assert.Equal(TipoErro.Proibido, rh.TipoDoErro);
        Assert.True(admin.Sucesso, admin.Erro);
        Assert.Equal((int)PerfilAcesso.RH, c.Banco.Usuarios.Single(u => u.IdFuncionario == 20).IdPerfil);
    }

    [Fact]
    public async Task CriarAcesso_JaTemAcesso_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Acesso().CriarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004", null);

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
    }

    [Fact]
    public async Task CriarAcesso_FuncionarioDesligado_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        SemAcesso(c).Ativo = false;

        var r = await c.Acesso().CriarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0020", null);

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
    }

    [Fact]
    public async Task CriarAcesso_EmailJaUsadoPorOutroUsuario_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var davi = SemAcesso(c);
        davi.Email = e.UsuarioAna.Email;

        var r = await c.Acesso().CriarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0020", null);

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
    }

    [Fact]
    public async Task CriarAcesso_ParaSiMesmo_EhProibido()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Acesso().CriarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0002", null);

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }

    // ---------- Alterar acesso ----------

    [Fact]
    public async Task AlterarAcesso_RhDesativaEReativaOLogin()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var desativado = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004", new AlterarAcessoDto(null, false));
        var reativado = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004", new AlterarAcessoDto(null, true));

        Assert.False(desativado.Valor!.Ativo);
        Assert.True(reativado.Valor!.Ativo);
        Assert.Equal(new[] { "AlterarAcesso", "AlterarAcesso" }, c.Banco.Logs.Select(l => l.Acao));
        Assert.Contains("desativado", c.Banco.Logs[0].Descricao);
    }

    [Fact]
    public async Task AlterarAcesso_AdminPromoveParaRh_ERhNaoPromoveParaAdmin()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var rhTentaAdmin = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004", new AlterarAcessoDto("Admin", null));
        var adminPromoveRh = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioAdmin), "FUNC-0004", new AlterarAcessoDto("RH", null));

        Assert.Equal(TipoErro.Proibido, rhTentaAdmin.TipoDoErro);
        Assert.True(adminPromoveRh.Sucesso, adminPromoveRh.Erro);
        Assert.Equal("RH", adminPromoveRh.Valor!.Perfil);
        Assert.Equal((int)PerfilAcesso.RH, e.UsuarioAna.IdPerfil);
        Assert.Contains("Colaborador -> RH", c.Banco.Logs.Single().Descricao);
    }

    [Fact]
    public async Task AlterarAcesso_Ninguem_AlteraOProprioAcesso()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var rh = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0002", new AlterarAcessoDto("Admin", null));
        var admin = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioAdmin), "FUNC-0001", new AlterarAcessoDto("Colaborador", null));

        Assert.Equal(TipoErro.Proibido, rh.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, admin.TipoDoErro);
        Assert.Equal((int)PerfilAcesso.RH, e.UsuarioRh.IdPerfil);
    }

    [Fact]
    public async Task AlterarAcesso_RhNaoMexeEmContaAdmin()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0001", new AlterarAcessoDto(null, false));

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
        Assert.True(e.UsuarioAdmin.Ativo);
    }

    [Fact]
    public async Task AlterarAcesso_AtivarLoginDeFuncionarioDesligado_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        e.Bruno.Ativo = false;
        e.UsuarioBruno.Ativo = false;

        var r = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0005", new AlterarAcessoDto(null, true));

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.False(e.UsuarioBruno.Ativo);
    }

    [Fact]
    public async Task AlterarAcesso_UltimoAdminNaoPodeSerRebaixadoNemDesativado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var segundo = c.NovoFuncionario(30, "Beto Admin");
        var usuarioBeto = c.NovoUsuario(segundo, "beto@hrauto.local", perfil: PerfilAcesso.Admin);
        e.UsuarioAdmin.Ativo = false; // Beto é o único Admin ativo; Alice ainda tem token válido

        var rebaixar = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioAdmin), "FUNC-0030", new AlterarAcessoDto("Colaborador", null));
        var desativar = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioAdmin), "FUNC-0030", new AlterarAcessoDto(null, false));

        Assert.Equal(TipoErro.Conflito, rebaixar.TipoDoErro);
        Assert.Equal(TipoErro.Conflito, desativar.TipoDoErro);
        Assert.Equal((int)PerfilAcesso.Admin, usuarioBeto.IdPerfil);
        Assert.True(usuarioBeto.Ativo);
    }

    [Fact]
    public async Task AlterarAcesso_AdminPodeRebaixarOutroAdmin_QuandoHaMaisDeUm()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var segundo = c.NovoFuncionario(30, "Beto Admin");
        c.NovoUsuario(segundo, "beto@hrauto.local", perfil: PerfilAcesso.Admin);

        var r = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioAdmin), "FUNC-0030", new AlterarAcessoDto("RH", null));

        Assert.True(r.Sucesso, r.Erro);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Gerente", null)]
    [InlineData("1", null)]
    public async Task AlterarAcesso_CorpoInvalido_EhRecusado(string? perfil, bool? ativo)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioAdmin), "FUNC-0004", new AlterarAcessoDto(perfil, ativo));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Fact]
    public async Task AlterarAcesso_FuncionarioSemAcesso_EhNaoEncontrado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        SemAcesso(c);

        var r = await c.Acesso().AlterarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0020", new AlterarAcessoDto(null, false));

        Assert.Equal(TipoErro.NaoEncontrado, r.TipoDoErro);
    }

    // ---------- Consultar e reenviar convite ----------

    [Fact]
    public async Task ConsultarAcesso_RhVeOPerfilEASituacao()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        SemAcesso(c);

        var com = await c.Acesso().ConsultarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004");
        var sem = await c.Acesso().ConsultarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0020");
        var proibido = await c.Acesso().ConsultarAsync(c.Solicitante(e.UsuarioAna), "FUNC-0004");

        Assert.True(com.Valor!.Possui);
        Assert.Equal("Colaborador", com.Valor.Perfil);
        Assert.False(sem.Valor!.Possui);
        Assert.Equal(TipoErro.Proibido, proibido.TipoDoErro);
    }

    [Fact]
    public async Task ReenviarConvite_EnviaOLinkParaOEmailDoUsuario()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Acesso().ReenviarConviteAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004");

        Assert.True(r.Sucesso, r.Erro);
        var email = Assert.Single(c.Email.Enviados);
        Assert.Equal(e.UsuarioAna.Email, email.Para);
        Assert.Contains("reset|" + e.UsuarioAna.Id, Uri.UnescapeDataString(email.Corpo));
        Assert.Equal("ReenviarConvite", c.Banco.Logs.Single().Acao);
    }

    [Fact]
    public async Task ReenviarConvite_AcessoDesativado_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        e.UsuarioAna.Ativo = false;

        var r = await c.Acesso().ReenviarConviteAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004");

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.Empty(c.Email.Enviados);
    }

    [Fact]
    public async Task ReenviarConvite_RhNaoReenviaParaAdmin()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Acesso().ReenviarConviteAsync(c.Solicitante(e.UsuarioRh), "FUNC-0001");

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
        Assert.Empty(c.Email.Enviados);
    }

    [Fact]
    public async Task ReenviarConvite_FalhaNoEmail_AvisaOUsuario()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.Email.Falhar = true;

        var r = await c.Acesso().ReenviarConviteAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004");

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.Contains("e-mail", r.Erro!);
    }
}
