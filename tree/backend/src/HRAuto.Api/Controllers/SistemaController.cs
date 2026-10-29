using HRAuto.Application.Sistema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Controllers;

/// <summary>Endpoints de diagnóstico, públicos (sem autenticação). Não devolvem nenhum dado de negócio.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/sistema")]
public class SistemaController : ControllerBase
{
    private readonly IConsultarStatusSistema _consultarStatus;
    private readonly IWebHostEnvironment _ambiente;

    public SistemaController(IConsultarStatusSistema consultarStatus, IWebHostEnvironment ambiente)
    {
        _consultarStatus = consultarStatus;
        _ambiente = ambiente;
    }

    /// <summary>Informa nome, versão, ambiente e a hora oficial (Brasília) do servidor.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(StatusSistemaDto), StatusCodes.Status200OK)]
    public ActionResult<StatusSistemaDto> ObterStatus() =>
        Ok(_consultarStatus.Executar(_ambiente.EnvironmentName));
}
