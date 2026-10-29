using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;
using static HRAuto.Tests.Domain.Apoio;

namespace HRAuto.Tests.Domain;

/// <summary>RF20/RF28: feriados (nacional, estadual, municipal), férias aprovadas e descanso semanal.</summary>
public class ResolvedorContextoDiaTests
{
    private static readonly JornadaPadrao Jornada = JornadaPadrao.Padrao;

    private static Feriado Feriado(string data, string tipo, string? cidade = null, string? estado = null) =>
        new() { Data = D(data), Nome = "Teste", Tipo = tipo, Cidade = cidade, Estado = estado };

    [Fact]
    public void Datas_DeDiaSemanaQualquer_SaoClassificadasPelaJornada()
    {
        var vazio = new HashSet<DateOnly>();

        Assert.Equal(ContextoDia.DiaUtil, ResolvedorContextoDia.Resolver(D("2026-10-20"), Jornada, [], vazio));        // terça
        Assert.Equal(ContextoDia.DescansoSemanal, ResolvedorContextoDia.Resolver(D("2026-10-24"), Jornada, [], vazio)); // sábado
        Assert.Equal(ContextoDia.DescansoSemanal, ResolvedorContextoDia.Resolver(D("2026-10-25"), Jornada, [], vazio)); // domingo
    }

    [Fact]
    public void FeriadoNacional_ValeParaTodos()
    {
        var datas = ResolvedorContextoDia.DatasDeFeriadoAplicaveis(
            [Feriado("2026-11-02", TiposFeriado.Nacional)], cidade: null, estado: null);

        Assert.Equal(ContextoDia.Feriado, ResolvedorContextoDia.Resolver(D("2026-11-02"), Jornada, [], datas));
    }

    [Fact]
    public void FeriadoEstadual_SoValeParaOMesmoEstado()
    {
        var feriados = new[] { Feriado("2026-07-09", TiposFeriado.Estadual, estado: "SP") };

        Assert.Contains(D("2026-07-09"), ResolvedorContextoDia.DatasDeFeriadoAplicaveis(feriados, "Santos", "sp"));
        Assert.DoesNotContain(D("2026-07-09"), ResolvedorContextoDia.DatasDeFeriadoAplicaveis(feriados, "Niterói", "RJ"));
        Assert.DoesNotContain(D("2026-07-09"), ResolvedorContextoDia.DatasDeFeriadoAplicaveis(feriados, null, null));
    }

    [Fact]
    public void FeriadoMunicipal_ExigeMesmaCidadeEEstado()
    {
        var feriados = new[] { Feriado("2026-09-08", TiposFeriado.Municipal, cidade: "Santos", estado: "SP") };

        Assert.Contains(D("2026-09-08"), ResolvedorContextoDia.DatasDeFeriadoAplicaveis(feriados, " santos ", "SP"));
        Assert.DoesNotContain(D("2026-09-08"), ResolvedorContextoDia.DatasDeFeriadoAplicaveis(feriados, "Guarujá", "SP"));
        Assert.DoesNotContain(D("2026-09-08"), ResolvedorContextoDia.DatasDeFeriadoAplicaveis(feriados, "Santos", "RJ"));
    }

    [Fact]
    public void FeriadoComTipoDesconhecido_EhIgnorado()
    {
        var datas = ResolvedorContextoDia.DatasDeFeriadoAplicaveis([Feriado("2026-12-24", "Outro")], "Santos", "SP");

        Assert.Empty(datas);
    }

    [Fact]
    public void FeriasAprovadas_TemPrioridadeSobreFeriadoEDescanso()
    {
        var ferias = new[] { NovasFerias(1, "2026-11-09", "2026-11-22", StatusSolicitacao.Aprovada) };
        var feriados = new HashSet<DateOnly> { D("2026-11-15") }; // domingo e feriado dentro das férias

        Assert.Equal(ContextoDia.Ferias, ResolvedorContextoDia.Resolver(D("2026-11-10"), Jornada, ferias, feriados));
        Assert.Equal(ContextoDia.Ferias, ResolvedorContextoDia.Resolver(D("2026-11-15"), Jornada, ferias, feriados));
        Assert.Equal(ContextoDia.DiaUtil, ResolvedorContextoDia.Resolver(D("2026-11-23"), Jornada, ferias, feriados));
    }

    [Theory]
    [InlineData("Pendente")]
    [InlineData("Rejeitada")]
    public void FeriasNaoAprovadas_NaoMudamODia(string status)
    {
        var ferias = new[] { NovasFerias(1, "2026-11-09", "2026-11-22", status) };

        Assert.Equal(ContextoDia.DiaUtil,
            ResolvedorContextoDia.Resolver(D("2026-11-10"), Jornada, ferias, new HashSet<DateOnly>()));
    }

    [Fact]
    public void FeriadoEmDiaUtil_PrevaleceSobreODiaUtil()
    {
        var feriados = new HashSet<DateOnly> { D("2026-10-20") };

        Assert.Equal(ContextoDia.Feriado, ResolvedorContextoDia.Resolver(D("2026-10-20"), Jornada, [], feriados));
    }
}
