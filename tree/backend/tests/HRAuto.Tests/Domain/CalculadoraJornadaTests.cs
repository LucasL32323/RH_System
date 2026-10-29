using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;
using static HRAuto.Tests.Domain.Apoio;

namespace HRAuto.Tests.Domain;

/// <summary>RF20, RF21, RF25: horas trabalhadas, extras, faltantes e atrasos pela jornada padrão (8 h, tolerância 10 min).</summary>
public class CalculadoraJornadaTests
{
    private readonly CalculadoraJornada _calculadora = new();
    private const string Terca = "2026-10-20"; // dia útil

    [Fact]
    public void JornadaExata_NaoGeraExtraNemFalta()
    {
        var ponto = PontoCompleto(Terca, "08:00", "12:00", "13:00", "17:00");

        var r = _calculadora.Calcular(ponto, D(Terca), ContextoDia.DiaUtil, diaEncerrado: true);

        Assert.Equal(SituacaoDia.Completo, r.Situacao);
        Assert.Equal(TimeSpan.FromHours(8), r.Trabalhado);
        Assert.Equal(TimeSpan.FromHours(8), r.CargaEsperada);
        Assert.Equal(TimeSpan.Zero, r.HorasExtras);
        Assert.Equal(TimeSpan.Zero, r.HorasFaltantes);
        Assert.Equal(TimeSpan.Zero, r.Saldo);
    }

    [Fact]
    public void ExtraDentroDaTolerancia_EhDesconsiderado()
    {
        var ponto = PontoCompleto(Terca, "08:00", "12:00", "13:00", "17:08"); // 8h08

        var r = _calculadora.Calcular(ponto, D(Terca), ContextoDia.DiaUtil, true);

        Assert.Equal(TimeSpan.Zero, r.HorasExtras);
    }

    [Fact]
    public void ExtraNoLimiteDaTolerancia_AindaEhDesconsiderado()
    {
        var ponto = PontoCompleto(Terca, "08:00", "12:00", "13:00", "17:10"); // exatamente 10 min

        var r = _calculadora.Calcular(ponto, D(Terca), ContextoDia.DiaUtil, true);

        Assert.Equal(TimeSpan.Zero, r.HorasExtras);
    }

    [Fact]
    public void ExtraAcimaDaTolerancia_ContaTudo()
    {
        var ponto = PontoCompleto(Terca, "08:00", "12:00", "13:00", "17:30"); // 8h30

        var r = _calculadora.Calcular(ponto, D(Terca), ContextoDia.DiaUtil, true);

        Assert.Equal(TimeSpan.FromMinutes(30), r.HorasExtras);
        Assert.Equal(TimeSpan.FromMinutes(30), r.Saldo);
    }

    [Fact]
    public void SaidaAntecipada_GeraHorasFaltantes()
    {
        var ponto = PontoCompleto(Terca, "08:00", "12:00", "13:00", "16:00"); // 7h

        var r = _calculadora.Calcular(ponto, D(Terca), ContextoDia.DiaUtil, true);

        Assert.Equal(TimeSpan.FromHours(7), r.Trabalhado);
        Assert.Equal(TimeSpan.FromHours(1), r.HorasFaltantes);
        Assert.Equal(TimeSpan.FromHours(-1), r.Saldo);
    }

    [Fact]
    public void Atraso_AcimaDaTolerancia_EhRegistrado_SemGerarExtra()
    {
        // Entrou 08:15 e saiu 17:15: trabalhou as 8 h certas, mas chegou 15 min atrasado.
        var ponto = PontoCompleto(Terca, "08:15", "12:15", "13:15", "17:15");

        var r = _calculadora.Calcular(ponto, D(Terca), ContextoDia.DiaUtil, true);

        Assert.Equal(TimeSpan.FromMinutes(15), r.Atraso);
        Assert.Equal(TimeSpan.Zero, r.HorasExtras);
        Assert.Equal(TimeSpan.Zero, r.HorasFaltantes);
    }

    [Fact]
    public void Atraso_DentroDaTolerancia_NaoConta()
    {
        var ponto = PontoCompleto(Terca, "08:09", "12:00", "13:00", "17:00");

        var r = _calculadora.Calcular(ponto, D(Terca), ContextoDia.DiaUtil, true);

        Assert.Equal(TimeSpan.Zero, r.Atraso);
    }

    [Theory]
    [InlineData(3)] // feriado
    [InlineData(4)] // férias
    [InlineData(2)] // descanso semanal
    public void TrabalhoEmDiaSemExpediente_TodoOTempoViraExtra(int contexto)
    {
        var ponto = PontoCompleto(Terca, "09:00", "11:00", "12:00", "14:00"); // 4 h

        var r = _calculadora.Calcular(ponto, D(Terca), (ContextoDia)contexto, true);

        Assert.Equal(TimeSpan.Zero, r.CargaEsperada);
        Assert.Equal(TimeSpan.FromHours(4), r.HorasExtras);
        Assert.Equal(TimeSpan.Zero, r.HorasFaltantes);
        Assert.Equal(TimeSpan.Zero, r.Atraso); // sem carga, não há atraso
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void DiaSemExpedienteSemBatidas_NaoCobraNada(int contexto)
    {
        var r = _calculadora.Calcular(null, D(Terca), (ContextoDia)contexto, true);

        Assert.Equal(SituacaoDia.SemExpediente, r.Situacao);
        Assert.Equal(TimeSpan.Zero, r.HorasFaltantes);
        Assert.Equal(TimeSpan.Zero, r.Saldo);
    }

    [Fact]
    public void DiaUtilEncerradoSemBatidas_EhFalta_ENaoDebitaHoras()
    {
        var r = _calculadora.Calcular(null, D(Terca), ContextoDia.DiaUtil, diaEncerrado: true);

        Assert.Equal(SituacaoDia.Falta, r.Situacao);
        Assert.Equal(TimeSpan.Zero, r.HorasFaltantes);
        Assert.Equal(TimeSpan.Zero, r.Saldo);
    }

    [Fact]
    public void DiaUtilAbertoSemBatidas_EstaEmAndamento()
    {
        var r = _calculadora.Calcular(null, D(Terca), ContextoDia.DiaUtil, diaEncerrado: false);

        Assert.Equal(SituacaoDia.EmAndamento, r.Situacao);
    }

    [Fact]
    public void DiaAbertoComBatidasParciais_CalculaOTrabalhadoAteAgora_SemExtraNemFalta()
    {
        var ponto = NovoPonto(Terca, "08:00", "12:00", null, null);

        var r = _calculadora.Calcular(ponto, D(Terca), ContextoDia.DiaUtil, diaEncerrado: false);

        Assert.Equal(SituacaoDia.EmAndamento, r.Situacao);
        Assert.Equal(TimeSpan.FromHours(4), r.Trabalhado);
        Assert.Equal(TimeSpan.Zero, r.HorasFaltantes);
    }

    [Fact]
    public void DiaEncerradoComBatidaFaltando_FicaInconsistente_AteHaverManutencao()
    {
        var ponto = NovoPonto(Terca, "08:00", "12:00", "13:00", null); // esqueceu a saída

        var r = _calculadora.Calcular(ponto, D(Terca), ContextoDia.DiaUtil, diaEncerrado: true);

        Assert.Equal(SituacaoDia.Inconsistente, r.Situacao);
        Assert.Equal(TimeSpan.Zero, r.HorasExtras);
        Assert.Equal(TimeSpan.Zero, r.HorasFaltantes);
    }

    [Fact]
    public void BatidasInvertidas_NuncaGeramHorasNegativas()
    {
        var ponto = PontoCompleto(Terca, "12:00", "08:00", "17:00", "13:00"); // dado corrompido

        var r = _calculadora.Calcular(ponto, D(Terca), ContextoDia.DiaUtil, true);

        Assert.Equal(TimeSpan.Zero, r.Trabalhado);
    }

    [Theory]
    [InlineData(150, true)]  // 2h30 de extra
    [InlineData(120, false)] // exatamente 2 h não passa do limite
    [InlineData(0, false)]
    public void ExcedeLimiteHoraExtra_SoAcimaDeDuasHoras(int minutosExtras, bool esperado)
    {
        var resumo = new ResumoDia(D(Terca), ContextoDia.DiaUtil, SituacaoDia.Completo,
            TimeSpan.FromHours(8) + TimeSpan.FromMinutes(minutosExtras), TimeSpan.FromHours(8),
            TimeSpan.FromMinutes(minutosExtras), TimeSpan.Zero, TimeSpan.Zero);

        Assert.Equal(esperado, _calculadora.ExcedeLimiteHoraExtra(resumo));
    }

    [Fact]
    public void JornadaPersonalizada_AlteraACargaEATolerancia()
    {
        var jornada = JornadaPadrao.Padrao with { CargaDiaria = TimeSpan.FromHours(6), ToleranciaDiaria = TimeSpan.Zero };
        var calculadora = new CalculadoraJornada(jornada);
        var ponto = PontoCompleto(Terca, "08:00", "11:00", "11:15", "14:20"); // 3h + 3h05 = 6h05

        var r = calculadora.Calcular(ponto, D(Terca), ContextoDia.DiaUtil, true);

        Assert.Equal(TimeSpan.FromMinutes(5), r.HorasExtras);
    }

    [Fact]
    public void Totalizar_SomaOPeriodo()
    {
        var dias = new[]
        {
            _calculadora.Calcular(PontoCompleto("2026-10-19", "08:00", "12:00", "13:00", "17:30"), D("2026-10-19"), ContextoDia.DiaUtil, true), // +30 min
            _calculadora.Calcular(PontoCompleto("2026-10-20", "08:20", "12:20", "13:20", "17:20"), D("2026-10-20"), ContextoDia.DiaUtil, true), // atraso 20 min
            _calculadora.Calcular(null, D("2026-10-21"), ContextoDia.DiaUtil, true),                                                          // falta
            _calculadora.Calcular(PontoCompleto("2026-10-22", "08:00", "12:00", "13:00", "16:00"), D("2026-10-22"), ContextoDia.DiaUtil, true), // -1 h
            _calculadora.Calcular(NovoPonto("2026-10-23", "08:00", "12:00", "13:00", null), D("2026-10-23"), ContextoDia.DiaUtil, true)            // inconsistente
        };

        var total = CalculadoraJornada.Totalizar(dias);

        Assert.Equal(TimeSpan.FromMinutes(30), total.HorasExtras);
        Assert.Equal(TimeSpan.FromHours(1), total.HorasFaltantes);
        Assert.Equal(TimeSpan.FromMinutes(20), total.Atrasos);
        Assert.Equal(1, total.Faltas);
        Assert.Equal(1, total.DiasInconsistentes);
        Assert.Equal(4, total.DiasTrabalhados);
        Assert.Equal(TimeSpan.FromMinutes(-30), total.Saldo);
    }
}
