using System.Net;
using HRAuto.Domain.Enums;
using static HRAuto.Tests.Api.ApoioApi;

namespace HRAuto.Tests.Api;

/// <summary>
/// RF22, RF23, RF24, RF36, RNF07: manutenção de ponto pela API de verdade (SQLite, JWT, relógio fixo em 21/10/2026).
/// Cada teste cria seus próprios usuários. Segunda-feira, 19/10/2026, é o dia do ponto corrigido.
/// </summary>
public class ManutencoesPontoEndpointsTests : IClassFixture<ApiFabrica>
{
    private const string Justificativa = "Esqueci de bater a saída; saí às 18:00";

    private readonly ApiFabrica _fabrica;
    private readonly HttpClient _http;

    public ManutencoesPontoEndpointsTests(ApiFabrica fabrica)
    {
        _fabrica = fabrica;
        _http = fabrica.CreateClient();
        _fabrica.Relogio.Definir("2026-10-21T10:00:00");
    }

    [Fact]
    public async Task ColaboradorPede_GestorAprovaCorrigindoASaida_BancoDeHorasEPontoSaoAtualizados()
    {
        var gestor = await _fabrica.CriarUsuarioAsync("Gil Gestor");
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza", idGestor: gestor.IdFuncionario);
        await _fabrica.CriarPontoAsync(ana.IdFuncionario, "2026-10-19", "08:00", "12:00", "13:00", null);
        var tokenAna = await _http.EntrarAsync(ana.Email);
        var tokenGestor = await _http.EntrarAsync(gestor.Email);

        var criada = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/manutencoes-ponto", tokenAna,
            new { data = "2026-10-19", justificativa = Justificativa }));

        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        var pedido = await criada.LerJsonAsync();
        var id = pedido.GetProperty("id").GetInt32();
        Assert.Equal("Pendente", pedido.GetProperty("status").GetString());
        Assert.Equal(ana.Codigo, pedido.GetProperty("codigoFuncionario").GetString());
        Assert.Equal(3, pedido.GetProperty("batidas").GetArrayLength());

        // O gestor foi avisado e vê o pedido na lista da equipe.
        var avisos = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/notificacoes?somenteNaoLidas=true", tokenGestor))).LerJsonAsync();
        Assert.Equal("Novo pedido de manutenção de ponto", avisos.GetProperty("itens")[0].GetProperty("titulo").GetString());
        var equipe = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/manutencoes-ponto?escopo=equipe&status=Pendente", tokenGestor))).LerJsonAsync();
        Assert.Equal(1, equipe.GetProperty("total").GetInt32());

        var decisao = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/manutencoes-ponto/{id}/decisao", tokenGestor,
            new { aprovar = true, observacao = "Confirmado com a equipe", batidas = new { saida = "18:00:00" } }));

        Assert.Equal(HttpStatusCode.OK, decisao.StatusCode);
        var resposta = await decisao.LerJsonAsync();
        Assert.Equal("Aprovada", resposta.GetProperty("manutencao").GetProperty("status").GetString());
        Assert.True(resposta.GetProperty("batidasAlteradas").GetBoolean());
        Assert.Equal("Saida", resposta.GetProperty("alteracoes")[0].GetProperty("tipo").GetString());
        Assert.Equal(60, resposta.GetProperty("fechamentoDoDia").GetProperty("extrasMinutos").GetInt32());

        var ponto = await _fabrica.ObterPontoAsync(ana.IdFuncionario, "2026-10-19");
        Assert.Equal(new TimeOnly(18, 0), ponto!.Saida);
        Assert.Equal(1.0m, await _fabrica.SaldoBancoHorasAsync(ana.IdFuncionario));

        // Ana recebe o aviso da decisão; a trilha de auditoria guarda quem pediu, quem decidiu e a batida ajustada.
        var lista = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/notificacoes", tokenAna))).LerJsonAsync();
        Assert.Equal("Manutenção de ponto aprovada", lista.GetProperty("itens")[0].GetProperty("titulo").GetString());
        Assert.Contains("SolicitarManutencaoPonto", await _fabrica.AcoesDeAuditoriaAsync(ana.IdUsuario));
        var acoesDoGestor = await _fabrica.AcoesDeAuditoriaAsync(gestor.IdUsuario);
        Assert.Contains("DecidirManutencaoPonto", acoesDoGestor);
        Assert.Contains("AjustarBatida", acoesDoGestor);
    }

    [Fact]
    public async Task DiaSemNenhumaBatida_CriaOPontoVazio_EAbonoSoRegistraADecisao()
    {
        var gestor = await _fabrica.CriarUsuarioAsync("Gil Gestor");
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza", idGestor: gestor.IdFuncionario);
        var tokenAna = await _http.EntrarAsync(ana.Email);
        var tokenGestor = await _http.EntrarAsync(gestor.Email);

        var criada = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/manutencoes-ponto", tokenAna,
            new { data = "2026-10-16", justificativa = "Consulta médica, atestado entregue ao RH" }));
        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        Assert.Equal(1, await _fabrica.ContarPontosAsync(ana.IdFuncionario));

        var id = (await criada.LerJsonAsync()).GetProperty("id").GetInt32();
        var abono = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/manutencoes-ponto/{id}/decisao", tokenGestor, new { aprovar = true }));

        Assert.Equal(HttpStatusCode.OK, abono.StatusCode);
        Assert.False((await abono.LerJsonAsync()).GetProperty("batidasAlteradas").GetBoolean());
        Assert.Equal(0m, await _fabrica.SaldoBancoHorasAsync(ana.IdFuncionario));
    }

    [Fact]
    public async Task RegrasDeNegocio_ViramErro400_Permissoes403_EEstados404E409()
    {
        var gestor = await _fabrica.CriarUsuarioAsync("Gil Gestor");
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza", idGestor: gestor.IdFuncionario);
        var bruno = await _fabrica.CriarUsuarioAsync("Bruno Lima", idGestor: gestor.IdFuncionario);
        await _fabrica.CriarPontoAsync(ana.IdFuncionario, "2026-10-19", "08:00", "12:00", "13:00", null);
        var tokenAna = await _http.EntrarAsync(ana.Email);
        var tokenBruno = await _http.EntrarAsync(bruno.Email);
        var tokenGestor = await _http.EntrarAsync(gestor.Email);

        var semJustificativa = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/manutencoes-ponto", tokenAna, new { data = "2026-10-19", justificativa = "ok" }));
        var diaFuturo = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/manutencoes-ponto", tokenAna, new { data = "2026-10-22", justificativa = Justificativa }));
        Assert.Equal(HttpStatusCode.BadRequest, semJustificativa.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, diaFuturo.StatusCode);

        var id = (await (await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/manutencoes-ponto", tokenAna,
            new { data = "2026-10-19", justificativa = Justificativa }))).LerJsonAsync()).GetProperty("id").GetInt32();

        // Um segundo pedido pendente para o mesmo dia, o colega e o próprio dono não decidem.
        var duplicado = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/manutencoes-ponto", tokenAna, new { data = "2026-10-19", justificativa = Justificativa }));
        var colega = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/manutencoes-ponto/{id}/decisao", tokenBruno, new { aprovar = true }));
        var proprio = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/manutencoes-ponto/{id}/decisao", tokenAna, new { aprovar = true }));
        var listaEquipeDoColega = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/manutencoes-ponto?escopo=equipe", tokenBruno));
        Assert.Equal(HttpStatusCode.BadRequest, duplicado.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, colega.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, proprio.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, listaEquipeDoColega.StatusCode);

        // Rejeitar exige motivo; com motivo funciona e o ponto não muda; decidir de novo é conflito.
        var semMotivo = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/manutencoes-ponto/{id}/decisao", tokenGestor, new { aprovar = false }));
        var rejeitado = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/manutencoes-ponto/{id}/decisao", tokenGestor,
            new { aprovar = false, observacao = "Sem comprovante da saída" }));
        var denovo = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/manutencoes-ponto/{id}/decisao", tokenGestor, new { aprovar = true }));
        var inexistente = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/manutencoes-ponto/999999/decisao", tokenGestor, new { aprovar = true }));
        Assert.Equal(HttpStatusCode.BadRequest, semMotivo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rejeitado.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, denovo.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Null((await _fabrica.ObterPontoAsync(ana.IdFuncionario, "2026-10-19"))!.Saida);
    }

    [Fact]
    public async Task SemLogin_Recebe401()
    {
        var resposta = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/manutencoes-ponto"));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
