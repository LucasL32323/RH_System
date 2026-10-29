using HRAuto.Domain.Common;
using HRAuto.Domain.Comunicacao;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;
using static HRAuto.Tests.Domain.Apoio;

namespace HRAuto.Tests.Domain;

/// <summary>RF22/RF23: correção das batidas na aprovação da manutenção e reajuste do banco de horas (RF24).</summary>
public class CorrecaoDePontoTests
{
    private static CorrecaoBatidas Correcao(string? entrada = null, string? saidaIntervalo = null, string? retorno = null, string? saida = null) =>
        new(entrada is null ? null : H(entrada), saidaIntervalo is null ? null : H(saidaIntervalo),
            retorno is null ? null : H(retorno), saida is null ? null : H(saida));

    // ---------- AplicarCorrecao ----------

    [Fact]
    public void Corrigir_BatidaQueFaltava_CompletaODia_ERegistraAntesNulo()
    {
        var ponto = NovoPonto("2026-10-19", "08:00", "12:00", "13:00", null);

        var alteracoes = RegrasManutencaoPonto.AplicarCorrecao(ponto, Correcao(saida: "18:00"));

        var alteracao = Assert.Single(alteracoes);
        Assert.Equal(TipoBatida.Saida, alteracao.Tipo);
        Assert.Null(alteracao.Antes);
        Assert.Equal(H("18:00"), alteracao.Depois);
        Assert.Equal(H("18:00"), ponto.Saida);
        Assert.Equal(4, ponto.TotalBatidas());
    }

    [Fact]
    public void Corrigir_BatidaExistente_TrocaOHorario_EGuardaOAnterior()
    {
        var ponto = PontoCompleto("2026-10-19", "08:00", "12:00", "13:00", "17:00");

        var alteracoes = RegrasManutencaoPonto.AplicarCorrecao(ponto, Correcao(entrada: "07:30", saida: "17:00"));

        var alteracao = Assert.Single(alteracoes); // a saída repetida não conta como mudança
        Assert.Equal(TipoBatida.Entrada, alteracao.Tipo);
        Assert.Equal(H("08:00"), alteracao.Antes);
        Assert.Equal(H("07:30"), ponto.Entrada);
    }

    [Fact]
    public void Corrigir_DiaSemBatidas_PodePreencherTudo()
    {
        var ponto = NovoPonto("2026-10-19", null, null, null, null);

        var alteracoes = RegrasManutencaoPonto.AplicarCorrecao(ponto, Correcao("08:00", "12:00", "13:00", "17:00"));

        Assert.Equal(4, alteracoes.Count);
        Assert.All(alteracoes, a => Assert.Null(a.Antes));
        Assert.Equal(4, ponto.TotalBatidas());
    }

    [Fact]
    public void Corrigir_RepetindoOQueJaEstava_NaoAlteraNada()
    {
        var ponto = PontoCompleto("2026-10-19", "08:00", "12:00", "13:00", "17:00");

        var alteracoes = RegrasManutencaoPonto.AplicarCorrecao(ponto, Correcao(entrada: "08:00"));

        Assert.Empty(alteracoes);
    }

    [Fact]
    public void Corrigir_SemNenhumHorario_EhRecusado()
    {
        var ponto = NovoPonto("2026-10-19", "08:00", null, null, null);

        var erro = Assert.Throws<DominioException>(() => RegrasManutencaoPonto.AplicarCorrecao(ponto, Correcao()));

        Assert.Contains("pelo menos um horário", erro.Message);
    }

    [Fact]
    public void Corrigir_DeixandoBuracoNoMeio_EhRecusado_ENadaMuda()
    {
        var ponto = NovoPonto("2026-10-19", "08:00", null, null, null);

        var erro = Assert.Throws<DominioException>(() => RegrasManutencaoPonto.AplicarCorrecao(ponto, Correcao(saida: "17:00")));

        Assert.Contains("batidas anteriores", erro.Message);
        Assert.Null(ponto.Saida);
    }

    [Fact]
    public void Corrigir_ForaDeOrdem_EhRecusado_ENadaMuda()
    {
        var ponto = PontoCompleto("2026-10-19", "08:00", "12:00", "13:00", "17:00");

        // A entrada nova (12:30) ficaria depois da saída para o intervalo (12:00): a correção inteira é recusada.
        var erro = Assert.Throws<DominioException>(() => RegrasManutencaoPonto.AplicarCorrecao(ponto, Correcao(entrada: "12:30")));

        Assert.Contains("precisa ser depois", erro.Message);
        Assert.Equal(H("08:00"), ponto.Entrada);
    }

    [Fact]
    public void Corrigir_UmaBatidaValidaEOutraInvalida_NaoAplicaNenhuma()
    {
        var ponto = PontoCompleto("2026-10-19", "08:00", "12:00", "13:00", "17:00");

        Assert.Throws<DominioException>(() =>
            RegrasManutencaoPonto.AplicarCorrecao(ponto, Correcao(entrada: "07:45", saida: "12:30")));

        Assert.Equal(H("08:00"), ponto.Entrada);
        Assert.Equal(H("17:00"), ponto.Saida);
    }

    [Fact]
    public void Corrigir_DuasBatidasComMenosDeUmMinuto_EhRecusado()
    {
        var ponto = NovoPonto("2026-10-19", "08:00", null, null, null);

        Assert.Throws<DominioException>(() => RegrasManutencaoPonto.AplicarCorrecao(ponto, Correcao(saidaIntervalo: "08:00")));
    }

    // ---------- Justificativa, observação e dia do pedido ----------

    [Fact]
    public void Justificativa_ApararEspacos_EAplicaLimites()
    {
        Assert.Equal("Esqueci de bater a saída", RegrasManutencaoPonto.NormalizarJustificativa("  Esqueci de bater a saída  "));
        Assert.Throws<DominioException>(() => RegrasManutencaoPonto.NormalizarJustificativa("curta"));
        Assert.Throws<DominioException>(() => RegrasManutencaoPonto.NormalizarJustificativa(new string('a', RegrasManutencaoPonto.TamanhoMaximoJustificativa + 1)));
        Assert.Equal(500, RegrasManutencaoPonto.NormalizarJustificativa(new string('a', 500)).Length);
    }

    [Fact]
    public void Analisar_ObservacaoMuitoLonga_EhRecusada()
    {
        var pedido = RegrasManutencaoPonto.Solicitar(
            NovoPonto("2026-10-19", "08:00", "12:00", "13:00", null, 1).ComId(10), 1, "Esqueci de bater a saída", Agora, []);

        var erro = Assert.Throws<DominioException>(() =>
            RegrasManutencaoPonto.Analisar(pedido, true, 2, new string('x', 501), Agora));

        Assert.Contains("500", erro.Message);
        Assert.Equal(StatusSolicitacao.Pendente, pedido.Status);
    }

    [Fact]
    public void DiaDoPedido_Hoje_ELimiteDe60Dias_SaoAceitos()
    {
        var hoje = D("2026-10-21");
        var admissao = D("2024-03-11");

        RegrasManutencaoPonto.ValidarDiaDoPedido(hoje, admissao, hoje);
        RegrasManutencaoPonto.ValidarDiaDoPedido(hoje.AddDays(-RegrasManutencaoPonto.PrazoMaximoDias), admissao, hoje);
    }

    [Fact]
    public void DiaDoPedido_Futuro_MuitoAntigo_OuAnteriorAAdmissao_SaoRecusados()
    {
        var hoje = D("2026-10-21");

        Assert.Throws<DominioException>(() => RegrasManutencaoPonto.ValidarDiaDoPedido(hoje.AddDays(1), D("2024-03-11"), hoje));
        Assert.Throws<DominioException>(() =>
            RegrasManutencaoPonto.ValidarDiaDoPedido(hoje.AddDays(-RegrasManutencaoPonto.PrazoMaximoDias - 1), D("2024-03-11"), hoje));
        Assert.Throws<DominioException>(() => RegrasManutencaoPonto.ValidarDiaDoPedido(D("2026-10-10"), D("2026-10-12"), hoje));
    }

    // ---------- Reajuste do banco de horas ----------

    private static ResumoDia Resumo(string data, string entrada, string saidaIntervalo, string retorno, string saida)
    {
        var ponto = PontoCompleto(data, entrada, saidaIntervalo, retorno, saida);
        return new CalculadoraJornada(JornadaPadrao.Padrao).Calcular(ponto, ponto.Data, ContextoDia.DiaUtil, diaEncerrado: true);
    }

    private static BancoHoras Lancamento(string data, string tipo, double horas, string origem = OrigensBancoHoras.Ponto) =>
        new() { IdFuncionario = 1, Data = D(data), Tipo = tipo, Quantidade = horas, Origem = origem };

    [Fact]
    public void Correcao_DiaSemLancamentoAnterior_LancaOSaldoInteiro()
    {
        var resumo = Resumo("2026-10-19", "08:00", "12:00", "13:00", "18:00"); // 9h => +1h

        var lancamento = RegrasBancoHoras.LancamentoDeCorrecao(1, resumo, []);

        Assert.NotNull(lancamento);
        Assert.Equal(TiposBancoHoras.Credito, lancamento!.Tipo);
        Assert.Equal(1.0, lancamento.Quantidade);
        Assert.Equal(OrigensBancoHoras.Ponto, lancamento.Origem);
        Assert.Contains("Correção", lancamento.Observacao);
    }

    [Fact]
    public void Correcao_SoLancaADiferencaParaOQueJaFoiLancado()
    {
        var resumo = Resumo("2026-10-19", "08:00", "12:00", "13:00", "18:30"); // +1,5h
        var existentes = new[] { Lancamento("2026-10-19", TiposBancoHoras.Credito, 1.0) };

        var lancamento = RegrasBancoHoras.LancamentoDeCorrecao(1, resumo, existentes);

        Assert.Equal(TiposBancoHoras.Credito, lancamento!.Tipo);
        Assert.Equal(0.5, lancamento.Quantidade);
    }

    [Fact]
    public void Correcao_SaldoMenorQueOJaLancado_GeraDebito()
    {
        var resumo = Resumo("2026-10-19", "08:00", "12:00", "13:00", "17:00"); // saldo 0
        var existentes = new[] { Lancamento("2026-10-19", TiposBancoHoras.Credito, 1.0) };

        var lancamento = RegrasBancoHoras.LancamentoDeCorrecao(1, resumo, existentes);

        Assert.Equal(TiposBancoHoras.Debito, lancamento!.Tipo);
        Assert.Equal(1.0, lancamento.Quantidade);
    }

    [Fact]
    public void Correcao_SemDiferenca_NaoLancaNada_EAjusteDoRhNaoEntraNaConta()
    {
        var resumo = Resumo("2026-10-19", "08:00", "12:00", "13:00", "18:00"); // +1h
        var existentes = new[]
        {
            Lancamento("2026-10-19", TiposBancoHoras.Credito, 1.0),
            Lancamento("2026-10-19", TiposBancoHoras.Debito, 3.0, OrigensBancoHoras.AjusteRH), // outro assunto
            Lancamento("2026-10-18", TiposBancoHoras.Credito, 9.0) // outro dia
        };

        Assert.Null(RegrasBancoHoras.LancamentoDeCorrecao(1, resumo, existentes));
    }

    [Fact]
    public void Correcao_DiaIncompleto_NaoLancaNada()
    {
        var ponto = NovoPonto("2026-10-19", "08:00", "12:00", "13:00", null);
        var resumo = new CalculadoraJornada(JornadaPadrao.Padrao).Calcular(ponto, ponto.Data, ContextoDia.DiaUtil, diaEncerrado: true);

        Assert.Null(RegrasBancoHoras.LancamentoDeCorrecao(1, resumo, []));
    }

    // ---------- Notificações ----------

    [Fact]
    public void NotificacaoDePedido_VaiParaODestinatario_SemDadosPessoais()
    {
        var pedido = new ManutencaoPonto { IdFuncionario = 4, IdPonto = 10 };

        var n = NotificacoesJornada.NovaManutencaoDePonto(3, pedido, D("2026-10-19"), Agora);

        Assert.Equal(3, n.IdFuncionario);
        Assert.Equal(TiposNotificacao.ManutencaoPonto, n.Tipo);
        Assert.Contains("FUNC-0004", n.Mensagem);
        Assert.Contains("19/10/2026", n.Mensagem);
        Assert.False(n.Lida);
    }

    [Fact]
    public void NotificacaoDeHolerite_DiferenciaNovoDeAtualizado()
    {
        var holerite = new Holerite { IdFuncionario = 4, Mes = 9, Ano = 2026 };

        var novo = NotificacoesJornada.HoleritePublicado(holerite, false, Agora);
        var atualizado = NotificacoesJornada.HoleritePublicado(holerite, true, Agora);

        Assert.Equal(4, novo.IdFuncionario);
        Assert.Equal("Novo holerite disponível", novo.Titulo);
        Assert.Contains("09/2026", novo.Mensagem);
        Assert.Equal("Holerite atualizado", atualizado.Titulo);
    }
}
