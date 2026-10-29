using HRAuto.Application.Common;

namespace HRAuto.Tests.Application;

/// <summary>RF25, RF36: cada pessoa lista e marca como lidas somente as próprias notificações.</summary>
public class NotificacoesAppTests
{
    [Fact]
    public async Task Listar_MostraSoAsMinhas_DaMaisRecenteParaAMaisAntiga()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovaNotificacao(e.Ana, "Antiga", envio: "2026-10-18T09:00:00");
        c.NovaNotificacao(e.Ana, "Recente", envio: "2026-10-20T09:00:00");
        c.NovaNotificacao(e.Bruno, "Do Bruno");

        var r = await c.ListarNotificacoes().ExecutarAsync(c.Solicitante(e.UsuarioAna), false, null, null);

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(2, r.Valor!.Total);
        Assert.Equal(new[] { "Recente", "Antiga" }, r.Valor.Itens.Select(n => n.Titulo).ToArray());
    }

    [Fact]
    public async Task Listar_SomenteNaoLidas_EPaginacao()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovaNotificacao(e.Ana, "Lida", lida: true, envio: "2026-10-19T09:00:00");
        c.NovaNotificacao(e.Ana, "Nova 1", envio: "2026-10-20T09:00:00");
        c.NovaNotificacao(e.Ana, "Nova 2", envio: "2026-10-21T09:00:00");

        var naoLidas = await c.ListarNotificacoes().ExecutarAsync(c.Solicitante(e.UsuarioAna), true, null, null);
        var pagina1 = await c.ListarNotificacoes().ExecutarAsync(c.Solicitante(e.UsuarioAna), false, 1, 2);
        var pagina2 = await c.ListarNotificacoes().ExecutarAsync(c.Solicitante(e.UsuarioAna), false, 2, 2);

        Assert.Equal(2, naoLidas.Valor!.Total);
        Assert.All(naoLidas.Valor.Itens, n => Assert.False(n.Lida));
        Assert.Equal(3, pagina1.Valor!.Total);
        Assert.Equal(2, pagina1.Valor.TotalPaginas);
        Assert.Equal(2, pagina1.Valor.Itens.Count);
        Assert.Equal("Lida", Assert.Single(pagina2.Valor!.Itens).Titulo);
    }

    [Fact]
    public async Task Listar_SemNotificacoes_DevolveListaVazia()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ListarNotificacoes().ExecutarAsync(c.Solicitante(e.UsuarioAna), false, null, null);

        Assert.Empty(r.Valor!.Itens);
        Assert.Equal(0, r.Valor.Total);
    }

    [Fact]
    public async Task Contar_ConsideraSoAsNaoLidasDeQuemPergunta()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovaNotificacao(e.Ana, "Nova");
        c.NovaNotificacao(e.Ana, "Lida", lida: true);
        c.NovaNotificacao(e.Bruno, "Do Bruno");

        var r = await c.ContarNaoLidas().ExecutarAsync(c.Solicitante(e.UsuarioAna));

        Assert.Equal(1, r.Valor!.Quantidade);
    }

    [Fact]
    public async Task MarcarComoLida_GravaUmaVez_ERepetirNaoGravaDeNovo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var notificacao = c.NovaNotificacao(e.Ana, "Nova");

        var primeira = await c.MarcarComoLida().ExecutarAsync(c.Solicitante(e.UsuarioAna), notificacao.Id);
        var salvamentos = c.Banco.Salvamentos;
        var segunda = await c.MarcarComoLida().ExecutarAsync(c.Solicitante(e.UsuarioAna), notificacao.Id);

        Assert.True(primeira.Sucesso, primeira.Erro);
        Assert.True(primeira.Valor!.Lida);
        Assert.True(notificacao.Lida);
        Assert.Equal(1, salvamentos);
        Assert.True(segunda.Sucesso);
        Assert.Equal(salvamentos, c.Banco.Salvamentos);
    }

    [Fact]
    public async Task MarcarComoLida_DeOutraPessoaOuInexistente_Da404_ENaoAltera()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var doBruno = c.NovaNotificacao(e.Bruno, "Do Bruno");

        var alheia = await c.MarcarComoLida().ExecutarAsync(c.Solicitante(e.UsuarioAna), doBruno.Id);
        var inexistente = await c.MarcarComoLida().ExecutarAsync(c.Solicitante(e.UsuarioAna), 99999);

        Assert.Equal(TipoErro.NaoEncontrado, alheia.TipoDoErro);
        Assert.Equal(TipoErro.NaoEncontrado, inexistente.TipoDoErro);
        Assert.Equal(alheia.Erro, inexistente.Erro);
        Assert.False(doBruno.Lida);
        Assert.Equal(0, c.Banco.Salvamentos);
    }

    [Fact]
    public async Task MarcarTodas_AfetaSoAsMinhasNaoLidas_EInformaQuantas()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var a1 = c.NovaNotificacao(e.Ana, "Nova 1");
        var a2 = c.NovaNotificacao(e.Ana, "Nova 2");
        c.NovaNotificacao(e.Ana, "Já lida", lida: true);
        var doBruno = c.NovaNotificacao(e.Bruno, "Do Bruno");

        var r = await c.MarcarTodasComoLidas().ExecutarAsync(c.Solicitante(e.UsuarioAna));

        Assert.Equal(2, r.Valor!.Marcadas);
        Assert.True(a1.Lida);
        Assert.True(a2.Lida);
        Assert.False(doBruno.Lida);
        Assert.Equal(1, c.Banco.Salvamentos);
    }

    [Fact]
    public async Task MarcarTodas_SemNadaPendente_NaoGrava()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.MarcarTodasComoLidas().ExecutarAsync(c.Solicitante(e.UsuarioAna));

        Assert.Equal(0, r.Valor!.Marcadas);
        Assert.Equal(0, c.Banco.Salvamentos);
    }
}
