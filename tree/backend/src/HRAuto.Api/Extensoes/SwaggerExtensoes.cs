using Microsoft.OpenApi.Models;

namespace HRAuto.Api.Extensoes;

public static class SwaggerExtensoes
{
    /// <summary>Swagger (RNF16) com o botão "Authorize" para colar o token JWT.</summary>
    public static IServiceCollection AddSwaggerComJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(opcoes =>
        {
            opcoes.SwaggerDoc("v1", new OpenApiInfo { Title = "HR-Auto API", Version = "v1" });

            opcoes.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Faça login em POST /api/v1/auth/login e cole aqui só o token (sem a palavra Bearer)."
            });
            opcoes.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }]
                    = Array.Empty<string>()
            });
        });
        return services;
    }
}
