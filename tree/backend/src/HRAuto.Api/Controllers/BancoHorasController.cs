using HRAuto.Api.Seguranca;
using HRAuto.Application.BancoDeHoras;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Controllers;

/// <summary>
/// Banco de horas (RF24). Saldo e extrato: o próprio funcionário, o gestor da equipe e o RH/Admin.
/// Ajuste manual: somente RH/Admin, com observação obrigatória.
/// </summary>
[Route("api/v1/banco-horas")]
public class BancoHorasController : ApiControllerBase
{
    /// <summary>Saldo e extrato de quem está logado. Sem período: últimos 90 dias.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(BancoHorasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Meu(
        [FromQuery] DateOnly? inicio, [FromQuery] DateOnly? fim, [FromServices] ConsultarBancoHoras caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, null, inicio, fim, ct));
    }

    /// <summary>Saldo e extrato de um funcionário (gestor da equipe, RH ou Admin).</summary>
    [HttpGet("{codigo}")]
    [ProducesResponseType(typeof(BancoHorasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DoFuncionario(
        string codigo, [FromQuery] DateOnly? inicio, [FromQuery] DateOnly? fim, [FromServices] ConsultarBancoHoras caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, codigo, inicio, fim, ct));
    }

    /// <summary>Lança um crédito ou débito manual. Devolve o lançamento e o novo saldo.</summary>
    [HttpPost("ajustes")]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(typeof(AjusteRegistradoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Ajustar([FromBody] AjustarBancoHorasDto dto, [FromServices] AjustarBancoHoras caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, dto, ct), StatusCodes.Status201Created);
    }
}
