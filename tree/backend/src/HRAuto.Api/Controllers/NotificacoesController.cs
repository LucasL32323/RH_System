using HRAuto.Application.Common;
using HRAuto.Application.Comunicacao;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Controllers;

/// <summary>Notificações (RF25, RF36). Cada pessoa vê e marca como lidas somente as suas.</summary>
[Route("api/v1/notificacoes")]
public class NotificacoesController : ApiControllerBase
{
    /// <summary>Minhas notificações, da mais recente para a mais antiga. <c>somenteNaoLidas=true</c> filtra as pendentes.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaDto<NotificacaoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> Listar(
        [FromQuery] bool somenteNaoLidas, [FromQuery] int? pagina, [FromQuery] int? tamanho,
        [FromServices] ListarNotificacoes caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, somenteNaoLidas, pagina, tamanho, ct));
    }

    /// <summary>Quantidade de notificações não lidas (selo do sino).</summary>
    [HttpGet("nao-lidas/quantidade")]
    [ProducesResponseType(typeof(QuantidadeNaoLidasDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> ContarNaoLidas([FromServices] ContarNotificacoesNaoLidas caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, ct));
    }

    /// <summary>Marca uma notificação como lida (repetir é seguro).</summary>
    [HttpPost("{id:int}/leitura")]
    [ProducesResponseType(typeof(NotificacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MarcarComoLida(int id, [FromServices] MarcarNotificacaoComoLida caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, id, ct));
    }

    /// <summary>Marca todas as minhas notificações como lidas e informa quantas eram.</summary>
    [HttpPost("leitura")]
    [ProducesResponseType(typeof(MarcacaoDeLeituraDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> MarcarTodasComoLidas([FromServices] MarcarTodasNotificacoesComoLidas caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, ct));
    }
}
