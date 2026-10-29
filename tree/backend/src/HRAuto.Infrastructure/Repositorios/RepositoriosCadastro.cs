using HRAuto.Application.Abstractions;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HRAuto.Infrastructure.Repositorios;

/// <summary>Cadastro de funcionários (RF09 a RF16). Consultas de lista são sem rastreamento; "ParaEdicao" devolve entidade rastreada.</summary>
public sealed class FuncionarioCadastroRepositorio : IFuncionarioCadastroRepositorio
{
    private const int ProfundidadeMaximaHierarquia = 50; // proteção contra dados circulares já existentes no banco

    private readonly HrAutoDbContext _db;

    public FuncionarioCadastroRepositorio(HrAutoDbContext db) => _db = db;

    private IQueryable<Funcionario> ComVinculos() =>
        _db.Funcionarios.Include(f => f.Cargo).Include(f => f.Departamento).Include(f => f.Gestor);

    public async Task<ResultadoPaginado<Funcionario>> ListarAsync(FiltroFuncionarios filtro, CancellationToken cancelamento = default)
    {
        var consulta = _db.Funcionarios.AsNoTracking().AsQueryable();
        if (filtro.IdFuncionario is { } id) consulta = consulta.Where(f => f.Id == id);
        if (filtro.IdCargo is { } cargo) consulta = consulta.Where(f => f.IdCargo == cargo);
        if (filtro.IdDepartamento is { } departamento) consulta = consulta.Where(f => f.IdDepartamento == departamento);
        if (filtro.Ativo is { } ativo) consulta = consulta.Where(f => f.Ativo == ativo);
        if (filtro.IdGestor is { } gestor) consulta = consulta.Where(f => f.IdGestor == gestor);

        var total = await consulta.CountAsync(cancelamento);
        var itens = await consulta
            .Include(f => f.Cargo).Include(f => f.Departamento).Include(f => f.Gestor)
            .OrderBy(f => f.Id)
            .Skip(filtro.Paginacao.Pular)
            .Take(filtro.Paginacao.Tamanho)
            .ToListAsync(cancelamento);

        return new ResultadoPaginado<Funcionario>(itens, total);
    }

    public Task<Funcionario?> ObterAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        ComVinculos().AsNoTracking().FirstOrDefaultAsync(f => f.Id == idFuncionario, cancelamento);

    public Task<Funcionario?> ObterParaEdicaoAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        ComVinculos().FirstOrDefaultAsync(f => f.Id == idFuncionario, cancelamento);

    public Task<FuncionarioDetalhes?> ObterDetalhesParaEdicaoAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        _db.FuncionariosDetalhes.FirstOrDefaultAsync(d => d.IdFuncionario == idFuncionario, cancelamento);

    public Task<bool> CpfJaCadastradoAsync(string cpfSomenteDigitos, int? ignorarIdFuncionario, CancellationToken cancelamento = default) =>
        _db.FuncionariosDetalhes.AnyAsync(
            d => d.Cpf == cpfSomenteDigitos && (ignorarIdFuncionario == null || d.IdFuncionario != ignorarIdFuncionario),
            cancelamento);

    public Task<bool> EmailJaCadastradoAsync(string emailNormalizado, int? ignorarIdFuncionario, CancellationToken cancelamento = default) =>
        _db.Funcionarios.AnyAsync(
            f => f.Email.ToLower() == emailNormalizado && (ignorarIdFuncionario == null || f.Id != ignorarIdFuncionario),
            cancelamento);

    public async Task<IReadOnlyList<int>> ObterCadeiaDeGestoresAsync(int idFuncionario, CancellationToken cancelamento = default)
    {
        var cadeia = new List<int>();
        int? atual = idFuncionario;
        while (atual is { } id && cadeia.Count < ProfundidadeMaximaHierarquia && !cadeia.Contains(id))
        {
            cadeia.Add(id);
            atual = await _db.Funcionarios.AsNoTracking()
                .Where(f => f.Id == id)
                .Select(f => f.IdGestor)
                .FirstOrDefaultAsync(cancelamento);
        }

        return cadeia;
    }

    public Task<int> ContarSubordinadosAtivosAsync(int idGestor, CancellationToken cancelamento = default) =>
        _db.Funcionarios.CountAsync(f => f.IdGestor == idGestor && f.Ativo, cancelamento);

    public Task<bool> EhSubordinadoDiretoAsync(int idGestor, int idFuncionario, CancellationToken cancelamento = default) =>
        _db.Funcionarios.AnyAsync(f => f.Id == idFuncionario && f.IdGestor == idGestor, cancelamento);

    public async Task<IReadOnlyDictionary<int, string>> ObterNomesAsync(IReadOnlyCollection<int> ids, CancellationToken cancelamento = default) =>
        await _db.Funcionarios.AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Nome, cancelamento);

    public void Adicionar(Funcionario funcionario) => _db.Funcionarios.Add(funcionario);

    public void AdicionarDetalhes(FuncionarioDetalhes detalhes) => _db.FuncionariosDetalhes.Add(detalhes);
}

public sealed class UsuarioGestaoRepositorio : IUsuarioGestaoRepositorio
{
    private readonly HrAutoDbContext _db;

    public UsuarioGestaoRepositorio(HrAutoDbContext db) => _db = db;

    public Task<Usuario?> ObterPorFuncionarioAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        _db.Usuarios.Include(u => u.Funcionario).FirstOrDefaultAsync(u => u.IdFuncionario == idFuncionario, cancelamento);

    public Task<bool> EmailEmUsoAsync(string emailNormalizado, int? ignorarIdUsuario, CancellationToken cancelamento = default) =>
        // A coluna usa collation NOCASE: a comparação ignora maiúsculas.
        _db.Usuarios.AnyAsync(u => u.Email == emailNormalizado && (ignorarIdUsuario == null || u.Id != ignorarIdUsuario), cancelamento);

    public async Task<IReadOnlyList<int>> ListarIdsFuncionariosRhAtivosAsync(CancellationToken cancelamento = default) =>
        await _db.Usuarios.AsNoTracking()
            .Where(u => u.IdPerfil == (int)PerfilAcesso.RH && u.Ativo && u.Funcionario!.Ativo)
            .Select(u => u.IdFuncionario)
            .ToListAsync(cancelamento);

    public Task<int> ContarAdminsAtivosAsync(CancellationToken cancelamento = default) =>
        _db.Usuarios.CountAsync(u => u.IdPerfil == (int)PerfilAcesso.Admin && u.Ativo, cancelamento);

    public void Adicionar(Usuario usuario) => _db.Usuarios.Add(usuario);
}

public sealed class CargoRepositorio : ICargoRepositorio
{
    private readonly HrAutoDbContext _db;

    public CargoRepositorio(HrAutoDbContext db) => _db = db;

    public async Task<IReadOnlyList<Cargo>> ListarAsync(CancellationToken cancelamento = default) =>
        await _db.Cargos.AsNoTracking().OrderBy(c => c.NomeCargo).ToListAsync(cancelamento);

    public Task<Cargo?> ObterAsync(int idCargo, CancellationToken cancelamento = default) =>
        _db.Cargos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == idCargo, cancelamento);

    public Task<Cargo?> ObterParaEdicaoAsync(int idCargo, CancellationToken cancelamento = default) =>
        _db.Cargos.FirstOrDefaultAsync(c => c.Id == idCargo, cancelamento);

    public Task<bool> NomeJaExisteAsync(string nome, int? ignorarId, CancellationToken cancelamento = default)
    {
        var minusculo = nome.ToLower();
        return _db.Cargos.AnyAsync(c => c.NomeCargo.ToLower() == minusculo && (ignorarId == null || c.Id != ignorarId), cancelamento);
    }

    public Task<int> ContarFuncionariosAsync(int idCargo, CancellationToken cancelamento = default) =>
        _db.Funcionarios.CountAsync(f => f.IdCargo == idCargo, cancelamento);

    public void Adicionar(Cargo cargo) => _db.Cargos.Add(cargo);

    public void Remover(Cargo cargo) => _db.Cargos.Remove(cargo);
}

public sealed class DepartamentoRepositorio : IDepartamentoRepositorio
{
    private readonly HrAutoDbContext _db;

    public DepartamentoRepositorio(HrAutoDbContext db) => _db = db;

    public async Task<IReadOnlyList<Departamento>> ListarAsync(CancellationToken cancelamento = default) =>
        await _db.Departamentos.AsNoTracking().OrderBy(d => d.Nome).ToListAsync(cancelamento);

    public Task<Departamento?> ObterAsync(int idDepartamento, CancellationToken cancelamento = default) =>
        _db.Departamentos.AsNoTracking().FirstOrDefaultAsync(d => d.Id == idDepartamento, cancelamento);

    public Task<Departamento?> ObterParaEdicaoAsync(int idDepartamento, CancellationToken cancelamento = default) =>
        _db.Departamentos.FirstOrDefaultAsync(d => d.Id == idDepartamento, cancelamento);

    public Task<bool> NomeJaExisteAsync(string nome, int? ignorarId, CancellationToken cancelamento = default)
    {
        var minusculo = nome.ToLower();
        return _db.Departamentos.AnyAsync(d => d.Nome.ToLower() == minusculo && (ignorarId == null || d.Id != ignorarId), cancelamento);
    }

    public Task<int> ContarFuncionariosAsync(int idDepartamento, CancellationToken cancelamento = default) =>
        _db.Funcionarios.CountAsync(f => f.IdDepartamento == idDepartamento, cancelamento);

    public void Adicionar(Departamento departamento) => _db.Departamentos.Add(departamento);

    public void Remover(Departamento departamento) => _db.Departamentos.Remove(departamento);
}

public sealed class AdvertenciaRepositorio : IAdvertenciaRepositorio
{
    private readonly HrAutoDbContext _db;

    public AdvertenciaRepositorio(HrAutoDbContext db) => _db = db;

    public Task<Advertencia?> ObterParaEdicaoAsync(int idAdvertencia, CancellationToken cancelamento = default) =>
        _db.Advertencias.FirstOrDefaultAsync(a => a.Id == idAdvertencia, cancelamento);

    public async Task<IReadOnlyList<Advertencia>> ListarDoFuncionarioAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        await _db.Advertencias.AsNoTracking()
            .Where(a => a.IdFuncionario == idFuncionario)
            .OrderByDescending(a => a.Data).ThenByDescending(a => a.Id)
            .ToListAsync(cancelamento);

    public async Task<ResultadoPaginado<Advertencia>> ListarAsync(FiltroAdvertencias filtro, CancellationToken cancelamento = default)
    {
        var consulta = _db.Advertencias.AsNoTracking().AsQueryable();
        if (filtro.IdFuncionario is { } id) consulta = consulta.Where(a => a.IdFuncionario == id);
        if (filtro.Status is { } status) consulta = consulta.Where(a => a.Status == status);

        var total = await consulta.CountAsync(cancelamento);
        var itens = await consulta
            .OrderByDescending(a => a.Data).ThenByDescending(a => a.Id)
            .Skip(filtro.Paginacao.Pular)
            .Take(filtro.Paginacao.Tamanho)
            .ToListAsync(cancelamento);

        return new ResultadoPaginado<Advertencia>(itens, total);
    }

    public void Adicionar(Advertencia advertencia) => _db.Advertencias.Add(advertencia);
}
