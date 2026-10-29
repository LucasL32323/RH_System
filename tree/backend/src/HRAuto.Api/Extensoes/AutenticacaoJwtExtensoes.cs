using HRAuto.Api.Seguranca;
using HRAuto.Application.Abstractions;
using HRAuto.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace HRAuto.Api.Extensoes;

public static class AutenticacaoJwtExtensoes
{
    /// <summary>Valida o JWT de acesso em cada requisição (RF01, RNF04): HS256, emissor, público, validade e assinatura.</summary>
    public static IServiceCollection AddAutenticacaoJwt(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opcoes =>
            {
                // Mantém os nomes curtos das claims ("sub", "role") em vez de convertê-los para URIs da Microsoft.
                opcoes.MapInboundClaims = false;
                opcoes.SaveToken = false;
            });

        // Configurado de forma preguiçosa (na primeira requisição): a chave vem de variável de ambiente ou dos testes.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<OpcoesJwt>, IRelogio>((opcoes, jwt, relogio) =>
            {
                jwt.Value.Validar();
                opcoes.TokenValidationParameters = ParametrosJwt.Acesso(jwt.Value, relogio);
            });

        return services;
    }
}
