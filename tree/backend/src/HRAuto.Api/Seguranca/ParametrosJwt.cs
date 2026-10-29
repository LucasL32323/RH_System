using HRAuto.Application.Abstractions;
using HRAuto.Application.Autenticacao;
using HRAuto.Infrastructure.Seguranca;
using Microsoft.IdentityModel.Tokens;

namespace HRAuto.Api.Seguranca;

/// <summary>Regras de validação do token de acesso, usadas pelo middleware JwtBearer.</summary>
public static class ParametrosJwt
{
    public static TokenValidationParameters Acesso(OpcoesJwt configuracao, IRelogio relogio) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = configuracao.Emissor,
        ValidateAudience = true,
        ValidAudience = configuracao.PublicoAcesso, // o token de redefinição de senha tem outro público e é recusado aqui
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = configuracao.ChaveDeAcesso(),
        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = NomesClaims.Nome,
        RoleClaimType = NomesClaims.Perfil,
        // A validade é conferida com o IRelogio do sistema (o mesmo que emitiu o token), não com o relógio da máquina.
        LifetimeValidator = (naoAntesDe, expiraEm, _, parametros) =>
        {
            if (expiraEm is null) return false;
            var agora = relogio.Agora().UtcDateTime;
            if (naoAntesDe is not null && naoAntesDe.Value > agora + parametros.ClockSkew) return false;
            return expiraEm.Value + parametros.ClockSkew > agora;
        }
    };
}
