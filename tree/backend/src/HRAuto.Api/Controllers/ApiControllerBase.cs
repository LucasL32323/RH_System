using System.Diagnostics.CodeAnalysis;
using HRAuto.Api.Seguranca;
using HRAuto.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Controllers;

/// <summary>
/// Base dos controllers autenticados. Converte o <see cref="Resultado{T}"/> da Application em resposta HTTP
/// (ProblemDetails, RFC 7807) para que o app Flutter trate todos os erros do mesmo jeito.
/// A segurança é "fechada por padrão": a política de fallback exige login em tudo que não tiver [AllowAnonymous].
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Quem está chamando, lido do token. Falso = token válido mas com claims incompletas (responder 401).</summary>
    protected bool TentarObterSolicitante([NotNullWhen(true)] out Solicitante? solicitante) =>
        User.TentarObterSolicitante(out solicitante);

    protected ActionResult SessaoInvalida() =>
        Falha(TipoErro.NaoAutenticado, "Sessão inválida. Entre novamente.");

    /// <summary>200 com o valor, ou o erro correspondente.</summary>
    protected ActionResult Responder<T>(Resultado<T> resultado) =>
        resultado.Sucesso ? Ok(resultado.Valor) : Falha(resultado.TipoDoErro, resultado.Erro);

    /// <summary>Status de sucesso escolhido pelo controller (ex.: 201), ou o erro correspondente.</summary>
    protected ActionResult Responder<T>(Resultado<T> resultado, int statusSucesso) =>
        resultado.Sucesso ? StatusCode(statusSucesso, resultado.Valor) : Falha(resultado.TipoDoErro, resultado.Erro);

    /// <summary>204 sem corpo, ou o erro correspondente.</summary>
    protected ActionResult Responder(Resultado resultado) =>
        resultado.Sucesso ? NoContent() : Falha(resultado.TipoDoErro, resultado.Erro);

    protected ActionResult Falha(TipoErro? tipo, string? detalhe)
    {
        var (status, titulo) = tipo switch
        {
            TipoErro.Validacao => (StatusCodes.Status400BadRequest, "Dados inválidos"),
            TipoErro.NaoAutenticado => (StatusCodes.Status401Unauthorized, "Não autenticado"),
            TipoErro.Proibido => (StatusCodes.Status403Forbidden, "Acesso negado"),
            TipoErro.NaoEncontrado => (StatusCodes.Status404NotFound, "Não encontrado"),
            TipoErro.Conflito => (StatusCodes.Status409Conflict, "Conflito"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno")
        };

        if (status == StatusCodes.Status401Unauthorized)
            Response.Headers.WWWAuthenticate = "Bearer";

        // Erro interno nunca devolve detalhe vindo da Application.
        return Problem(
            title: titulo,
            detail: status == StatusCodes.Status500InternalServerError ? "Ocorreu um erro inesperado." : detalhe,
            statusCode: status);
    }
}
