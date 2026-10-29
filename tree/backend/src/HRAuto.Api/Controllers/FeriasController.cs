using HRAuto.Application.Afastamentos;
using HRAuto.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Controllers;

/// <summary>
/// Férias (RF26, RF27). O funcionário solicita e cancela (enquanto Pendente); o gestor da equipe e o RH/Admin decidem.
/// A permissão de cada operação é conferida no caso de uso, pois depende de quem é o gestor de quem.
/// </summary>
[Route("api/v1/ferias")]
public class FeriasController : ApiControllerBase
{
    /// <summary>Lista solicitações. <c>escopo</c>: minhas (padrão), equipe (gestor) ou todas (RH/Admin).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaDto<FeriasDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Listar(
        [FromQuery] string? escopo, [FromQuery] string? codigo, [FromQuery] string? status,
        [FromQuery] int? pagina, [FromQuery] int? tamanho, [FromServices] ListarFerias caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, new FiltroListaFeriasDto(escopo, codigo, status, pagina, tamanho), ct));
    }

    /// <summary>Solicita as próprias férias. Nasce Pendente e o gestor (ou o RH) é avisado.</summary>
    [HttpPost]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(typeof(FeriasDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Solicitar([FromBody] SolicitarFeriasDto dto, [FromServices] SolicitarFerias caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, dto, ct), StatusCodes.Status201Created);
    }

    /// <summary>Aprova ou rejeita uma solicitação pendente (gestor direto, RH ou Admin).</summary>
    [HttpPost("{id:int}/decisao")]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(typeof(FeriasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Decidir(int id, [FromBody] DecidirFeriasDto dto, [FromServices] DecidirFerias caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, id, dto, ct));
    }

    /// <summary>Cancela a própria solicitação, desde que ainda esteja Pendente.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Cancelar(int id, [FromServices] CancelarSolicitacaoFerias caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, id, ct));
    }
}
