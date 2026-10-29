using HRAuto.Api.Extensoes;
using HRAuto.Api.Middleware;
using HRAuto.Application;
using HRAuto.Application.Abstractions;
using HRAuto.Application.Autenticacao;
using HRAuto.Infrastructure;
using HRAuto.Infrastructure.Seguranca;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ---- Composição das camadas (Clean Architecture) ----
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---- API REST (RNF16), erros em ProblemDetails e Swagger com JWT ----
builder.Services.AddControllersDaApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorDeExcecoes>();
builder.Services.AddSwaggerComJwt();

// ---- Segurança: login JWT (RF01), perfis (RF05), força bruta e CORS ----
builder.Services.AddAutenticacaoJwt();
builder.Services.AddAutorizacao();
builder.Services.AddLimiteDeRequisicoes();
builder.Services.AddCorsFlutter();
builder.Services.AddOpcoesRecuperacaoSenha();

// ---- Health check para monitoramento (RNF03) ----
builder.Services.AddHealthChecks();

var app = builder.Build();

// ---- Falha cedo: chave JWT e URL de redefinição inválidas derrubam a subida, não o primeiro login ----
app.Services.GetRequiredService<IOptions<OpcoesJwt>>().Value.Validar();
_ = app.Services.GetRequiredService<OpcoesRecuperacaoSenha>();

// ---- Banco: aplica as migrations e grava o seed (perfis, cargos, departamentos, admin de teste) ----
using (var escopo = app.Services.CreateScope())
{
    await escopo.ServiceProvider.GetRequiredService<IInicializadorBanco>().ExecutarAsync();
}

// Primeiro da fila: captura exceções de tudo o que vem depois e devolve ProblemDetails.
app.UseExceptionHandler();
app.UseStatusCodePages(); // 401/403/404 sem corpo também viram ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

// Somente HTTPS (RNF04). Em Development o launchSettings já expõe a porta https.
app.UseHttpsRedirection();

// Respostas da API nunca ficam em cache (token, dados pessoais) e o navegador não "adivinha" o tipo do conteúdo.
app.Use(async (contexto, proximo) =>
{
    if (contexto.Request.Path.StartsWithSegments("/api"))
    {
        contexto.Response.Headers.CacheControl = "no-store";
        contexto.Response.Headers.XContentTypeOptions = "nosniff";
    }
    await proximo();
});

app.UseRouting();
app.UseCors(ServicosExtensoes.PoliticaCorsFlutter);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Necessário para os testes de integração (WebApplicationFactory<Program>).
public partial class Program { }
