using HRAuto.Api.Extensoes;
using HRAuto.Application.Autenticacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HRAuto.Api.Controllers;

/// <summary>Resposta simples com uma mensagem para o usuário.</summary>
public sealed record MensagemDto(string Mensagem);

/// <summary>
/// Autenticação (RF01 a RF04). Login, "esqueci a senha" e redefinição são anônimos e têm limite de tentativas
/// por IP (força bruta) e corpo pequeno. Logout e troca de senha exigem token.
/// </summary>
[Route("api/v1/auth")]
public class AuthController : ApiControllerBase
{
    private const int LimiteCorpoBytes = 16 * 1024;

    /// <summary>
    /// Entra com e-mail ou código do funcionário (FUNC-0001) e senha e recebe o token JWT (válido por 8 h) com o perfil.
    /// Envie o identificador em "login" (ou, por compatibilidade, em "email").
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasLimite.Autenticacao)]
    [RequestSizeLimit(LimiteCorpoBytes)]
    [ProducesResponseType(typeof(LoginRespostaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Login([FromBody] CredenciaisDto credenciais, [FromServices] AutenticarUsuario caso, CancellationToken ct) =>
        Responder(await caso.ExecutarAsync(credenciais, ct));

    /// <summary>Registra o logout na auditoria. O app apaga o token do aparelho (RF02).</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Logout([FromServices] EncerrarSessao caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, ct));
    }

    /// <summary>O usuário logado troca a própria senha informando a atual (RF04).</summary>
    [HttpPost("alterar-senha")]
    [RequestSizeLimit(LimiteCorpoBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> AlterarSenha([FromBody] AlterarSenhaDto dto, [FromServices] AlterarSenha caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, dto, ct));
    }

    /// <summary>
    /// Pede o e-mail de recuperação (RF03). Responde 202 com a mesma mensagem exista ou não a conta,
    /// para não revelar quais e-mails estão cadastrados.
    /// </summary>
    [HttpPost("esqueci-senha")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasLimite.Autenticacao)]
    [RequestSizeLimit(LimiteCorpoBytes)]
    [ProducesResponseType(typeof(MensagemDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> EsqueciSenha([FromBody] SolicitarRecuperacaoDto dto, [FromServices] RecuperarSenha caso, CancellationToken ct)
    {
        var resultado = await caso.SolicitarAsync(dto, ct);
        return resultado.Sucesso
            ? Accepted(new MensagemDto(RecuperarSenha.MensagemSolicitacao))
            : Falha(resultado.TipoDoErro, resultado.Erro);
    }

    /// <summary>Define a nova senha com o token recebido por e-mail (uso único, 30 minutos).</summary>
    [HttpPost("redefinir-senha")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasLimite.Autenticacao)]
    [RequestSizeLimit(LimiteCorpoBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> RedefinirSenha([FromBody] RedefinirSenhaDto dto, [FromServices] RecuperarSenha caso, CancellationToken ct) =>
        Responder(await caso.RedefinirAsync(dto, ct));
}
