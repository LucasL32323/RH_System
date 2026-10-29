using HRAuto.Application.Autenticacao;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Controllers;

/// <summary>Dados de quem está logado: cabeçalho do dashboard (RF45) com código FUNC-0001, cargo e perfil.</summary>
[Route("api/v1/eu")]
public class EuController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(MeusDadosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Obter([FromServices] ConsultarMeusDados caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, ct));
    }
}
