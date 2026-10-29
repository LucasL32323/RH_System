using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Entities;

namespace HRAuto.Application.Comunicacao;

// ---------- Saída ----------

public sealed record NotificacaoDto(int Id, string Tipo, string Titulo, string Mensagem, DateTime DataEnvio, bool Lida);

public sealed record QuantidadeNaoLidasDto(int Quantidade);

public sealed record MarcacaoDeLeituraDto(int Marcadas);

internal static class MapeadorNotificacoes
{
    public static NotificacaoDto Notificacao(Notificacao n) =>
        new(n.Id, n.Tipo, n.Titulo, n.Mensagem, n.DataEnvio, n.Lida);
}

/// <summary>RF36: notificações do próprio usuário, da mais recente para a mais antiga. Cada pessoa só enxerga as suas.</summary>
public sealed class ListarNotificacoes
{
    private readonly INotificacaoRepositorio _notificacoes;

    public ListarNotificacoes(INotificacaoRepositorio notificacoes) => _notificacoes = notificacoes;

    public async Task<Resultado<PaginaDto<NotificacaoDto>>> ExecutarAsync(
        Solicitante solicitante, bool somenteNaoLidas, int? pagina, int? tamanho, CancellationToken cancelamento = default)
    {
        var paginacao = Paginacao.De(pagina, tamanho);
        var resultado = await _notificacoes.ListarAsync(solicitante.IdFuncionario, somenteNaoLidas, paginacao, cancelamento);
        var itens = resultado.Itens.Select(MapeadorNotificacoes.Notificacao).ToList();
        return Resultado<PaginaDto<NotificacaoDto>>.Ok(PaginaDto<NotificacaoDto>.De(itens, paginacao, resultado.Total));
    }
}

/// <summary>Número do selo (badge) do sino de notificações.</summary>
public sealed class ContarNotificacoesNaoLidas
{
    private readonly INotificacaoRepositorio _notificacoes;

    public ContarNotificacoesNaoLidas(INotificacaoRepositorio notificacoes) => _notificacoes = notificacoes;

    public async Task<Resultado<QuantidadeNaoLidasDto>> ExecutarAsync(Solicitante solicitante, CancellationToken cancelamento = default) =>
        Resultado<QuantidadeNaoLidasDto>.Ok(new QuantidadeNaoLidasDto(
            await _notificacoes.ContarNaoLidasAsync(solicitante.IdFuncionario, cancelamento)));
}

/// <summary>
/// Marca uma notificação como lida. Repetir a chamada é seguro (idempotente). Notificação de outra pessoa responde
/// "não encontrada", igual a uma que não existe, para não revelar o que os outros receberam.
/// </summary>
public sealed class MarcarNotificacaoComoLida
{
    private readonly INotificacaoRepositorio _notificacoes;
    private readonly IUnidadeDeTrabalho _unidade;

    public MarcarNotificacaoComoLida(INotificacaoRepositorio notificacoes, IUnidadeDeTrabalho unidade)
    {
        _notificacoes = notificacoes;
        _unidade = unidade;
    }

    public async Task<Resultado<NotificacaoDto>> ExecutarAsync(Solicitante solicitante, int idNotificacao, CancellationToken cancelamento = default)
    {
        var notificacao = await _notificacoes.ObterParaEdicaoAsync(idNotificacao, cancelamento);
        if (notificacao is null || notificacao.IdFuncionario != solicitante.IdFuncionario)
            return Resultado<NotificacaoDto>.Falha(TipoErro.NaoEncontrado, "Notificação não encontrada.");

        if (!notificacao.Lida)
        {
            notificacao.Lida = true;
            await _unidade.SalvarAsync(cancelamento);
        }

        return Resultado<NotificacaoDto>.Ok(MapeadorNotificacoes.Notificacao(notificacao));
    }
}

/// <summary>Marca todas as notificações não lidas do usuário como lidas e informa quantas eram.</summary>
public sealed class MarcarTodasNotificacoesComoLidas
{
    private readonly INotificacaoRepositorio _notificacoes;
    private readonly IUnidadeDeTrabalho _unidade;

    public MarcarTodasNotificacoesComoLidas(INotificacaoRepositorio notificacoes, IUnidadeDeTrabalho unidade)
    {
        _notificacoes = notificacoes;
        _unidade = unidade;
    }

    public async Task<Resultado<MarcacaoDeLeituraDto>> ExecutarAsync(Solicitante solicitante, CancellationToken cancelamento = default)
    {
        var naoLidas = await _notificacoes.ListarNaoLidasParaEdicaoAsync(solicitante.IdFuncionario, cancelamento);
        foreach (var notificacao in naoLidas) notificacao.Lida = true;

        if (naoLidas.Count > 0) await _unidade.SalvarAsync(cancelamento);
        return Resultado<MarcacaoDeLeituraDto>.Ok(new MarcacaoDeLeituraDto(naoLidas.Count));
    }
}
