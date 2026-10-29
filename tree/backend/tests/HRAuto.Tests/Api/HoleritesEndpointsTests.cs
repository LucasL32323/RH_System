using System.Net;
using System.Net.Http.Headers;
using System.Text;
using HRAuto.Domain.Enums;
using static HRAuto.Tests.Api.ApoioApi;

namespace HRAuto.Tests.Api;

/// <summary>
/// RF29, RF30, RNF08, RNF17: holerites pela API de verdade (SQLite, JWT, pasta temporária de PDFs, relógio fixo em 21/10/2026).
/// </summary>
public class HoleritesEndpointsTests : IClassFixture<ApiFabrica>
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF");

    private readonly ApiFabrica _fabrica;
    private readonly HttpClient _http;

    public HoleritesEndpointsTests(ApiFabrica fabrica)
    {
        _fabrica = fabrica;
        _http = fabrica.CreateClient();
        _fabrica.Relogio.Definir("2026-10-21T10:00:00");
    }

    private static HttpRequestMessage Publicacao(
        string token, string codigo, int mes, int ano, byte[] conteudo, string nome = "holerite.pdf", bool substituir = false)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(codigo), "Codigo" },
            { new StringContent(mes.ToString()), "Mes" },
            { new StringContent(ano.ToString()), "Ano" }
        };
        if (substituir) form.Add(new StringContent("true"), "Substituir");
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(arquivo, "Arquivo", nome);

        var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/v1/holerites") { Content = form };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return pedido;
    }

    [Fact]
    public async Task RhPublica_ColaboradorListaEBaixaOProprio_OutrosNaoVeem()
    {
        var rh = await _fabrica.CriarUsuarioAsync("Rita RH", PerfilAcesso.RH);
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var bruno = await _fabrica.CriarUsuarioAsync("Bruno Lima");
        var tokenRh = await _http.EntrarAsync(rh.Email);
        var tokenAna = await _http.EntrarAsync(ana.Email);
        var tokenBruno = await _http.EntrarAsync(bruno.Email);

        var publicado = await _http.SendAsync(Publicacao(tokenRh, ana.Codigo, 9, 2026, Pdf, "setembro.pdf"));

        Assert.Equal(HttpStatusCode.Created, publicado.StatusCode);
        var texto = await publicado.Content.ReadAsStringAsync();
        Assert.DoesNotContain("FUNC-", System.Text.Json.JsonDocument.Parse(texto).RootElement.GetProperty("holerite").GetProperty("nomeArquivo").GetString()); // sem caminho nem código
        var id = System.Text.Json.JsonDocument.Parse(texto).RootElement.GetProperty("holerite").GetProperty("id").GetInt32();
        Assert.Equal(1, _fabrica.ContarArquivosDeHolerite());

        // Ana vê o aviso, lista o holerite e baixa o PDF idêntico ao enviado.
        var aviso = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/notificacoes", tokenAna))).LerJsonAsync();
        Assert.Equal("Novo holerite disponível", aviso.GetProperty("itens")[0].GetProperty("titulo").GetString());
        var lista = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/holerites", tokenAna))).LerJsonAsync();
        Assert.Equal(1, lista.GetArrayLength());
        Assert.Equal(9, lista[0].GetProperty("mes").GetInt32());

        var baixado = await _http.SendAsync(Pedido(HttpMethod.Get, $"/api/v1/holerites/{id}/arquivo", tokenAna));
        Assert.Equal(HttpStatusCode.OK, baixado.StatusCode);
        Assert.Equal("application/pdf", baixado.Content.Headers.ContentType!.MediaType);
        Assert.Equal("holerite-2026-09.pdf", baixado.Content.Headers.ContentDisposition!.FileName);
        Assert.Equal(Pdf, await baixado.Content.ReadAsByteArrayAsync());

        // Colega não baixa (404), não lista os dela (403) e não publica (403). O RH baixa e fica na auditoria.
        var doColega = await _http.SendAsync(Pedido(HttpMethod.Get, $"/api/v1/holerites/{id}/arquivo", tokenBruno));
        var listaAlheia = await _http.SendAsync(Pedido(HttpMethod.Get, $"/api/v1/holerites?codigo={ana.Codigo}", tokenBruno));
        var publicarSemPermissao = await _http.SendAsync(Publicacao(tokenBruno, ana.Codigo, 8, 2026, Pdf));
        var peloRh = await _http.SendAsync(Pedido(HttpMethod.Get, $"/api/v1/holerites/{id}/arquivo", tokenRh));
        Assert.Equal(HttpStatusCode.NotFound, doColega.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, listaAlheia.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, publicarSemPermissao.StatusCode);
        Assert.Equal(HttpStatusCode.OK, peloRh.StatusCode);

        var acoesDoRh = await _fabrica.AcoesDeAuditoriaAsync(rh.IdUsuario);
        Assert.Contains("PublicarHolerite", acoesDoRh);
        Assert.Contains("ConsultaHolerite", acoesDoRh);
    }

    [Fact]
    public async Task ArquivoInvalido_Da400_ENadaFicaNaPasta()
    {
        var rh = await _fabrica.CriarUsuarioAsync("Rita RH", PerfilAcesso.RH);
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var tokenRh = await _http.EntrarAsync(rh.Email);
        var antes = _fabrica.ContarArquivosDeHolerite();
        var grande = new byte[5 * 1024 * 1024 + 1];
        Pdf.CopyTo(grande, 0);

        var naoPdf = await _http.SendAsync(Publicacao(tokenRh, ana.Codigo, 9, 2026, Pdf, "holerite.docx"));
        var semAssinatura = await _http.SendAsync(Publicacao(tokenRh, ana.Codigo, 9, 2026, Encoding.ASCII.GetBytes("MZ qualquer coisa")));
        var vazio = await _http.SendAsync(Publicacao(tokenRh, ana.Codigo, 9, 2026, Array.Empty<byte>()));
        var excedido = await _http.SendAsync(Publicacao(tokenRh, ana.Codigo, 9, 2026, grande));
        var mesFuturo = await _http.SendAsync(Publicacao(tokenRh, ana.Codigo, 11, 2026, Pdf));
        var codigoInvalido = await _http.SendAsync(Publicacao(tokenRh, "ana", 9, 2026, Pdf));
        var inexistente = await _http.SendAsync(Publicacao(tokenRh, "FUNC-9999", 9, 2026, Pdf));

        foreach (var r in new[] { naoPdf, semAssinatura, vazio, excedido, mesFuturo, codigoInvalido })
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(antes, _fabrica.ContarArquivosDeHolerite());
    }

    [Fact]
    public async Task CompetenciaRepetida_Da409_ComSubstituir_Da200_EOColaboradorBaixaAVersaoNova()
    {
        var rh = await _fabrica.CriarUsuarioAsync("Rita RH", PerfilAcesso.RH);
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var tokenRh = await _http.EntrarAsync(rh.Email);
        var tokenAna = await _http.EntrarAsync(ana.Email);
        var novo = Encoding.ASCII.GetBytes("%PDF-1.7 versão corrigida");

        var primeiro = await _http.SendAsync(Publicacao(tokenRh, ana.Codigo, 9, 2026, Pdf));
        var repetido = await _http.SendAsync(Publicacao(tokenRh, ana.Codigo, 9, 2026, novo));
        var substituido = await _http.SendAsync(Publicacao(tokenRh, ana.Codigo, 9, 2026, novo, substituir: true));

        Assert.Equal(HttpStatusCode.Created, primeiro.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal(HttpStatusCode.OK, substituido.StatusCode);
        var id = (await substituido.LerJsonAsync()).GetProperty("holerite").GetProperty("id").GetInt32();
        var baixado = await _http.SendAsync(Pedido(HttpMethod.Get, $"/api/v1/holerites/{id}/arquivo", tokenAna));
        Assert.Equal(novo, await baixado.Content.ReadAsByteArrayAsync());
        var lista = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/holerites", tokenAna))).LerJsonAsync();
        Assert.Equal(1, lista.GetArrayLength());
    }

    [Fact]
    public async Task SemLogin_Recebe401()
    {
        var resposta = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/holerites"));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
