using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HRAuto.Api.Seguranca;
using HRAuto.Application.Autenticacao;
using HRAuto.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace HRAuto.Api.Extensoes;

/// <summary>Nomes das políticas de limite de requisições (atributo <c>[EnableRateLimiting]</c>).</summary>
public static class PoliticasLimite
{
    /// <summary>Login, esqueci-senha e redefinir-senha: o alvo clássico de força bruta.</summary>
    public const string Autenticacao = "autenticacao";
}

/// <summary>Seção "RateLimit" do appsettings. Os valores padrão valem se a seção não existir.</summary>
public sealed class OpcoesLimiteRequisicoes
{
    public const string Secao = "RateLimit";

    /// <summary>Quantas chamadas de autenticação cada IP pode fazer por janela.</summary>
    public int AutenticacaoLimite { get; set; } = 10;

    public int AutenticacaoJanelaSegundos { get; set; } = 60;
}

/// <summary>Opções de CORS (seção "Cors"). Sem origens configuradas, só aceita localhost (desenvolvimento do Flutter Web).</summary>
public sealed class OpcoesCors
{
    public const string Secao = "Cors";
    public string[] Origens { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Configuração da camada Api, dividida em métodos para o Program.cs ficar legível.
/// Tudo que lê configuração (chave JWT, limites, origens) o faz de forma preguiçosa, via IOptions,
/// para que variáveis de ambiente e as configurações dos testes de integração já estejam carregadas.
/// </summary>
public static class ServicosExtensoes
{
    public const string PoliticaCorsFlutter = "Flutter";

    /// <summary>Controllers com JSON em padrão ISO 8601 e enums escritos por nome (ex.: "Entrada").</summary>
    public static IServiceCollection AddControllersDaApi(this IServiceCollection services)
    {
        services.AddControllers().AddJsonOptions(opcoes =>
            opcoes.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        return services;
    }

    /// <summary>
    /// Seguro por padrão: toda rota exige login, a menos que tenha [AllowAnonymous]. Assim, um endpoint novo
    /// esquecido sem atributo nunca fica público por engano.
    /// </summary>
    public static IServiceCollection AddAutorizacao(this IServiceCollection services)
    {
        services.AddAuthorization(opcoes =>
        {
            opcoes.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            opcoes.AddPolicy(Papeis.PoliticaRhOuAdmin, p => p.RequireAuthenticatedUser().RequireRole(NomesPerfil.RH, NomesPerfil.Admin));
            opcoes.AddPolicy(Papeis.PoliticaAdmin, p => p.RequireAuthenticatedUser().RequireRole(NomesPerfil.Admin));
        });
        return services;
    }

    /// <summary>Limite de tentativas de login/recuperação por IP (força bruta). Responde 429 com Retry-After.</summary>
    public static IServiceCollection AddLimiteDeRequisicoes(this IServiceCollection services)
    {
        services.AddOptions<OpcoesLimiteRequisicoes>().BindConfiguration(OpcoesLimiteRequisicoes.Secao);

        services.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            opcoes.AddPolicy(PoliticasLimite.Autenticacao, contexto =>
            {
                var configuracao = contexto.RequestServices.GetRequiredService<IOptions<OpcoesLimiteRequisicoes>>().Value;
                var chave = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";

                return RateLimitPartition.GetFixedWindowLimiter(chave, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, configuracao.AutenticacaoLimite),
                    Window = TimeSpan.FromSeconds(Math.Max(1, configuracao.AutenticacaoJanelaSegundos)),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            opcoes.OnRejected = async (rejeicao, cancelamento) =>
            {
                var resposta = rejeicao.HttpContext.Response;
                resposta.StatusCode = StatusCodes.Status429TooManyRequests;
                if (rejeicao.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                    resposta.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                await resposta.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Muitas tentativas",
                        Detail = "Muitas tentativas em pouco tempo. Aguarde um instante e tente de novo."
                    },
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken: cancelamento);
            };
        });
        return services;
    }

    /// <summary>
    /// CORS: com <c>Cors:Origens</c> configurado (produção) só essas origens passam; sem configuração,
    /// só localhost (Flutter Web em desenvolvimento, porta aleatória).
    /// </summary>
    public static IServiceCollection AddCorsFlutter(this IServiceCollection services)
    {
        services.AddOptions<OpcoesCors>().BindConfiguration(OpcoesCors.Secao);

        services.AddOptions<CorsOptions>().Configure<IOptions<OpcoesCors>>((cors, opcoes) =>
        {
            var origens = opcoes.Value.Origens.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();
            cors.AddPolicy(PoliticaCorsFlutter, politica =>
            {
                if (origens.Length > 0)
                    politica.WithOrigins(origens);
                else
                    politica.SetIsOriginAllowed(origem => Uri.TryCreate(origem, UriKind.Absolute, out var uri) && uri.IsLoopback);

                politica.AllowAnyHeader().AllowAnyMethod();
            });
        });
        services.AddCors();
        return services;
    }

    /// <summary>
    /// Endereço da tela de redefinição de senha, usado no link do e-mail (RF03). Vem SEMPRE da configuração,
    /// nunca do cabeçalho Host da requisição (evita link de recuperação apontando para site de terceiros).
    /// </summary>
    public static IServiceCollection AddOpcoesRecuperacaoSenha(this IServiceCollection services)
    {
        services.AddSingleton(provedor =>
        {
            var url = provedor.GetRequiredService<IConfiguration>()["App:UrlRedefinicaoSenha"];
            if (string.IsNullOrWhiteSpace(url)
                || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException(
                    "App:UrlRedefinicaoSenha deve ser uma URL http(s) completa da tela de redefinição de senha do app " +
                    "(ex.: https://rh.suaempresa.com.br/#/redefinir-senha). Defina a variável de ambiente App__UrlRedefinicaoSenha.");

            return new OpcoesRecuperacaoSenha(url.Trim());
        });
        return services;
    }
}
