using HRAuto.Application.Afastamentos;
using HRAuto.Application.Common;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

/// <summary>
/// RF26 (solicitar), RF27 (decidir) e listagem de férias.
/// Relógio: quarta-feira, 21/10/2026. Ana, Bruno e Carla foram admitidos em 11/03/2024 (direito a férias).
/// Datas usadas: 16/11/2026 é segunda; 22/11 é domingo.
/// </summary>
public class FeriasAppTests
{
    private static SolicitarFeriasDto Pedido(string inicio = "2026-11-16", string fim = "2026-11-30", int? dias = null, string? obs = null) =>
        new(ApoioApp.Data(inicio), ApoioApp.Data(fim), dias, obs);

    // ---------- Solicitar ----------

    [Fact]
    public async Task Solicitar_Valido_NascePendente_AvisaOGestor_EAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido(obs: "  Viagem em família  "));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("FUNC-0004", r.Valor!.CodigoFuncionario);
        Assert.Equal("Ana Souza", r.Valor.NomeFuncionario);
        Assert.Equal(StatusSolicitacao.Pendente, r.Valor.Status);
        Assert.Equal(15, r.Valor.Dias);
        Assert.Equal(new DateOnly(2026, 10, 21), r.Valor.DataSolicitacao);
        Assert.Equal("Viagem em família", r.Valor.Observacao);
        Assert.True(r.Valor.Id > 0);

        var gravada = Assert.Single(c.Banco.Ferias);
        Assert.Equal(e.Ana.Id, gravada.IdFuncionario);

        var aviso = Assert.Single(c.Banco.Notificacoes);
        Assert.Equal(e.Gestor.Id, aviso.IdFuncionario);
        Assert.Equal("Nova solicitação de férias", aviso.Titulo);

        var log = Assert.Single(c.Banco.Logs);
        Assert.Equal("SolicitarFerias", log.Acao);
        Assert.Equal("FERIAS", log.Tabela);
        Assert.Equal(gravada.Id, log.IdRegistro);
        Assert.Equal(1, c.Banco.TransacoesConfirmadas);
    }

    [Fact]
    public async Task Solicitar_DiasDivergentesDoPeriodo_Da400()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido(dias: 10));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("não corresponde", r.Erro);
        Assert.Empty(c.Banco.Ferias);
    }

    [Fact]
    public async Task Solicitar_SemGestorAtivo_AvisaOsRh_ENuncaOProprioSolicitante()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        e.Gestor.Ativo = false; // gestor desligado

        var daAna = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido());
        var daCarla = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(e.UsuarioCarla), Pedido("2026-12-07", "2026-12-21"));

        Assert.True(daAna.Sucesso, daAna.Erro);
        Assert.True(daCarla.Sucesso, daCarla.Erro);
        Assert.Equal(2, c.Banco.Notificacoes.Count);
        Assert.All(c.Banco.Notificacoes, n => Assert.Equal(e.Rh.Id, n.IdFuncionario));
    }

    [Fact]
    public async Task Solicitar_RhSemGestor_Grava_MasNinguemEAvisado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(e.UsuarioRh), Pedido());

        Assert.True(r.Sucesso, r.Erro);
        Assert.Single(c.Banco.Ferias);
        Assert.Empty(c.Banco.Notificacoes);
    }

    [Theory]
    [InlineData("2026-11-22", "2026-11-30")] // começa no domingo
    [InlineData("2026-11-21", "2026-11-30")] // sábado: o domingo cai nos 2 dias seguintes
    [InlineData("2026-11-20", "2026-11-30")] // sexta: idem
    public async Task Solicitar_InicioPertoDeDomingo_Da400(string inicio, string fim)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido(inicio, fim));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("domingo, feriado", r.Erro);
        Assert.Empty(c.Banco.Ferias);
    }

    [Fact]
    public async Task Solicitar_FeriadoDaCidadeDoFuncionario_Bloqueia_DeOutraCidadeNao()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa(); // detalhes: Santos/SP
        c.NovoFeriado("2026-11-18", "Data de Santos", TiposFeriado.Municipal, "Santos", "SP");
        c.NovoFeriado("2026-11-17", "Data de Praia Grande", TiposFeriado.Municipal, "Praia Grande", "SP");

        var bloqueado = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido("2026-11-16", "2026-11-30"));
        var liberado = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido("2026-11-19", "2026-11-30"));

        Assert.Equal(TipoErro.Validacao, bloqueado.TipoDoErro);
        Assert.True(liberado.Sucesso, liberado.Erro);
    }

    [Fact]
    public async Task Solicitar_SobrepostaAPendenteOuAprovada_Da400_MasRejeitadaNaoConta()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovasFerias(e.Ana, "2026-11-23", "2026-11-27");

        var sobreposta = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido("2026-11-25", "2026-12-09"));

        Assert.Equal(TipoErro.Validacao, sobreposta.TipoDoErro);
        Assert.Contains("sobrepõe", sobreposta.Erro);

        var c2 = new Cenario();
        var e2 = c2.NovaEmpresa();
        c2.NovasFerias(e2.Ana, "2026-11-16", "2026-11-30", StatusSolicitacao.Rejeitada);

        var aposRejeicao = await c2.SolicitarFerias().ExecutarAsync(c2.Solicitante(e2.UsuarioAna), Pedido());

        Assert.True(aposRejeicao.Sucesso, aposRejeicao.Erro);
    }

    [Fact]
    public async Task Solicitar_AcimaDoSaldoDoPeriodo_Da400()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovasFerias(e.Ana, "2026-12-01", "2026-12-20", StatusSolicitacao.Aprovada); // 20 dias

        var r = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido("2026-11-16", "2026-11-26")); // +11 = 31

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("Saldo insuficiente", r.Erro);
        Assert.Contains("10 dias", r.Erro);
    }

    [Fact]
    public async Task Solicitar_AntesDeCompletar12Meses_Da400()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var novo = c.NovoFuncionario(30, "Nina Nova", admissao: "2026-03-01");
        var usuario = c.NovoUsuario(novo, "nina@hrauto.local");

        var r = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(usuario), Pedido());

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("12 meses", r.Erro);
        Assert.NotNull(e);
    }

    [Fact]
    public async Task Solicitar_DataNoPassado_Da400_EHojeVale()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var passado = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido("2026-10-20", "2026-10-30"));
        var hoje = await c.SolicitarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido("2026-10-21", "2026-11-04"));

        Assert.Equal(TipoErro.Validacao, passado.TipoDoErro);
        Assert.True(hoje.Sucesso, hoje.Erro);
    }

    [Fact]
    public async Task Solicitar_DadosIncompletos_Da400()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ana = c.Solicitante(e.UsuarioAna);
        var caso = c.SolicitarFerias();

        var semDto = await caso.ExecutarAsync(ana, null);
        var semInicio = await caso.ExecutarAsync(ana, new SolicitarFeriasDto(null, new DateOnly(2026, 11, 30), null, null));
        var semFim = await caso.ExecutarAsync(ana, new SolicitarFeriasDto(new DateOnly(2026, 11, 16), null, null, null));
        var fimAntes = await caso.ExecutarAsync(ana, Pedido("2026-11-30", "2026-11-16"));
        var obsLonga = await caso.ExecutarAsync(ana, Pedido(obs: new string('x', SolicitarFerias.TamanhoMaximoObservacao + 1)));

        foreach (var r in new[] { semDto, semInicio, semFim, fimAntes, obsLonga })
            Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Empty(c.Banco.Ferias);
    }

    // ---------- Listar ----------

    private static Cenario ComSolicitacoes(out Empresa e)
    {
        var c = new Cenario();
        e = c.NovaEmpresa();
        c.NovasFerias(e.Ana, "2026-11-16", "2026-11-30", StatusSolicitacao.Pendente, "2026-10-01");
        c.NovasFerias(e.Ana, "2027-01-11", "2027-01-20", StatusSolicitacao.Aprovada, "2026-10-10");
        c.NovasFerias(e.Bruno, "2026-12-07", "2026-12-21", StatusSolicitacao.Pendente, "2026-10-05");
        c.NovasFerias(e.Carla, "2026-11-23", "2026-12-07", StatusSolicitacao.Pendente, "2026-10-03");
        return c;
    }

    [Fact]
    public async Task Listar_Minhas_PorPadrao_MostraSoAsDeQuemPergunta_DaMaisRecente()
    {
        var c = ComSolicitacoes(out var e);

        var r = await c.ListarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), null);

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(2, r.Valor!.Total);
        Assert.Equal(StatusSolicitacao.Aprovada, r.Valor.Itens[0].Status); // solicitada em 10/10
        Assert.All(r.Valor.Itens, f => Assert.Equal("Ana Souza", f.NomeFuncionario));
    }

    [Fact]
    public async Task Listar_Minhas_ComCodigoDeOutraPessoa_Recusado_ComOProprioCodigoVale()
    {
        var c = ComSolicitacoes(out var e);
        var ana = c.Solicitante(e.UsuarioAna);

        var alheio = await c.ListarFerias().ExecutarAsync(ana, new FiltroListaFeriasDto(null, "FUNC-0005", null, null, null));
        var proprio = await c.ListarFerias().ExecutarAsync(ana, new FiltroListaFeriasDto("minhas", "FUNC-0004", null, null, null));

        Assert.Equal(TipoErro.Proibido, alheio.TipoDoErro);
        Assert.Equal(2, proprio.Valor!.Total);
    }

    [Fact]
    public async Task Listar_Equipe_MostraSubordinadosDiretos_ComFiltros()
    {
        var c = ComSolicitacoes(out var e);
        var gestor = c.Solicitante(e.UsuarioGestor);

        var todas = await c.ListarFerias().ExecutarAsync(gestor, new FiltroListaFeriasDto("equipe", null, null, null, null));
        var pendentes = await c.ListarFerias().ExecutarAsync(gestor, new FiltroListaFeriasDto("EQUIPE", null, "pendente", null, null));
        var doBruno = await c.ListarFerias().ExecutarAsync(gestor, new FiltroListaFeriasDto("equipe", "FUNC-0005", null, null, null));
        var daCarla = await c.ListarFerias().ExecutarAsync(gestor, new FiltroListaFeriasDto("equipe", "FUNC-0006", null, null, null));

        Assert.Equal(3, todas.Valor!.Total);
        Assert.DoesNotContain(todas.Valor.Itens, f => f.CodigoFuncionario == "FUNC-0006");
        Assert.Equal(2, pendentes.Valor!.Total);
        Assert.Equal("Bruno Lima", Assert.Single(doBruno.Valor!.Itens).NomeFuncionario);
        Assert.Equal(0, daCarla.Valor!.Total); // fora da equipe: nada vaza
    }

    [Fact]
    public async Task Listar_Equipe_QuemNaoEGestor_Recusado()
    {
        var c = ComSolicitacoes(out var e);

        var r = await c.ListarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), new FiltroListaFeriasDto("equipe", null, null, null, null));

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }

    [Fact]
    public async Task Listar_Todas_SoRhEAdmin()
    {
        var c = ComSolicitacoes(out var e);
        var filtro = new FiltroListaFeriasDto("todas", null, null, null, null);

        var gestor = await c.ListarFerias().ExecutarAsync(c.Solicitante(e.UsuarioGestor), filtro);
        var rh = await c.ListarFerias().ExecutarAsync(c.Solicitante(e.UsuarioRh), filtro);
        var admin = await c.ListarFerias().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), filtro);

        Assert.Equal(TipoErro.Proibido, gestor.TipoDoErro);
        Assert.Equal(4, rh.Valor!.Total);
        Assert.Equal(4, admin.Valor!.Total);
    }

    [Fact]
    public async Task Listar_Paginacao()
    {
        var c = ComSolicitacoes(out var e);

        var p1 = await c.ListarFerias().ExecutarAsync(c.Solicitante(e.UsuarioRh), new FiltroListaFeriasDto("todas", null, null, 1, 3));
        var p2 = await c.ListarFerias().ExecutarAsync(c.Solicitante(e.UsuarioRh), new FiltroListaFeriasDto("todas", null, null, 2, 3));

        Assert.Equal(3, p1.Valor!.Itens.Count);
        Assert.Single(p2.Valor!.Itens);
        Assert.Equal(2, p1.Valor.TotalPaginas);
    }

    [Fact]
    public async Task Listar_FiltrosInvalidos_Da400()
    {
        var c = ComSolicitacoes(out var e);
        var rh = c.Solicitante(e.UsuarioRh);

        var escopo = await c.ListarFerias().ExecutarAsync(rh, new FiltroListaFeriasDto("geral", null, null, null, null));
        var status = await c.ListarFerias().ExecutarAsync(rh, new FiltroListaFeriasDto("todas", null, "talvez", null, null));
        var codigo = await c.ListarFerias().ExecutarAsync(rh, new FiltroListaFeriasDto("todas", "Ana", null, null, null));

        Assert.Equal(TipoErro.Validacao, escopo.TipoDoErro);
        Assert.Equal(TipoErro.Validacao, status.TipoDoErro);
        Assert.Equal(TipoErro.Validacao, codigo.TipoDoErro);
    }

    // ---------- Decidir ----------

    [Fact]
    public async Task Decidir_GestorAprova_RegistraDecisor_AvisaOFuncionario_EAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ferias = c.NovasFerias(e.Ana, "2026-11-16", "2026-11-30");

        var r = await c.DecidirFerias().ExecutarAsync(c.Solicitante(e.UsuarioGestor), ferias.Id, new DecidirFeriasDto(true, "Aprovado, bom descanso"));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(StatusSolicitacao.Aprovada, r.Valor!.Status);
        Assert.Equal(StatusSolicitacao.Aprovada, ferias.Status);
        Assert.Contains("[Aprovada por FUNC-0003] Aprovado, bom descanso", ferias.Observacao);

        var aviso = Assert.Single(c.Banco.Notificacoes);
        Assert.Equal(e.Ana.Id, aviso.IdFuncionario);
        Assert.Equal("Férias aprovada", aviso.Titulo);

        var log = Assert.Single(c.Banco.Logs);
        Assert.Equal("DecidirFerias", log.Acao);
        Assert.Equal(ferias.Id, log.IdRegistro);
        Assert.Contains("FUNC-0004", log.Descricao);
    }

    [Fact]
    public async Task Decidir_RhRejeita_ExigeMotivo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ferias = c.NovasFerias(e.Carla, "2026-11-16", "2026-11-30");
        var rh = c.Solicitante(e.UsuarioRh);

        var semMotivo = await c.DecidirFerias().ExecutarAsync(rh, ferias.Id, new DecidirFeriasDto(false, null));
        Assert.Equal(TipoErro.Validacao, semMotivo.TipoDoErro);
        Assert.Equal(StatusSolicitacao.Pendente, ferias.Status);
        Assert.Empty(c.Banco.Notificacoes);

        var comMotivo = await c.DecidirFerias().ExecutarAsync(rh, ferias.Id, new DecidirFeriasDto(false, "Equipe reduzida no período"));
        Assert.True(comMotivo.Sucesso, comMotivo.Erro);
        Assert.Equal(StatusSolicitacao.Rejeitada, ferias.Status);
        Assert.Equal("Férias rejeitada", Assert.Single(c.Banco.Notificacoes).Titulo);
    }

    [Fact]
    public async Task Decidir_QuemNaoPode_Recusado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var deAna = c.NovasFerias(e.Ana, "2026-11-16", "2026-11-30");
        var deCarla = c.NovasFerias(e.Carla, "2026-11-16", "2026-11-30");
        var aprovar = new DecidirFeriasDto(true, null);

        var colega = await c.DecidirFerias().ExecutarAsync(c.Solicitante(e.UsuarioBruno), deAna.Id, aprovar);
        var gestorDeOutraEquipe = await c.DecidirFerias().ExecutarAsync(c.Solicitante(e.UsuarioGestor), deCarla.Id, aprovar);

        Assert.Equal(TipoErro.Proibido, colega.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, gestorDeOutraEquipe.TipoDoErro);
        Assert.Equal(StatusSolicitacao.Pendente, deAna.Status);
        Assert.Equal(StatusSolicitacao.Pendente, deCarla.Status);
        Assert.Empty(c.Banco.Logs);
    }

    [Fact]
    public async Task Decidir_AProprias_Ferias_Recusado_ParaTodos()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var doRh = c.NovasFerias(e.Rh, "2026-11-16", "2026-11-30");
        var doAdmin = c.NovasFerias(e.Admin, "2026-11-16", "2026-11-30");

        var rh = await c.DecidirFerias().ExecutarAsync(c.Solicitante(e.UsuarioRh), doRh.Id, new DecidirFeriasDto(true, null));
        var admin = await c.DecidirFerias().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), doAdmin.Id, new DecidirFeriasDto(true, null));

        Assert.Equal(TipoErro.Proibido, rh.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, admin.TipoDoErro);
        Assert.Contains("própria", rh.Erro);
    }

    [Fact]
    public async Task Decidir_FeriasDeQuemTemAcessoRh_ExigeAdminOuSerOGestorDireto()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var outroRh = c.NovoFuncionario(20, "Rui RH");
        c.NovoUsuario(outroRh, "rui@hrauto.local", perfil: PerfilAcesso.RH);
        var ferias = c.NovasFerias(outroRh, "2026-11-16", "2026-11-30");
        var aprovar = new DecidirFeriasDto(true, null);

        var porRh = await c.DecidirFerias().ExecutarAsync(c.Solicitante(e.UsuarioRh), ferias.Id, aprovar);
        Assert.Equal(TipoErro.Proibido, porRh.TipoDoErro);

        outroRh.IdGestor = e.Rh.Id; // agora a Rita é a gestora direta do Rui
        var comoGestora = await c.DecidirFerias().ExecutarAsync(c.Solicitante(e.UsuarioRh), ferias.Id, aprovar);
        Assert.True(comoGestora.Sucesso, comoGestora.Erro);

        var outra = c.NovasFerias(outroRh, "2027-02-01", "2027-02-15");
        outroRh.IdGestor = null;
        var porAdmin = await c.DecidirFerias().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), outra.Id, aprovar);
        Assert.True(porAdmin.Sucesso, porAdmin.Erro);
    }

    [Fact]
    public async Task Decidir_JaDecidida_Da409_InexistenteDa404_SemAprovarDa400()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var decidida = c.NovasFerias(e.Ana, "2026-11-16", "2026-11-30", StatusSolicitacao.Aprovada);
        var pendente = c.NovasFerias(e.Bruno, "2026-11-16", "2026-11-30");
        var gestor = c.Solicitante(e.UsuarioGestor);

        var jaDecidida = await c.DecidirFerias().ExecutarAsync(gestor, decidida.Id, new DecidirFeriasDto(false, "Motivo qualquer"));
        var inexistente = await c.DecidirFerias().ExecutarAsync(gestor, 99999, new DecidirFeriasDto(true, null));
        var semDecisao = await c.DecidirFerias().ExecutarAsync(gestor, pendente.Id, new DecidirFeriasDto(null, null));
        var semDto = await c.DecidirFerias().ExecutarAsync(gestor, pendente.Id, null);
        var obsLonga = await c.DecidirFerias().ExecutarAsync(gestor, pendente.Id, new DecidirFeriasDto(true, new string('x', 501)));

        Assert.Equal(TipoErro.Conflito, jaDecidida.TipoDoErro);
        Assert.Equal(StatusSolicitacao.Aprovada, decidida.Status);
        Assert.Equal(TipoErro.NaoEncontrado, inexistente.TipoDoErro);
        Assert.Equal(TipoErro.Validacao, semDecisao.TipoDoErro);
        Assert.Equal(TipoErro.Validacao, semDto.TipoDoErro);
        Assert.Equal(TipoErro.Validacao, obsLonga.TipoDoErro);
        Assert.Equal(StatusSolicitacao.Pendente, pendente.Status);
    }

    // ---------- Cancelar ----------

    [Fact]
    public async Task Cancelar_PendenteDaPropriaPessoa_RemoveEAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ferias = c.NovasFerias(e.Ana, "2026-11-16", "2026-11-30");

        var r = await c.CancelarSolicitacaoFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), ferias.Id);

        Assert.True(r.Sucesso, r.Erro);
        Assert.Empty(c.Banco.Ferias);
        var log = Assert.Single(c.Banco.Logs);
        Assert.Equal("CancelarSolicitacaoFerias", log.Acao);
        Assert.Equal(ferias.Id, log.IdRegistro);
    }

    [Fact]
    public async Task Cancelar_DeOutraPessoa_Recusado_JaDecidida_Da409_Inexistente_Da404()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var pendente = c.NovasFerias(e.Ana, "2026-11-16", "2026-11-30");
        var aprovada = c.NovasFerias(e.Bruno, "2026-12-07", "2026-12-21", StatusSolicitacao.Aprovada);

        var alheia = await c.CancelarSolicitacaoFerias().ExecutarAsync(c.Solicitante(e.UsuarioBruno), pendente.Id);
        var gestor = await c.CancelarSolicitacaoFerias().ExecutarAsync(c.Solicitante(e.UsuarioGestor), pendente.Id);
        var decidida = await c.CancelarSolicitacaoFerias().ExecutarAsync(c.Solicitante(e.UsuarioBruno), aprovada.Id);
        var inexistente = await c.CancelarSolicitacaoFerias().ExecutarAsync(c.Solicitante(e.UsuarioAna), 99999);

        Assert.Equal(TipoErro.Proibido, alheia.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, gestor.TipoDoErro);
        Assert.Equal(TipoErro.Conflito, decidida.TipoDoErro);
        Assert.Equal(TipoErro.NaoEncontrado, inexistente.TipoDoErro);
        Assert.Equal(2, c.Banco.Ferias.Count);
        Assert.Empty(c.Banco.Logs);
    }
}
