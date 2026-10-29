using HRAuto.Application.Cadastro;
using HRAuto.Application.Common;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

/// <summary>RF09, RF10, RF16 (cadastro e edição) e RF13 (lista e ficha).</summary>
public class CadastroFuncionarioTests
{
    // ---------- Cadastrar ----------

    [Fact]
    public async Task Cadastrar_RhComDadosValidos_GravaFuncionarioDetalhesEAcesso_EEnviaConvite()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), c.CadastroValido(e));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("FUNC-1001", r.Valor!.Codigo);
        Assert.True(r.Valor.AcessoCriado);
        Assert.Equal("Colaborador", r.Valor.Perfil);
        Assert.True(r.Valor.ConviteEnviado);

        var f = c.Banco.Funcionarios.Single(x => x.Id == 1001);
        Assert.Equal("novo@hrauto.local", f.Email);
        Assert.Equal(3, f.IdGestor);
        Assert.True(f.Ativo);

        var detalhes = c.Banco.Detalhes.Single(d => d.IdFuncionario == 1001);
        Assert.Equal("52998224725", detalhes.Cpf);
        Assert.Equal("11010000", detalhes.Cep);
        Assert.Equal("SP", detalhes.Estado);

        var usuario = c.Banco.Usuarios.Single(u => u.IdFuncionario == 1001);
        Assert.Equal((int)PerfilAcesso.Colaborador, usuario.IdPerfil);
        Assert.StartsWith("hash:", usuario.Senha);
        Assert.NotEqual("hash:" + Cenario.SenhaPadrao, usuario.Senha);

        var email = Assert.Single(c.Email.Enviados);
        Assert.Equal("novo@hrauto.local", email.Para);
        Assert.Contains("reset|", Uri.UnescapeDataString(email.Corpo));

        Assert.Equal(1, c.Banco.TransacoesConfirmadas);
    }

    [Fact]
    public async Task Cadastrar_LogDeAuditoria_NaoGuardaDadosPessoais()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), c.CadastroValido(e));

        var log = c.Banco.Logs.Single(l => l.Acao == "CadastrarFuncionario");
        Assert.Equal("FUNCIONARIO", log.Tabela);
        Assert.Equal(1001, log.IdRegistro);
        Assert.DoesNotContain("529", log.Descricao);
        Assert.DoesNotContain("123456789", log.Descricao);
        Assert.DoesNotContain("Flores", log.Descricao);
    }

    [Fact]
    public async Task Cadastrar_SemAcesso_NaoCriaUsuarioNemEnviaEmail()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var dto = c.CadastroValido(e) with { CriarAcesso = false };

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), dto);

        Assert.True(r.Sucesso, r.Erro);
        Assert.False(r.Valor!.AcessoCriado);
        Assert.Null(r.Valor.Perfil);
        Assert.DoesNotContain(c.Banco.Usuarios, u => u.IdFuncionario == 1001);
        Assert.Empty(c.Email.Enviados);
    }

    [Fact]
    public async Task Cadastrar_FalhaNoEmail_MantemOCadastro_ESinalizaQueOConviteNaoFoi()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.Email.Falhar = true;

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), c.CadastroValido(e));

        Assert.True(r.Sucesso, r.Erro);
        Assert.False(r.Valor!.ConviteEnviado);
        Assert.Contains(c.Banco.Funcionarios, f => f.Id == 1001);
        Assert.Contains(c.Banco.Usuarios, u => u.IdFuncionario == 1001);
    }

    [Fact]
    public async Task Cadastrar_AdminPodeCriarAcessoAdmin()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var dto = c.CadastroValido(e) with { Perfil = "Admin" };

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), dto);

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("Admin", r.Valor!.Perfil);
        Assert.Equal((int)PerfilAcesso.Admin, c.Banco.Usuarios.Single(u => u.IdFuncionario == 1001).IdPerfil);
    }

    [Theory]
    [InlineData("RH")]
    [InlineData("Admin")]
    public async Task Cadastrar_RhNaoPodeConcederPerfilRhOuAdmin(string perfil)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), c.CadastroValido(e) with { Perfil = perfil });

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
        Assert.DoesNotContain(c.Banco.Funcionarios, f => f.Id == 1001);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("root")]
    [InlineData("rh")] // o nome é exato: RH
    public async Task Cadastrar_PerfilInvalido_EhRecusado(string perfil)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), c.CadastroValido(e) with { Perfil = perfil });

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Fact]
    public async Task Cadastrar_ColaboradorEGestor_SaoProibidos()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var comoColaborador = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioCarla), c.CadastroValido(e));
        var comoGestor = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioGestor), c.CadastroValido(e));

        Assert.Equal(TipoErro.Proibido, comoColaborador.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, comoGestor.TipoDoErro);
        Assert.Equal(6, c.Banco.Funcionarios.Count);
    }

    [Fact]
    public async Task Cadastrar_CorpoVazio_Falha()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), null);

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Fact]
    public async Task Cadastrar_CpfInvalido_NaoGravaNada()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), c.CadastroValido(e, cpf: "111.111.111-11"));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("CPF", r.Erro!);
        Assert.Equal(6, c.Banco.Funcionarios.Count);
        Assert.Equal(0, c.Banco.Salvamentos);
    }

    [Fact]
    public async Task Cadastrar_CpfJaCadastrado_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.Banco.Detalhes.Single(d => d.IdFuncionario == e.Ana.Id).Cpf = "52998224725";

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), c.CadastroValido(e));

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.Contains("CPF", r.Erro!);
        Assert.Equal(6, c.Banco.Funcionarios.Count);
    }

    [Fact]
    public async Task Cadastrar_EmailJaUsadoPorFuncionarioOuUsuario_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var comoFuncionario = await c.Cadastrar().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), c.CadastroValido(e, email: e.Ana.Email.ToUpperInvariant()));
        var comoUsuario = await c.Cadastrar().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), c.CadastroValido(e, email: "admin@hrauto.local"));

        Assert.Equal(TipoErro.Conflito, comoFuncionario.TipoDoErro);
        Assert.Equal(TipoErro.Conflito, comoUsuario.TipoDoErro);
        Assert.Equal(6, c.Banco.Funcionarios.Count);
    }

    [Fact]
    public async Task Cadastrar_CargoOuDepartamentoInexistente_EhRecusado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var cargo = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), c.CadastroValido(e) with { IdCargo = 99 });
        var departamento = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), c.CadastroValido(e) with { IdDepartamento = 99 });

        Assert.Contains("cargo", cargo.Erro!);
        Assert.Contains("departamento", departamento.Erro!);
    }

    [Theory]
    [InlineData("FUNC-0999", "não existe")]
    [InlineData("3", "inválido")]
    [InlineData("gestor", "inválido")]
    public async Task Cadastrar_GestorInvalidoOuInexistente_EhRecusado(string gestor, string trecho)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), c.CadastroValido(e) with { CodigoGestor = gestor });

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains(trecho, r.Erro!);
    }

    [Fact]
    public async Task Cadastrar_GestorDesligado_EhRecusado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        e.Gestor.Ativo = false;

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), c.CadastroValido(e));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("desligado", r.Erro!);
    }

    [Fact]
    public async Task Cadastrar_SemGestor_Funciona()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), c.CadastroValido(e) with { CodigoGestor = null });

        Assert.True(r.Sucesso, r.Erro);
        Assert.Null(c.Banco.Funcionarios.Single(f => f.Id == 1001).IdGestor);
    }

    [Fact]
    public async Task Cadastrar_ConflitoNaGravacao_DesfazTudo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.Banco.ConflitoNoProximoSalvar = true;

        var r = await c.Cadastrar().ExecutarAsync(c.Solicitante(e.UsuarioRh), c.CadastroValido(e));

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.Equal(1, c.Banco.TransacoesDescartadas);
        Assert.Equal(0, c.Banco.TransacoesConfirmadas);
        Assert.Empty(c.Email.Enviados);
    }

    // ---------- Atualizar ----------

    private static AtualizarFuncionarioDto Atualizacao(Empresa e, Cenario c, string? nome = null, string? cpf = null, string? email = null,
        string? gestor = "FUNC-0003", int? idCargo = null) => new(
        nome ?? "Ana Souza Lima", cpf ?? "529.982.247-25", "987654321", null, null,
        new DateOnly(1995, 5, 5), new DateOnly(2024, 3, 11),
        email ?? "ana@hrauto.local", "13988887777", idCargo ?? e.Cargo.Id, e.Departamento.Id, gestor,
        new EnderecoDto("11015-000", "Av. Ana Costa", "500", "Ap 12", "Gonzaga", "Santos", "SP"));

    [Fact]
    public async Task Atualizar_RhAlteraDados_AuditaSoOsNomesDosCampos()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var outroCargo = c.NovoCargo(2, "Analista Sênior");

        var r = await c.AtualizarFuncionario().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), "FUNC-0004", Atualizacao(e, c, idCargo: outroCargo.Id));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("Ana Souza Lima", r.Valor!.Nome);
        Assert.Equal("Analista Sênior", r.Valor.Cargo);
        Assert.Equal("Gil Gestor", r.Valor.NomeGestor);
        Assert.Equal("Ana Souza Lima", e.Ana.Nome);
        Assert.Equal(2, e.Ana.IdCargo);

        var detalhes = c.Banco.Detalhes.Single(d => d.IdFuncionario == e.Ana.Id);
        Assert.Equal("52998224725", detalhes.Cpf);
        Assert.Equal("Av. Ana Costa", detalhes.Endereco);

        var log = c.Banco.Logs.Single(l => l.Acao == "AtualizarFuncionario");
        Assert.Contains("nome", log.Descricao);
        Assert.Contains("cargo", log.Descricao);
        Assert.DoesNotContain("529", log.Descricao);
        Assert.DoesNotContain("Costa", log.Descricao);
    }

    [Fact]
    public async Task Atualizar_SemMudancas_NaoGravaNemAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        // Primeiro ajusta, depois repete o mesmo corpo.
        await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004", Atualizacao(e, c));
        var salvamentos = c.Banco.Salvamentos;
        var logs = c.Banco.Logs.Count;

        var r = await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004", Atualizacao(e, c));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(salvamentos, c.Banco.Salvamentos);
        Assert.Equal(logs, c.Banco.Logs.Count);
    }

    [Fact]
    public async Task Atualizar_MudarEmail_AcompanhaOLogin()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AtualizarFuncionario().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), "FUNC-0004", Atualizacao(e, c, email: "Ana.Nova@hrauto.local"));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("ana.nova@hrauto.local", e.Ana.Email);
        Assert.Equal("ana.nova@hrauto.local", e.UsuarioAna.Email);
    }

    [Fact]
    public async Task Atualizar_RemoverGestor_Funciona()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004", Atualizacao(e, c, gestor: null));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Null(e.Ana.IdGestor);
        Assert.Null(r.Valor!.CodigoGestor);
    }

    [Fact]
    public async Task Atualizar_CriaDetalhesQuandoNaoExistem()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.Banco.Detalhes.RemoveAll(d => d.IdFuncionario == e.Ana.Id);

        var r = await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004", Atualizacao(e, c));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("52998224725", c.Banco.Detalhes.Single(d => d.IdFuncionario == e.Ana.Id).Cpf);
    }

    [Fact]
    public async Task Atualizar_CpfDeOutroFuncionario_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.Banco.Detalhes.Single(d => d.IdFuncionario == e.Bruno.Id).Cpf = "52998224725";

        var r = await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004", Atualizacao(e, c));

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.NotEqual("Ana Souza Lima", e.Ana.Nome);
    }

    [Fact]
    public async Task Atualizar_EmailDeOutroFuncionario_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AtualizarFuncionario().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), "FUNC-0004", Atualizacao(e, c, email: e.Bruno.Email));

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
    }

    [Fact]
    public async Task Atualizar_GestorQueFormariaCiclo_EhRecusado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        // Gil é gestor de Ana; escolher Ana como gestor de Gil fecharia o ciclo.
        var dto = Atualizacao(e, c, nome: "Gil Gestor", gestor: "FUNC-0004") with { Email = e.Gestor.Email };
        var r = await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0003", dto);

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("circular", r.Erro!);
        Assert.Null(e.Gestor.IdGestor);
    }

    [Fact]
    public async Task Atualizar_GestorEleMesmo_EhRecusado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004", Atualizacao(e, c, gestor: "FUNC-0004"));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Fact]
    public async Task Atualizar_RhNaoAlteraDadosDeRhNemAdmin_MasAdminAltera()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var dtoRh = Atualizacao(e, c, nome: "Rita Souza", email: e.Rh.Email, gestor: null);
        var dtoAdmin = Atualizacao(e, c, nome: "Alice Souza", email: e.Admin.Email, gestor: null);

        var rhEditaRh = await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0002", dtoRh);
        var rhEditaAdmin = await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0001", dtoAdmin);
        var adminEditaRh = await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), "FUNC-0002", dtoRh);

        Assert.Equal(TipoErro.Proibido, rhEditaRh.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, rhEditaAdmin.TipoDoErro);
        Assert.True(adminEditaRh.Sucesso, adminEditaRh.Erro);
        Assert.Equal("Rita Souza", e.Rh.Nome);
    }

    [Fact]
    public async Task Atualizar_ColaboradorEGestor_SaoProibidos()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var proprio = await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioAna), "FUNC-0004", Atualizacao(e, c));
        var gestor = await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioGestor), "FUNC-0004", Atualizacao(e, c));

        Assert.Equal(TipoErro.Proibido, proprio.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, gestor.TipoDoErro);
    }

    [Theory]
    [InlineData("FUNC-0999", TipoErro.NaoEncontrado)]
    [InlineData("4", TipoErro.Validacao)]
    public async Task Atualizar_CodigoInexistenteOuInvalido(string codigo, TipoErro esperado)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AtualizarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioRh), codigo, Atualizacao(e, c));

        Assert.Equal(esperado, r.TipoDoErro);
    }

    // ---------- Listar ----------

    [Fact]
    public async Task Listar_RhVeTodos_ComPaginacao()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var caso = c.ListarFuncionarios();

        var primeira = await caso.ExecutarAsync(c.Solicitante(e.UsuarioRh), new FiltroListaFuncionariosDto(null, null, null, null, 1, 4));
        var segunda = await caso.ExecutarAsync(c.Solicitante(e.UsuarioRh), new FiltroListaFuncionariosDto(null, null, null, null, 2, 4));

        Assert.Equal(6, primeira.Valor!.Total);
        Assert.Equal(2, primeira.Valor.TotalPaginas);
        Assert.Equal(4, primeira.Valor.Itens.Count);
        Assert.Equal(2, segunda.Valor!.Itens.Count);
        Assert.Equal("FUNC-0001", primeira.Valor.Itens[0].Codigo);
        Assert.Equal("FUNC-0006", segunda.Valor.Itens[1].Codigo);
    }

    [Fact]
    public async Task Listar_BuscaPeloCodigo_EMostraCargoDepartamentoEGestor()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ListarFuncionarios().ExecutarAsync(
            c.Solicitante(e.UsuarioAdmin), new FiltroListaFuncionariosDto("func-0004", null, null, null, null, null));

        var item = Assert.Single(r.Valor!.Itens);
        Assert.Equal("Ana Souza", item.Nome);
        Assert.Equal("Analista", item.Cargo);
        Assert.Equal("Tecnologia", item.Departamento);
        Assert.Equal("FUNC-0003", item.CodigoGestor);
        Assert.Equal("Gil Gestor", item.NomeGestor);
    }

    [Theory]
    [InlineData("Ana")]
    [InlineData("0004")]
    [InlineData("FUNC-4")]
    public async Task Listar_BuscaPorNomeOuFormatoErrado_EhRecusada(string busca)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ListarFuncionarios().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), new FiltroListaFuncionariosDto(busca, null, null, null, null, null));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("FUNC-0001", r.Erro!);
    }

    [Fact]
    public async Task Listar_FiltraPorSituacao()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        e.Carla.Ativo = false;

        var ativos = await c.ListarFuncionarios().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), new FiltroListaFuncionariosDto(null, null, null, true, null, null));
        var desligados = await c.ListarFuncionarios().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), new FiltroListaFuncionariosDto(null, null, null, false, null, null));

        Assert.Equal(5, ativos.Valor!.Total);
        Assert.Equal("FUNC-0006", Assert.Single(desligados.Valor!.Itens).Codigo);
    }

    [Fact]
    public async Task Listar_GestorVeSomenteASuaEquipe()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ListarFuncionarios().ExecutarAsync(c.Solicitante(e.UsuarioGestor), null);

        Assert.Equal(new[] { "FUNC-0004", "FUNC-0005" }, r.Valor!.Itens.Select(i => i.Codigo));
    }

    [Fact]
    public async Task Listar_GestorBuscandoFuncionarioDeFora_NaoEncontraNada()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ListarFuncionarios().ExecutarAsync(
            c.Solicitante(e.UsuarioGestor), new FiltroListaFuncionariosDto("FUNC-0006", null, null, null, null, null));

        Assert.Empty(r.Valor!.Itens);
    }

    [Fact]
    public async Task Listar_ColaboradorQueNaoEGestor_EhProibido()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ListarFuncionarios().ExecutarAsync(c.Solicitante(e.UsuarioCarla), null);

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }

    [Theory]
    [InlineData(null, null, 1, 20)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-3, 5000, 1, 100)]
    [InlineData(3, 10, 3, 10)]
    public void Paginacao_CorrigeValoresForaDaFaixa(int? pagina, int? tamanho, int paginaEsperada, int tamanhoEsperado)
    {
        var p = Paginacao.De(pagina, tamanho);

        Assert.Equal(paginaEsperada, p.Pagina);
        Assert.Equal(tamanhoEsperado, p.Tamanho);
        Assert.Equal((paginaEsperada - 1) * tamanhoEsperado, p.Pular);
    }

    // ---------- Consultar ficha ----------

    [Fact]
    public async Task Consultar_RhVeTudo_EAuditaAConsultaDeDadosPessoais()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.Banco.Detalhes.Single(d => d.IdFuncionario == e.Ana.Id).Cpf = "52998224725";

        var r = await c.ConsultarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004");

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("529.982.247-25", r.Valor!.Documentos!.Cpf);
        Assert.Equal("Santos", r.Valor.Endereco!.Cidade);
        Assert.Equal(new DateOnly(1995, 5, 5), r.Valor.DataNascimento);
        Assert.True(r.Valor.Acesso!.Possui);
        Assert.Equal("Colaborador", r.Valor.Acesso.Perfil);

        var log = c.Banco.Logs.Single();
        Assert.Equal("ConsultaDadosPessoais", log.Acao);
        Assert.Equal(e.Ana.Id, log.IdRegistro);
        Assert.Equal(e.UsuarioRh.Id, log.IdUsuario);
    }

    [Fact]
    public async Task Consultar_OProprioFuncionarioVeSeusDados_SemAuditoriaESemAcesso()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ConsultarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioAna), "FUNC-0004");

        Assert.True(r.Sucesso, r.Erro);
        Assert.NotNull(r.Valor!.Documentos);
        Assert.NotNull(r.Valor.Endereco);
        Assert.Null(r.Valor.Acesso);
        Assert.Empty(c.Banco.Logs);
    }

    [Fact]
    public async Task Consultar_GestorVeAEquipeSemDadosSensiveis()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ConsultarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioGestor), "FUNC-0004");

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("Ana Souza", r.Valor!.Nome);
        Assert.Null(r.Valor.Documentos);
        Assert.Null(r.Valor.Endereco);
        Assert.Null(r.Valor.DataNascimento);
        Assert.Null(r.Valor.Acesso);
        Assert.Empty(c.Banco.Logs);
    }

    [Theory]
    [InlineData("FUNC-0006")] // fora da equipe do gestor
    [InlineData("FUNC-0999")] // inexistente: a resposta é a mesma, para não revelar quais códigos existem
    public async Task Consultar_GestorForaDaEquipe_EhProibido(string codigo)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ConsultarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioGestor), codigo);

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }

    [Fact]
    public async Task Consultar_ColaboradorVendoOutro_EhProibido()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ConsultarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioCarla), "FUNC-0004");

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }

    [Theory]
    [InlineData("FUNC-0999", TipoErro.NaoEncontrado)]
    [InlineData("abc", TipoErro.Validacao)]
    [InlineData(null, TipoErro.Validacao)]
    public async Task Consultar_RhComCodigoInexistenteOuInvalido(string? codigo, TipoErro esperado)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ConsultarFuncionario().ExecutarAsync(c.Solicitante(e.UsuarioRh), codigo);

        Assert.Equal(esperado, r.TipoDoErro);
    }
}
