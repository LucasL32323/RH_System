using System.Net;
using System.Net.Http.Json;
using HRAuto.Application.RegistroPonto;
using HRAuto.Domain.Enums;
using static HRAuto.Tests.Api.ApoioApi;

namespace HRAuto.Tests.Api;

/// <summary>RF17 a RF21, RF24, RF25, RNF08, RNF09: ponto e espelho de ponto pela API de verdade (banco SQLite, JWT, relógio fixo).</summary>
public class PontoEndpointsTests : IClassFixture<ApiFabrica>
{
    private readonly ApiFabrica _fabrica;
    private readonly HttpClient _http;

    public PontoEndpointsTests(ApiFabrica fabrica)
    {
        _fabrica = fabrica;
        _http = fabrica.CreateClient();
        _fabrica.Relogio.Definir("2026-10-21T10:00:00"); // quarta-feira
    }

    // O token vale 8 h e o relógio do teste anda o dia inteiro; por isso cada passo entra de novo.
    private async Task<HttpResponseMessage> BaterPontoAsync(UsuarioDeTeste usuario, string agora)
    {
        _fabrica.Relogio.Definir(agora);
        var token = await _http.EntrarAsync(usuario.Email);
        return await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/ponto/batidas", token));
    }

    [Fact]
    public async Task DiaCompleto_QuatroBatidas_FechaODia_EGeraCreditoNoBancoDeHoras()
    {
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var horarios = new[] { "2026-10-21T08:00:00", "2026-10-21T12:00:00", "2026-10-21T13:00:00", "2026-10-21T17:30:00" };

        BatidaRegistradaDto? ultima = null;
        for (var i = 0; i < horarios.Length; i++)
        {
            var resposta = await BaterPontoAsync(ana, horarios[i]);

            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
            ultima = await resposta.Content.ReadFromJsonAsync<BatidaRegistradaDto>(Json);
            Assert.Equal((TipoBatida)(i + 1), ultima!.Tipo);
        }

        Assert.Equal(30, ultima!.FechamentoDoDia!.ExtrasMinutos); // 8h30 trabalhadas
        Assert.Equal(0.5m, await _fabrica.SaldoBancoHorasAsync(ana.IdFuncionario));
        Assert.Equal(1, await _fabrica.ContarPontosAsync(ana.IdFuncionario));

        var token = await _http.EntrarAsync(ana.Email);
        var hoje = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/ponto/hoje", token));
        var status = await hoje.Content.ReadFromJsonAsync<StatusPontoHojeDto>(Json);
        Assert.Equal(4, status!.Batidas.Count);
        Assert.Null(status.ProximaBatida);
    }

    [Fact]
    public async Task QuintaBatida_Recebe400()
    {
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        foreach (var horario in new[] { "T08:00:00", "T12:00:00", "T13:00:00", "T17:00:00" })
            await BaterPontoAsync(ana, "2026-10-21" + horario);

        var quinta = await BaterPontoAsync(ana, "2026-10-21T17:30:00");

        Assert.Equal(HttpStatusCode.BadRequest, quinta.StatusCode);
        Assert.Equal("application/problem+json", quinta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BatidaRepetida_NoMesmoMinuto_Recebe400_ENaoDuplicaNada()
    {
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        await BaterPontoAsync(ana, "2026-10-21T08:00:00");

        var repetida = await BaterPontoAsync(ana, "2026-10-21T08:00:20");

        Assert.Equal(HttpStatusCode.BadRequest, repetida.StatusCode);
        Assert.Equal(1, await _fabrica.ContarPontosAsync(ana.IdFuncionario));
    }

    [Fact]
    public async Task Espelho_PermissoesPorPerfilEEquipe()
    {
        var gil = await _fabrica.CriarUsuarioAsync("Gil Prado");
        var rita = await _fabrica.CriarUsuarioAsync("Rita Alves", PerfilAcesso.RH);
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza", idGestor: gil.IdFuncionario);
        var bruno = await _fabrica.CriarUsuarioAsync("Bruno Reis");
        var tokenAna = await _http.EntrarAsync(ana.Email);
        var tokenGil = await _http.EntrarAsync(gil.Email);
        var tokenRita = await _http.EntrarAsync(rita.Email);

        async Task<HttpStatusCode> Consultar(string token, string consulta) =>
            (await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/ponto/espelho" + consulta, token))).StatusCode;

        Assert.Equal(HttpStatusCode.OK, await Consultar(tokenAna, ""));                                     // o próprio
        Assert.Equal(HttpStatusCode.Forbidden, await Consultar(tokenAna, $"?codigo={bruno.Codigo}"));        // colega
        Assert.Equal(HttpStatusCode.OK, await Consultar(tokenGil, $"?codigo={ana.Codigo}"));                 // gestor vê a equipe
        Assert.Equal(HttpStatusCode.Forbidden, await Consultar(tokenGil, $"?codigo={bruno.Codigo}"));        // mas não outra equipe
        Assert.Equal(HttpStatusCode.OK, await Consultar(tokenRita, $"?codigo={bruno.Codigo.ToLowerInvariant()}")); // RH vê todos
        Assert.Equal(HttpStatusCode.NotFound, await Consultar(tokenRita, "?codigo=FUNC-9999"));
        Assert.Equal(HttpStatusCode.BadRequest, await Consultar(tokenRita, "?codigo=Ana"));
    }

    [Fact]
    public async Task Espelho_PeriodoInvalido_Recebe400()
    {
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var token = await _http.EntrarAsync(ana.Email);

        var invertido = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/ponto/espelho?de=2026-10-20&ate=2026-10-19", token));
        var naoEhData = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/ponto/espelho?de=isto-nao-e-data", token));

        Assert.Equal(HttpStatusCode.BadRequest, invertido.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, naoEhData.StatusCode);
    }

    [Fact]
    public async Task Espelho_MostraOsDiasETotais()
    {
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        foreach (var horario in new[] { "T08:00:00", "T12:00:00", "T13:00:00", "T17:30:00" })
            await BaterPontoAsync(ana, "2026-10-21" + horario);
        var token = await _http.EntrarAsync(ana.Email);

        var resposta = await _http.SendAsync(Pedido(HttpMethod.Get, "/api/v1/ponto/espelho?de=2026-10-19&ate=2026-10-21", token));
        var espelho = await resposta.Content.ReadFromJsonAsync<EspelhoPontoDto>(Json);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(ana.Codigo, espelho!.Codigo);
        Assert.Equal(3, espelho.Dias.Count);
        Assert.Equal(SituacaoDia.Falta, espelho.Dias[0].Situacao);      // segunda sem batida
        Assert.Equal(SituacaoDia.Completo, espelho.Dias[2].Situacao);   // hoje completo
        Assert.Equal(30, espelho.Totais.ExtrasMinutos);
        Assert.Equal(0.5m, espelho.SaldoBancoHorasHoras);
    }

    [Fact]
    public async Task Sincronizar_FilaOffline_EReenvioNaoDuplica()
    {
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var token = await _http.EntrarAsync(ana.Email);
        var fila = new
        {
            batidas = new object[]
            {
                new { data = "2026-10-20", tipo = "Entrada", horario = "08:00:00" },
                new { data = "2026-10-20", tipo = "SaidaIntervalo", horario = "12:00:00" },
                new { data = "2026-10-20", tipo = "RetornoIntervalo", horario = "13:00:00" },
                new { data = "2026-10-20", tipo = "Saida", horario = "17:00:00" },
                new { data = "2026-10-25", tipo = "Entrada", horario = "08:00:00" } // dia futuro: recusada
            }
        };

        var primeira = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/ponto/batidas/sincronizar", token, fila));
        var reenvio = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/ponto/batidas/sincronizar", token, fila));

        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);
        var itens1 = await primeira.Content.ReadFromJsonAsync<List<ResultadoBatidaOfflineDto>>(Json);
        Assert.Equal(4, itens1!.Count(i => i.Situacao == SituacaoItemOffline.Aplicada));
        Assert.Equal(SituacaoItemOffline.Recusada, itens1[4].Situacao);

        var itens2 = await reenvio.Content.ReadFromJsonAsync<List<ResultadoBatidaOfflineDto>>(Json);
        Assert.Equal(4, itens2!.Count(i => i.Situacao == SituacaoItemOffline.JaRegistrada));
        Assert.Equal(1, await _fabrica.ContarPontosAsync(ana.IdFuncionario)); // RNF09: nada duplicado
    }

    [Fact]
    public async Task Sincronizar_FilaVaziaOuTipoInvalido_Recebe400()
    {
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var token = await _http.EntrarAsync(ana.Email);

        var vazia = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/ponto/batidas/sincronizar", token, new { batidas = Array.Empty<object>() }));
        var tipoInvalido = await _http.SendAsync(Pedido(HttpMethod.Post, "/api/v1/ponto/batidas/sincronizar", token,
            new { batidas = new object[] { new { data = "2026-10-20", tipo = "Almoco", horario = "08:00:00" } } }));

        Assert.Equal(HttpStatusCode.BadRequest, vazia.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tipoInvalido.StatusCode);
    }

    [Fact]
    public async Task Sincronizar_CorpoGrandeDemais_EhRecusado()
    {
        var ana = await _fabrica.CriarUsuarioAsync("Ana Souza");
        var token = await _http.EntrarAsync(ana.Email);
        var pedido = Pedido(HttpMethod.Post, "/api/v1/ponto/batidas/sincronizar", token);
        pedido.Content = new StringContent(new string(' ', 70 * 1024) + "{}", System.Text.Encoding.UTF8, "application/json");

        var resposta = await _http.SendAsync(pedido);

        Assert.True(resposta.StatusCode is HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.BadRequest, $"status {(int)resposta.StatusCode}");
    }
}
