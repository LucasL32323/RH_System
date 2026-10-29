using HRAuto.Application.Common;
using HRAuto.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Middleware;

/// <summary>
/// Última rede de proteção: transforma exceções não tratadas em ProblemDetails (RFC 7807) sem vazar detalhes internos.
/// - DominioException (regra de negócio violada que escapou do caso de uso) -> 400
/// - ConflitoDePersistenciaException (índice único / concorrência) -> 409
/// - BadHttpRequestException (corpo grande demais, JSON quebrado) -> o status dela, com mensagem genérica
/// - qualquer outra -> 500 genérico; o stack trace vai só para o log, com o traceId que também volta ao cliente.
/// </summary>
public sealed class TratadorDeExcecoes : IExceptionHandler
{
    private readonly ILogger<TratadorDeExcecoes> _log;

    public TratadorDeExcecoes(ILogger<TratadorDeExcecoes> log) => _log = log;

    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excecao, CancellationToken cancelamento)
    {
        var (status, titulo, detalhe) = excecao switch
        {
            DominioException e => (StatusCodes.Status400BadRequest, "Dados inválidos", e.Message),
            ConflitoDePersistenciaException e => (StatusCodes.Status409Conflict, "Conflito", e.Message),
            BadHttpRequestException e => (e.StatusCode, "Requisição inválida", "Não foi possível ler a requisição."),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado. Tente novamente em instantes.")
        };

        if (status >= 500)
            _log.LogError(excecao, "Erro não tratado em {Metodo} {Caminho} (traceId {TraceId})",
                contexto.Request.Method, contexto.Request.Path, contexto.TraceIdentifier);
        else
            _log.LogWarning("Requisição recusada ({Status}) em {Metodo} {Caminho}: {Tipo}",
                status, contexto.Request.Method, contexto.Request.Path, excecao.GetType().Name);

        var problema = new ProblemDetails { Status = status, Title = titulo, Detail = detalhe };
        problema.Extensions["traceId"] = contexto.TraceIdentifier;

        contexto.Response.StatusCode = status;
        await contexto.Response.WriteAsJsonAsync(problema, options: null, contentType: "application/problem+json", cancellationToken: cancelamento);
        return true;
    }
}
