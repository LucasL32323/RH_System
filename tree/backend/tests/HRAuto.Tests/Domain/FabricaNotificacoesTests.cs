using HRAuto.Domain.Comunicacao;
using HRAuto.Domain.Common;
using HRAuto.Domain.Disciplina;
using HRAuto.Domain.Enums;
using static HRAuto.Tests.Domain.Apoio;

namespace HRAuto.Tests.Domain;

/// <summary>RF25 e RF36: notificações geradas pelas regras (hora extra, régua, decisões).</summary>
public class FabricaNotificacoesTests
{
    [Fact]
    public void HoraExtra_NotificaOGestor_ComCodigoEDuracao()
    {
        var n = FabricaNotificacoes.HoraExtra(9, 7, TimeSpan.FromMinutes(150), D("2026-10-20"), Agora);

        Assert.Equal(9, n.IdFuncionario); // destinatário: o gestor
        Assert.Equal(TiposNotificacao.HoraExtra, n.Tipo);
        Assert.Contains("FUNC-0007", n.Mensagem);
        Assert.Contains("2h30min", n.Mensagem);
        Assert.Contains("20/10/2026", n.Mensagem);
        Assert.False(n.Lida);
        Assert.Equal(Agora, n.DataEnvio);
    }

    [Fact]
    public void SugestaoDaRegua_Nenhuma_NaoCriaNotificacao()
    {
        var sugestao = ReguaConsequencias.Avaliar([]);

        Assert.Null(FabricaNotificacoes.SugestaoDaRegua(4, 7, sugestao, Agora));
    }

    [Fact]
    public void SugestaoDaRegua_Suspensao_NotificaORH()
    {
        var sugestao = ReguaConsequencias.Avaliar(
        [
            NovaAdvertencia(TipoAdvertencia.Leve, "2026-09-01"),
            NovaAdvertencia(TipoAdvertencia.Leve, "2026-09-10"),
            NovaAdvertencia(TipoAdvertencia.Leve, "2026-09-20")
        ]);

        var n = FabricaNotificacoes.SugestaoDaRegua(4, 7, sugestao, Agora);

        Assert.NotNull(n);
        Assert.Equal(4, n!.IdFuncionario);
        Assert.Equal("Sugestão de suspensão", n.Titulo);
        Assert.Contains("FUNC-0007", n.Mensagem);
    }

    [Fact]
    public void DecisaoDeFerias_InformaOFuncionario()
    {
        var ferias = NovasFerias(7, "2026-11-09", "2026-11-22", StatusSolicitacao.Aprovada);

        var n = FabricaNotificacoes.DecisaoDeFerias(ferias, Agora);

        Assert.Equal(7, n.IdFuncionario);
        Assert.Equal("Férias aprovada", n.Titulo);
        Assert.Contains("09/11/2026 a 22/11/2026", n.Mensagem);
    }

    [Fact]
    public void DecisaoDeFerias_AindaPendente_EhRecusada()
    {
        var ferias = NovasFerias(7, "2026-11-09", "2026-11-22", StatusSolicitacao.Pendente);

        Assert.Throws<DominioException>(() => FabricaNotificacoes.DecisaoDeFerias(ferias, Agora));
    }

    [Fact]
    public void DecisaoDeManutencao_Rejeitada_InformaOFuncionario()
    {
        var pedido = new HRAuto.Domain.Entities.ManutencaoPonto { IdFuncionario = 7, Status = StatusSolicitacao.Rejeitada };

        var n = FabricaNotificacoes.DecisaoDeManutencao(pedido, Agora);

        Assert.Equal(7, n.IdFuncionario);
        Assert.Equal(TiposNotificacao.ManutencaoPonto, n.Tipo);
        Assert.Contains("rejeitada", n.Mensagem);
    }

    [Fact]
    public void Notificacao_SemDestinatario_EhRecusada()
    {
        Assert.Throws<DominioException>(() =>
            FabricaNotificacoes.HoraExtra(0, 7, TimeSpan.FromHours(3), D("2026-10-20"), Agora));
    }
}
