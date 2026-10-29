using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;
using static HRAuto.Tests.Domain.Apoio;

namespace HRAuto.Tests.Domain;

/// <summary>RF22 e RF23: pedido de ajuste/abono com justificativa e análise pelo gestor.</summary>
public class ManutencaoPontoTests
{
    private static Ponto PontoSalvo(int idFuncionario = 1, string data = "2026-10-19") =>
        NovoPonto(data, "08:00", "12:00", "13:00", null, idFuncionario).ComId(10);

    [Fact]
    public void Solicitar_Valido_CriaPedidoPendente()
    {
        var pedido = RegrasManutencaoPonto.Solicitar(PontoSalvo(), 1, "  Esqueci de bater a saída  ", Agora, []);

        Assert.Equal(StatusSolicitacao.Pendente, pedido.Status);
        Assert.Equal(10, pedido.IdPonto);
        Assert.Equal(1, pedido.IdFuncionario);
        Assert.Equal("Esqueci de bater a saída", pedido.Justificativa);
        Assert.Equal(Agora, pedido.DataSolicitacao);
        Assert.Null(pedido.DataAnalise);
        Assert.Null(pedido.IdResponsavel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curta")]
    public void Solicitar_SemJustificativaSuficiente_EhRecusado(string? justificativa)
    {
        var erro = Assert.Throws<DominioException>(() =>
            RegrasManutencaoPonto.Solicitar(PontoSalvo(), 1, justificativa, Agora, []));

        Assert.Contains("justificativa", erro.Message);
    }

    [Fact]
    public void Solicitar_PontoDeOutroFuncionario_EhRecusado()
    {
        var erro = Assert.Throws<DominioException>(() =>
            RegrasManutencaoPonto.Solicitar(PontoSalvo(idFuncionario: 1), 2, "Justificativa válida", Agora, []));

        Assert.Contains("próprio ponto", erro.Message);
    }

    [Fact]
    public void Solicitar_PontoAindaNaoSalvo_EhRecusado()
    {
        var semId = NovoPonto("2026-10-19", null, null, null, null);

        Assert.Throws<DominioException>(() =>
            RegrasManutencaoPonto.Solicitar(semId, 1, "Justificativa válida", Agora, []));
    }

    [Fact]
    public void Solicitar_DiaFuturo_EhRecusado()
    {
        var futuro = NovoPonto("2026-10-21", null, null, null, null).ComId(11);

        Assert.Throws<DominioException>(() =>
            RegrasManutencaoPonto.Solicitar(futuro, 1, "Justificativa válida", Agora, []));
    }

    [Fact]
    public void Solicitar_JaHaPedidoPendenteParaOMesmoPonto_EhRecusado()
    {
        var ponto = PontoSalvo();
        var primeiro = RegrasManutencaoPonto.Solicitar(ponto, 1, "Primeira justificativa", Agora, []);

        Assert.Throws<DominioException>(() =>
            RegrasManutencaoPonto.Solicitar(ponto, 1, "Segunda justificativa", Agora, [primeiro]));
    }

    [Fact]
    public void Solicitar_DepoisDeUmPedidoRejeitado_PodePedirDeNovo()
    {
        var ponto = PontoSalvo();
        var rejeitado = RegrasManutencaoPonto.Solicitar(ponto, 1, "Primeira justificativa", Agora, []);
        RegrasManutencaoPonto.Analisar(rejeitado, false, 5, "Falta comprovante", Agora);

        var novo = RegrasManutencaoPonto.Solicitar(ponto, 1, "Justificativa com comprovante", Agora, [rejeitado]);

        Assert.Equal(StatusSolicitacao.Pendente, novo.Status);
    }

    [Fact]
    public void Analisar_Aprovar_RegistraResponsavelEData()
    {
        var pedido = RegrasManutencaoPonto.Solicitar(PontoSalvo(), 1, "Esqueci de bater a saída", Agora, []);
        var analise = Agora.AddHours(2);

        RegrasManutencaoPonto.Analisar(pedido, true, 5, null, analise);

        Assert.Equal(StatusSolicitacao.Aprovada, pedido.Status);
        Assert.Equal(5, pedido.IdResponsavel);
        Assert.Equal(analise, pedido.DataAnalise);
        Assert.Null(pedido.Observacao);
    }

    [Fact]
    public void Analisar_Rejeitar_ExigeMotivo()
    {
        var pedido = RegrasManutencaoPonto.Solicitar(PontoSalvo(), 1, "Esqueci de bater a saída", Agora, []);

        Assert.Throws<DominioException>(() => RegrasManutencaoPonto.Analisar(pedido, false, 5, "  ", Agora));
        Assert.Equal(StatusSolicitacao.Pendente, pedido.Status); // continua pendente

        RegrasManutencaoPonto.Analisar(pedido, false, 5, "Sem comprovação", Agora);

        Assert.Equal(StatusSolicitacao.Rejeitada, pedido.Status);
        Assert.Equal("Sem comprovação", pedido.Observacao);
    }

    [Fact]
    public void Analisar_PedidoJaAnalisado_EhRecusado()
    {
        var pedido = RegrasManutencaoPonto.Solicitar(PontoSalvo(), 1, "Esqueci de bater a saída", Agora, []);
        RegrasManutencaoPonto.Analisar(pedido, true, 5, null, Agora);

        var erro = Assert.Throws<DominioException>(() => RegrasManutencaoPonto.Analisar(pedido, false, 6, "Mudei de ideia", Agora));

        Assert.Contains("já foi analisado", erro.Message);
    }

    [Fact]
    public void Analisar_OProprioPedido_EhRecusado()
    {
        var pedido = RegrasManutencaoPonto.Solicitar(PontoSalvo(), 1, "Esqueci de bater a saída", Agora, []);

        Assert.Throws<DominioException>(() => RegrasManutencaoPonto.Analisar(pedido, true, 1, null, Agora));
    }
}
