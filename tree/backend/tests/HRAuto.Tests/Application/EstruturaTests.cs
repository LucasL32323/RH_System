using HRAuto.Application.Cadastro;
using HRAuto.Application.Common;

namespace HRAuto.Tests.Application;

/// <summary>RF14 (cargos) e RF15 (departamentos).</summary>
public class EstruturaTests
{
    // ---------- Cargos ----------

    [Fact]
    public async Task Cargos_RhListaEmOrdemAlfabetica_ColaboradorNao()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovoCargo(2, "Contador");

        var rh = await c.GerenciarCargos().ListarAsync(c.Solicitante(e.UsuarioRh));
        var colaborador = await c.GerenciarCargos().ListarAsync(c.Solicitante(e.UsuarioAna));

        Assert.Equal(new[] { "Analista", "Contador" }, rh.Valor!.Select(x => x.Nome));
        Assert.Equal(TipoErro.Proibido, colaborador.TipoDoErro);
    }

    [Fact]
    public async Task Cargos_Criar_NormalizaONomeGeraIdEAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.GerenciarCargos().CriarAsync(
            c.Solicitante(e.UsuarioRh), new SalvarEstruturaDto("  Analista   de   RH ", " Cuida do pessoal "));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("Analista de RH", r.Valor!.Nome);
        Assert.Equal("Cuida do pessoal", r.Valor.Descricao);
        Assert.True(r.Valor.Id > 0);
        var log = c.Banco.Logs.Single();
        Assert.Equal("CriarCargo", log.Acao);
        Assert.Equal(r.Valor.Id, log.IdRegistro);
        Assert.Equal(1, c.Banco.TransacoesConfirmadas);
    }

    [Theory]
    [InlineData("analista")]   // mesmo nome com outra caixa
    [InlineData(" ANALISTA ")]
    public async Task Cargos_NomeDuplicado_EhConflito(string nome)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.GerenciarCargos().CriarAsync(c.Solicitante(e.UsuarioRh), new SalvarEstruturaDto(nome, null));

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("A")]
    public async Task Cargos_NomeInvalido_EhRecusado(string? nome)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.GerenciarCargos().CriarAsync(c.Solicitante(e.UsuarioRh), new SalvarEstruturaDto(nome, null));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Fact]
    public async Task Cargos_Criar_ColaboradorEhProibido()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.GerenciarCargos().CriarAsync(c.Solicitante(e.UsuarioGestor), new SalvarEstruturaDto("Novo cargo", null));

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }

    [Fact]
    public async Task Cargos_Atualizar_RenomeiaEAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.GerenciarCargos().AtualizarAsync(c.Solicitante(e.UsuarioRh), e.Cargo.Id, new SalvarEstruturaDto("Analista Pleno", "Nova descrição"));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("Analista Pleno", e.Cargo.NomeCargo);
        Assert.Equal("AtualizarCargo", c.Banco.Logs.Single().Acao);
    }

    [Fact]
    public async Task Cargos_Atualizar_MesmoNomeDoProprioCargo_NaoEhConflito_ENaoAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.GerenciarCargos().AtualizarAsync(c.Solicitante(e.UsuarioRh), e.Cargo.Id, new SalvarEstruturaDto("Analista", null));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Empty(c.Banco.Logs);
    }

    [Fact]
    public async Task Cargos_Atualizar_NomeDeOutroCargo_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var outro = c.NovoCargo(2, "Contador");

        var r = await c.GerenciarCargos().AtualizarAsync(c.Solicitante(e.UsuarioRh), outro.Id, new SalvarEstruturaDto("Analista", null));

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.Equal("Contador", outro.NomeCargo);
    }

    [Fact]
    public async Task Cargos_Atualizar_Inexistente_EhNaoEncontrado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.GerenciarCargos().AtualizarAsync(c.Solicitante(e.UsuarioRh), 99, new SalvarEstruturaDto("Qualquer", null));

        Assert.Equal(TipoErro.NaoEncontrado, r.TipoDoErro);
    }

    [Fact]
    public async Task Cargos_Excluir_EmUso_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.GerenciarCargos().ExcluirAsync(c.Solicitante(e.UsuarioRh), e.Cargo.Id);

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.Contains("6 funcionário(s)", r.Erro!);
        Assert.Contains(c.Banco.Cargos, x => x.Id == e.Cargo.Id);
    }

    [Fact]
    public async Task Cargos_Excluir_SemUso_Remove_EAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var livre = c.NovoCargo(2, "Estagiário");

        var r = await c.GerenciarCargos().ExcluirAsync(c.Solicitante(e.UsuarioRh), livre.Id);

        Assert.True(r.Sucesso, r.Erro);
        Assert.DoesNotContain(c.Banco.Cargos, x => x.Id == 2);
        Assert.Equal("ExcluirCargo", c.Banco.Logs.Single().Acao);
    }

    [Fact]
    public async Task Cargos_Excluir_ConflitoDeChaveEstrangeira_VirouConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var livre = c.NovoCargo(2, "Estagiário");
        c.Banco.ConflitoNoProximoSalvar = true; // alguém passou a usar o cargo entre a checagem e a gravação

        var r = await c.GerenciarCargos().ExcluirAsync(c.Solicitante(e.UsuarioRh), livre.Id);

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
    }

    // ---------- Departamentos ----------

    [Fact]
    public async Task Departamentos_CriarAtualizarListarEExcluir()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var rh = c.Solicitante(e.UsuarioRh);

        var criado = await c.GerenciarDepartamentos().CriarAsync(rh, new SalvarEstruturaDto("Financeiro", "Contas"));
        var atualizado = await c.GerenciarDepartamentos().AtualizarAsync(rh, criado.Valor!.Id, new SalvarEstruturaDto("Financeiro e Fiscal", null));
        var lista = await c.GerenciarDepartamentos().ListarAsync(rh);
        var excluido = await c.GerenciarDepartamentos().ExcluirAsync(rh, criado.Valor.Id);

        Assert.True(criado.Sucesso, criado.Erro);
        Assert.Equal("Financeiro e Fiscal", atualizado.Valor!.Nome);
        Assert.Equal(new[] { "Financeiro e Fiscal", "Tecnologia" }, lista.Valor!.Select(x => x.Nome));
        Assert.True(excluido.Sucesso, excluido.Erro);
        Assert.Equal(new[] { "CriarDepartamento", "AtualizarDepartamento", "ExcluirDepartamento" }, c.Banco.Logs.Select(l => l.Acao));
    }

    [Fact]
    public async Task Departamentos_NomeDuplicadoEEmUso()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var rh = c.Solicitante(e.UsuarioRh);

        var duplicado = await c.GerenciarDepartamentos().CriarAsync(rh, new SalvarEstruturaDto("tecnologia", null));
        var emUso = await c.GerenciarDepartamentos().ExcluirAsync(rh, e.Departamento.Id);

        Assert.Equal(TipoErro.Conflito, duplicado.TipoDoErro);
        Assert.Equal(TipoErro.Conflito, emUso.TipoDoErro);
    }

    [Fact]
    public async Task Departamentos_ColaboradorEhProibido_EmTudo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var colaborador = c.Solicitante(e.UsuarioAna);
        var dto = new SalvarEstruturaDto("Novo", null);

        Assert.Equal(TipoErro.Proibido, (await c.GerenciarDepartamentos().ListarAsync(colaborador)).TipoDoErro);
        Assert.Equal(TipoErro.Proibido, (await c.GerenciarDepartamentos().CriarAsync(colaborador, dto)).TipoDoErro);
        Assert.Equal(TipoErro.Proibido, (await c.GerenciarDepartamentos().AtualizarAsync(colaborador, 1, dto)).TipoDoErro);
        Assert.Equal(TipoErro.Proibido, (await c.GerenciarDepartamentos().ExcluirAsync(colaborador, 1)).TipoDoErro);
    }
}
