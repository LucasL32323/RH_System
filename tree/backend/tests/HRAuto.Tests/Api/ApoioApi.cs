using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HRAuto.Application.Autenticacao;

namespace HRAuto.Tests.Api;

/// <summary>Atalhos dos testes de integração: JSON no mesmo formato da API, pedidos com token e login.</summary>
internal static class ApoioApi
{
    /// <summary>Mesmas opções da API: camelCase e enums por nome.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static HttpRequestMessage Pedido(HttpMethod metodo, string url, string? token = null, object? corpo = null)
    {
        var pedido = new HttpRequestMessage(metodo, url);
        if (token is not null) pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (corpo is not null) pedido.Content = JsonContent.Create(corpo, options: Json);
        return pedido;
    }

    public static async Task<string> EntrarAsync(this HttpClient cliente, string email, string senha = ApiFabrica.SenhaPadrao)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new CredenciaisDto(email, senha), Json);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<LoginRespostaDto>(Json))!.Token;
    }

    public static async Task<JsonElement> LerJsonAsync(this HttpResponseMessage resposta) =>
        JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();
}
