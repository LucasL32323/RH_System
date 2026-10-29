using HRAuto.Application.Abstractions;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;
using HRAuto.Domain.ValueObjects;
using HRAuto.Infrastructure.Persistencia;
using HRAuto.Infrastructure.Seguranca;
using HRAuto.Tests.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HRAuto.Tests.Api;

/// <summary>Usuário criado direto no banco de teste (senha: <see cref="ApiFabrica.SenhaPadrao"/>).</summary>
public sealed record UsuarioDeTeste(int IdUsuario, int IdFuncionario, string Codigo, string Email);

/// <summary>
/// Sobe a API em memória com um banco SQLite temporário (arquivo apagado ao final), para os testes de integração
/// não tocarem no hrauto.db de desenvolvimento. Troca o relógio por um relógio fixo (quarta-feira, 21/10/2026, 10:00,
/// Brasília) que o teste move à vontade, e o envio de e-mail por um dublê que guarda as mensagens.
/// </summary>
public class ApiFabrica : WebApplicationFactory<Program>
{
    public const string SenhaPadrao = "Senha@1234";
    public const string ChaveJwtDeTeste = "chave-de-teste-de-integracao-com-mais-de-32-caracteres";

    private readonly string _arquivoBanco = Path.Combine(Path.GetTempPath(), $"hrauto-teste-{Guid.NewGuid():N}.db");

    // Pasta temporária dos PDFs de holerite (apagada ao final), para os testes não escreverem em dados/holerites.
    private readonly string _pastaHolerites = Path.Combine(Path.GetTempPath(), $"hrauto-holerites-{Guid.NewGuid():N}");

    internal RelogioFixo Relogio { get; } = new("2026-10-21T10:00:00");
    internal EnviadorEmailFalso Email { get; } = new();

    /// <summary>Tentativas de login/recuperação por IP e por janela. Alto, para não atrapalhar os demais testes.</summary>
    protected virtual int LimiteAutenticacao => 1000;

    protected override void ConfigureWebHost(IWebHostBuilder construtor)
    {
        construtor.ConfigureAppConfiguration((_, configuracao) =>
        {
            configuracao.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Banco:ConnectionString"] = $"Data Source={_arquivoBanco};Pooling=False",
                ["Holerites:Pasta"] = _pastaHolerites,
                ["Seed:CriarAdminDeTeste"] = "true",
                ["Seed:AdminEmail"] = "admin@hrauto.local",
                ["Seed:AdminSenha"] = "Admin@123",
                ["Jwt:Chave"] = ChaveJwtDeTeste,
                ["App:UrlRedefinicaoSenha"] = "http://localhost:8080/#/redefinir-senha",
                ["RateLimit:AutenticacaoLimite"] = LimiteAutenticacao.ToString()
            });
        });

        construtor.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<IRelogio>();
            servicos.AddSingleton<IRelogio>(Relogio);
            servicos.RemoveAll<IEnviadorEmail>();
            servicos.AddSingleton<IEnviadorEmail>(Email);
        });
    }

    /// <summary>Cria funcionário + usuário (ativos) com e-mail único. Garante que a API já subiu (migrations e seed).</summary>
    public async Task<UsuarioDeTeste> CriarUsuarioAsync(
        string nome, PerfilAcesso perfil = PerfilAcesso.Colaborador, int? idGestor = null, string admissao = "2024-03-11")
    {
        using var escopo = Services.CreateScope(); // Services sobe o servidor, se ainda não subiu
        var db = escopo.ServiceProvider.GetRequiredService<HrAutoDbContext>();
        var email = $"{Guid.NewGuid():N}@hrauto.local";

        var funcionario = new Funcionario
        {
            Nome = nome,
            Email = email,
            DataNascimento = new DateOnly(1995, 5, 5),
            DataAdmissao = DateOnly.ParseExact(admissao, "yyyy-MM-dd"),
            Ativo = true,
            IdGestor = idGestor,
            IdCargo = await db.Cargos.Select(c => c.Id).FirstAsync(),
            IdDepartamento = await db.Departamentos.Select(d => d.Id).FirstAsync()
        };
        var usuario = new Usuario
        {
            Funcionario = funcionario,
            IdPerfil = (int)perfil,
            Email = email,
            Senha = new HashSenhaBCrypt(10).Gerar(SenhaPadrao), // custo mínimo: deixa os testes mais rápidos
            Ativo = true
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        return new UsuarioDeTeste(usuario.Id, funcionario.Id, CodigoFuncionario.Formatar(funcionario.Id), email);
    }

    // ---- Consultas ao banco para conferir o que a API gravou ----

    public async Task<IReadOnlyList<string>> AcoesDeAuditoriaAsync(int idUsuario)
    {
        using var escopo = Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<HrAutoDbContext>();
        return await db.LogsAuditoria.Where(l => l.IdUsuario == idUsuario).OrderBy(l => l.Id).Select(l => l.Acao).ToListAsync();
    }

    public async Task<int> ContarPontosAsync(int idFuncionario)
    {
        using var escopo = Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<HrAutoDbContext>();
        return await db.Pontos.CountAsync(p => p.IdFuncionario == idFuncionario);
    }

    public async Task<decimal> SaldoBancoHorasAsync(int idFuncionario)
    {
        using var escopo = Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<HrAutoDbContext>();
        return RegrasBancoHoras.Saldo(await db.BancoHoras.Where(b => b.IdFuncionario == idFuncionario).ToListAsync());
    }

    /// <summary>Grava direto no banco o ponto de um dia (use null para batida não feita). Devolve o id do ponto.</summary>
    public async Task<int> CriarPontoAsync(
        int idFuncionario, string data, string? entrada, string? saidaIntervalo, string? retorno, string? saida)
    {
        using var escopo = Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<HrAutoDbContext>();
        var ponto = new Ponto
        {
            IdFuncionario = idFuncionario,
            Data = ApoioApp.Data(data),
            Entrada = entrada is null ? null : ApoioApp.Hora(entrada),
            SaidaIntervalo = saidaIntervalo is null ? null : ApoioApp.Hora(saidaIntervalo),
            RetornoIntervalo = retorno is null ? null : ApoioApp.Hora(retorno),
            Saida = saida is null ? null : ApoioApp.Hora(saida)
        };
        db.Pontos.Add(ponto);
        await db.SaveChangesAsync();
        return ponto.Id;
    }

    /// <summary>Ponto do dia como está no banco agora, ou nulo se não existe.</summary>
    public async Task<Ponto?> ObterPontoAsync(int idFuncionario, string data)
    {
        using var escopo = Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<HrAutoDbContext>();
        var dia = ApoioApp.Data(data);
        return await db.Pontos.AsNoTracking().FirstOrDefaultAsync(p => p.IdFuncionario == idFuncionario && p.Data == dia);
    }

    /// <summary>Quantos arquivos de holerite existem na pasta de teste.</summary>
    public int ContarArquivosDeHolerite() =>
        Directory.Exists(_pastaHolerites) ? Directory.GetFiles(_pastaHolerites, "*.pdf", SearchOption.AllDirectories).Length : 0;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_arquivoBanco); } catch (IOException) { /* arquivo ainda em uso: o SO limpa a pasta temp */ }
        try { if (Directory.Exists(_pastaHolerites)) Directory.Delete(_pastaHolerites, recursive: true); } catch (IOException) { /* idem */ }
    }
}

/// <summary>Mesma API, mas com limite de 3 tentativas de autenticação por janela (para testar o bloqueio de força bruta).</summary>
public sealed class ApiFabricaComLimiteBaixo : ApiFabrica
{
    protected override int LimiteAutenticacao => 3;
}
