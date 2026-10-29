using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;
using static HRAuto.Tests.Domain.Apoio;

namespace HRAuto.Tests.Domain;

/// <summary>RF24: créditos e débitos diários do ponto e ajustes manuais do RH.</summary>
public class BancoHorasTests
{
    private readonly CalculadoraJornada _calculadora = new();

    private ResumoDia Dia(string data, string saida, bool completo = true)
    {
        var ponto = completo
            ? PontoCompleto(data, "08:00", "12:00", "13:00", saida)
            : NovoPonto(data, "08:00", "12:00", "13:00", null);
        return _calculadora.Calcular(ponto, D(data), ContextoDia.DiaUtil, true);
    }

    [Fact]
    public void DiaComExtra_GeraCredito()
    {
        var lancamento = RegrasBancoHoras.LancamentoDoDia(1, Dia("2026-10-20", "17:30"), []);

        Assert.NotNull(lancamento);
        Assert.Equal(TiposBancoHoras.Credito, lancamento!.Tipo);
        Assert.Equal(0.5, lancamento.Quantidade);
        Assert.Equal(OrigensBancoHoras.Ponto, lancamento.Origem);
        Assert.Equal(D("2026-10-20"), lancamento.Data);
        Assert.Equal(1, lancamento.IdFuncionario);
    }

    [Fact]
    public void DiaComHorasFaltantes_GeraDebito()
    {
        var lancamento = RegrasBancoHoras.LancamentoDoDia(1, Dia("2026-10-20", "16:00"), []);

        Assert.Equal(TiposBancoHoras.Debito, lancamento!.Tipo);
        Assert.Equal(1.0, lancamento.Quantidade);
    }

    [Fact]
    public void QuantidadeEmHorasDecimais_ComQuatroCasas()
    {
        var lancamento = RegrasBancoHoras.LancamentoDoDia(1, Dia("2026-10-20", "17:20"), []); // +20 min

        Assert.Equal(0.3333, lancamento!.Quantidade);
    }

    [Fact]
    public void DiaSemSaldo_NaoGeraLancamento()
    {
        Assert.Null(RegrasBancoHoras.LancamentoDoDia(1, Dia("2026-10-20", "17:00"), []));
    }

    [Fact]
    public void DiaInconsistente_NaoGeraLancamento()
    {
        Assert.Null(RegrasBancoHoras.LancamentoDoDia(1, Dia("2026-10-20", "17:30", completo: false), []));
    }

    [Fact]
    public void Falta_NaoGeraLancamento()
    {
        var falta = _calculadora.Calcular(null, D("2026-10-20"), ContextoDia.DiaUtil, true);

        Assert.Null(RegrasBancoHoras.LancamentoDoDia(1, falta, []));
    }

    [Fact]
    public void FechamentoRepetido_NaoDuplicaOLancamentoDoDia()
    {
        var resumo = Dia("2026-10-20", "17:30");
        var primeiro = RegrasBancoHoras.LancamentoDoDia(1, resumo, [])!;

        Assert.Null(RegrasBancoHoras.LancamentoDoDia(1, resumo, [primeiro]));
    }

    [Fact]
    public void AjusteManualNoMesmoDia_NaoImpedeOLancamentoDoPonto()
    {
        var resumo = Dia("2026-10-20", "17:30");
        var ajuste = RegrasBancoHoras.AjusteManual(1, D("2026-10-20"), TiposBancoHoras.Credito, 1, "Compensação acordada", Hoje);

        Assert.NotNull(RegrasBancoHoras.LancamentoDoDia(1, resumo, [ajuste]));
    }

    [Fact]
    public void Saldo_SomaCreditosEDescontaDebitos()
    {
        var lancamentos = new[]
        {
            new BancoHoras { Tipo = TiposBancoHoras.Credito, Quantidade = 2.5 },
            new BancoHoras { Tipo = TiposBancoHoras.Credito, Quantidade = 0.3333 },
            new BancoHoras { Tipo = TiposBancoHoras.Debito, Quantidade = 1.0 }
        };

        Assert.Equal(1.8333m, RegrasBancoHoras.Saldo(lancamentos));
    }

    [Fact]
    public void Saldo_PodeFicarNegativo_EVazioEhZero()
    {
        var debitos = new[] { new BancoHoras { Tipo = TiposBancoHoras.Debito, Quantidade = 3 } };

        Assert.Equal(-3m, RegrasBancoHoras.Saldo(debitos));
        Assert.Equal(0m, RegrasBancoHoras.Saldo([]));
    }

    [Fact]
    public void Saldo_TipoDesconhecido_EhRecusado()
    {
        Assert.Throws<DominioException>(() =>
            RegrasBancoHoras.Saldo([new BancoHoras { Tipo = "Outro", Quantidade = 1 }]));
    }

    [Fact]
    public void AjusteManual_Valido_GravaOrigemAjusteRH_ComObservacao()
    {
        var ajuste = RegrasBancoHoras.AjusteManual(3, D("2026-10-19"), TiposBancoHoras.Debito, 2.25, "  Folga autorizada  ", Hoje);

        Assert.Equal(OrigensBancoHoras.AjusteRH, ajuste.Origem);
        Assert.Equal(TiposBancoHoras.Debito, ajuste.Tipo);
        Assert.Equal(2.25, ajuste.Quantidade);
        Assert.Equal("Folga autorizada", ajuste.Observacao);
    }

    [Theory]
    [InlineData("Credito", 0.0, "Observação válida", "2026-10-20")]      // horas zero
    [InlineData("Credito", -1.0, "Observação válida", "2026-10-20")]     // horas negativas
    [InlineData("Saldo", 1.0, "Observação válida", "2026-10-20")]        // tipo inválido
    [InlineData("Credito", 1.0, "ok", "2026-10-20")]                     // observação curta
    [InlineData("Credito", 1.0, null, "2026-10-20")]                     // sem observação
    [InlineData("Credito", 1.0, "Observação válida", "2026-10-21")]      // data futura
    public void AjusteManual_Invalido_EhRecusado(string tipo, double horas, string? observacao, string data)
    {
        Assert.Throws<DominioException>(() => RegrasBancoHoras.AjusteManual(1, D(data), tipo, horas, observacao, Hoje));
    }

    [Fact]
    public void AjusteManual_HorasNaN_EhRecusado()
    {
        Assert.Throws<DominioException>(() =>
            RegrasBancoHoras.AjusteManual(1, D("2026-10-20"), TiposBancoHoras.Credito, double.NaN, "Observação válida", Hoje));
    }
}
