using HRAuto.Api.Seguranca;
using HRAuto.Application.Common;
using HRAuto.Application.Disciplina;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Controllers;

/// <summary>
/// Advertências (RF33), régua de consequências (RF34) e semáforo (RF47).
/// Registrar, cancelar e listar tudo é do RH/Admin; a situação (semáforo e histórico) é do próprio funcionário,
/// do gestor da equipe e do RH/Admin. A sugestão da régua só aparece para RH/Admin.
/// </summary>
[Route("api/v1/advertencias")]
public class AdvertenciasController : ApiControllerBase
{
    /// <summary>Lista paginada de todas as advertências, com filtro por código do funcionário e status.</summary>
    [HttpGet]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [ProducesResponseType(typeof(PaginaDto<AdvertenciaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Listar(
        [FromQuery] string? codigo, [FromQuery] string? status, [FromQuery] int? pagina, [FromQuery] int? tamanho,
        [FromServices] ConsultarAdvertencias caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, new FiltroListaAdvertenciasDto(codigo, status, pagina, tamanho), ct));
    }

    /// <summary>Registra a advertência. Devolve também o semáforo e a sugestão da régua já atualizados.</summary>
    [HttpPost]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(typeof(AdvertenciaRegistradaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Registrar([FromBody] RegistrarAdvertenciaDto dto, [FromServices] RegistrarAdvertencia caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, dto, ct), StatusCodes.Status201Created);
    }

    /// <summary>Cancela a advertência (motivo obrigatório). Ela sai do semáforo e da régua, mas fica no histórico.</summary>
    [HttpPost("{id:int}/cancelamento")]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(typeof(AdvertenciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Cancelar(int id, [FromBody] CancelarAdvertenciaDto dto, [FromServices] CancelarAdvertencia caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, id, dto, ct));
    }

    /// <summary>Semáforo e histórico de quem está logado.</summary>
    [HttpGet("situacao")]
    [ProducesResponseType(typeof(SituacaoDisciplinarDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> MinhaSituacao([FromServices] ConsultarSituacaoDisciplinar caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, null, ct));
    }

    /// <summary>Semáforo e histórico de um funcionário (gestor da equipe, RH ou Admin).</summary>
    [HttpGet("situacao/{codigo}")]
    [ProducesResponseType(typeof(SituacaoDisciplinarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Situacao(string codigo, [FromServices] ConsultarSituacaoDisciplinar caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, codigo, ct));
    }
}
