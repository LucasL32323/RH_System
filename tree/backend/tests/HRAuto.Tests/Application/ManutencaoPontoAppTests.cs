using HRAuto.Application.Common;
using HRAuto.Application.ManutencaoDePonto;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;

namespace HRAuto.Tests.Application;

/// <summary>
/// RF22 (solicitar), RF23 (decidir) e listagem de pedidos de manutenção de ponto.
/// Relógio: quarta-feira, 21/10/2026, 08:00. Segunda 19/10 é o dia do ponto usado nos testes.
/// Jornada padrão: 08:00, 8 horas, tolerância de 10 minutos.
/// </summary>
public class ManutencaoPontoAppTests
{
    private const string Justificativa = "Esqueci de bater a saída; saí às 18:00";

    private static SolicitarManutencaoPontoDto Pedido(string data = "2026-10-19", string? justificativa = Justificativa) =>
        new(ApoioApp.Data(data), justificativa);

    private static DecidirManutencaoPontoDto Aprovar(BatidasCorrigidasDto? batidas = null, string? observacao = null) =>
        new(true, observacao, batidas);

    private static BatidasCorrigidasDto Saida(string hora) => new(null, null, null, ApoioApp.Hora(hora));

    // ---------- Solicitar ----------

    [Fact]
    public async Task Solicitar_DiaComPonto_NascePendente_AvisaOGestor_EAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ponto = c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", null);

        var r = await c.SolicitarManutencao().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido(justificativa: "  " + Justificativa + "  "));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("FUNC-0004", r.Valor!.CodigoFuncionario);
        Assert.Equal("Ana Souza", r.Valor.NomeFuncionario);
        Assert.Equal(new DateOnly(2026, 10, 19), r.Valor.DataPonto);
        Assert.Equal(3, r.Valor.Batidas.Count);
        Assert.Equal(Justificativa, r.Valor.Justificativa);
        Assert.Equal(StatusSolicitacao.Pendente, r.Valor.Status);
        Assert.Null(r.Valor.CodigoResponsavel);
        Assert.True(r.Valor.Id > 0);

        var gravado = Assert.Single(c.Banco.ManutencoesPonto);
        Assert.Equal(ponto.Id, gravado.IdPonto);
        Assert.Equal(new DateTime(2026, 10, 21, 8, 0, 0), gravado.DataSolicitacao);

        var aviso = Assert.Single(c.Banco.Notificacoes);
        Assert.Equal(e.Gestor.Id, aviso.IdFuncionario);
        Assert.Equal(TiposNotificacao.ManutencaoPonto, aviso.Tipo);

        var log = Assert.Single(c.Banco.Logs);
        Assert.Equal("SolicitarManutencaoPonto", log.Acao);
        Assert.Equal("MANUTENCAO_PONTO", log.Tabela);
        Assert.Equal(gravado.Id, log.IdRegistro);
        Assert.Equal(1, c.Banco.TransacoesConfirmadas);
    }

    [Fact]
    public async Task Solicitar_DiaSemNenhumaBatida_CriaOPontoVazio_ENaMesmaTransacao()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.SolicitarManutencao().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido());

        Assert.True(r.Sucesso, r.Erro);
        var ponto = Assert.Single(c.Banco.Pontos);
        Assert.True(ponto.Id > 0);
        Assert.Equal(0, ponto.TotalBatidas());
        Assert.Equal(ponto.Id, Assert.Single(c.Banco.ManutencoesPonto).IdPonto);
        Assert.Empty(r.Valor!.Batidas);
        Assert.Equal(1, c.Banco.TransacoesConfirmadas);
    }

    [Fact]
    public async Task Solicitar_SemGestorAtivo_AvisaOsRh_ENuncaOProprioSolicitante()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        e.Gestor.Ativo = false;
        c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", null);
        c.NovoPontoGravado(e.Carla, "2026-10-19", "08:00", "12:00", "13:00", null);

        Assert.True((await c.SolicitarManutencao().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido())).Sucesso);
        Assert.True((await c.SolicitarManutencao().ExecutarAsync(c.Solicitante(e.UsuarioCarla), Pedido())).Sucesso);

        Assert.Equal(2, c.Banco.Notificacoes.Count);
        Assert.All(c.Banco.Notificacoes, n => Assert.Equal(e.Rh.Id, n.IdFuncionario));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("curta")]
    public async Task Solicitar_SemJustificativaSuficiente_Da400_ENaoCriaNada(string? justificativa)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.SolicitarManutencao().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido(justificativa: justificativa));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("justificativa", r.Erro);
        Assert.Empty(c.Banco.ManutencoesPonto);
        Assert.Empty(c.Banco.Pontos); // nem o ponto vazio é criado
        Assert.Empty(c.Banco.Notificacoes);
    }

    [Theory]
    [InlineData("2026-10-22", "2026-10-21T08:00:00", "ainda não aconteceu")] // amanhã
    [InlineData("2026-08-01", "2026-10-21T08:00:00", "últimos 60 dias")]
    [InlineData("2024-03-08", "2024-04-20T08:00:00", "anterior à admissão")] // Ana foi admitida em 11/03/2024
    public async Task Solicitar_DiaInvalido_Da400_ENaoCriaNada(string data, string agora, string trecho)
    {
        var c = new Cenario(agora);
        var e = c.NovaEmpresa();

        var r = await c.SolicitarManutencao().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido(data));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains(trecho, r.Erro);
        Assert.Empty(c.Banco.Pontos);
        Assert.Empty(c.Banco.ManutencoesPonto);
    }

    [Fact]
    public async Task Solicitar_SemData_Da400()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.SolicitarManutencao().ExecutarAsync(c.Solicitante(e.UsuarioAna), new SolicitarManutencaoPontoDto(null, Justificativa));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Fact]
    public async Task Solicitar_JaExistePendenteNoMesmoDia_Da400()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ponto = c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", null);
        c.NovaManutencao(e.Ana, ponto);

        var r = await c.SolicitarManutencao().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido());

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("pendente", r.Erro);
        Assert.Single(c.Banco.ManutencoesPonto);
    }

    [Fact]
    public async Task Solicitar_NovoPedidoDepoisDeDecidido_EhPermitido()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ponto = c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", null);
        c.NovaManutencao(e.Ana, ponto, StatusSolicitacao.Rejeitada);

        var r = await c.SolicitarManutencao().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido());

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(2, c.Banco.ManutencoesPonto.Count);
    }

    [Fact]
    public async Task Solicitar_FuncionarioInativo_Da403()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        e.Ana.Ativo = false;

        var r = await c.SolicitarManutencao().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido());

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }

    [Fact]
    public async Task Solicitar_ConflitoAoGravar_Da409_EDescartaATransacao()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.Banco.ConflitoNoProximoSalvar = true; // simula dois cliques criando o ponto do dia ao mesmo tempo

        var r = await c.SolicitarManutencao().ExecutarAsync(c.Solicitante(e.UsuarioAna), Pedido());

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.Equal(0, c.Banco.TransacoesConfirmadas);
        Assert.Equal(1, c.Banco.TransacoesDescartadas);
    }

    // ---------- Listar ----------

    [Fact]
    public async Task Listar_PorPadraoMostraSoOsMeusPedidos_DoMaisRecenteAoMaisAntigo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var segunda = c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", null);
        var sexta = c.NovoPontoGravado(e.Ana, "2026-10-16", "08:00", "12:00", "13:00", null);
        var doBruno = c.NovoPontoGravado(e.Bruno, "2026-10-19", "08:00", "12:00", "13:00", null);
        c.NovaManutencao(e.Ana, sexta, solicitadaEm: "2026-10-19T09:00:00");
        c.NovaManutencao(e.Ana, segunda, solicitadaEm: "2026-10-20T09:00:00");
        c.NovaManutencao(e.Bruno, doBruno);

        var r = await c.ListarManutencoes().ExecutarAsync(c.Solicitante(e.UsuarioAna), null);

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(2, r.Valor!.Total);
        Assert.Equal(new DateOnly(2026, 10, 19), r.Valor.Itens[0].DataPonto);
        Assert.Equal(new DateOnly(2026, 10, 16), r.Valor.Itens[1].DataPonto);
        Assert.All(r.Valor.Itens, i => Assert.Equal("FUNC-0004", i.CodigoFuncionario));
    }

    [Fact]
    public async Task Listar_Equipe_SoParaGestor_ESoDosSubordinadosDiretos()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovaManutencao(e.Ana, c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", null, null, null));
        c.NovaManutencao(e.Bruno, c.NovoPontoGravado(e.Bruno, "2026-10-19", "08:00", null, null, null));
        c.NovaManutencao(e.Carla, c.NovoPontoGravado(e.Carla, "2026-10-19", "08:00", null, null, null));

        var doGestor = await c.ListarManutencoes().ExecutarAsync(c.Solicitante(e.UsuarioGestor), new FiltroListaManutencoesDto("equipe", null, null, null, null));
        var deQuemNaoEGestor = await c.ListarManutencoes().ExecutarAsync(c.Solicitante(e.UsuarioAna), new FiltroListaManutencoesDto("equipe", null, null, null, null));

        Assert.True(doGestor.Sucesso, doGestor.Erro);
        Assert.Equal(2, doGestor.Valor!.Total);
        Assert.DoesNotContain(doGestor.Valor.Itens, i => i.CodigoFuncionario == "FUNC-0006");
        Assert.Equal(TipoErro.Proibido, deQuemNaoEGestor.TipoDoErro);
    }

    [Fact]
    public async Task Listar_Todas_SoParaRh_ComFiltroDeCodigoEStatus()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovaManutencao(e.Ana, c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", null, null, null), StatusSolicitacao.Aprovada);
        c.NovaManutencao(e.Ana, c.NovoPontoGravado(e.Ana, "2026-10-16", "08:00", null, null, null));
        c.NovaManutencao(e.Bruno, c.NovoPontoGravado(e.Bruno, "2026-10-19", "08:00", null, null, null));

        var todas = await c.ListarManutencoes().ExecutarAsync(c.Solicitante(e.UsuarioRh), new FiltroListaManutencoesDto("todas", null, null, null, null));
        var daAnaPendentes = await c.ListarManutencoes().ExecutarAsync(c.Solicitante(e.UsuarioRh), new FiltroListaManutencoesDto("todas", "FUNC-0004", "pendente", null, null));
        var negado = await c.ListarManutencoes().ExecutarAsync(c.Solicitante(e.UsuarioGestor), new FiltroListaManutencoesDto("todas", null, null, null, null));

        Assert.Equal(3, todas.Valor!.Total);
        Assert.Equal(1, daAnaPendentes.Valor!.Total);
        Assert.Equal(StatusSolicitacao.Pendente, daAnaPendentes.Valor.Itens[0].Status);
        Assert.Equal(TipoErro.Proibido, negado.TipoDoErro);
    }

    [Theory]
    [InlineData("outros", null, null)]
    [InlineData("minhas", "FUNC-ABC", null)]
    [InlineData("minhas", null, "cancelada")]
    public async Task Listar_ParametrosInvalidos_Da400(string escopo, string? codigo, string? status)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ListarManutencoes().ExecutarAsync(c.Solicitante(e.UsuarioAna), new FiltroListaManutencoesDto(escopo, codigo, status, null, null));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Fact]
    public async Task Listar_MinhasComCodigoDeOutraPessoa_Da403()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ListarManutencoes().ExecutarAsync(c.Solicitante(e.UsuarioAna), new FiltroListaManutencoesDto("minhas", "FUNC-0005", null, null, null));

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }

    // ---------- Decidir: aprovação com correção ----------

    [Fact]
    public async Task Decidir_AprovarComSaidaCorrigida_GravaABatida_ReajustaOBancoDeHoras_AvisaEAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ponto = c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", null);
        var pedido = c.NovaManutencao(e.Ana, ponto);

        var r = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioGestor), pedido.Id, Aprovar(Saida("18:00"), "Confirmado com a equipe"));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(StatusSolicitacao.Aprovada, r.Valor!.Manutencao.Status);
        Assert.Equal("FUNC-0003", r.Valor.Manutencao.CodigoResponsavel);
        Assert.Equal(new DateTime(2026, 10, 21, 8, 0, 0), r.Valor.Manutencao.DataAnalise);
        Assert.Equal("Confirmado com a equipe", r.Valor.Manutencao.Observacao);
        Assert.Equal(4, r.Valor.Manutencao.Batidas.Count);

        Assert.True(r.Valor.BatidasAlteradas);
        var alteracao = Assert.Single(r.Valor.Alteracoes);
        Assert.Equal(TipoBatida.Saida, alteracao.Tipo);
        Assert.Null(alteracao.Antes);
        Assert.Equal(ApoioApp.Hora("18:00"), alteracao.Depois);
        Assert.Equal(ApoioApp.Hora("18:00"), ponto.Saida);

        // 9 horas trabalhadas: 1 hora extra vai para o banco de horas, sem alerta ao gestor (abaixo de 2 h).
        Assert.NotNull(r.Valor.FechamentoDoDia);
        Assert.Equal(60, r.Valor.FechamentoDoDia!.ExtrasMinutos);
        var lancamento = Assert.Single(c.Banco.BancoHoras);
        Assert.Equal(TiposBancoHoras.Credito, lancamento.Tipo);
        Assert.Equal(1.0, lancamento.Quantidade);
        Assert.Equal(OrigensBancoHoras.Ponto, lancamento.Origem);
        Assert.Equal(new DateOnly(2026, 10, 19), lancamento.Data);

        var aviso = Assert.Single(c.Banco.Notificacoes);
        Assert.Equal(e.Ana.Id, aviso.IdFuncionario);
        Assert.Equal("Manutenção de ponto aprovada", aviso.Titulo);

        Assert.Equal(new[] { "DecidirManutencaoPonto", "AjustarBatida" }, c.Banco.Logs.Select(l => l.Acao).ToArray());
        var logAjuste = c.Banco.Logs[1];
        Assert.Equal("PONTO", logAjuste.Tabela);
        Assert.Equal(ponto.Id, logAjuste.IdRegistro);
        Assert.Contains("(vazia)", logAjuste.Descricao);
        Assert.Contains("18:00:00", logAjuste.Descricao);
    }

    [Fact]
    public async Task Decidir_CorrigirDiaJaFechado_LancaSoADiferenca_NosDoisSentidos()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ponto = c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", "17:00"); // saldo zero, nada lançado

        var primeiro = c.NovaManutencao(e.Ana, ponto);
        var r1 = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioGestor), primeiro.Id, Aprovar(Saida("18:30")));
        var segundo = c.NovaManutencao(e.Ana, ponto);
        var r2 = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioGestor), segundo.Id, Aprovar(Saida("17:30")));

        Assert.True(r1.Sucesso, r1.Erro);
        Assert.True(r2.Sucesso, r2.Erro);
        Assert.Equal(2, c.Banco.BancoHoras.Count);
        Assert.Equal(TiposBancoHoras.Credito, c.Banco.BancoHoras[0].Tipo);
        Assert.Equal(1.5, c.Banco.BancoHoras[0].Quantidade);
        Assert.Equal(TiposBancoHoras.Debito, c.Banco.BancoHoras[1].Tipo);
        Assert.Equal(1.0, c.Banco.BancoHoras[1].Quantidade);
        Assert.Equal(0.5m, RegrasBancoHoras.Saldo(c.Banco.BancoHoras)); // 17:30 => 8,5 h trabalhadas => +0,5 h de saldo no dia
    }

    [Fact]
    public async Task Decidir_CorrecaoQueNaoMudaNada_NaoGeraLancamentoNemAjuste()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ponto = c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", "17:00");
        var pedido = c.NovaManutencao(e.Ana, ponto);

        var r = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioGestor), pedido.Id, Aprovar(Saida("17:00")));

        Assert.True(r.Sucesso, r.Erro);
        Assert.False(r.Valor!.BatidasAlteradas);
        Assert.Null(r.Valor.FechamentoDoDia);
        Assert.Empty(c.Banco.BancoHoras);
        Assert.Equal(new[] { "DecidirManutencaoPonto" }, c.Banco.Logs.Select(l => l.Acao).ToArray());
    }

    [Fact]
    public async Task Decidir_CorrecaoQueNaoCompletaOsQuatroHorarios_GravaMasNaoMexeNoBanco()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ponto = c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", null, null, null);
        var pedido = c.NovaManutencao(e.Ana, ponto);

        var r = await c.DecidirManutencao().ExecutarAsync(
            c.Solicitante(e.UsuarioGestor), pedido.Id, Aprovar(new BatidasCorrigidasDto(null, ApoioApp.Hora("12:00"), null, null)));

        Assert.True(r.Sucesso, r.Erro);
        Assert.True(r.Valor!.BatidasAlteradas);
        Assert.Null(r.Valor.FechamentoDoDia);
        Assert.Empty(c.Banco.BancoHoras);
        Assert.Equal(ApoioApp.Hora("12:00"), ponto.SaidaIntervalo);
    }

    [Fact]
    public async Task Decidir_AprovarSemBatidas_EhAbono_SoRegistraADecisao()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ponto = c.NovoPontoGravado(e.Ana, "2026-10-19", null, null, null, null);
        var pedido = c.NovaManutencao(e.Ana, ponto);

        var r = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioGestor), pedido.Id, Aprovar());

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(StatusSolicitacao.Aprovada, r.Valor!.Manutencao.Status);
        Assert.False(r.Valor.BatidasAlteradas);
        Assert.Empty(r.Valor.Alteracoes);
        Assert.Equal(0, ponto.TotalBatidas());
        Assert.Empty(c.Banco.BancoHoras);
    }

    [Fact]
    public async Task Decidir_CorrecaoInvalida_Da400_ENadaMuda()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ponto = c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", null, null, null);
        var pedido = c.NovaManutencao(e.Ana, ponto);

        var r = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioGestor), pedido.Id, Aprovar(Saida("18:00"))); // pula 2 batidas

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Equal(StatusSolicitacao.Pendente, pedido.Status);
        Assert.Null(ponto.Saida);
        Assert.Empty(c.Banco.Notificacoes);
        Assert.Empty(c.Banco.Logs);
        Assert.Equal(0, c.Banco.Salvamentos);
    }

    // ---------- Decidir: rejeição ----------

    [Fact]
    public async Task Decidir_Rejeitar_ExigeMotivo_ENaoMexeNoPonto()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ponto = c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", null);
        var pedido = c.NovaManutencao(e.Ana, ponto);

        var semMotivo = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioGestor), pedido.Id, new DecidirManutencaoPontoDto(false, null, null));
        Assert.Equal(TipoErro.Validacao, semMotivo.TipoDoErro);
        Assert.Equal(StatusSolicitacao.Pendente, pedido.Status);

        var comMotivo = await c.DecidirManutencao().ExecutarAsync(
            c.Solicitante(e.UsuarioGestor), pedido.Id, new DecidirManutencaoPontoDto(false, "Sem comprovante da saída", null));
        Assert.True(comMotivo.Sucesso, comMotivo.Erro);
        Assert.Equal(StatusSolicitacao.Rejeitada, comMotivo.Valor!.Manutencao.Status);
        Assert.Null(ponto.Saida);
        Assert.Empty(c.Banco.BancoHoras);
        Assert.Equal("Manutenção de ponto rejeitada", Assert.Single(c.Banco.Notificacoes).Titulo);
    }

    [Fact]
    public async Task Decidir_RejeitarComBatidas_Da400()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ponto = c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", null);
        var pedido = c.NovaManutencao(e.Ana, ponto);

        var r = await c.DecidirManutencao().ExecutarAsync(
            c.Solicitante(e.UsuarioGestor), pedido.Id, new DecidirManutencaoPontoDto(false, "Não procede", Saida("18:00")));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Null(ponto.Saida);
        Assert.Equal(StatusSolicitacao.Pendente, pedido.Status);
    }

    // ---------- Decidir: permissões e estados ----------

    [Fact]
    public async Task Decidir_QuemNaoEGestorDoFuncionario_Da403()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var pedido = c.NovaManutencao(e.Ana, c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", null));

        var colega = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioBruno), pedido.Id, Aprovar());
        var outroGestor = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioCarla), pedido.Id, Aprovar());

        Assert.Equal(TipoErro.Proibido, colega.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, outroGestor.TipoDoErro);
        Assert.Equal(StatusSolicitacao.Pendente, pedido.Status);
    }

    [Fact]
    public async Task Decidir_OProprioPedido_Da403_MesmoSendoRh()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var doRh = c.NovaManutencao(e.Rh, c.NovoPontoGravado(e.Rh, "2026-10-19", "08:00", "12:00", "13:00", null));

        var r = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioRh), doRh.Id, Aprovar());

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
        Assert.Contains("seu próprio pedido", r.Erro);
    }

    [Fact]
    public async Task Decidir_RhDecidePedidoDeColaborador_MasNaoDeAdmin()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var deCarla = c.NovaManutencao(e.Carla, c.NovoPontoGravado(e.Carla, "2026-10-19", "08:00", "12:00", "13:00", null));
        var doAdmin = c.NovaManutencao(e.Admin, c.NovoPontoGravado(e.Admin, "2026-10-19", "08:00", "12:00", "13:00", null));

        var carla = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioRh), deCarla.Id, Aprovar());
        var admin = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioRh), doAdmin.Id, Aprovar());
        var adminDecideAdmin = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), doAdmin.Id, Aprovar());

        Assert.True(carla.Sucesso, carla.Erro);
        Assert.Equal(TipoErro.Proibido, admin.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, adminDecideAdmin.TipoDoErro); // ninguém decide o próprio pedido
    }

    [Fact]
    public async Task Decidir_PedidoInexistente_Da404_EJaDecidido_Da409()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var decidido = c.NovaManutencao(e.Ana, c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", null), StatusSolicitacao.Aprovada);

        var inexistente = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioGestor), 999, Aprovar());
        var repetido = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioGestor), decidido.Id, Aprovar());

        Assert.Equal(TipoErro.NaoEncontrado, inexistente.TipoDoErro);
        Assert.Equal(TipoErro.Conflito, repetido.TipoDoErro);
    }

    [Fact]
    public async Task Decidir_SemAprovarOuRejeitar_Da400()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var pedido = c.NovaManutencao(e.Ana, c.NovoPontoGravado(e.Ana, "2026-10-19", "08:00", "12:00", "13:00", null));

        var nulo = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioGestor), pedido.Id, null);
        var semCampo = await c.DecidirManutencao().ExecutarAsync(c.Solicitante(e.UsuarioGestor), pedido.Id, new DecidirManutencaoPontoDto(null, null, null));

        Assert.Equal(TipoErro.Validacao, nulo.TipoDoErro);
        Assert.Equal(TipoErro.Validacao, semCampo.TipoDoErro);
    }
}
