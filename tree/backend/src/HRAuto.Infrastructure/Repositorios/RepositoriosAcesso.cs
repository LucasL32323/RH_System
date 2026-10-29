using HRAuto.Application.Abstractions;
using HRAuto.Domain.Entities;
using HRAuto.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HRAuto.Infrastructure.Repositorios;

/// <summary>Usuários (login, troca e recuperação de senha). Entidades rastreadas: o caso de uso altera e a unidade de trabalho grava.</summary>
public sealed class UsuarioRepositorio : IUsuarioRepositorio
{
    private readonly HrAutoDbContext _db;

    public UsuarioRepositorio(HrAutoDbContext db) => _db = db;

    // A coluna email usa collation NOCASE, então a comparação já ignora maiúsculas/minúsculas.
    public Task<Usuario?> ObterPorEmailAsync(string emailNormalizado, CancellationToken cancelamento = default) =>
        _db.Usuarios
            .Include(u => u.Funcionario)
            .FirstOrDefaultAsync(u => u.Email == emailNormalizado, cancelamento);

    public Task<Usuario?> ObterPorIdAsync(int idUsuario, CancellationToken cancelamento = default) =>
        _db.Usuarios
            .Include(u => u.Funcionario)
            .FirstOrDefaultAsync(u => u.Id == idUsuario, cancelamento);
}

/// <summary>Funcionários (somente leitura nesta fase).</summary>
public sealed class FuncionarioRepositorio : IFuncionarioRepositorio
{
    private readonly HrAutoDbContext _db;

    public FuncionarioRepositorio(HrAutoDbContext db) => _db = db;

    public Task<Funcionario?> ObterPorIdAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        _db.Funcionarios
            .AsNoTracking()
            .Include(f => f.Cargo)
            .Include(f => f.Departamento)
            .FirstOrDefaultAsync(f => f.Id == idFuncionario, cancelamento);

    public Task<FuncionarioDetalhes?> ObterDetalhesAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        _db.FuncionariosDetalhes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.IdFuncionario == idFuncionario, cancelamento);

    public Task<bool> EhGestorAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        _db.Funcionarios.AnyAsync(f => f.IdGestor == idFuncionario && f.Ativo, cancelamento);
}
