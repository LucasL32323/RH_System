using System.Globalization;
using HRAuto.Domain.Common;
using HRAuto.Domain.Disciplina;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Domain.Comunicacao;

/// <summary>
/// Cria as notificações geradas pelas regras de negócio (RF25, RF36). A API as grava; o app as lista.
/// A data/hora recebida deve vir do relógio do servidor (Brasília).
/// </summary>
public static class FabricaNotificacoes
{
    /// <summary>RF25: avisa o gestor que o funcionário passou do limite diário de horas extras.</summary>
    public static Notificacao HoraExtra(int idGestor, int idFuncionario, TimeSpan horasExtras, DateOnly dia, DateTime agora) =>
        Criar(idGestor, TiposNotificacao.HoraExtra, "Hora extra acima do limite",
            $"{CodigoFuncionario.Formatar(idFuncionario)} fez {Duracao(horasExtras)} de hora extra em {dia:dd/MM/yyyy}.", agora);

    /// <summary>RF34: avisa o RH que a régua sugere uma consequência. Para sugestão "Nenhuma" não se cria notificação.</summary>
    public static Notificacao? SugestaoDaRegua(int idRh, int idFuncionario, SugestaoConsequencia sugestao, DateTime agora)
    {
        if (sugestao.Acao == AcaoSugerida.Nenhuma) return null;

        var titulo = sugestao.Acao == AcaoSugerida.Suspensao ? "Sugestão de suspensão" : "Sugestão de justa causa";
        return Criar(idRh, TiposNotificacao.Advertencia, titulo,
            $"{CodigoFuncionario.Formatar(idFuncionario)}: {sugestao.Mensagem}", agora);
    }

    /// <summary>RF36: informa o funcionário da decisão sobre a solicitação de férias.</summary>
    public static Notificacao DecisaoDeFerias(Ferias ferias, DateTime agora)
    {
        var resultado = ResultadoDaDecisao(ferias.Status);
        return Criar(ferias.IdFuncionario, TiposNotificacao.Ferias, $"Férias {resultado}",
            $"Sua solicitação de férias de {ferias.DataInicio:dd/MM/yyyy} a {ferias.DataFim:dd/MM/yyyy} foi {resultado}.", agora);
    }

    /// <summary>RF36: informa o funcionário da decisão sobre o pedido de manutenção de ponto.</summary>
    public static Notificacao DecisaoDeManutencao(ManutencaoPonto pedido, DateTime agora)
    {
        var resultado = ResultadoDaDecisao(pedido.Status);
        return Criar(pedido.IdFuncionario, TiposNotificacao.ManutencaoPonto, $"Manutenção de ponto {resultado}",
            $"Seu pedido de manutenção de ponto foi {resultado}.", agora);
    }

    // Aprovada -> "aprovada", Rejeitada -> "rejeitada". Pedido ainda pendente não gera notificação.
    private static string ResultadoDaDecisao(string status) => status switch
    {
        StatusSolicitacao.Aprovada => "aprovada",
        StatusSolicitacao.Rejeitada => "rejeitada",
        _ => throw new DominioException("A solicitação ainda não foi decidida.")
    };

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

    // 2,5 h -> "2h30min"
    private static string Duracao(TimeSpan duracao)
    {
        var minutos = (int)Math.Round(duracao.TotalMinutes, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"{minutos / 60}h{minutos % 60:D2}min");
    }
}
