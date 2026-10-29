using HRAuto.Application.Common;
using HRAuto.Application.Disciplina;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

/// <summary>RF33 (registrar e cancelar), RF34 (régua) e RF47 (semáforo).</summary>
public class AdvertenciaAppTests
{
    private static RegistrarAdvertenciaDto Leve(string codigo = "FUNC-0004", string motivo = "Atrasos repetidos") =>
        new(codigo, "Leve", motivo, null, null);

    // ---------- Registrar ----------

    [Fact]
    public async Task Registrar_Leve_GravaNotificaOFuncionario_AtualizaSemaforoEAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioRh), Leve());

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("Leve", r.Valor!.Advertencia.Tipo);
        Assert.Equal("Ativa", r.Valor.Advertencia.Status);
        Assert.Equal("FUNC-0004", r.Valor.Advertencia.CodigoFuncionario);
        Assert.Equal("Ana Souza", r.Valor.Advertencia.NomeFuncionario);
        Assert.Equal("Rita RH", r.Valor.Advertencia.NomeResponsavel);
        Assert.Equal(new DateOnly(2026, 10, 21), r.Valor.Advertencia.Data);
        Assert.Equal("Amarelo", r.Valor.Semaforo.Nivel);
        Assert.Equal(1, r.Valor.Semaforo.QuantidadeAtivas);
        Assert.Equal("Nenhuma", r.Valor.Regua.Acao);

        var gravada = Assert.Single(c.Banco.Advertencias);
        Assert.Equal(e.Rh.Id, gravada.IdResponsavel);

        var aviso = Assert.Single(c.Banco.Notificacoes);
        Assert.Equal(e.Ana.Id, aviso.IdFuncionario);
        Assert.Equal("Advertência registrada", aviso.Titulo);

        var log = c.Banco.Logs.Single();
        Assert.Equal("RegistrarAdvertencia", log.Acao);
        Assert.Equal(gravada.Id, log.IdRegistro);
        Assert.Equal(1, c.Banco.TransacoesConfirmadas);
    }

    [Fact]
    public async Task Registrar_TerceiraLeve_SemaforoVermelho_ERegraSugereSuspensao_ParaORh()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-01");
        c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-15");

        var r = await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioRh), Leve());

        Assert.Equal("Vermelho", r.Valor!.Semaforo.Nivel);
        Assert.Equal(3, r.Valor.Semaforo.QuantidadeAtivas);
        Assert.Equal("Suspensao", r.Valor.Regua.Acao);
        Assert.Equal(3, r.Valor.Regua.LevesDesdeUltimaSuspensao);

        // Ana é avisada da advertência; a RH (que é a solicitante e também RH ativa) recebe UMA sugestão, sem duplicar.
        Assert.Equal(2, c.Banco.Notificacoes.Count);
        var sugestao = c.Banco.Notificacoes.Single(n => n.Titulo == "Sugestão de suspensão");
        Assert.Equal(e.Rh.Id, sugestao.IdFuncionario);
        Assert.Contains("FUNC-0004", sugestao.Mensagem);
    }

    [Fact]
    public async Task Registrar_SugestaoDaRegua_TambemVaiParaOsOutrosRhAtivos_ENuncaParaQuemRecebeAAdvertencia()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var outroRh = c.NovoFuncionario(20, "Rui RH");
        c.NovoUsuario(outroRh, "rui@hrauto.local", perfil: PerfilAcesso.RH);
        c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-01");
        c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-15");

        await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), Leve());

        var destinatarios = c.Banco.Notificacoes.Where(n => n.Titulo == "Sugestão de suspensão").Select(n => n.IdFuncionario).OrderBy(x => x).ToList();
        Assert.Equal(new[] { e.Admin.Id, e.Rh.Id, outroRh.Id }.OrderBy(x => x).ToList(), destinatarios);
        Assert.DoesNotContain(c.Banco.Notificacoes, n => n.IdFuncionario == e.Ana.Id && n.Titulo.StartsWith("Sugestão"));
    }

    [Fact]
    public async Task Registrar_SuspensaoConsomeAsLeves_ENaoSugereNada()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        for (var i = 1; i <= 3; i++) c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, $"2026-09-0{i}");

        var r = await c.RegistrarAdvertencia().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), new RegistrarAdvertenciaDto("FUNC-0004", "Suspensao", "Reincidência", "3 dias", null));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("Nenhuma", r.Valor!.Regua.Acao);
        Assert.Equal(0, r.Valor.Regua.LevesDesdeUltimaSuspensao);
        Assert.Equal(1, r.Valor.Regua.SuspensoesAtivas);
        Assert.Equal("Vermelho", r.Valor.Semaforo.Nivel); // 4 ativas
        Assert.DoesNotContain(c.Banco.Notificacoes, n => n.Titulo.StartsWith("Sugestão"));
    }

    [Fact]
    public async Task Registrar_SegundaSuspensao_SugereJustaCausa()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Suspensao, "2026-08-01");

        var r = await c.RegistrarAdvertencia().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), new RegistrarAdvertenciaDto("FUNC-0004", "suspensao", "Nova falta grave", null, null));

        Assert.Equal("JustaCausa", r.Valor!.Regua.Acao);
        Assert.Contains(c.Banco.Notificacoes, n => n.Titulo == "Sugestão de justa causa" && n.IdFuncionario == e.Rh.Id);
    }

    [Fact]
    public async Task Registrar_JustaCausa_NaoGeraSugestaoParaORh()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.RegistrarAdvertencia().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), new RegistrarAdvertenciaDto("FUNC-0004", "JustaCausa", "Falta gravíssima", null, null));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("Nenhuma", r.Valor!.Regua.Acao);
        var aviso = Assert.Single(c.Banco.Notificacoes);
        Assert.Equal(e.Ana.Id, aviso.IdFuncionario);
    }

    [Fact]
    public async Task Registrar_AdvertenciaCanceladaNaoContaNoSemaforo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-01", ativa: false);
        c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-15", ativa: false);

        var r = await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioRh), Leve());

        Assert.Equal(1, r.Valor!.Semaforo.QuantidadeAtivas);
        Assert.Equal("Amarelo", r.Valor.Semaforo.Nivel);
    }

    [Fact]
    public async Task Registrar_ComDataPassada_UsaAData()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.RegistrarAdvertencia().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), new RegistrarAdvertenciaDto("FUNC-0004", "Leve", "Atraso", null, new DateOnly(2026, 10, 10)));

        Assert.Equal(new DateOnly(2026, 10, 10), r.Valor!.Advertencia.Data);
    }

    [Theory]
    [InlineData("FUNC-0004", "Grave", "Motivo ok", "inválido")]
    [InlineData("FUNC-0004", "1", "Motivo ok", "inválido")]
    [InlineData("FUNC-0004", null, "Motivo ok", "inválido")]
    [InlineData("FUNC-0004", "Leve", "", "motivo")]
    [InlineData("FUNC-0004", "Leve", "  ", "motivo")]
    [InlineData("4", "Leve", "Motivo ok", "inválido")]
    public async Task Registrar_DadosInvalidos_NaoGravamNada(string codigo, string? tipo, string motivo, string trecho)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.RegistrarAdvertencia().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), new RegistrarAdvertenciaDto(codigo, tipo, motivo, null, null));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains(trecho, r.Erro!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(c.Banco.Advertencias);
        Assert.Empty(c.Banco.Notificacoes);
        Assert.Empty(c.Banco.Logs);
    }

    [Fact]
    public async Task Registrar_DataFutura_EhRecusada()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.RegistrarAdvertencia().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), new RegistrarAdvertenciaDto("FUNC-0004", "Leve", "Atraso", null, new DateOnly(2026, 10, 30)));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Empty(c.Banco.Advertencias);
    }

    [Fact]
    public async Task Registrar_ColaboradorEGestor_SaoProibidos()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var gestor = await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioGestor), Leve());
        var colaborador = await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioCarla), Leve());

        Assert.Equal(TipoErro.Proibido, gestor.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, colaborador.TipoDoErro);
        Assert.Empty(c.Banco.Advertencias);
    }

    [Theory]
    [InlineData("FUNC-0999", TipoErro.NaoEncontrado)]
    public async Task Registrar_FuncionarioInexistente(string codigo, TipoErro esperado)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioRh), Leve(codigo));

        Assert.Equal(esperado, r.TipoDoErro);
    }

    [Fact]
    public async Task Registrar_FuncionarioDesligado_EhConflito()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        e.Ana.Ativo = false;

        var r = await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioRh), Leve());

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
    }

    [Fact]
    public async Task Registrar_RhNaoAdvertePessoaRhOuAdmin_MasAdminAdverteRh()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var rhContraAdmin = await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioRh), Leve("FUNC-0001"));
        var rhContraSiMesma = await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioRh), Leve("FUNC-0002"));
        var adminContraRh = await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), Leve("FUNC-0002"));

        Assert.Equal(TipoErro.Proibido, rhContraAdmin.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, rhContraSiMesma.TipoDoErro);
        Assert.True(adminContraRh.Sucesso, adminContraRh.Erro);
    }

    [Fact]
    public async Task Registrar_AdminNaoAdvertiASiMesmo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), Leve("FUNC-0001"));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("si mesmo", r.Erro!);
    }

    [Fact]
    public async Task Registrar_AdvertenciaComTipoDesconhecidoNoBanco_FalhaSemGravar()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var corrompida = c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-01");
        corrompida.Tipo = "Verbal";

        var r = await c.RegistrarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioRh), Leve());

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Single(c.Banco.Advertencias);
    }

    // ---------- Cancelar ----------

    [Fact]
    public async Task Cancelar_RhCancela_SaiDoSemaforo_FicaNoHistorico_AvisaOFuncionario()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var advertencia = c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-01");

        var r = await c.CancelarAdvertencia().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), advertencia.Id, new CancelarAdvertenciaDto("Registrada por engano"));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("Cancelada", r.Valor!.Status);
        Assert.Contains("Registrada por engano", r.Valor.Descricao);
        Assert.Contains(c.Banco.Advertencias, a => a.Id == advertencia.Id);

        var aviso = Assert.Single(c.Banco.Notificacoes);
        Assert.Equal("Advertência cancelada", aviso.Titulo);
        Assert.Equal(e.Ana.Id, aviso.IdFuncionario);
        Assert.Equal("CancelarAdvertencia", c.Banco.Logs.Single().Acao);

        var situacao = await c.SituacaoDisciplinar().ExecutarAsync(c.Solicitante(e.UsuarioAna), null);
        Assert.Equal("Verde", situacao.Valor!.Semaforo.Nivel);
        Assert.Single(situacao.Valor.Advertencias);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ok")]
    public async Task Cancelar_SemMotivoSuficiente_EhRecusado(string? motivo)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var advertencia = c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-01");

        var r = await c.CancelarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioRh), advertencia.Id, new CancelarAdvertenciaDto(motivo));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Equal("Ativa", advertencia.Status);
        Assert.Empty(c.Banco.Notificacoes);
    }

    [Fact]
    public async Task Cancelar_JaCancelada_EhRecusado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var advertencia = c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-01", ativa: false);

        var r = await c.CancelarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioRh), advertencia.Id, new CancelarAdvertenciaDto("Motivo válido"));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Fact]
    public async Task Cancelar_Inexistente_EhNaoEncontrado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.CancelarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioRh), 99, new CancelarAdvertenciaDto("Motivo válido"));

        Assert.Equal(TipoErro.NaoEncontrado, r.TipoDoErro);
    }

    [Fact]
    public async Task Cancelar_ColaboradorEhProibido_ERhNaoCancelaAdvertenciaDeAdmin()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var deAna = c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-01");
        var doAdmin = c.NovaAdvertencia(e.Admin, e.Admin, TipoAdvertencia.Leve, "2026-09-02");
        var dto = new CancelarAdvertenciaDto("Motivo válido");

        var colaborador = await c.CancelarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioAna), deAna.Id, dto);
        var rhContraAdmin = await c.CancelarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioRh), doAdmin.Id, dto);
        var adminContraAdmin = await c.CancelarAdvertencia().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), doAdmin.Id, dto);

        Assert.Equal(TipoErro.Proibido, colaborador.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, rhContraAdmin.TipoDoErro);
        Assert.True(adminContraAdmin.Sucesso, adminContraAdmin.Erro);
    }

    // ---------- Lista geral ----------

    [Fact]
    public async Task Listar_RhFiltraPorFuncionarioEStatus_ComPaginacao()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-01");
        c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-09-10", ativa: false);
        c.NovaAdvertencia(e.Bruno, e.Rh, TipoAdvertencia.Suspensao, "2026-09-20");
        var caso = c.ConsultarAdvertencias();
        var rh = c.Solicitante(e.UsuarioRh);

        var todas = await caso.ExecutarAsync(rh, null);
        var daAna = await caso.ExecutarAsync(rh, new FiltroListaAdvertenciasDto("FUNC-0004", null, null, null));
        var ativasDaAna = await caso.ExecutarAsync(rh, new FiltroListaAdvertenciasDto("func-0004", "ativa", null, null));
        var pagina = await caso.ExecutarAsync(rh, new FiltroListaAdvertenciasDto(null, null, 2, 2));

        Assert.Equal(3, todas.Valor!.Total);
        Assert.Equal("Bruno Lima", todas.Valor.Itens[0].NomeFuncionario); // mais recente primeiro
        Assert.Equal(2, daAna.Valor!.Total);
        Assert.Equal("Ativa", Assert.Single(ativasDaAna.Valor!.Itens).Status);
        Assert.Single(pagina.Valor!.Itens);
        Assert.Equal(2, pagina.Valor.TotalPaginas);
    }

    [Theory]
    [InlineData("FUNC-4", null)]
    [InlineData(null, "pendente")]
    public async Task Listar_FiltroInvalido_EhRecusado(string? codigo, string? status)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ConsultarAdvertencias().ExecutarAsync(c.Solicitante(e.UsuarioRh), new FiltroListaAdvertenciasDto(codigo, status, null, null));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Fact]
    public async Task Listar_ColaboradorEhProibido()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ConsultarAdvertencias().ExecutarAsync(c.Solicitante(e.UsuarioGestor), null);

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }

    // ---------- Situação disciplinar (semáforo) ----------

    [Theory]
    [InlineData(0, "Verde")]
    [InlineData(1, "Amarelo")]
    [InlineData(2, "Amarelo")]
    [InlineData(3, "Vermelho")]
    [InlineData(5, "Vermelho")]
    public async Task Situacao_SemaforoPelaQuantidadeDeAtivas(int ativas, string cor)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        for (var i = 0; i < ativas; i++) c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, $"2026-09-{i + 1:D2}");
        c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, "2026-08-01", ativa: false); // cancelada não conta

        var r = await c.SituacaoDisciplinar().ExecutarAsync(c.Solicitante(e.UsuarioAna), null);

        Assert.Equal(cor, r.Valor!.Semaforo.Nivel);
        Assert.Equal(ativas, r.Valor.Semaforo.QuantidadeAtivas);
        Assert.Equal(ativas + 1, r.Valor.Advertencias.Count);
    }

    [Fact]
    public async Task Situacao_OProprioFuncionarioNaoVeASugestaoDaRegua()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        for (var i = 1; i <= 3; i++) c.NovaAdvertencia(e.Ana, e.Rh, TipoAdvertencia.Leve, $"2026-09-0{i}");

        var propria = await c.SituacaoDisciplinar().ExecutarAsync(c.Solicitante(e.UsuarioAna), "FUNC-0004");
        var gestor = await c.SituacaoDisciplinar().ExecutarAsync(c.Solicitante(e.UsuarioGestor), "FUNC-0004");
        var rh = await c.SituacaoDisciplinar().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0004");

        Assert.Null(propria.Valor!.Regua);
        Assert.Null(gestor.Valor!.Regua);
        Assert.Equal("Suspensao", rh.Valor!.Regua!.Acao);
        Assert.All(new[] { propria, gestor, rh }, r => Assert.Equal("Vermelho", r.Valor!.Semaforo.Nivel));
    }

    [Fact]
    public async Task Situacao_GestorSoVeAEquipe_ColaboradorSoVeASiMesmo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var gestorVendoCarla = await c.SituacaoDisciplinar().ExecutarAsync(c.Solicitante(e.UsuarioGestor), "FUNC-0006");
        var carlaVendoAna = await c.SituacaoDisciplinar().ExecutarAsync(c.Solicitante(e.UsuarioCarla), "FUNC-0004");
        var anaVendoBruno = await c.SituacaoDisciplinar().ExecutarAsync(c.Solicitante(e.UsuarioAna), "FUNC-0005");
        var carlaVendoCarla = await c.SituacaoDisciplinar().ExecutarAsync(c.Solicitante(e.UsuarioCarla), "FUNC-0006");

        Assert.Equal(TipoErro.Proibido, gestorVendoCarla.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, carlaVendoAna.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, anaVendoBruno.TipoDoErro);
        Assert.True(carlaVendoCarla.Sucesso, carlaVendoCarla.Erro);
    }

    [Theory]
    [InlineData("FUNC-0999", TipoErro.NaoEncontrado)]
    [InlineData("xyz", TipoErro.Validacao)]
    public async Task Situacao_RhComCodigoInexistenteOuInvalido(string codigo, TipoErro esperado)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.SituacaoDisciplinar().ExecutarAsync(c.Solicitante(e.UsuarioRh), codigo);

        Assert.Equal(esperado, r.TipoDoErro);
    }

    [Fact]
    public async Task Situacao_FuncionarioSemAdvertencias_EVerde_ComListaVazia()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.SituacaoDisciplinar().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0006");

        Assert.Equal("Verde", r.Valor!.Semaforo.Nivel);
        Assert.Empty(r.Valor.Advertencias);
        Assert.Equal("Nenhuma", r.Valor.Regua!.Acao);
        Assert.Equal("Carla Dias", r.Valor.Nome);
    }
}
