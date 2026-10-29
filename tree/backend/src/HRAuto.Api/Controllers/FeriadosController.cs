using HRAuto.Api.Seguranca;
using HRAuto.Application.Calendario;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Controllers;

/// <summary>Feriados (RF28). Qualquer pessoa logada consulta; cadastrar e excluir é do RH/Admin.</summary>
[Route("api/v1/feriados")]
public class FeriadosController : ApiControllerBase
{
    /// <summary>Feriados de um ano (padrão: o ano atual), em ordem de data.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FeriadoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Listar([FromQuery] int? ano, [FromServices] ListarFeriados caso, CancellationToken ct) =>
        Responder(await caso.ExecutarAsync(ano, ct));

    [HttpPost]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(typeof(FeriadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Criar([FromBody] CriarFeriadoDto dto, [FromServices] GerenciarFeriados caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.CriarAsync(solicitante, dto, ct), StatusCodes.Status201Created);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Excluir(int id, [FromServices] GerenciarFeriados caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExcluirAsync(solicitante, id, ct));
    }
}
