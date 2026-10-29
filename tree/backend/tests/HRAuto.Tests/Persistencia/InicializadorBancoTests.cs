using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace HRAuto.Tests.Persistencia;

/// <summary>Seed (perfis, cargos, departamentos, admin de teste) e regras de integridade do banco.</summary>
public class InicializadorBancoTests
{
    [Fact]
    public async Task Seed_CriaOsTresPerfis_ComIdsDoEnum()
    {
        using var banco = new BancoDeTeste();
        await banco.InicializarAsync();

        await using var db = banco.NovoContexto();
        var perfis = await db.Perfis.OrderBy(p => p.Id).ToListAsync();

        Assert.Equal(3, perfis.Count);
        Assert.Equal((int)PerfilAcesso.Colaborador, perfis[0].Id);
        Assert.Equal(NomesPerfil.Colaborador, perfis[0].Nome);
        Assert.Equal((int)PerfilAcesso.RH, perfis[1].Id);
        Assert.Equal(NomesPerfil.RH, perfis[1].Nome);
        Assert.Equal((int)PerfilAcesso.Admin, perfis[2].Id);
        Assert.Equal(NomesPerfil.Admin, perfis[2].Nome);
    }

    [Fact]
    public async Task Seed_CriaCargosEDepartamentos()
    {
        using var banco = new BancoDeTeste();
        await banco.InicializarAsync();

        await using var db = banco.NovoContexto();

        Assert.Equal(5, await db.Cargos.CountAsync());
        Assert.Equal(5, await db.Departamentos.CountAsync());
        Assert.True(await db.Departamentos.AnyAsync(d => d.Nome == "Recursos Humanos"));
    }

    [Fact]
    public async Task Seed_CriaAdminDeTeste_ComSenhaEmHashBCrypt_ECodigoFunc0001()
    {
        using var banco = new BancoDeTeste();
        await banco.InicializarAsync();

        await using var db = banco.NovoContexto();
        var usuario = await db.Usuarios.Include(u => u.Funcionario).SingleAsync();

        Assert.Equal((int)PerfilAcesso.Admin, usuario.IdPerfil);
        Assert.Equal("admin@hrauto.local", usuario.Email); // e-mail normalizado para minúsculas
        Assert.True(usuario.Ativo);
        Assert.NotEqual("Admin@123", usuario.Senha);
        Assert.True(new HashSenhaBCrypt(10).Verificar("Admin@123", usuario.Senha));
        Assert.Equal("FUNC-0001", usuario.Funcionario!.Codigo);
        Assert.Equal(new DateOnly(2026, 10, 16), usuario.Funcionario.DataAdmissao);
    }

    [Fact]
    public async Task Seed_SemAdminDeTeste_NaoCriaUsuarios()
    {
        using var banco = new BancoDeTeste();
        await banco.InicializarAsync(criarAdmin: false);

        await using var db = banco.NovoContexto();

        Assert.Equal(0, await db.Usuarios.CountAsync());
        Assert.Equal(0, await db.Funcionarios.CountAsync());
        Assert.Equal(3, await db.Perfis.CountAsync()); // perfis sempre existem
    }

    [Fact]
    public async Task Seed_RodarDuasVezes_NaoDuplicaRegistros()
    {
        using var banco = new BancoDeTeste();
        await banco.InicializarAsync();
        await banco.InicializarAsync();

        await using var db = banco.NovoContexto();

        Assert.Equal(3, await db.Perfis.CountAsync());
        Assert.Equal(5, await db.Cargos.CountAsync());
        Assert.Equal(5, await db.Departamentos.CountAsync());
        Assert.Equal(1, await db.Usuarios.CountAsync());
        Assert.Equal(1, await db.Funcionarios.CountAsync());
    }

    [Fact]
    public async Task ChaveEstrangeira_EhImposta_PeloBanco()
    {
        using var banco = new BancoDeTeste();
        await banco.InicializarAsync();

        await using var db = banco.NovoContexto();
        db.Pontos.Add(new Ponto { IdFuncionario = 999, Data = new DateOnly(2026, 10, 16) }); // funcionário inexistente

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Ponto_UmRegistroPorFuncionarioPorDia_RNF09()
    {
        using var banco = new BancoDeTeste();
        await banco.InicializarAsync();

        await using var db = banco.NovoContexto();
        var idFuncionario = await db.Funcionarios.Select(f => f.Id).SingleAsync();
        var dia = new DateOnly(2026, 10, 16);

        db.Pontos.Add(new Ponto { IdFuncionario = idFuncionario, Data = dia, Entrada = new TimeOnly(8, 0) });
        await db.SaveChangesAsync();

        db.Pontos.Add(new Ponto { IdFuncionario = idFuncionario, Data = dia, Entrada = new TimeOnly(8, 5) });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Usuario_EmailUnico_SemDiferenciarMaiusculas()
    {
        using var banco = new BancoDeTeste();
        await banco.InicializarAsync();

        await using var db = banco.NovoContexto();
        var idFuncionario = await db.Funcionarios.Select(f => f.Id).SingleAsync();

        db.Usuarios.Add(new Usuario
        {
            IdFuncionario = idFuncionario,
            IdPerfil = (int)PerfilAcesso.Colaborador,
            Email = "ADMIN@HRAUTO.LOCAL", // mesmo e-mail do admin, em maiúsculas
            Senha = "hash"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DatasEHoras_SaoGravadasComoTextoIso8601()
    {
        using var banco = new BancoDeTeste();
        await banco.InicializarAsync();

        await using (var db = banco.NovoContexto())
        {
            var idFuncionario = await db.Funcionarios.Select(f => f.Id).SingleAsync();
            db.Pontos.Add(new Ponto
            {
                IdFuncionario = idFuncionario,
                Data = new DateOnly(2026, 10, 16),
                Entrada = new TimeOnly(8, 30, 15)
            });
            db.Notificacoes.Add(new Notificacao
            {
                IdFuncionario = idFuncionario,
                Tipo = "Aviso",
                Titulo = "Teste",
                Mensagem = "Mensagem",
                DataEnvio = new DateTime(2026, 10, 16, 9, 5, 7)
            });
            await db.SaveChangesAsync();
        }

        await using var leitura = banco.NovoContexto();
        var conexao = leitura.Database.GetDbConnection();

        string Escalar(string sql)
        {
            using var cmd = conexao.CreateCommand();
            cmd.CommandText = sql;
            return (string)cmd.ExecuteScalar()!;
        }

        Assert.Equal("2026-10-16", Escalar("SELECT data FROM PONTO"));
        Assert.Equal("08:30:15", Escalar("SELECT entrada FROM PONTO"));
        Assert.Equal("2026-10-16T09:05:07", Escalar("SELECT data_envio FROM NOTIFICACAO"));

        // E volta como objeto, sem perda.
        var notificacao = await leitura.Notificacoes.SingleAsync();
        Assert.Equal(new DateTime(2026, 10, 16, 9, 5, 7), notificacao.DataEnvio);
    }

    [Fact]
    public async Task ColunasComNomeDiferente_Pergunta_Resposta_SaoMapeadas()
    {
        using var banco = new BancoDeTeste();
        await banco.InicializarAsync();

        await using var db = banco.NovoContexto();
        var idFuncionario = await db.Funcionarios.Select(f => f.Id).SingleAsync();

        var formulario = new Formulario
        {
            Titulo = "Clima",
            Tipo = "Clima",
            IdAutor = idFuncionario,
            DataCriacao = new DateTime(2026, 10, 16, 8, 0, 0)
        };
        db.Formularios.Add(formulario);
        await db.SaveChangesAsync();

        var pergunta = new Pergunta { IdFormulario = formulario.Id, Enunciado = "Como você está hoje?", TipoResposta = "Escolha", Ordem = 1, Obrigatoria = true };
        db.Perguntas.Add(pergunta);
        await db.SaveChangesAsync();

        // Resposta anônima: IdFuncionario nulo (RNF08).
        db.Respostas.Add(new Resposta { IdPergunta = pergunta.Id, IdFuncionario = null, Conteudo = "Bem", DataResposta = new DateTime(2026, 10, 16, 8, 1, 0) });
        await db.SaveChangesAsync();

        var salva = await db.Respostas.AsNoTracking().SingleAsync();
        Assert.Equal("Bem", salva.Conteudo);
        Assert.Null(salva.IdFuncionario);
    }
}
