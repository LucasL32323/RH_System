using HRAuto.Domain.Afastamentos;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using static HRAuto.Tests.Domain.Apoio;

namespace HRAuto.Tests.Domain;

/// <summary>RF26 e RF27: solicitação de férias (CLT: 12 meses, frações de 5/14 dias, início fora de véspera de DSR/feriado).</summary>
public class FeriasTests
{
    // Admitido em 11/03/2024: em 09/11/2026 já tem 2 anos completos de casa. Hoje = 20/10/2026 (terça).
    private static readonly DadosFuncionario Funcionario = new(1, D("2024-03-11"), true);
    private static readonly IReadOnlySet<DateOnly> SemFeriados = new HashSet<DateOnly>();

    private static Ferias Solicitar(string inicio, string fim, int dias, IEnumerable<Ferias>? existentes = null,
        IReadOnlySet<DateOnly>? feriados = null, DadosFuncionario? funcionario = null, string? observacao = null) =>
        RegrasFerias.Solicitar(funcionario ?? Funcionario, D(inicio), D(fim), dias, observacao, Hoje,
            existentes ?? [], feriados ?? SemFeriados);

    [Fact]
    public void DatasDeTeste_CaemNosDiasDaSemanaEsperados()
    {
        // Garante a premissa dos demais testes.
        Assert.Equal(DayOfWeek.Monday, D("2026-11-09").DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, D("2026-11-06").DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, D("2026-11-08").DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, D("2026-12-25").DayOfWeek);
    }

    [Fact]
    public void Solicitar_QuatorzeDias_CriaSolicitacaoPendente()
    {
        var ferias = Solicitar("2026-11-09", "2026-11-22", 14, observacao: " Viagem em família ");

        Assert.Equal(StatusSolicitacao.Pendente, ferias.Status);
        Assert.Equal(1, ferias.IdFuncionario);
        Assert.Equal(14, ferias.Dias);
        Assert.Equal(Hoje, ferias.DataSolicitacao);
        Assert.Equal("Viagem em família", ferias.Observacao);
    }

    [Fact]
    public void Solicitar_TrintaDiasSeguidos_EhAceito()
    {
        Assert.Equal(30, Solicitar("2026-11-09", "2026-12-08", 30).Dias);
    }

    [Fact]
    public void Solicitar_QuantidadeDeDiasNaoBateComOPeriodo_EhRecusado()
    {
        var erro = Assert.Throws<DominioException>(() => Solicitar("2026-11-09", "2026-11-22", 10));

        Assert.Contains("não corresponde", erro.Message);
    }

    [Fact]
    public void Solicitar_DataFinalAntesDaInicial_EhRecusado()
    {
        Assert.Throws<DominioException>(() => Solicitar("2026-11-22", "2026-11-09", 14));
    }

    [Fact]
    public void Solicitar_InicioNoPassado_EhRecusado()
    {
        Assert.Throws<DominioException>(() => Solicitar("2026-10-12", "2026-10-25", 14));
    }

    [Fact]
    public void Solicitar_ComMenosDeDozeMesesDeCasa_EhRecusado()
    {
        var recemAdmitido = new DadosFuncionario(1, D("2026-03-02"), true);

        var erro = Assert.Throws<DominioException>(() =>
            Solicitar("2026-11-09", "2026-11-22", 14, funcionario: recemAdmitido));

        Assert.Contains("12 meses", erro.Message);
    }

    [Fact]
    public void Solicitar_FuncionarioInativo_EhRecusado()
    {
        Assert.Throws<DominioException>(() =>
            Solicitar("2026-11-09", "2026-11-22", 14, funcionario: Funcionario with { Ativo = false }));
    }

    [Theory]
    [InlineData("2026-11-09", "2026-11-12", 4)]
    [InlineData("2026-11-09", "2026-11-09", 1)]
    public void Solicitar_MenosDeCincoDias_EhRecusado(string inicio, string fim, int dias)
    {
        var erro = Assert.Throws<DominioException>(() => Solicitar(inicio, fim, dias));

        Assert.Contains("mínimo", erro.Message);
    }

    [Theory]
    [InlineData("2026-11-06", "2026-11-19", 14)] // sexta: 2 dias depois é domingo
    [InlineData("2026-11-07", "2026-11-20", 14)] // sábado
    [InlineData("2026-11-08", "2026-11-21", 14)] // domingo
    public void Solicitar_InicioNaVesperaDeDomingo_EhRecusado(string inicio, string fim, int dias)
    {
        var erro = Assert.Throws<DominioException>(() => Solicitar(inicio, fim, dias));

        Assert.Contains("domingo, feriado", erro.Message);
    }

    [Theory]
    [InlineData("2026-12-22", true)]  // terça: 22, 23 e 24 livres
    [InlineData("2026-12-23", false)] // quarta: 2 dias depois é feriado 25/12
    [InlineData("2026-12-24", false)] // quinta: 1 dia antes do feriado
    [InlineData("2026-12-25", false)] // no próprio feriado
    public void InicioPermitido_RespeitaFeriados(string inicio, bool permitido)
    {
        var feriados = new HashSet<DateOnly> { D("2026-12-25") };

        Assert.Equal(permitido, RegrasFerias.InicioPermitido(D(inicio), feriados));
    }

    [Fact]
    public void Solicitar_SobrepostaAOutraSolicitacaoAtiva_EhRecusado()
    {
        var existente = NovasFerias(1, "2026-11-16", "2026-11-29", StatusSolicitacao.Aprovada);

        var erro = Assert.Throws<DominioException>(() => Solicitar("2026-11-09", "2026-11-22", 14, [existente]));

        Assert.Contains("sobrepõe", erro.Message);
    }

    [Fact]
    public void Solicitar_SobrepostaARejeitada_EhAceita()
    {
        var rejeitada = NovasFerias(1, "2026-11-09", "2026-11-22", StatusSolicitacao.Rejeitada);

        Assert.NotNull(Solicitar("2026-11-09", "2026-11-22", 14, [rejeitada]));
    }

    [Fact]
    public void Solicitar_FeriasDeOutroFuncionario_SaoIgnoradas()
    {
        var deOutro = NovasFerias(2, "2026-11-09", "2026-12-08", StatusSolicitacao.Aprovada); // 30 dias de outra pessoa

        Assert.NotNull(Solicitar("2026-11-09", "2026-11-22", 14, [deOutro]));
    }

    [Fact]
    public void Solicitar_AlemDoSaldoDoPeriodo_EhRecusado()
    {
        var existente = NovasFerias(1, "2026-11-09", "2026-12-08", StatusSolicitacao.Aprovada); // 30 dias

        var erro = Assert.Throws<DominioException>(() => Solicitar("2027-01-11", "2027-01-15", 5, [existente]));

        Assert.Contains("Saldo insuficiente", erro.Message);
        Assert.Contains("restam 0", erro.Message);
    }

    [Fact]
    public void Solicitar_FracionadoEmTresPeriodos_EhAceito()
    {
        var existentes = new[]
        {
            NovasFerias(1, "2026-11-09", "2026-11-22", StatusSolicitacao.Aprovada), // 14
            NovasFerias(1, "2026-12-07", "2026-12-14", StatusSolicitacao.Pendente)  // 8
        };

        var terceira = Solicitar("2027-01-11", "2027-01-18", 8, existentes); // 14 + 8 + 8 = 30

        Assert.Equal(8, terceira.Dias);
    }

    [Fact]
    public void Solicitar_QuartaFracao_EhRecusada()
    {
        var existentes = new[]
        {
            NovasFerias(1, "2026-11-09", "2026-11-22", StatusSolicitacao.Aprovada), // 14
            NovasFerias(1, "2026-12-07", "2026-12-11", StatusSolicitacao.Aprovada), // 5
            NovasFerias(1, "2027-01-11", "2027-01-15", StatusSolicitacao.Aprovada)  // 5
        };

        var erro = Assert.Throws<DominioException>(() => Solicitar("2027-02-01", "2027-02-05", 5, existentes));

        Assert.Contains("no máximo 3", erro.Message);
    }

    [Fact]
    public void Solicitar_SemNenhumaFracaoDeQuatorzeDias_EhRecusada()
    {
        // 10 + 10: sobram só 10 dias, então nunca haverá uma fração de 14.
        var existente = NovasFerias(1, "2026-11-09", "2026-11-18", StatusSolicitacao.Aprovada);

        var erro = Assert.Throws<DominioException>(() => Solicitar("2026-12-07", "2026-12-16", 10, [existente]));

        Assert.Contains("no mínimo 14", erro.Message);
    }

    [Fact]
    public void Solicitar_PrimeiraFracaoPequena_EhAceitaSeAindaCabeUmaDeQuatorze()
    {
        // 10 dias agora; ainda sobram 20, onde cabe uma fração de 14.
        Assert.Equal(10, Solicitar("2026-11-09", "2026-11-18", 10).Dias);
    }

    [Fact]
    public void Solicitar_FeriasDePeriodoAquisitivoDiferente_NaoSomamSaldo()
    {
        // 30 dias já usados no período que começa em 11/03/2026 (anos completos = 2);
        // um pedido a partir de 11/03/2027 pertence ao próximo período (anos completos = 3).
        var existente = NovasFerias(1, "2026-11-09", "2026-12-08", StatusSolicitacao.Aprovada);

        Assert.Equal(14, Solicitar("2027-03-15", "2027-03-28", 14, [existente]).Dias);
    }

    [Theory]
    [InlineData("2024-03-11", "2025-03-10", 0)]
    [InlineData("2024-03-11", "2025-03-11", 1)]
    [InlineData("2024-03-11", "2026-11-09", 2)]
    [InlineData("2024-03-11", "2024-03-10", 0)] // antes da admissão
    [InlineData("2024-02-29", "2025-02-27", 0)] // admissão em 29/02: em ano não bissexto o aniversário vale em 28/02
    [InlineData("2024-02-29", "2025-02-28", 1)]
    public void AnosCompletosDeCasa_ContaAniversariosDeAdmissao(string admissao, string data, int esperado)
    {
        Assert.Equal(esperado, RegrasFerias.AnosCompletosDeCasa(D(admissao), D(data)));
    }

    [Fact]
    public void CobreDia_SoQuandoAprovadaEDentroDoPeriodo()
    {
        var aprovada = NovasFerias(1, "2026-11-09", "2026-11-22", StatusSolicitacao.Aprovada);
        var pendente = NovasFerias(1, "2026-11-09", "2026-11-22", StatusSolicitacao.Pendente);

        Assert.True(RegrasFerias.CobreDia(aprovada, D("2026-11-09")));
        Assert.True(RegrasFerias.CobreDia(aprovada, D("2026-11-22")));
        Assert.False(RegrasFerias.CobreDia(aprovada, D("2026-11-23")));
        Assert.False(RegrasFerias.CobreDia(aprovada, D("2026-11-08")));
        Assert.False(RegrasFerias.CobreDia(pendente, D("2026-11-10")));
    }

    [Fact]
    public void Decidir_Aprovar_MudaStatusERegistraODecisorNaObservacao()
    {
        var ferias = Solicitar("2026-11-09", "2026-11-22", 14, observacao: "Viagem");

        RegrasFerias.Decidir(ferias, aprovar: true, idDecisor: 5, observacao: null);

        Assert.Equal(StatusSolicitacao.Aprovada, ferias.Status);
        Assert.Equal("Viagem\n[Aprovada por FUNC-0005]", ferias.Observacao);
    }

    [Fact]
    public void Decidir_Rejeitar_ExigeMotivo()
    {
        var ferias = Solicitar("2026-11-09", "2026-11-22", 14);

        Assert.Throws<DominioException>(() => RegrasFerias.Decidir(ferias, false, 5, null));
        Assert.Equal(StatusSolicitacao.Pendente, ferias.Status);

        RegrasFerias.Decidir(ferias, false, 5, "Equipe reduzida no período");

        Assert.Equal(StatusSolicitacao.Rejeitada, ferias.Status);
        Assert.Equal("[Rejeitada por FUNC-0005] Equipe reduzida no período", ferias.Observacao);
    }

    [Fact]
    public void Decidir_OProprioFuncionario_EhRecusado()
    {
        var ferias = Solicitar("2026-11-09", "2026-11-22", 14);

        Assert.Throws<DominioException>(() => RegrasFerias.Decidir(ferias, true, 1, null));
    }

    [Fact]
    public void Decidir_SolicitacaoJaDecidida_EhRecusado()
    {
        var ferias = Solicitar("2026-11-09", "2026-11-22", 14);
        RegrasFerias.Decidir(ferias, true, 5, null);

        var erro = Assert.Throws<DominioException>(() => RegrasFerias.Decidir(ferias, false, 6, "Mudei de ideia"));

        Assert.Contains("já foi decidida", erro.Message);
    }
}
