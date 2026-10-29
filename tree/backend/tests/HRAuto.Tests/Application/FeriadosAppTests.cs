using HRAuto.Application.Calendario;
using HRAuto.Application.Common;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

/// <summary>RF28: consulta e cadastro de feriados. Relógio: 21/10/2026.</summary>
public class FeriadosAppTests
{
    private static CriarFeriadoDto Natal() => new(new DateOnly(2026, 12, 25), "Natal", "Nacional", null, null);

    // ---------- Listar ----------

    [Fact]
    public async Task Listar_SemAno_UsaOAnoAtual_EmOrdemDeData()
    {
        var c = new Cenario();
        c.NovoFeriado("2026-12-25", "Natal");
        c.NovoFeriado("2026-01-01", "Confraternização Universal");
        c.NovoFeriado("2025-12-25", "Natal de 2025");
        c.NovoFeriado("2027-01-01", "Confraternização de 2027");

        var r = await c.ListarFeriados().ExecutarAsync(null);

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(new[] { "Confraternização Universal", "Natal" }, r.Valor!.Select(f => f.Nome).ToArray());
    }

    [Fact]
    public async Task Listar_ComAno_TrazSoAqueleAno()
    {
        var c = new Cenario();
        c.NovoFeriado("2027-01-01", "Confraternização de 2027");
        c.NovoFeriado("2026-12-25", "Natal");

        var r = await c.ListarFeriados().ExecutarAsync(2027);

        Assert.Equal("Confraternização de 2027", Assert.Single(r.Valor!).Nome);
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    [InlineData(0)]
    public async Task Listar_AnoForaDaFaixa_Da400(int ano)
    {
        var c = new Cenario();

        var r = await c.ListarFeriados().ExecutarAsync(ano);

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    // ---------- Criar ----------

    [Fact]
    public async Task Criar_Nacional_GravaEAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.GerenciarFeriados().CriarAsync(c.Solicitante(e.UsuarioRh), Natal());

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("Natal", r.Valor!.Nome);
        Assert.Equal("Nacional", r.Valor.Tipo);
        Assert.True(r.Valor.Id > 0);

        var gravado = Assert.Single(c.Banco.Feriados);
        Assert.Equal(new DateOnly(2026, 12, 25), gravado.Data);

        var log = Assert.Single(c.Banco.Logs);
        Assert.Equal("CriarFeriado", log.Acao);
        Assert.Equal("FERIADO", log.Tabela);
        Assert.Equal(gravado.Id, log.IdRegistro);
        Assert.Equal(1, c.Banco.TransacoesConfirmadas);
    }

    [Fact]
    public async Task Criar_Municipal_NormalizaEstado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.GerenciarFeriados().CriarAsync(
            c.Solicitante(e.UsuarioAdmin), new CriarFeriadoDto(new DateOnly(2026, 1, 26), "Aniversário de Santos", "municipal", "Santos", "sp"));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("Municipal", r.Valor!.Tipo);
        Assert.Equal("SP", r.Valor.Estado);
        Assert.Equal("Santos", r.Valor.Cidade);
    }

    [Fact]
    public async Task Criar_Colaborador_Recusado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.GerenciarFeriados().CriarAsync(c.Solicitante(e.UsuarioGestor), Natal());

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
        Assert.Empty(c.Banco.Feriados);
    }

    [Fact]
    public async Task Criar_Duplicado_Da409_MasMesmaDataComOutroTipoPode()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var rh = c.Solicitante(e.UsuarioRh);
        await c.GerenciarFeriados().CriarAsync(rh, Natal());

        var repetido = await c.GerenciarFeriados().CriarAsync(rh, Natal());
        var estadual = await c.GerenciarFeriados().CriarAsync(rh, new CriarFeriadoDto(new DateOnly(2026, 12, 25), "Natal paulista", "Estadual", null, "SP"));

        Assert.Equal(TipoErro.Conflito, repetido.TipoDoErro);
        Assert.True(estadual.Sucesso, estadual.Erro);
        Assert.Equal(2, c.Banco.Feriados.Count);
    }

    [Fact]
    public async Task Criar_MunicipalRepetidoComCidadeEmOutraCaixa_Da409()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var rh = c.Solicitante(e.UsuarioRh);
        await c.GerenciarFeriados().CriarAsync(rh, new CriarFeriadoDto(new DateOnly(2026, 1, 26), "Aniversário de Santos", "Municipal", "Santos", "SP"));

        var r = await c.GerenciarFeriados().CriarAsync(rh, new CriarFeriadoDto(new DateOnly(2026, 1, 26), "Outro nome", "Municipal", "SANTOS", "sp"));

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
    }

    [Fact]
    public async Task Criar_DadosInvalidos_Da400()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var rh = c.Solicitante(e.UsuarioRh);
        var caso = c.GerenciarFeriados();

        var semDto = await caso.CriarAsync(rh, null);
        var semData = await caso.CriarAsync(rh, new CriarFeriadoDto(null, "Natal", "Nacional", null, null));
        var anoAntigo = await caso.CriarAsync(rh, new CriarFeriadoDto(new DateOnly(1990, 12, 25), "Natal", "Nacional", null, null));
        var tipoRuim = await caso.CriarAsync(rh, new CriarFeriadoDto(new DateOnly(2026, 12, 25), "Natal", "Regional", null, null));
        var nomeCurto = await caso.CriarAsync(rh, new CriarFeriadoDto(new DateOnly(2026, 12, 25), "N", "Nacional", null, null));
        var municipalSemCidade = await caso.CriarAsync(rh, new CriarFeriadoDto(new DateOnly(2026, 12, 25), "Data local", "Municipal", null, "SP"));

        foreach (var r in new[] { semDto, semData, anoAntigo, tipoRuim, nomeCurto, municipalSemCidade })
            Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Empty(c.Banco.Feriados);
        Assert.Empty(c.Banco.Logs);
    }

    // ---------- Excluir ----------

    [Fact]
    public async Task Excluir_RemoveEAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var feriado = c.NovoFeriado("2026-12-25", "Natal");

        var r = await c.GerenciarFeriados().ExcluirAsync(c.Solicitante(e.UsuarioRh), feriado.Id);

        Assert.True(r.Sucesso, r.Erro);
        Assert.Empty(c.Banco.Feriados);
        var log = Assert.Single(c.Banco.Logs);
        Assert.Equal("ExcluirFeriado", log.Acao);
        Assert.Equal(feriado.Id, log.IdRegistro);
    }

    [Fact]
    public async Task Excluir_Colaborador_Recusado_EInexistente_Da404()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var feriado = c.NovoFeriado("2026-12-25", "Natal");

        var colaborador = await c.GerenciarFeriados().ExcluirAsync(c.Solicitante(e.UsuarioAna), feriado.Id);
        var inexistente = await c.GerenciarFeriados().ExcluirAsync(c.Solicitante(e.UsuarioRh), 99999);

        Assert.Equal(TipoErro.Proibido, colaborador.TipoDoErro);
        Assert.Equal(TipoErro.NaoEncontrado, inexistente.TipoDoErro);
        Assert.Single(c.Banco.Feriados);
        Assert.Equal(TiposFeriado.Nacional, c.Banco.Feriados[0].Tipo);
    }
}
