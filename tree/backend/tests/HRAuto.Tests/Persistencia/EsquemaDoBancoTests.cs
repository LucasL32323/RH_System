using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HRAuto.Tests.Persistencia;

/// <summary>
/// RNF15: "Banco recriado do zero só com as migrations". Estes testes montam o banco pelas migrations e o
/// comparam com o modelo físico oficial (modelo_fisico_sqlite.sql): tabelas, colunas, tipos, chaves primárias e FKs.
/// Pré-requisito: a migration inicial gerada (ver README, "Banco de dados").
/// </summary>
public class EsquemaDoBancoTests
{
    private sealed record Esquema(
        SortedSet<string> Tabelas,
        SortedSet<string> Colunas,      // "TABELA.coluna:TIPO"
        SortedSet<string> Chaves,       // "TABELA.coluna#posição"
        SortedSet<string> Estrangeiras, // "TABELA.coluna->TABELA_PAI.coluna_pai"
        SortedSet<string> Indices);     // nomes dos índices criados explicitamente

    private static Esquema Ler(SqliteConnection conexao)
    {
        var e = new Esquema(new(), new(), new(), new(), new());

        using (var cmd = conexao.CreateCommand())
        {
            cmd.CommandText =
                "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory'";
            using var r = cmd.ExecuteReader();
            while (r.Read()) e.Tabelas.Add(r.GetString(0));
        }

        foreach (var tabela in e.Tabelas)
        {
            using (var cmd = conexao.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info('{tabela}')";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    e.Colunas.Add($"{tabela}.{r.GetString(1)}:{r.GetString(2).ToUpperInvariant()}");
                    if (r.GetInt32(5) > 0) e.Chaves.Add($"{tabela}.{r.GetString(1)}#{r.GetInt32(5)}");
                }
            }

            using (var cmd = conexao.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA foreign_key_list('{tabela}')";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    e.Estrangeiras.Add($"{tabela}.{r.GetString(3)}->{r.GetString(2)}.{r.GetString(4)}");
            }
        }

        using (var cmd = conexao.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE 'IX_%'";
            using var r = cmd.ExecuteReader();
            while (r.Read()) e.Indices.Add(r.GetString(0));
        }

        return e;
    }

    private static (Esquema Oficial, Esquema Migrations) Montar()
    {
        // 1) Banco criado pelo SQL oficial.
        var oficial = new SqliteConnection("Data Source=:memory:");
        oficial.Open();
        using (var cmd = oficial.CreateCommand())
        {
            cmd.CommandText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "modelo_fisico_sqlite.sql"));
            cmd.ExecuteNonQuery();
        }

        // 2) Banco criado só pelas migrations.
        using var banco = new BancoDeTeste();
        using var contexto = banco.NovoContexto();
        Assert.True(contexto.Database.GetMigrations().Any(),
            "Nenhuma migration encontrada. Gere a inicial: dotnet ef migrations add ModeloFisicoInicial " +
            "--project src/HRAuto.Infrastructure --output-dir Persistencia/Migrations");
        contexto.Database.Migrate();

        var conexaoMigrations = (SqliteConnection)contexto.Database.GetDbConnection();
        return (Ler(oficial), Ler(conexaoMigrations));
    }

    [Fact]
    public void Migrations_CriamAsMesmas25Tabelas_DoModeloFisico()
    {
        var (oficial, migrations) = Montar();

        Assert.Equal(25, oficial.Tabelas.Count);
        Assert.Equal(oficial.Tabelas, migrations.Tabelas);
    }

    [Fact]
    public void Migrations_CriamAsMesmasColunasETipos_DoModeloFisico()
    {
        var (oficial, migrations) = Montar();

        Assert.Equal(oficial.Colunas, migrations.Colunas);
    }

    [Fact]
    public void Migrations_CriamAsMesmasChavesPrimariasEEstrangeiras_DoModeloFisico()
    {
        var (oficial, migrations) = Montar();

        Assert.Equal(oficial.Chaves, migrations.Chaves);
        Assert.Equal(oficial.Estrangeiras, migrations.Estrangeiras);
    }

    [Fact]
    public void Migrations_CriamOsIndicesDefinidos()
    {
        var (_, migrations) = Montar();

        // Índices do modelo físico (menos idx_ponto_func_data, trocado por um índice único com as mesmas colunas)
        // + índices únicos acrescentados: e-mail do usuário, ponto por dia e código do certificado.
        var esperados = new[]
        {
            "idx_ferias_func", "idx_holerite_func", "idx_notificacao_func", "idx_log_usuario",
            "ux_ponto_func_data", "ux_usuario_email", "ux_certificado_codigo"
        };
        foreach (var indice in esperados)
            Assert.Contains(indice, migrations.Indices);
    }
}
