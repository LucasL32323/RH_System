using HRAuto.Application.Abstractions;
using HRAuto.Infrastructure.Persistencia;
using HRAuto.Infrastructure.Persistencia.Seed;
using HRAuto.Infrastructure.Seguranca;
using HRAuto.Infrastructure.Tempo;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HRAuto.Tests.Persistencia;

/// <summary>
/// Cria um SQLite em memória por teste. A conexão fica aberta até o Dispose (senão o banco em memória some).
/// O banco é montado só com as migrations, como exige o RNF15.
/// </summary>
public sealed class BancoDeTeste : IDisposable
{
    private readonly SqliteConnection _conexao;

    public BancoDeTeste()
    {
        _conexao = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _conexao.Open();
    }

    public HrAutoDbContext NovoContexto()
    {
        var opcoes = new DbContextOptionsBuilder<HrAutoDbContext>().UseSqlite(_conexao).Options;
        return new HrAutoDbContext(opcoes);
    }

    /// <summary>Aplica as migrations e o seed. Por padrão cria também o admin de teste.</summary>
    public async Task InicializarAsync(bool criarAdmin = true)
    {
        await using var contexto = NovoContexto();
        await NovoInicializador(contexto, criarAdmin).ExecutarAsync();
    }

    public static InicializadorBanco NovoInicializador(HrAutoDbContext contexto, bool criarAdmin) =>
        new(
            contexto,
            new HashSenhaBCrypt(10), // custo mínimo: deixa os testes mais rápidos
            new RelogioBrasilia(() => new DateTimeOffset(2026, 10, 16, 15, 0, 0, TimeSpan.Zero)),
            Options.Create(new OpcoesSeed
            {
                CriarAdminDeTeste = criarAdmin,
                AdminEmail = "Admin@HRAuto.local",
                AdminSenha = "Admin@123"
            }),
            NullLogger<InicializadorBanco>.Instance);

    public void Dispose() => _conexao.Dispose();
}
