using HRAuto.Application.Common;
using HRAuto.Domain.Entities;

namespace HRAuto.Application.Abstractions;

// Contratos do cadastro (RF09 a RF16) e da disciplina (RF33, RF34, RF47).
// Consultas "ObterParaEdicao" devolvem entidades rastreadas; as demais são somente leitura.

/// <summary>Resultado de uma consulta paginada: os itens da página e o total geral.</summary>
public sealed record ResultadoPaginado<T>(IReadOnlyList<T> Itens, int Total);

/// <summary>Filtros da lista de funcionários. A busca é pelo código (FUNC-0001), nunca pelo nome.</summary>
public sealed record FiltroFuncionarios(
    int? IdFuncionario,
    int? IdCargo,
    int? IdDepartamento,
    bool? Ativo,
    int? IdGestor,
    Paginacao Paginacao);

public interface IFuncionarioCadastroRepositorio
{
    /// <summary>Lista com cargo, departamento e gestor, ordenada por id (somente leitura).</summary>
    Task<ResultadoPaginado<Funcionario>> ListarAsync(FiltroFuncionarios filtro, CancellationToken cancelamento = default);

    /// <summary>Funcionário com cargo, departamento e gestor (somente leitura).</summary>
    Task<Funcionario?> ObterAsync(int idFuncionario, CancellationToken cancelamento = default);

    /// <summary>Mesmo que <see cref="ObterAsync"/>, mas rastreado para ser alterado.</summary>
    Task<Funcionario?> ObterParaEdicaoAsync(int idFuncionario, CancellationToken cancelamento = default);

    Task<FuncionarioDetalhes?> ObterDetalhesParaEdicaoAsync(int idFuncionario, CancellationToken cancelamento = default);

    Task<bool> CpfJaCadastradoAsync(string cpfSomenteDigitos, int? ignorarIdFuncionario, CancellationToken cancelamento = default);

    /// <summary>Compara com FUNCIONARIO.email, sem diferenciar maiúsculas.</summary>
    Task<bool> EmailJaCadastradoAsync(string emailNormalizado, int? ignorarIdFuncionario, CancellationToken cancelamento = default);

    /// <summary>Ids de todos os gestores acima do funcionário informado (gestor, gestor do gestor...). Para detectar ciclos.</summary>
    Task<IReadOnlyList<int>> ObterCadeiaDeGestoresAsync(int idFuncionario, CancellationToken cancelamento = default);

    Task<int> ContarSubordinadosAtivosAsync(int idGestor, CancellationToken cancelamento = default);

    /// <summary>Verdadeiro se o funcionário é subordinado direto (id_gestor) do gestor.</summary>
    Task<bool> EhSubordinadoDiretoAsync(int idGestor, int idFuncionario, CancellationToken cancelamento = default);

    /// <summary>Nomes por id (para mostrar o responsável de uma advertência sem uma consulta por linha).</summary>
    Task<IReadOnlyDictionary<int, string>> ObterNomesAsync(IReadOnlyCollection<int> ids, CancellationToken cancelamento = default);

    void Adicionar(Funcionario funcionario);

    void AdicionarDetalhes(FuncionarioDetalhes detalhes);
}

public interface IUsuarioGestaoRepositorio
{
    /// <summary>Conta de acesso do funcionário, rastreada, com o funcionário (ou nulo se ainda não tem acesso).</summary>
    Task<Usuario?> ObterPorFuncionarioAsync(int idFuncionario, CancellationToken cancelamento = default);

    Task<bool> EmailEmUsoAsync(string emailNormalizado, int? ignorarIdUsuario, CancellationToken cancelamento = default);

    /// <summary>Ids de funcionário dos usuários RH ativos (destino das sugestões da régua).</summary>
    Task<IReadOnlyList<int>> ListarIdsFuncionariosRhAtivosAsync(CancellationToken cancelamento = default);

    /// <summary>Quantos usuários Admin ativos existem (o último não pode ser desativado nem rebaixado).</summary>
    Task<int> ContarAdminsAtivosAsync(CancellationToken cancelamento = default);

    void Adicionar(Usuario usuario);
}

public interface ICargoRepositorio
{
    Task<IReadOnlyList<Cargo>> ListarAsync(CancellationToken cancelamento = default);

    /// <summary>Somente leitura.</summary>
    Task<Cargo?> ObterAsync(int idCargo, CancellationToken cancelamento = default);

    /// <summary>Rastreado para ser alterado.</summary>
    Task<Cargo?> ObterParaEdicaoAsync(int idCargo, CancellationToken cancelamento = default);

    Task<bool> NomeJaExisteAsync(string nome, int? ignorarId, CancellationToken cancelamento = default);

    Task<int> ContarFuncionariosAsync(int idCargo, CancellationToken cancelamento = default);

    void Adicionar(Cargo cargo);

    void Remover(Cargo cargo);
}

public interface IDepartamentoRepositorio
{
    Task<IReadOnlyList<Departamento>> ListarAsync(CancellationToken cancelamento = default);

    Task<Departamento?> ObterAsync(int idDepartamento, CancellationToken cancelamento = default);

    Task<Departamento?> ObterParaEdicaoAsync(int idDepartamento, CancellationToken cancelamento = default);

    Task<bool> NomeJaExisteAsync(string nome, int? ignorarId, CancellationToken cancelamento = default);

    Task<int> ContarFuncionariosAsync(int idDepartamento, CancellationToken cancelamento = default);

    void Adicionar(Departamento departamento);

    void Remover(Departamento departamento);
}

/// <summary>Filtros da lista de advertências (todas as pessoas). Status: "Ativa" ou "Cancelada"; nulo = todas.</summary>
public sealed record FiltroAdvertencias(int? IdFuncionario, string? Status, Paginacao Paginacao);

public interface IAdvertenciaRepositorio
{
    /// <summary>Rastreada para ser cancelada.</summary>
    Task<Advertencia?> ObterParaEdicaoAsync(int idAdvertencia, CancellationToken cancelamento = default);

    /// <summary>Todas as advertências do funcionário (ativas e canceladas), da mais recente para a mais antiga (somente leitura).</summary>
    Task<IReadOnlyList<Advertencia>> ListarDoFuncionarioAsync(int idFuncionario, CancellationToken cancelamento = default);

    Task<ResultadoPaginado<Advertencia>> ListarAsync(FiltroAdvertencias filtro, CancellationToken cancelamento = default);

    void Adicionar(Advertencia advertencia);
}
