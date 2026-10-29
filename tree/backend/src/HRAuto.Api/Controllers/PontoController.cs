using HRAuto.Application.RegistroPonto;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Controllers;

/// <summary>
/// Registro de ponto (RF17 a RF21). Qualquer perfil logado bate o próprio ponto; o horário é sempre o do servidor
/// (Brasília), nunca o do aparelho, exceto na sincronização offline (RF19).
/// </summary>
[Route("api/v1/ponto")]
public class PontoController : ApiControllerBase
{
    /// <summary>Batidas de hoje, próxima batida esperada e hora do servidor (para o botão do app).</summary>
    [HttpGet("hoje")]
    [ProducesResponseType(typeof(StatusPontoHojeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Hoje([FromServices] ConsultarStatusPontoHoje caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, ct));
    }

    /// <summary>Registra a próxima batida do dia (entrada, saída p/ intervalo, retorno ou saída). Sem corpo.</summary>
    [HttpPost("batidas")]
    [ProducesResponseType(typeof(BatidaRegistradaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> RegistrarBatida([FromServices] RegistrarBatida caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, ct), StatusCodes.Status201Created);
    }

    /// <summary>
    /// Envia a fila de batidas feitas sem internet (RF19). Reenviar a mesma fila não duplica nada (RNF09).
    /// Cada item volta com a situação: Aplicada, JaRegistrada ou Recusada (com o motivo).
    /// </summary>
    [HttpPost("batidas/sincronizar")]
    [RequestSizeLimit(64 * 1024)]
    [ProducesResponseType(typeof(IReadOnlyList<ResultadoBatidaOfflineDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Sincronizar([FromBody] SincronizarBatidasDto dto, [FromServices] SincronizarBatidas caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, dto, ct));
    }

    /// <summary>
    /// Espelho de ponto (RF21). Sem parâmetros: o mês corrente do próprio usuário. Com <c>codigo</c> (FUNC-0007):
    /// o gestor vê a equipe e RH/Admin veem qualquer funcionário; os demais recebem 403.
    /// </summary>
    [HttpGet("espelho")]
    [ProducesResponseType(typeof(EspelhoPontoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Espelho(
        [FromQuery] string? codigo,
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromServices] ConsultarEspelhoPonto caso,
        CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, codigo, de, ate, ct));
    }
}
