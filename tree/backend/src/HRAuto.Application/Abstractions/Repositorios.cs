using HRAuto.Application.Common;
using HRAuto.Domain.Entities;

namespace HRAuto.Application.Abstractions;

// Contratos de acesso a dados usados pelos casos de uso. A implementação (EF Core + SQLite) fica na Infrastructure.
// Métodos "Adicionar" só marcam a entidade; a gravação acontece em IUnidadeDeTrabalho.SalvarAsync.

public interface IUsuarioRepositorio
{
    /// <summary>Busca pelo e-mail já normalizado (minúsculas). Traz o funcionário junto. Entidade rastreada (pode ser alterada).</summary>
    Task<Usuario?> ObterPorEmailAsync(string emailNormalizado, CancellationToken cancelamento = default);

    /// <summary>Busca pelo id, com o funcionário. Entidade rastreada (pode ser alterada).</summary>
    Task<Usuario?> ObterPorIdAsync(int idUsuario, CancellationToken cancelamento = default);
}

public interface IFuncionarioRepositorio
{
    /// <summary>Funcionário com cargo e departamento (somente leitura).</summary>
    Task<Funcionario?> ObterPorIdAsync(int idFuncionario, CancellationToken cancelamento = default);

    /// <summary>Documentos e endereço (cidade/estado definem os feriados aplicáveis).</summary>
    Task<FuncionarioDetalhes?> ObterDetalhesAsync(int idFuncionario, CancellationToken cancelamento = default);

    /// <summary>Verdadeiro se algum funcionário ativo tem este funcionário como gestor (id_gestor).</summary>
    Task<bool> EhGestorAsync(int idFuncionario, CancellationToken cancelamento = default);
}

public interface IPontoRepositorio
{
    /// <summary>Registro do dia (rastreado, pode ser alterado), ou nulo se não houve nenhuma batida.</summary>
    Task<Ponto?> ObterDoDiaAsync(int idFuncionario, DateOnly data, CancellationToken cancelamento = default);

    /// <summary>Registros do período, inclusive nas pontas (somente leitura).</summary>
    Task<IReadOnlyList<Ponto>> ListarPeriodoAsync(int idFuncionario, DateOnly inicio, DateOnly fim, CancellationToken cancelamento = default);

    void Adicionar(Ponto ponto);
}

public interface IFeriadoRepositorio
{
    /// <summary>Feriados do período, inclusive nas pontas, ordenados por data (somente leitura).</summary>
    Task<IReadOnlyList<Feriado>> ListarPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken cancelamento = default);

    /// <summary>Feriado rastreado para ser removido.</summary>
    Task<Feriado?> ObterParaEdicaoAsync(int idFeriado, CancellationToken cancelamento = default);

    /// <summary>Já existe feriado igual (mesma data, tipo, cidade e estado)? Cidade e estado são comparados sem diferenciar maiúsculas.</summary>
    Task<bool> ExisteAsync(DateOnly data, string tipo, string? cidade, string? estado, CancellationToken cancelamento = default);

    void Adicionar(Feriado feriado);

    void Remover(Feriado feriado);
}

/// <summary>
/// Filtros da lista de férias. IdFuncionario = só as dele; IdGestor = só dos subordinados diretos desse gestor
/// (escopo "equipe"); os dois nulos = todas. Status: Pendente, Aprovada ou Rejeitada; nulo = todos.
/// </summary>
public sealed record FiltroFerias(int? IdFuncionario, int? IdGestor, string? Status, Paginacao Paginacao);

public interface IFeriasRepositorio
{
    /// <summary>Férias APROVADAS do funcionário que tocam o período (somente leitura).</summary>
    Task<IReadOnlyList<Ferias>> ListarAprovadasNoPeriodoAsync(int idFuncionario, DateOnly inicio, DateOnly fim, CancellationToken cancelamento = default);

    /// <summary>Férias Pendentes e Aprovadas do funcionário (as que consomem saldo e impedem sobreposição). Somente leitura.</summary>
    Task<IReadOnlyList<Ferias>> ListarAtivasDoFuncionarioAsync(int idFuncionario, CancellationToken cancelamento = default);

    /// <summary>Solicitação rastreada, para ser decidida ou cancelada.</summary>
    Task<Ferias?> ObterParaEdicaoAsync(int idFerias, CancellationToken cancelamento = default);

    /// <summary>Lista paginada, da solicitação mais recente para a mais antiga (somente leitura).</summary>
    Task<ResultadoPaginado<Ferias>> ListarAsync(FiltroFerias filtro, CancellationToken cancelamento = default);

    void Adicionar(Ferias ferias);

    void Remover(Ferias ferias);
}

public interface IBancoHorasRepositorio
{
    Task<IReadOnlyList<BancoHoras>> ListarDoDiaAsync(int idFuncionario, DateOnly data, CancellationToken cancelamento = default);

    /// <summary>Todos os lançamentos do funcionário (para o saldo).</summary>
    Task<IReadOnlyList<BancoHoras>> ListarTodosAsync(int idFuncionario, CancellationToken cancelamento = default);

    /// <summary>Lançamentos do período, inclusive nas pontas, do mais recente para o mais antigo (somente leitura).</summary>
    Task<IReadOnlyList<BancoHoras>> ListarPeriodoAsync(int idFuncionario, DateOnly inicio, DateOnly fim, CancellationToken cancelamento = default);

    void Adicionar(BancoHoras lancamento);
}

public interface INotificacaoRepositorio
{
    void Adicionar(Notificacao notificacao);

    /// <summary>Notificações do funcionário, da mais recente para a mais antiga (somente leitura).</summary>
    Task<ResultadoPaginado<Notificacao>> ListarAsync(int idFuncionario, bool somenteNaoLidas, Paginacao paginacao, CancellationToken cancelamento = default);

    Task<int> ContarNaoLidasAsync(int idFuncionario, CancellationToken cancelamento = default);

    /// <summary>Notificação rastreada, para ser marcada como lida.</summary>
    Task<Notificacao?> ObterParaEdicaoAsync(int idNotificacao, CancellationToken cancelamento = default);

    /// <summary>Notificações não lidas do funcionário, rastreadas, para serem marcadas como lidas.</summary>
    Task<IReadOnlyList<Notificacao>> ListarNaoLidasParaEdicaoAsync(int idFuncionario, CancellationToken cancelamento = default);
}

/// <summary>
/// Log de auditoria (RF08, RNF07). Só marca o registro; ele é gravado junto com o resto em
/// <see cref="IUnidadeDeTrabalho.SalvarAsync"/>. A data e hora vêm do relógio do servidor (Brasília).
/// </summary>
public interface IAuditoria
{
    void Registrar(int idUsuario, string acao, string? tabela, int? idRegistro, string? descricao);
}
