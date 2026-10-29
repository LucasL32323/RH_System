using HRAuto.Domain.Common;
using HRAuto.Domain.Disciplina;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using static HRAuto.Tests.Domain.Apoio;

namespace HRAuto.Tests.Domain;

/// <summary>RF33, RF34, RF47: registro, semáforo (0 / 1-2 / 3+) e régua (3 leves = suspensão; 2 suspensões = justa causa).</summary>
public class AdvertenciaTests
{
    private static Advertencia Leve(string data, string status = StatusAdvertencia.Ativa, int id = 0) =>
        NovaAdvertencia(TipoAdvertencia.Leve, data, status, id);

    private static Advertencia Suspensao(string data, int id = 0) =>
        NovaAdvertencia(TipoAdvertencia.Suspensao, data, id: id);

    // ---------- Registro ----------

    [Fact]
    public void Registrar_Valido_CriaAdvertenciaAtiva()
    {
        var a = RegrasAdvertencia.Registrar(1, 2, TipoAdvertencia.Leve, "  Atraso reiterado ", "  Três atrasos na semana  ", D("2026-10-19"), Hoje);

        Assert.Equal(StatusAdvertencia.Ativa, a.Status);
        Assert.Equal("Leve", a.Tipo);
        Assert.Equal("Atraso reiterado", a.Motivo);
        Assert.Equal("Três atrasos na semana", a.Descricao);
        Assert.Equal(1, a.IdFuncionario);
        Assert.Equal(2, a.IdResponsavel);
    }

    [Fact]
    public void Registrar_SemDescricao_GravaNulo()
    {
        Assert.Null(RegrasAdvertencia.Registrar(1, 2, TipoAdvertencia.Suspensao, "Falta grave", "  ", Hoje, Hoje).Descricao);
    }

    [Theory]
    [InlineData(0, 2, "Motivo", "2026-10-19")]   // sem funcionário
    [InlineData(1, 0, "Motivo", "2026-10-19")]   // sem responsável
    [InlineData(1, 1, "Motivo", "2026-10-19")]   // responsável = o próprio funcionário
    [InlineData(1, 2, "  ", "2026-10-19")]       // sem motivo
    [InlineData(1, 2, "ab", "2026-10-19")]       // motivo curto
    [InlineData(1, 2, "Motivo", "2026-10-21")]   // data futura
    public void Registrar_Invalido_EhRecusado(int idFuncionario, int idResponsavel, string motivo, string data)
    {
        Assert.Throws<DominioException>(() =>
            RegrasAdvertencia.Registrar(idFuncionario, idResponsavel, TipoAdvertencia.Leve, motivo, null, D(data), Hoje));
    }

    [Fact]
    public void Registrar_TipoForaDoEnum_EhRecusado()
    {
        Assert.Throws<DominioException>(() =>
            RegrasAdvertencia.Registrar(1, 2, (TipoAdvertencia)99, "Motivo", null, Hoje, Hoje));
    }

    [Fact]
    public void Cancelar_DeixaDeContar_EMantemOHistorico()
    {
        var a = Leve("2026-10-01");

        RegrasAdvertencia.Cancelar(a, "Registrada por engano");

        Assert.Equal(StatusAdvertencia.Cancelada, a.Status);
        Assert.Equal("[Cancelada] Registrada por engano", a.Descricao);
        Assert.Equal(0, SemaforoAdvertencias.Calcular([a]).QuantidadeAtivas);
    }

    [Fact]
    public void Cancelar_ExigeMotivo_ESoVaiParaAtivas()
    {
        var a = Leve("2026-10-01");
        Assert.Throws<DominioException>(() => RegrasAdvertencia.Cancelar(a, ""));

        RegrasAdvertencia.Cancelar(a, "Motivo do cancelamento");
        Assert.Throws<DominioException>(() => RegrasAdvertencia.Cancelar(a, "Outro motivo qualquer"));
    }

    [Fact]
    public void LerTipo_ConverteTextoDoBanco_EFalhaComValorDesconhecido()
    {
        Assert.Equal(TipoAdvertencia.JustaCausa, RegrasAdvertencia.LerTipo("JustaCausa"));
        Assert.Throws<DominioException>(() => RegrasAdvertencia.LerTipo("Verbal"));
        Assert.Throws<DominioException>(() => RegrasAdvertencia.LerTipo("7"));
    }

    // ---------- Semáforo (RF47) ----------

    [Theory]
    [InlineData(0, 1)] // verde
    [InlineData(1, 2)] // amarelo
    [InlineData(2, 2)]
    [InlineData(3, 3)] // vermelho
    [InlineData(7, 3)]
    public void Semaforo_PelaQuantidadeDeAtivas(int ativas, int nivelEsperado)
    {
        var lista = Enumerable.Range(0, ativas).Select(i => Leve("2026-10-0" + (i % 9 + 1))).ToList();

        var r = SemaforoAdvertencias.Calcular(lista);

        Assert.Equal((NivelSemaforo)nivelEsperado, r.Nivel);
        Assert.Equal(ativas, r.QuantidadeAtivas);
    }

    [Fact]
    public void Semaforo_IgnoraCanceladas_ContaTodosOsTiposAtivos()
    {
        var lista = new[]
        {
            Leve("2026-09-01"),
            Leve("2026-09-02", StatusAdvertencia.Cancelada),
            Suspensao("2026-09-03")
        };

        var r = SemaforoAdvertencias.Calcular(lista);

        Assert.Equal(2, r.QuantidadeAtivas);
        Assert.Equal(NivelSemaforo.Amarelo, r.Nivel);
    }

    // ---------- Régua de consequências (RF34) ----------

    [Fact]
    public void Regua_SemAdvertencias_NaoSugereNada()
    {
        Assert.Equal(AcaoSugerida.Nenhuma, ReguaConsequencias.Avaliar([]).Acao);
    }

    [Fact]
    public void Regua_DuasLeves_NaoSugereNada()
    {
        var r = ReguaConsequencias.Avaliar([Leve("2026-09-01"), Leve("2026-09-10")]);

        Assert.Equal(AcaoSugerida.Nenhuma, r.Acao);
        Assert.Equal(2, r.LevesDesdeUltimaSuspensao);
    }

    [Fact]
    public void Regua_TresLeves_SugereSuspensao()
    {
        var r = ReguaConsequencias.Avaliar([Leve("2026-09-01"), Leve("2026-09-10"), Leve("2026-09-20")]);

        Assert.Equal(AcaoSugerida.Suspensao, r.Acao);
        Assert.Equal(3, r.LevesDesdeUltimaSuspensao);
        Assert.Contains("3 advertências leves", r.Mensagem);
    }

    [Fact]
    public void Regua_LeveCanceladaNaoConta()
    {
        var r = ReguaConsequencias.Avaliar(
            [Leve("2026-09-01"), Leve("2026-09-10", StatusAdvertencia.Cancelada), Leve("2026-09-20")]);

        Assert.Equal(AcaoSugerida.Nenhuma, r.Acao);
    }

    [Fact]
    public void Regua_DepoisQueORHRegistraASuspensao_ASugestaoNaoSeRepete()
    {
        var r = ReguaConsequencias.Avaliar(
            [Leve("2026-09-01"), Leve("2026-09-10"), Leve("2026-09-20"), Suspensao("2026-09-25")]);

        Assert.Equal(AcaoSugerida.Nenhuma, r.Acao);
        Assert.Equal(0, r.LevesDesdeUltimaSuspensao);
        Assert.Equal(1, r.SuspensoesAtivas);
    }

    [Fact]
    public void Regua_TresNovasLevesDepoisDaSuspensao_SugerirNovaSuspensao()
    {
        var r = ReguaConsequencias.Avaliar(
        [
            Leve("2026-09-01"), Leve("2026-09-10"), Leve("2026-09-20"), Suspensao("2026-09-25"),
            Leve("2026-10-01"), Leve("2026-10-05"), Leve("2026-10-10")
        ]);

        Assert.Equal(AcaoSugerida.Suspensao, r.Acao);
        Assert.Equal(3, r.LevesDesdeUltimaSuspensao);
    }

    [Fact]
    public void Regua_DuasSuspensoes_SugereJustaCausa_ComPrioridadeSobreAsLeves()
    {
        var r = ReguaConsequencias.Avaliar(
        [
            Suspensao("2026-08-01"), Suspensao("2026-09-01"),
            Leve("2026-10-01"), Leve("2026-10-05"), Leve("2026-10-10")
        ]);

        Assert.Equal(AcaoSugerida.JustaCausa, r.Acao);
        Assert.Equal(2, r.SuspensoesAtivas);
    }

    [Fact]
    public void Regua_SuspensaoCanceladaNaoContaParaJustaCausa()
    {
        var r = ReguaConsequencias.Avaliar(
        [
            Suspensao("2026-08-01"),
            NovaAdvertencia(TipoAdvertencia.Suspensao, "2026-09-01", StatusAdvertencia.Cancelada)
        ]);

        Assert.Equal(AcaoSugerida.Nenhuma, r.Acao);
    }

    [Fact]
    public void Regua_JustaCausaJaAplicada_NaoSugereMais()
    {
        var r = ReguaConsequencias.Avaliar(
        [
            Suspensao("2026-08-01"), Suspensao("2026-09-01"),
            NovaAdvertencia(TipoAdvertencia.JustaCausa, "2026-09-15")
        ]);

        Assert.Equal(AcaoSugerida.Nenhuma, r.Acao);
        Assert.Contains("já aplicada", r.Mensagem);
    }

    [Fact]
    public void Regua_UsaAOrdemCronologica_NaoAOrdemDaLista()
    {
        // Lista fora de ordem: a suspensão (25/09) é posterior às 3 leves, então elas foram consumidas.
        var r = ReguaConsequencias.Avaliar(
            [Suspensao("2026-09-25"), Leve("2026-09-20"), Leve("2026-09-01"), Leve("2026-09-10")]);

        Assert.Equal(AcaoSugerida.Nenhuma, r.Acao);
    }

    [Fact]
    public void Regua_EmpateDeDataDesempataPeloId()
    {
        // Mesma data: a leve (id 1) vem antes da suspensão (id 2), então é consumida; a leve 3 conta depois.
        var r = ReguaConsequencias.Avaliar(
        [
            Leve("2026-09-01", id: 1), Suspensao("2026-09-01", id: 2), Leve("2026-09-01", id: 3)
        ]);

        Assert.Equal(1, r.LevesDesdeUltimaSuspensao);
    }
}
