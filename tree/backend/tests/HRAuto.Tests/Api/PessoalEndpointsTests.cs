using System.Net;
using HRAuto.Domain.Enums;
using static HRAuto.Tests.Api.ApoioApi;

namespace HRAuto.Tests.Api;

/// <summary>
/// RF24, RF26, RF27, RF28, RF36: férias, banco de horas, feriados e notificações pela API de verdade
/// (SQLite, JWT, relógio fixo em 21/10/2026). Cada teste cria seus próprios usuários.
/// </summary>
public class PessoalEndpointsTests : IClassFixture<ApiFabrica>
{
    private readonly ApiFabrica _fabrica;
    private readonly HttpClient _http;

    public PessoalEndpointsTests(ApiFabrica fabrica)
    {
        _fabrica = fabrica;
        _http = fabrica.CreateClient();
        _fabrica.Relogio.Definir("2026-10-21T10:00:00");
    }

    [Fact]
    public async Task Ferias_ColaboradorSolicita_GestorAprova_ColaboradorRecebeOAviso()
    {
        var gestor = await _fabrica.CriarUsuarioAsync("Gil Gestor");
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza", idGestor: gestor.IdFuncionario);
        var tokenAna = await _http.EntrarAsync(ana.Email);
        var tokenGestor = await _http.EntrarAsync(gestor.Email);

        var criada = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/ferias", tokenAna,
            new { inicio = "2026-11-16", fim = "2026-11-30", observacao = "Viagem em família" }));

        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        var ferias = await criada.LerJsonAsync();
        var id = ferias.GetProperty("id").GetInt32();
        Assert.Equal("Pendente", ferias.GetProperty("status").GetString());
        Assert.Equal(15, ferias.GetProperty("dias").GetInt32());
        Assert.Equal(ana.Codigo, ferias.GetProperty("codigoFuncionario").GetString());

        // O gestor foi avisado e enxerga a solicitação na lista da equipe.
        var avisos = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/notificacoes?somenteNaoLidas=true", tokenGestor))).LerJsonAsync();
        Assert.Equal(1, avisos.GetProperty("total").GetInt32());
        Assert.Equal("Nova solicitação de férias", avisos.GetProperty("itens")[0].GetProperty("titulo").GetString());

        var equipe = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/ferias?escopo=equipe&status=Pendente", tokenGestor))).LerJsonAsync();
        Assert.Equal(1, equipe.GetProperty("total").GetInt32());

        var decisao = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/ferias/{id}/decisao", tokenGestor,
            new { aprovar = true, observacao = "Bom descanso" }));
        Assert.Equal(HttpStatusCode.OK, decisao.StatusCode);
        Assert.Equal("Aprovada", (await decisao.LerJsonAsync()).GetProperty("status").GetString());

        // Ana recebe o aviso da decisão, marca como lido e o selo zera.
        var quantidade = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/notificacoes/nao-lidas/quantidade", tokenAna))).LerJsonAsync();
        Assert.Equal(1, quantidade.GetProperty("quantidade").GetInt32());

        var lista = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/notificacoes", tokenAna))).LerJsonAsync();
        var idAviso = lista.GetProperty("itens")[0].GetProperty("id").GetInt32();
        Assert.Equal("Férias aprovada", lista.GetProperty("itens")[0].GetProperty("titulo").GetString());

        var lida = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/notificacoes/{idAviso}/leitura", tokenAna));
        Assert.Equal(HttpStatusCode.OK, lida.StatusCode);

        var depois = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/notificacoes/nao-lidas/quantidade", tokenAna))).LerJsonAsync();
        Assert.Equal(0, depois.GetProperty("quantidade").GetInt32());

        Assert.Contains("DecidirFerias", await _fabrica.AcoesDeAuditoriaAsync(gestor.IdUsuario));
        Assert.Contains("SolicitarFerias", await _fabrica.AcoesDeAuditoriaAsync(ana.IdUsuario));
    }

    [Fact]
    public async Task Ferias_RegrasDeNegocio_ViramErro400_EPermissoesErro403()
    {
        var gestor = await _fabrica.CriarUsuarioAsync("Gil Gestor");
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza", idGestor: gestor.IdFuncionario);
        var bruno = await _fabrica.CriarUsuarioAsync("Bruno Lima", idGestor: gestor.IdFuncionario);
        var tokenAna = await _http.EntrarAsync(ana.Email);
        var tokenBruno = await _http.EntrarAsync(bruno.Email);

        var domingo = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/ferias", tokenAna, new { inicio = "2026-11-22", fim = "2026-11-30" }));
        Assert.Equal(HttpStatusCode.BadRequest, domingo.StatusCode);

        var criada = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/ferias", tokenAna, new { inicio = "2026-11-16", fim = "2026-11-30" }));
        var id = (await criada.LerJsonAsync()).GetProperty("id").GetInt32();

        // Colega não decide, não lista a equipe e não cancela a solicitação alheia.
        var colega = await _http.SendAsync(Pedido(HttpMethod.Post, $"/api/v1/ferias/{id}/decisao", tokenBruno, new { aprovar = true }));
        var equipe = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/ferias?escopo=equipe", tokenBruno));
        var alheia = await _http.SendAsync(Pedido(HttpMethod.Delete, $"/api/v1/ferias/{id}", tokenBruno));
        Assert.Equal(HttpStatusCode.Forbidden, colega.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, equipe.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, alheia.StatusCode);

        // A própria pessoa cancela enquanto está Pendente; depois some.
        var cancelada = await _http.SendAsync(Pedido(HttpMethod.Delete, $"/api/v1/ferias/{id}", tokenAna));
        Assert.Equal(HttpStatusCode.NoContent, cancelada.StatusCode);
        var denovo = await _http.SendAsync(Pedido(HttpMethod.Delete, $"/api/v1/ferias/{id}", tokenAna));
        Assert.Equal(HttpStatusCode.NotFound, denovo.StatusCode);
    }

    [Fact]
    public async Task BancoHoras_RhAjusta_ColaboradorVeOSaldo_EOutrosNaoVeem()
    {
        var rh = await _fabrica.CriarUsuarioAsync("Rita RH", PerfilAcesso.RH);
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var bruno = await _fabrica.CriarUsuarioAsync("Bruno Lima");
        var tokenRh = await _http.EntrarAsync(rh.Email);
        var tokenAna = await _http.EntrarAsync(ana.Email);

        var ajuste = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/banco-horas/ajustes", tokenRh,
            new { codigoFuncionario = ana.Codigo, tipo = "Credito", horas = 2.5, observacao = "Compensação de sábado trabalhado" }));
        Assert.Equal(HttpStatusCode.Created, ajuste.StatusCode);
        Assert.Equal("+2h30min", (await ajuste.LerJsonAsync()).GetProperty("saldoFormatado").GetString());

        var meu = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/banco-horas", tokenAna))).LerJsonAsync();
        Assert.Equal(2.5m, meu.GetProperty("saldoHoras").GetDecimal());
        Assert.Equal(1, meu.GetProperty("lancamentos").GetArrayLength());
        Assert.Equal(2.5m, await _fabrica.SaldoBancoHorasAsync(ana.IdFuncionario));

        // Colaborador não vê o saldo de outra pessoa nem faz ajuste; o RH vê qualquer um.
        var outro = await _http.SendAsync(Pedido(HttpMethod.Get, $"/api/v1/banco-horas/{bruno.Codigo}", tokenAna));
        var proprioAjuste = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/banco-horas/ajustes", tokenAna,
            new { codigoFuncionario = ana.Codigo, tipo = "Credito", horas = 8, observacao = "Tentativa indevida" }));
        var peloRh = await _http.SendAsync(Pedido(HttpMethod.Get, $"/api/v1/banco-horas/{ana.Codigo}", tokenRh));
        Assert.Equal(HttpStatusCode.Forbidden, outro.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, proprioAjuste.StatusCode);
        Assert.Equal(HttpStatusCode.OK, peloRh.StatusCode);

        // Observação curta é recusada e nada é gravado.
        var semObservacao = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/banco-horas/ajustes", tokenRh,
            new { codigoFuncionario = ana.Codigo, tipo = "Debito", horas = 1, observacao = "ok" }));
        Assert.Equal(HttpStatusCode.BadRequest, semObservacao.StatusCode);
        Assert.Equal(2.5m, await _fabrica.SaldoBancoHorasAsync(ana.IdFuncionario));

        Assert.Contains("AjustarBancoHoras", await _fabrica.AcoesDeAuditoriaAsync(rh.IdUsuario));
    }

    [Fact]
    public async Task Feriados_RhCadastraEExclui_ColaboradorSoConsulta()
    {
        var rh = await _fabrica.CriarUsuarioAsync("Rita RH", PerfilAcesso.RH);
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var tokenRh = await _http.EntrarAsync(rh.Email);
        var tokenAna = await _http.EntrarAsync(ana.Email);
        var feriado = new { data = "2026-03-15", nome = "Aniversário de Testópolis", tipo = "Municipal", cidade = "Testópolis", estado = "sp" };

        var criado = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/feriados", tokenRh, feriado));
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        var json = await criado.LerJsonAsync();
        var id = json.GetProperty("id").GetInt32();
        Assert.Equal("SP", json.GetProperty("estado").GetString());

        var repetido = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/feriados", tokenRh, feriado));
        var porColaborador = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/feriados", tokenAna, feriado));
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, porColaborador.StatusCode);

        var lista = await (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/feriados?ano=2026", tokenAna))).LerJsonAsync();
        Assert.Contains(lista.EnumerateArray(), f => f.GetProperty("id").GetInt32() == id);

        var excluidoPorColaborador = await _http.SendAsync(Pedido(HttpMethod.Delete, $"/api/v1/feriados/{id}", tokenAna));
        var excluido = await _http.SendAsync(Pedido(HttpMethod.Delete, $"/api/v1/feriados/{id}", tokenRh));
        var denovo = await _http.SendAsync(Pedido(HttpMethod.Delete, $"/api/v1/feriados/{id}", tokenRh));
        Assert.Equal(HttpStatusCode.Forbidden, excluidoPorColaborador.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, excluido.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, denovo.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/ferias")]
    [InlineData("/api/v1/banco-horas")]
    [InlineData("/api/v1/notificacoes")]
    [InlineData("/api/v1/feriados")]
    public async Task SemToken_Da401(string url)
    {
        var resposta = await _http.SendAsync(Pedido(HttpMethod.Get, url));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
