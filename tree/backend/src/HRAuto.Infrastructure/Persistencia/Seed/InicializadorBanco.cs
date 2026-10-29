using HRAuto.Application.Abstractions;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRAuto.Infrastructure.Persistencia.Seed;

/// <summary>
/// Aplica as migrations (o banco é recriado do zero só com elas, RNF15) e grava os dados iniciais.
/// É idempotente: pode rodar a cada subida da API sem duplicar registros.
/// </summary>
public sealed class InicializadorBanco : IInicializadorBanco
{
    private static readonly (PerfilAcesso Perfil, string Descricao)[] Perfis =
    {
        (PerfilAcesso.Colaborador, "Acessa as próprias informações: ponto, férias, holerites, advertências e treinamentos."),
        (PerfilAcesso.RH, "Gerencia funcionários, ponto, férias, advertências, avisos, treinamentos e formulários."),
        (PerfilAcesso.Admin, "Acesso total, inclusive usuários, perfis e auditoria.")
    };

    private static readonly string[] Cargos =
    {
        "Administrador do Sistema", "Analista de RH", "Gerente de Equipe", "Analista de Sistemas", "Assistente Administrativo"
    };

    private static readonly string[] Departamentos =
    {
        "Recursos Humanos", "Tecnologia da Informação", "Administrativo", "Financeiro", "Operações"
    };

    private readonly HrAutoDbContext _db;
    private readonly IHashSenha _hash;
    private readonly IRelogio _relogio;
    private readonly OpcoesSeed _opcoes;
    private readonly ILogger<InicializadorBanco> _log;

    public InicializadorBanco(
        HrAutoDbContext db,
        IHashSenha hash,
        IRelogio relogio,
        IOptions<OpcoesSeed> opcoes,
        ILogger<InicializadorBanco> log)
    {
        _db = db;
        _hash = hash;
        _relogio = relogio;
        _opcoes = opcoes.Value;
        _log = log;
    }

    public async Task ExecutarAsync(CancellationToken cancelamento = default)
    {
        // Sem migrations o banco não teria tabelas: avisa com o comando exato em vez de falhar com "no such table".
        if (!_db.Database.GetMigrations().Any())
            throw new InvalidOperationException(
                "Nenhuma migration encontrada. Gere a inicial (uma única vez) na pasta backend: " +
                "dotnet ef migrations add ModeloFisicoInicial --project src/HRAuto.Infrastructure --output-dir Persistencia/Migrations");

        await _db.Database.MigrateAsync(cancelamento);

        await SemearPerfisAsync(cancelamento);
        await SemearCargosAsync(cancelamento);
        await SemearDepartamentosAsync(cancelamento);

        if (_opcoes.CriarAdminDeTeste)
            await SemearAdminDeTesteAsync(cancelamento);
    }

    // Os ids dos perfis são fixos (1 = Colaborador, 2 = RH, 3 = Admin), iguais ao enum PerfilAcesso.
    private async Task SemearPerfisAsync(CancellationToken ct)
    {
        foreach (var (perfil, descricao) in Perfis)
        {
            var id = (int)perfil;
            if (await _db.Perfis.AnyAsync(p => p.Id == id, ct)) continue;

            var entidade = new Perfil { Nome = NomesPerfil.De(perfil), Descricao = descricao };
            _db.Perfis.Add(entidade);
            _db.Entry(entidade).Property(p => p.Id).CurrentValue = id;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SemearCargosAsync(CancellationToken ct)
    {
        foreach (var nome in Cargos)
            if (!await _db.Cargos.AnyAsync(c => c.NomeCargo == nome, ct))
                _db.Cargos.Add(new Cargo { NomeCargo = nome });
        await _db.SaveChangesAsync(ct);
    }

    private async Task SemearDepartamentosAsync(CancellationToken ct)
    {
        foreach (var nome in Departamentos)
            if (!await _db.Departamentos.AnyAsync(d => d.Nome == nome, ct))
                _db.Departamentos.Add(new Departamento { Nome = nome });
        await _db.SaveChangesAsync(ct);
    }

    private async Task SemearAdminDeTesteAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.AdminSenha))
            throw new InvalidOperationException("Seed:AdminSenha é obrigatória quando Seed:CriarAdminDeTeste = true.");

        var email = _opcoes.AdminEmail.Trim().ToLowerInvariant();
        if (await _db.Usuarios.AnyAsync(u => u.Email == email, ct)) return;

        var idCargo = await _db.Cargos.Where(c => c.NomeCargo == "Administrador do Sistema").Select(c => c.Id).SingleAsync(ct);
        var idDepartamento = await _db.Departamentos.Where(d => d.Nome == "Tecnologia da Informação").Select(d => d.Id).SingleAsync(ct);

        var funcionario = new Funcionario
        {
            Nome = _opcoes.AdminNome,
            DataNascimento = new DateOnly(1990, 1, 1), // valor fictício de teste
            DataAdmissao = _relogio.Hoje(),
            Ativo = true,
            Email = email,
            IdCargo = idCargo,
            IdDepartamento = idDepartamento
        };

        _db.Usuarios.Add(new Usuario
        {
            Funcionario = funcionario,
            IdPerfil = (int)PerfilAcesso.Admin,
            Email = email,
            Senha = _hash.Gerar(_opcoes.AdminSenha),
            Ativo = true
        });

        await _db.SaveChangesAsync(ct);
        _log.LogWarning("Usuário administrador de TESTE criado ({Email}). Não use em produção.", email);
    }
}
