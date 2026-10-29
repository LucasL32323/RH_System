using System.Net;
using System.Net.Http.Json;
using HRAuto.Application.Autenticacao;
using HRAuto.Domain.Enums;
using static HRAuto.Tests.Api.ApoioApi;

namespace HRAuto.Tests.Api;

/// <summary>RF01, RNF05: POST /api/v1/auth/login com o código FUNC-0001 pela API de verdade (SQLite, JWT).</summary>
public class LoginPorCodigoEndpointsTests : IClassFixture<ApiFabrica>
{
    private readonly ApiFabrica _fabrica;
    private readonly HttpClient _http;

    public LoginPorCodigoEndpointsTests(ApiFabrica fabrica)
    {
        _fabrica = fabrica;
        _http = fabrica.CreateClient();
        _fabrica.Relogio.Definir("2026-10-21T10:00:00");
    }

    [Fact]
    public async Task Login_PeloCodigoNoCampoLogin_DevolveTokenQueAbreAAreaDoUsuario()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Ana Souza", PerfilAcesso.RH);

        var resposta = await _http.PostAsJsonAsync(
            "/api/v1/auth/login", new { login = usuario.Codigo.ToLowerInvariant(), senha = ApiFabrica.SenhaPadrao }, Json);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.LerJsonAsync();
        Assert.Equal(usuario.Codigo, corpo.GetProperty("usuario").GetProperty("codigo").GetString());
        Assert.Equal("RH", corpo.GetProperty("usuario").GetProperty("perfil").GetString());

        var token = corpo.GetProperty("token").GetString();
        var eu = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/eu", token));
        Assert.Equal(HttpStatusCode.OK, eu.StatusCode);
        Assert.Contains("Login", await _fabrica.AcoesDeAuditoriaAsync(usuario.IdUsuario));
    }

    [Fact]
    public async Task Login_PeloCodigo_NoCampoEmailAntigo_TambemFunciona()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Bia Lima");

        var resposta = await _http.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto(usuario.Codigo, ApiFabrica.SenhaPadrao), Json);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_ComEmailContinuaFuncionando_PeloCampoLogin()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Caio Dias");

        var resposta = await _http.PostAsJsonAsync("/api/v1/auth/login", new { login = usuario.Email, senha = ApiFabrica.SenhaPadrao }, Json);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_CodigoComSenhaErradaOuInexistente_Recebem401ComAMesmaMensagem()
    {
        var usuario = await _fabrica.CriarUsuarioAsync("Davi Reis");

        var senhaErrada = await _http.PostAsJsonAsync("/api/v1/auth/login", new { login = usuario.Codigo, senha = "errada" }, Json);
        var inexistente = await _http.PostAsJsonAsync("/api/v1/auth/login", new { login = "FUNC-9999", senha = "errada" }, Json);
        var malFormado = await _http.PostAsJsonAsync("/api/v1/auth/login", new { login = "Davi Reis", senha = ApiFabrica.SenhaPadrao }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, senhaErrada.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, malFormado.StatusCode);

        var mensagem = (await senhaErrada.LerJsonAsync()).GetProperty("detail").GetString();
        Assert.Equal(mensagem, (await inexistente.LerJsonAsync()).GetProperty("detail").GetString());
        Assert.Equal(mensagem, (await malFormado.LerJsonAsync()).GetProperty("detail").GetString());
        Assert.Contains("LoginFalha", await _fabrica.AcoesDeAuditoriaAsync(usuario.IdUsuario));
    }

    [Fact]
    public async Task Login_SemIdentificador_Recebe400()
    {
        var resposta = await _http.PostAsJsonAsync("/api/v1/auth/login", new { senha = ApiFabrica.SenhaPadrao }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
