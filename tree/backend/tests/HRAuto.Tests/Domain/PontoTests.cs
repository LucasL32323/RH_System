using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using static HRAuto.Tests.Domain.Apoio;

namespace HRAuto.Tests.Domain;

/// <summary>RF17, RF18, RF19 e RNF09: sequência de batidas e sincronização offline sem duplicar.</summary>
public class PontoTests
{
    [Fact]
    public void RegistrarBatida_SegueAsQuatroBatidasNaOrdem()
    {
        var ponto = Ponto.Criar(1, D("2026-10-20"));

        Assert.Equal(TipoBatida.Entrada, ponto.RegistrarBatida(H("08:00")));
        Assert.Equal(TipoBatida.SaidaIntervalo, ponto.RegistrarBatida(H("12:00")));
        Assert.Equal(TipoBatida.RetornoIntervalo, ponto.RegistrarBatida(H("13:00")));
        Assert.Equal(TipoBatida.Saida, ponto.RegistrarBatida(H("17:00")));

        Assert.Equal(4, ponto.TotalBatidas());
        Assert.Null(ponto.ProximaBatida());
        Assert.Equal(H("17:00"), ponto.Saida);
    }

    [Fact]
    public void RegistrarBatida_QuintaBatida_EhBloqueada()
    {
        var ponto = PontoCompleto("2026-10-20", "08:00", "12:00", "13:00", "17:00");

        var erro = Assert.Throws<DominioException>(() => ponto.RegistrarBatida(H("18:00")));

        Assert.Contains("quatro batidas", erro.Message);
    }

    [Fact]
    public void RegistrarBatida_AntesDaBatidaAnterior_EhForaDeOrdem()
    {
        var ponto = NovoPonto("2026-10-20", "08:00", null, null, null);

        var erro = Assert.Throws<DominioException>(() => ponto.RegistrarBatida(H("07:30")));

        Assert.Contains("fora de ordem", erro.Message);
        Assert.Null(ponto.SaidaIntervalo); // nada foi gravado
    }

    [Theory]
    [InlineData("08:00")] // mesmo horário
    public void RegistrarBatida_NoMesmoMinutoDaAnterior_EhRepetida(string hora)
    {
        var ponto = NovoPonto("2026-10-20", "08:00", null, null, null);

        var erro = Assert.Throws<DominioException>(() => ponto.RegistrarBatida(H(hora)));

        Assert.Contains("repetida", erro.Message);
        Assert.Equal(1, ponto.TotalBatidas());
    }

    [Fact]
    public void RegistrarBatida_UmMinutoDepois_EhAceita()
    {
        var ponto = NovoPonto("2026-10-20", "08:00", null, null, null);

        Assert.Equal(TipoBatida.SaidaIntervalo, ponto.RegistrarBatida(H("08:01")));
    }

    [Theory]
    [InlineData(0, 1)] // sem batidas -> entrada
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    public void ProximaBatida_EhAPrimeiraAindaVazia(int feitas, int esperada)
    {
        var ponto = NovoPonto("2026-10-20",
            feitas >= 1 ? "08:00" : null,
            feitas >= 2 ? "12:00" : null,
            feitas >= 3 ? "13:00" : null,
            null);

        Assert.Equal((TipoBatida)esperada, ponto.ProximaBatida());
        Assert.Equal(feitas, ponto.TotalBatidas());
    }

    [Fact]
    public void Criar_SemFuncionario_EhRecusado()
    {
        Assert.Throws<DominioException>(() => Ponto.Criar(0, D("2026-10-20")));
    }

    [Fact]
    public void Sincronizar_BatidaNova_EhAplicada()
    {
        var ponto = NovoPonto("2026-10-20", "08:00", null, null, null);

        var resultado = ponto.AplicarBatidaSincronizada(TipoBatida.SaidaIntervalo, H("12:03"));

        Assert.Equal(ResultadoSincronizacao.Aplicada, resultado);
        Assert.Equal(H("12:03"), ponto.SaidaIntervalo);
    }

    [Fact]
    public void Sincronizar_MesmaBatidaDuasVezes_NaoDuplica()
    {
        var ponto = NovoPonto("2026-10-20", "08:00", null, null, null);
        ponto.AplicarBatidaSincronizada(TipoBatida.SaidaIntervalo, H("12:03"));

        var segunda = ponto.AplicarBatidaSincronizada(TipoBatida.SaidaIntervalo, H("12:03"));

        Assert.Equal(ResultadoSincronizacao.JaRegistrada, segunda);
        Assert.Equal(2, ponto.TotalBatidas());
    }

    [Fact]
    public void Sincronizar_TipoJaGravadoComOutroHorario_EhRecusado()
    {
        var ponto = NovoPonto("2026-10-20", "08:00", null, null, null);

        var erro = Assert.Throws<DominioException>(() => ponto.AplicarBatidaSincronizada(TipoBatida.Entrada, H("08:10")));

        Assert.Contains("manutenção", erro.Message);
        Assert.Equal(H("08:00"), ponto.Entrada); // a batida original continua
    }

    [Fact]
    public void Sincronizar_FaltandoBatidaAnterior_EhRecusado()
    {
        var ponto = NovoPonto("2026-10-20", "08:00", null, null, null);

        var erro = Assert.Throws<DominioException>(() => ponto.AplicarBatidaSincronizada(TipoBatida.RetornoIntervalo, H("13:00")));

        Assert.Contains("Falta a batida", erro.Message);
    }

    [Fact]
    public void Sincronizar_HorarioAntesDaBatidaAnterior_EhForaDeOrdem()
    {
        var ponto = NovoPonto("2026-10-20", "08:00", "12:00", null, null);

        Assert.Throws<DominioException>(() => ponto.AplicarBatidaSincronizada(TipoBatida.RetornoIntervalo, H("11:00")));
    }

    [Fact]
    public void Sincronizar_FilaOfflineInteiraEmOrdem_FechaODia()
    {
        var ponto = Ponto.Criar(1, D("2026-10-20"));

        ponto.AplicarBatidaSincronizada(TipoBatida.Entrada, H("08:00"));
        ponto.AplicarBatidaSincronizada(TipoBatida.SaidaIntervalo, H("12:00"));
        ponto.AplicarBatidaSincronizada(TipoBatida.RetornoIntervalo, H("13:00"));
        ponto.AplicarBatidaSincronizada(TipoBatida.Saida, H("17:00"));

        Assert.Equal(4, ponto.TotalBatidas());
    }
}
