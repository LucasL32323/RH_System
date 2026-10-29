using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Domain.Comunicacao;

/// <summary>
/// Notificações do banco de horas e do pedido de férias (RF24, RF26, RF36). A decisão sobre as férias
/// continua em <see cref="FabricaNotificacoes.DecisaoDeFerias"/>. Só entram códigos (FUNC-0001), nunca dados pessoais.
/// </summary>
public static class NotificacoesJornada
{
    /// <summary>RF24: avisa o funcionário que o RH lançou um ajuste no banco de horas dele.</summary>
    public static Notificacao AjusteBancoHoras(BancoHoras ajuste, DateTime agora)
    {
        var sinal = ajuste.Tipo == TiposBancoHoras.Credito ? "crédito" : "débito";
        return Criar(ajuste.IdFuncionario, TiposNotificacao.Aviso, "Ajuste no banco de horas",
            $"O RH lançou um {sinal} de {Horas(ajuste.Quantidade)} no seu banco de horas em {ajuste.Data:dd/MM/yyyy}.", agora);
    }

    /// <summary>RF26: avisa o gestor (ou o RH, se não houver gestor) que há uma solicitação de férias para decidir.</summary>
    public static Notificacao NovaSolicitacaoDeFerias(int idDestinatario, Ferias ferias, DateTime agora) =>
        Criar(idDestinatario, TiposNotificacao.Ferias, "Nova solicitação de férias",
            $"{CodigoFuncionario.Formatar(ferias.IdFuncionario)} solicitou férias de {ferias.DataInicio:dd/MM/yyyy} " +
            $"a {ferias.DataFim:dd/MM/yyyy} ({ferias.Dias} dias).", agora);

    /// <summary>RF22: avisa o gestor (ou o RH, se não houver gestor) que há um pedido de manutenção de ponto para analisar.</summary>
    public static Notificacao NovaManutencaoDePonto(int idDestinatario, ManutencaoPonto pedido, DateOnly dataPonto, DateTime agora) =>
        Criar(idDestinatario, TiposNotificacao.ManutencaoPonto, "Novo pedido de manutenção de ponto",
            $"{CodigoFuncionario.Formatar(pedido.IdFuncionario)} pediu a manutenção do ponto de {dataPonto:dd/MM/yyyy}.", agora);

    /// <summary>RF29: avisa o funcionário que o holerite do mês está disponível (ou foi substituído por uma nova versão).</summary>
    public static Notificacao HoleritePublicado(Holerite holerite, bool substituido, DateTime agora) =>
        Criar(holerite.IdFuncionario, TiposNotificacao.Aviso,
            substituido ? "Holerite atualizado" : "Novo holerite disponível",
            substituido
                ? $"O holerite de {holerite.Mes:D2}/{holerite.Ano} foi atualizado pelo RH."
                : $"O holerite de {holerite.Mes:D2}/{holerite.Ano} já está disponível.", agora);

    private static Notificacao Criar(int idDestinatario, string tipo, string titulo, string mensagem, DateTime agora)
    {
        if (idDestinatario <= 0) throw new DominioException("O destinatário da notificação é obrigatório.");
        return new Notificacao
        {
            IdFuncionario = idDestinatario,
            Tipo = tipo,
            Titulo = titulo,
            Mensagem = mensagem,
            DataEnvio = agora,
            Lida = false
        };
    }

    // 2,5 -> "2h30min"
    private static string Horas(double horas)
    {
        var minutos = (int)Math.Round(horas * 60, MidpointRounding.AwayFromZero);
        return $"{minutos / 60}h{minutos % 60:D2}min";
    }
}
