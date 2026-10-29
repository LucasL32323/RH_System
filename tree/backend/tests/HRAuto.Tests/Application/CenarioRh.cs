using HRAuto.Application.Abstractions;
using HRAuto.Application.Cadastro;
using HRAuto.Application.Autenticacao;
using HRAuto.Application.Common;
using HRAuto.Application.Disciplina;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

// Dublês e atalhos da Fase 4 (parte 2): cadastro de funcionários, acessos, cargos, departamentos e advertências.

internal sealed class FuncionarioCadastroRepositorioFake : IFuncionarioCadastroRepositorio
{
    private readonly BancoFake _banco;

    public FuncionarioCadastroRepositorioFake(BancoFake banco) => _banco = banco;

    // Imita o Include do EF: cargo, departamento e gestor vêm preenchidos a partir das "tabelas".
    private Funcionario ComVinculos(Funcionario f)
    {
        f.Cargo = _banco.Cargos.FirstOrDefault(c => c.Id == f.IdCargo) ?? f.Cargo;
        f.Departamento = _banco.Departamentos.FirstOrDefault(d => d.Id == f.IdDepartamento) ?? f.Departamento;
        f.Gestor = f.IdGestor is { } g ? _banco.Funcionarios.FirstOrDefault(x => x.Id == g) : null;
        return f;
    }

    public Task<ResultadoPaginado<Funcionario>> ListarAsync(FiltroFuncionarios filtro, CancellationToken cancelamento = default)
    {
        var consulta = _banco.Funcionarios.AsEnumerable();
        if (filtro.IdFuncionario is { } id) consulta = consulta.Where(f => f.Id == id);
        if (filtro.IdCargo is { } cargo) consulta = consulta.Where(f => f.IdCargo == cargo);
        if (filtro.IdDepartamento is { } dep) consulta = consulta.Where(f => f.IdDepartamento == dep);
        if (filtro.Ativo is { } ativo) consulta = consulta.Where(f => f.Ativo == ativo);
        if (filtro.IdGestor is { } gestor) consulta = consulta.Where(f => f.IdGestor == gestor);

        var todos = consulta.OrderBy(f => f.Id).ToList();
        var itens = todos.Skip(filtro.Paginacao.Pular).Take(filtro.Paginacao.Tamanho).Select(ComVinculos).ToList();
        return Task.FromResult(new ResultadoPaginado<Funcionario>(itens, todos.Count));
    }

    public Task<Funcionario?> ObterAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Funcionarios.FirstOrDefault(f => f.Id == idFuncionario) is { } f ? ComVinculos(f) : null);

    public Task<Funcionario?> ObterParaEdicaoAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        ObterAsync(idFuncionario, cancelamento);

    public Task<FuncionarioDetalhes?> ObterDetalhesParaEdicaoAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Detalhes.FirstOrDefault(d => d.IdFuncionario == idFuncionario));

    public Task<bool> CpfJaCadastradoAsync(string cpfSomenteDigitos, int? ignorarIdFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Detalhes.Any(d => d.Cpf == cpfSomenteDigitos && d.IdFuncionario != ignorarIdFuncionario));

    public Task<bool> EmailJaCadastradoAsync(string emailNormalizado, int? ignorarIdFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Funcionarios.Any(f =>
            string.Equals(f.Email, emailNormalizado, StringComparison.OrdinalIgnoreCase) && f.Id != ignorarIdFuncionario));

    public Task<IReadOnlyList<int>> ObterCadeiaDeGestoresAsync(int idFuncionario, CancellationToken cancelamento = default)
    {
        var cadeia = new List<int>();
        int? atual = idFuncionario;
        while (atual is { } id && cadeia.Count < 50 && !cadeia.Contains(id))
        {
            cadeia.Add(id);
            atual = _banco.Funcionarios.FirstOrDefault(f => f.Id == id)?.IdGestor;
        }

        return Task.FromResult<IReadOnlyList<int>>(cadeia);
    }

    public Task<int> ContarSubordinadosAtivosAsync(int idGestor, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Funcionarios.Count(f => f.Ativo && f.IdGestor == idGestor));

    public Task<bool> EhSubordinadoDiretoAsync(int idGestor, int idFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Funcionarios.Any(f => f.Id == idFuncionario && f.IdGestor == idGestor));

    public Task<IReadOnlyDictionary<int, string>> ObterNomesAsync(IReadOnlyCollection<int> ids, CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyDictionary<int, string>>(
            _banco.Funcionarios.Where(f => ids.Contains(f.Id)).ToDictionary(f => f.Id, f => f.Nome));

    public void Adicionar(Funcionario funcionario) => _banco.Funcionarios.Add(funcionario);

    public void AdicionarDetalhes(FuncionarioDetalhes detalhes) => _banco.Detalhes.Add(detalhes);
}

internal sealed class UsuarioGestaoRepositorioFake : IUsuarioGestaoRepositorio
{
    private readonly BancoFake _banco;

    public UsuarioGestaoRepositorioFake(BancoFake banco) => _banco = banco;

    public Task<Usuario?> ObterPorFuncionarioAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Usuarios.FirstOrDefault(u => u.IdFuncionario == idFuncionario));

    public Task<bool> EmailEmUsoAsync(string emailNormalizado, int? ignorarIdUsuario, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Usuarios.Any(u =>
            string.Equals(u.Email, emailNormalizado, StringComparison.OrdinalIgnoreCase) && u.Id != ignorarIdUsuario));

    public Task<IReadOnlyList<int>> ListarIdsFuncionariosRhAtivosAsync(CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<int>>(_banco.Usuarios
            .Where(u => u.IdPerfil == (int)PerfilAcesso.RH && u.Ativo && (u.Funcionario?.Ativo ?? true))
            .Select(u => u.IdFuncionario).ToList());

    public Task<int> ContarAdminsAtivosAsync(CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Usuarios.Count(u => u.IdPerfil == (int)PerfilAcesso.Admin && u.Ativo));

    public void Adicionar(Usuario usuario) => _banco.Usuarios.Add(usuario);
}

internal sealed class CargoRepositorioFake : ICargoRepositorio
{
    private readonly BancoFake _banco;

    public CargoRepositorioFake(BancoFake banco) => _banco = banco;

    public Task<IReadOnlyList<Cargo>> ListarAsync(CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<Cargo>>(_banco.Cargos.OrderBy(c => c.NomeCargo).ToList());

    public Task<Cargo?> ObterAsync(int idCargo, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Cargos.FirstOrDefault(c => c.Id == idCargo));

    public Task<Cargo?> ObterParaEdicaoAsync(int idCargo, CancellationToken cancelamento = default) => ObterAsync(idCargo, cancelamento);

    public Task<bool> NomeJaExisteAsync(string nome, int? ignorarId, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Cargos.Any(c => string.Equals(c.NomeCargo, nome, StringComparison.OrdinalIgnoreCase) && c.Id != ignorarId));

    public Task<int> ContarFuncionariosAsync(int idCargo, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Funcionarios.Count(f => f.IdCargo == idCargo));

    public void Adicionar(Cargo cargo) => _banco.Cargos.Add(cargo);

    public void Remover(Cargo cargo) => _banco.Cargos.Remove(cargo);
}

internal sealed class DepartamentoRepositorioFake : IDepartamentoRepositorio
{
    private readonly BancoFake _banco;

    public DepartamentoRepositorioFake(BancoFake banco) => _banco = banco;

    public Task<IReadOnlyList<Departamento>> ListarAsync(CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<Departamento>>(_banco.Departamentos.OrderBy(d => d.Nome).ToList());

    public Task<Departamento?> ObterAsync(int idDepartamento, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Departamentos.FirstOrDefault(d => d.Id == idDepartamento));

    public Task<Departamento?> ObterParaEdicaoAsync(int idDepartamento, CancellationToken cancelamento = default) =>
        ObterAsync(idDepartamento, cancelamento);

    public Task<bool> NomeJaExisteAsync(string nome, int? ignorarId, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Departamentos.Any(d => string.Equals(d.Nome, nome, StringComparison.OrdinalIgnoreCase) && d.Id != ignorarId));

    public Task<int> ContarFuncionariosAsync(int idDepartamento, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Funcionarios.Count(f => f.IdDepartamento == idDepartamento));

    public void Adicionar(Departamento departamento) => _banco.Departamentos.Add(departamento);

    public void Remover(Departamento departamento) => _banco.Departamentos.Remove(departamento);
}

internal sealed class AdvertenciaRepositorioFake : IAdvertenciaRepositorio
{
    private readonly BancoFake _banco;

    public AdvertenciaRepositorioFake(BancoFake banco) => _banco = banco;

    public Task<Advertencia?> ObterParaEdicaoAsync(int idAdvertencia, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Advertencias.FirstOrDefault(a => a.Id == idAdvertencia));

    public Task<IReadOnlyList<Advertencia>> ListarDoFuncionarioAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<Advertencia>>(_banco.Advertencias
            .Where(a => a.IdFuncionario == idFuncionario)
            .OrderByDescending(a => a.Data).ThenByDescending(a => a.Id).ToList());

    public Task<ResultadoPaginado<Advertencia>> ListarAsync(FiltroAdvertencias filtro, CancellationToken cancelamento = default)
    {
        var consulta = _banco.Advertencias.AsEnumerable();
        if (filtro.IdFuncionario is { } id) consulta = consulta.Where(a => a.IdFuncionario == id);
        if (filtro.Status is { } status) consulta = consulta.Where(a => a.Status == status);
        var todas = consulta.OrderByDescending(a => a.Data).ThenByDescending(a => a.Id).ToList();
        return Task.FromResult(new ResultadoPaginado<Advertencia>(
            todas.Skip(filtro.Paginacao.Pular).Take(filtro.Paginacao.Tamanho).ToList(), todas.Count));
    }

    public void Adicionar(Advertencia advertencia) => _banco.Advertencias.Add(advertencia);
}

/// <summary>Pessoas de uma empresa de teste: admin, RH, gestor, dois colaboradores da equipe e um de fora.</summary>
internal sealed record Empresa(
    Funcionario Admin, Usuario UsuarioAdmin,
    Funcionario Rh, Usuario UsuarioRh,
    Funcionario Gestor, Usuario UsuarioGestor,
    Funcionario Ana, Usuario UsuarioAna,
    Funcionario Bruno, Usuario UsuarioBruno,
    Funcionario Carla, Usuario UsuarioCarla,
    Cargo Cargo, Departamento Departamento);

internal sealed partial class Cenario
{
    private FuncionarioCadastroRepositorioFake FuncionariosCadastro => new(Banco);
    private UsuarioGestaoRepositorioFake UsuariosGestao => new(Banco);
    private CargoRepositorioFake Cargos => new(Banco);
    private DepartamentoRepositorioFake Departamentos => new(Banco);
    private AdvertenciaRepositorioFake Advertencias => new(Banco);

    // ---- Dados ----

    public Cargo NovoCargo(int id, string nome)
    {
        var cargo = new Cargo { NomeCargo = nome }.DefinirId(id);
        Banco.Cargos.Add(cargo);
        return cargo;
    }

    public Departamento NovoDepartamento(int id, string nome)
    {
        var departamento = new Departamento { Nome = nome }.DefinirId(id);
        Banco.Departamentos.Add(departamento);
        return departamento;
    }

    public FuncionarioDetalhes NovosDetalhes(Funcionario funcionario, string cpf)
    {
        var detalhes = new FuncionarioDetalhes
        {
            IdFuncionario = funcionario.Id, Cpf = cpf, Rg = "123456789", Endereco = "Rua A", Numero = "10",
            Bairro = "Centro", Cidade = "Santos", Estado = "SP", Cep = "11010000"
        };
        Banco.Detalhes.Add(detalhes);
        return detalhes;
    }

    public Advertencia NovaAdvertencia(Funcionario funcionario, Funcionario responsavel, TipoAdvertencia tipo, string data, bool ativa = true)
    {
        var advertencia = new Advertencia
        {
            IdFuncionario = funcionario.Id, IdResponsavel = responsavel.Id, Data = ApoioApp.Data(data), Tipo = tipo.ToString(),
            Motivo = "Atraso reiterado", Status = ativa ? StatusAdvertencia.Ativa : StatusAdvertencia.Cancelada
        }.DefinirId(Banco.Advertencias.Count + 1);
        Banco.Advertencias.Add(advertencia);
        return advertencia;
    }

    /// <summary>Admin (1), RH (2), gestor (3) com Ana (4) e Bruno (5) na equipe, e Carla (6) fora dela. Todos com acesso e CPF.</summary>
    public Empresa NovaEmpresa()
    {
        var cargo = NovoCargo(1, "Analista");
        var departamento = NovoDepartamento(1, "Tecnologia");

        Funcionario F(int id, string nome, int? gestor = null)
        {
            var f = NovoFuncionario(id, nome, gestor);
            f.IdCargo = cargo.Id;
            f.IdDepartamento = departamento.Id;
            NovosDetalhes(f, $"{id:D3}00000000"[..11]);
            return f;
        }

        var admin = F(1, "Alice Admin");
        var rh = F(2, "Rita RH");
        var gestor = F(3, "Gil Gestor");
        var ana = F(4, "Ana Souza", gestor.Id);
        var bruno = F(5, "Bruno Lima", gestor.Id);
        var carla = F(6, "Carla Dias");

        return new Empresa(
            admin, NovoUsuario(admin, "admin@hrauto.local", perfil: PerfilAcesso.Admin),
            rh, NovoUsuario(rh, "rh@hrauto.local", perfil: PerfilAcesso.RH),
            gestor, NovoUsuario(gestor, "gil@hrauto.local"),
            ana, NovoUsuario(ana, "ana@hrauto.local"),
            bruno, NovoUsuario(bruno, "bruno@hrauto.local"),
            carla, NovoUsuario(carla, "carla@hrauto.local"),
            cargo, departamento);
    }

    // ---- Casos de uso ----

    public EnviadorDeConvite Convite() =>
        new(Tokens, Email, new OpcoesRecuperacaoSenha("https://hrauto.local/redefinir-senha"));

    public CadastrarFuncionario Cadastrar() => new(
        FuncionariosCadastro, UsuariosGestao, Cargos, Departamentos, Hash, Convite(), _auditoria, _unidade, Relogio);

    public ListarFuncionarios ListarFuncionarios() => new(FuncionariosCadastro, new FuncionarioRepositorioFake(Banco));

    public ConsultarFuncionario ConsultarFuncionario() =>
        new(FuncionariosCadastro, new FuncionarioRepositorioFake(Banco), UsuariosGestao, _auditoria, _unidade);

    public AtualizarFuncionario AtualizarFuncionario() =>
        new(FuncionariosCadastro, UsuariosGestao, Cargos, Departamentos, _auditoria, _unidade, Relogio);

    public AlterarSituacaoFuncionario AlterarSituacao() =>
        new(FuncionariosCadastro, UsuariosGestao, _auditoria, _unidade, Relogio);

    public GerenciarAcessoUsuario Acesso() =>
        new(FuncionariosCadastro, UsuariosGestao, Hash, Convite(), _auditoria, _unidade);

    public GerenciarCargos GerenciarCargos() => new(Cargos, _auditoria, _unidade);

    public GerenciarDepartamentos GerenciarDepartamentos() => new(Departamentos, _auditoria, _unidade);

    public RegistrarAdvertencia RegistrarAdvertencia() => new(
        FuncionariosCadastro, UsuariosGestao, Advertencias, new NotificacaoRepositorioFake(Banco), _auditoria, _unidade, Relogio);

    public CancelarAdvertencia CancelarAdvertencia() => new(
        Advertencias, FuncionariosCadastro, UsuariosGestao, new NotificacaoRepositorioFake(Banco), _auditoria, _unidade, Relogio);

    public ConsultarAdvertencias ConsultarAdvertencias() => new(Advertencias, FuncionariosCadastro);

    public ConsultarSituacaoDisciplinar SituacaoDisciplinar() => new(Advertencias, FuncionariosCadastro);

    /// <summary>Corpo válido para cadastrar um funcionário; os testes trocam só o campo que querem exercitar.</summary>
    public CadastrarFuncionarioDto CadastroValido(Empresa e, string email = "novo@hrauto.local", string cpf = "529.982.247-25") => new(
        "Diego Alves", cpf, "123456789", null, null,
        new DateOnly(1999, 3, 4), new DateOnly(2026, 10, 19),
        email, "(13) 99999-0000", e.Cargo.Id, e.Departamento.Id, "FUNC-0003",
        new EnderecoDto("11010-000", "Rua das Flores", "100", null, "Centro", "Santos", "sp"),
        null, null);
}
