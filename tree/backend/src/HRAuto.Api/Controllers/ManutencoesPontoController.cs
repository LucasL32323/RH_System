using HRAuto.Application.Common;
using HRAuto.Application.ManutencaoDePonto;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Controllers;

/// <summary>
/// Manutenção de ponto (RF22, RF23). O colaborador pede o ajuste/abono de um dia com justificativa; o gestor da equipe e o
/// RH/Admin aprovam (podendo corrigir as batidas) ou rejeitam. A permissão de cada operação é conferida no caso de uso,
/// pois depende de quem é o gestor de quem.
/// </summary>
[Route("api/v1/manutencoes-ponto")]
public class ManutencoesPontoController : ApiControllerBase
{
    /// <summary>Lista pedidos. <c>escopo</c>: minhas (padrão), equipe (gestor) ou todas (RH/Admin).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaDto<ManutencaoPontoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Listar(
        [FromQuery] string? escopo, [FromQuery] string? codigo, [FromQuery] string? status,
        [FromQuery] int? pagina, [FromQuery] int? tamanho, [FromServices] ListarManutencoesPonto caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, new FiltroListaManutencoesDto(escopo, codigo, status, pagina, tamanho), ct));
    }

    /// <summary>Pede a manutenção do ponto de um dia (justificativa obrigatória). Nasce Pendente e o gestor (ou o RH) é avisado.</summary>
    [HttpPost]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(typeof(ManutencaoPontoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Solicitar(
        [FromBody] SolicitarManutencaoPontoDto dto, [FromServices] SolicitarManutencaoPonto caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, dto, ct), StatusCodes.Status201Created);
    }

    /// <summary>Aprova (com ou sem batidas corrigidas) ou rejeita um pedido pendente (gestor direto, RH ou Admin).</summary>
    [HttpPost("{id:int}/decisao")]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(typeof(ManutencaoDecididaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Decidir(
        int id, [FromBody] DecidirManutencaoPontoDto dto, [FromServices] DecidirManutencaoPonto caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, id, dto, ct));
    }
}
