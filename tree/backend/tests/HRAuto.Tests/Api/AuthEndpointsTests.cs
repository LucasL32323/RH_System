using System.Net;
using System.Net.Http.Json;
using HRAuto.Application.Abstractions;
using HRAuto.Application.Autenticacao;
using HRAuto.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using static HRAuto.Tests.Api.ApoioApi;

namespace HRAuto.Tests.Api;

/// <summary>RF01 a RF04, RNF04, RNF05: login, JWT, rotas protegidas, troca e recuperação de senha, pela API de verdade.</summary>
public class AuthEndpointsTests : IClassFixture<ApiFabrica>
{
    private readonly ApiFabrica _fabrica;
    private readonly HttpClient _http;

    public AuthEndpointsTests(ApiFabrica fabrica)
    {
        _fabrica = fabrica;
        _http = fabrica.CreateClient();
        _fabrica.Relogio.Definir("2026-10-21T10:00:00");
    }

    [Fact]
    public async Task Login_ComAdminDeTeste_DevolveTokenEPerfilAdmin()
    {
        var resposta = await _http.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto("ADMIN@hrauto.local", "Admin@123"), Json);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("no-store", resposta.Headers.CacheControl?.ToString());
        var corpo = await resposta.LerJsonAsync();
        Assert.Equal("Admin", corpo.GetProperty("usuario").GetProperty("perfil").GetString());
        Assert.Equal("FUNC-0001", corpo.GetProperty("usuario").GetProperty("codigo").GetString());
        Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task Login_SenhaErradaOuEmailInexistente_Recebem401ComAMesmaMensagem()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Ana Souza");

        var senhaErrada = await _http.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto(usuario.Email, "errada"), Json);
        var inexistente = await _http.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto("ninguem@hrauto.local", "errada"), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, senhaErrada.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);
        Assert.Equal("application/problem+json", senhaErrada.Content.Headers.ContentType?.MediaType);
        Assert.Equal((await senhaErrada.LerJsonAsync()).GetProperty("detail").GetString(),
                     (await inexistente.LerJsonAsync()).GetProperty("detail").GetString());
        Assert.Contains("LoginFalha", await _fabrica.AcoesDeAuditoriaAsync(usuario.IdUsuario));
    }

    [Fact]
    public async Task Login_CamposVaziosOuJsonQuebrado_Recebem400()
    {
        var vazio = await _http.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto("", ""), Json);
        var quebrado = await _http.PostAsync("/api/v1/auth/login",
            new StringContent("{isto nao e json", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, vazio.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, quebrado.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/v1/eu")]
    [InlineData("GET", "/api/v1/ponto/hoje")]
    [InlineData("GET", "/api/v1/ponto/espelho")]
    [InlineData("POST", "/api/v1/ponto/batidas")]
    [InlineData("POST", "/api/v1/auth/logout")]
    public async Task RotasProtegidas_SemToken_Recebem401(string metodo, string url)
    {
        var resposta = await _http.SendAsync(Pedido(new HttpMethod(metodo), url));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task RotasPublicas_NaoExigemLogin()
    {
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/api/v1/sistema/status")).StatusCode);
    }

    [Fact]
    public async Task Eu_ComToken_DevolveOsDadosDeQuemEstaLogado()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Ana Souza", PerfilAcesso.RH);
        var token = await _http.EntrarAsync(usuario.Email);

        var resposta = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/eu", token));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.LerJsonAsync();
        Assert.Equal(usuario.Codigo, corpo.GetProperty("codigo").GetString());
        Assert.Equal("Ana Souza", corpo.GetProperty("nome").GetString());
        Assert.Equal("RH", corpo.GetProperty("perfil").GetString());
        Assert.Equal("2024-03-11", corpo.GetProperty("dataAdmissao").GetString());
    }

    [Fact]
    public async Task TokenAdulteradoOuExpirado_Recebe401()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var token = await _http.EntrarAsync(usuario.Email);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/eu", token + "x"))).StatusCode);

        // O token vale 8 h; um minuto depois do prazo ele deixa de servir.
        _fabrica.Relogio.Definir("2026-10-21T18:01:00");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/eu", token))).StatusCode);
    }

    [Fact]
    public async Task TokenDeRedefinicaoDeSenha_NaoServeComoLogin()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var reset = _fabrica.Services.GetRequiredService<IServicoToken>().GerarRedefinicaoSenha(usuario.IdUsuario, "0123456789abcdef");

        var resposta = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/eu", reset));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task EsqueciSenha_ResponderIgualParaContaExistenteEInexistente_ERedefinirTemUsoUnico()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Ana Souza");

        var existe = await _http.PostAsJsonAsync("/api/v1/auth/esqueci-senha", new SolicitarRecuperacaoDto(usuario.Email), Json);
        var naoExiste = await _http.PostAsJsonAsync("/api/v1/auth/esqueci-senha", new SolicitarRecuperacaoDto("fantasma@hrauto.local"), Json);
        var invalido = await _http.PostAsJsonAsync("/api/v1/auth/esqueci-senha", new SolicitarRecuperacaoDto("isto-nao-e-email"), Json);

        Assert.Equal(HttpStatusCode.Accepted, existe.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, naoExiste.StatusCode);
        Assert.Equal(await existe.Content.ReadAsStringAsync(), await naoExiste.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);

        var email = _fabrica.Email.Enviados.Last(e => e.Para == usuario.Email);
        var token = Uri.UnescapeDataString(email.Corpo.Split("token=")[1].Split('\n')[0].Trim());

        var redefinir = await _http.PostAsJsonAsync("/api/v1/auth/redefinir-senha", new RedefinirSenhaDto(token, "NovaSenha@2026"), Json);
        var reutilizar = await _http.PostAsJsonAsync("/api/v1/auth/redefinir-senha", new RedefinirSenhaDto(token, "OutraSenha@2026"), Json);
        var loginNovo = await _http.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto(usuario.Email, "NovaSenha@2026"), Json);
        var loginAntigo = await _http.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto(usuario.Email, ApiFabrica.SenhaPadrao), Json);

        Assert.Equal(HttpStatusCode.NoContent, redefinir.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reutilizar.StatusCode); // o link já foi usado
        Assert.Equal(HttpStatusCode.OK, loginNovo.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, loginAntigo.StatusCode);
    }

    [Fact]
    public async Task RedefinirSenha_ComTokenInvalidoOuSenhaFraca_Recebe400()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Ana Souza");
        await _http.PostAsJsonAsync("/api/v1/auth/esqueci-senha", new SolicitarRecuperacaoDto(usuario.Email), Json);
        var token = Uri.UnescapeDataString(_fabrica.Email.Enviados.Last(e => e.Para == usuario.Email).Corpo.Split("token=")[1].Split('\n')[0].Trim());

        var tokenInvalido = await _http.PostAsJsonAsync("/api/v1/auth/redefinir-senha", new RedefinirSenhaDto("lixo", "NovaSenha@2026"), Json);
        var senhaFraca = await _http.PostAsJsonAsync("/api/v1/auth/redefinir-senha", new RedefinirSenhaDto(token, "fraca"), Json);

        Assert.Equal(HttpStatusCode.BadRequest, tokenInvalido.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, senhaFraca.StatusCode);
    }

    [Fact]
    public async Task AlterarSenha_ExigeToken_SenhaAtualCorreta_ENovaSenhaForte()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var token = await _http.EntrarAsync(usuario.Email);

        var semToken = await _http.PostAsJsonAsync("/api/v1/auth/alterar-senha", new AlterarSenhaDto("a", "b"), Json);
        var atualErrada = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/auth/alterar-senha", token, new AlterarSenhaDto("errada", "Nova@Senha9")));
        var fraca = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/auth/alterar-senha", token, new AlterarSenhaDto(ApiFabrica.SenhaPadrao, "fraca")));
        var ok = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/auth/alterar-senha", token, new AlterarSenhaDto(ApiFabrica.SenhaPadrao, "Nova@Senha9")));
        var loginNovo = await _http.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto(usuario.Email, "Nova@Senha9"), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, semToken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, atualErrada.StatusCode); // 400 e não 401: o app não pode deslogar por erro de digitação
        Assert.Equal(HttpStatusCode.BadRequest, fraca.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginNovo.StatusCode);
        Assert.Contains("AlterarSenha", await _fabrica.AcoesDeAuditoriaAsync(usuario.IdUsuario));
    }

    [Fact]
    public async Task Logout_FicaNaAuditoria()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var token = await _http.EntrarAsync(usuario.Email);

        var resposta = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/auth/logout", token));

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var acoes = await _fabrica.AcoesDeAuditoriaAsync(usuario.IdUsuario);
        Assert.Contains("Login", acoes);
        Assert.Contains("Logout", acoes);
    }

    [Fact]
    public async Task RotaInexistente_E404_EMetodoErrado_E405()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var token = await _http.EntrarAsync(usuario.Email);

        Assert.Equal(HttpStatusCode.NotFound, (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/nada", token))).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await _http.SendAsync(Pedido(HttpMethod.Put, "/api/v1/eu", token))).StatusCode);
    }
}

/// <summary>Proteção contra força bruta: depois do limite, o login responde 429 com Retry-After.</summary>
public class ForcaBrutaTests : IClassFixture<ApiFabricaComLimiteBaixo>
{
    private readonly HttpClient _http;

    public ForcaBrutaTests(ApiFabricaComLimiteBaixo fabrica) => _http = fabrica.CreateClient();

    [Fact]
    public async Task AposOLimite_OLoginRecebe429ComRetryAfter()
    {
        var codigos = new List<HttpStatusCode>();
        HttpResponseMessage? ultima = null;

        for (var i = 0; i < 5; i++)
        {
            ultima = await _http.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto("alguem@hrauto.local", "errada"), Json);
            codigos.Add(ultima.StatusCode);
        }

        Assert.Equal(
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests },
            codigos.ToArray());
        Assert.True(ultima!.Headers.Contains("Retry-After"));
        Assert.Equal("application/problem+json", ultima.Content.Headers.ContentType?.MediaType);
    }
}
