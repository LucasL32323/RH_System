using HRAuto.Domain.Comunicacao;
using HRAuto.Domain.Common;
using HRAuto.Domain.Enums;
using static HRAuto.Tests.Domain.Apoio;

namespace HRAuto.Tests.Domain;

/// <summary>RF24 e RF26: avisos do ajuste de banco de horas e do pedido de férias.</summary>
public class NotificacoesJornadaTests
{
    private static readonly DateTime Agora = new(2026, 10, 21, 8, 0, 0);

    [Fact]
    public void AjusteBancoHoras_Credito_AvisaOFuncionario()
    {
        var ajuste = HRAuto.Domain.Jornada.RegrasBancoHoras.AjusteManual(4, D("2026-10-20"), "Credito", 2.5, "Compensação autorizada", D("2026-10-21"));

        var aviso = NotificacoesJornada.AjusteBancoHoras(ajuste, Agora);

        Assert.Equal(4, aviso.IdFuncionario);
        Assert.Equal(TiposNotificacao.Aviso, aviso.Tipo);
        Assert.Equal("Ajuste no banco de horas", aviso.Titulo);
        Assert.Contains("crédito de 2h30min", aviso.Mensagem);
        Assert.Contains("20/10/2026", aviso.Mensagem);
        Assert.Equal(Agora, aviso.DataEnvio);
        Assert.False(aviso.Lida);
    }

    [Fact]
    public void AjusteBancoHoras_Debito_UsaOTermoDebito()
    {
        var ajuste = HRAuto.Domain.Jornada.RegrasBancoHoras.AjusteManual(4, D("2026-10-20"), "Debito", 0.25, "Saída antecipada", D("2026-10-21"));

        var aviso = NotificacoesJornada.AjusteBancoHoras(ajuste, Agora);

        Assert.Contains("débito de 0h15min", aviso.Mensagem);
    }

    [Fact]
    public void NovaSolicitacaoDeFerias_InformaCodigoPeriodoEDias_SemNomeDaPessoa()
    {
        var ferias = NovasFerias(4, "2026-11-16", "2026-11-30", StatusSolicitacao.Pendente);

        var aviso = NotificacoesJornada.NovaSolicitacaoDeFerias(3, ferias, Agora);

        Assert.Equal(3, aviso.IdFuncionario);
        Assert.Equal(TiposNotificacao.Ferias, aviso.Tipo);
        Assert.Equal("Nova solicitação de férias", aviso.Titulo);
        Assert.Contains("FUNC-0004", aviso.Mensagem);
        Assert.Contains("16/11/2026", aviso.Mensagem);
        Assert.Contains("30/11/2026", aviso.Mensagem);
        Assert.Contains("15 dias", aviso.Mensagem);
        Assert.False(aviso.Lida);
    }

    [Fact]
    public void DestinatarioInvalido_Recusa()
    {
        var ferias = NovasFerias(4, "2026-11-16", "2026-11-30", StatusSolicitacao.Pendente);
        Assert.Throws<DominioException>(() => NotificacoesJornada.NovaSolicitacaoDeFerias(0, ferias, Agora));
    }
}
