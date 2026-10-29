using System.Net;
using System.Net.Http.Json;
using HRAuto.Application.Sistema;
using HRAuto.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRAuto.Tests.Api;

/// <summary>Testes de integração: sobem a API em memória (com migrations e seed) e chamam os endpoints reais.</summary>
public class SistemaEndpointsTests : IClassFixture<ApiFabrica>
{
    private readonly ApiFabrica _fabrica;
    private readonly HttpClient _cliente;

    public SistemaEndpointsTests(ApiFabrica fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task Health_RetornaOkEHealthy()
    {
        var resposta = await _cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Status_RetornaDadosDaApiComHoraDeBrasilia()
    {
        var resposta = await _cliente.GetAsync("/api/v1/sistema/status");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var dto = await resposta.Content.ReadFromJsonAsync<StatusSistemaDto>();
        Assert.NotNull(dto);
        Assert.Equal(ConsultarStatusSistema.NomeAplicacao, dto!.Nome);
        Assert.Equal(ConsultarStatusSistema.VersaoAtual, dto.Versao);
        Assert.Equal(TimeSpan.FromHours(-3), dto.HoraServidor.Offset);
    }

    [Fact]
    public async Task SubidaDaApi_AplicaMigrationsEGravaSeed()
    {
        _ = _cliente; // garante que a API já subiu (e rodou o inicializador) antes de consultar o banco

        using var escopo = _fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<HrAutoDbContext>();

        Assert.Equal(3, await db.Perfis.CountAsync());
        Assert.Equal(1, await db.Usuarios.CountAsync());
    }
}
