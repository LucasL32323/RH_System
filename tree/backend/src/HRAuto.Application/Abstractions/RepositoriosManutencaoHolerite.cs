using HRAuto.Application.Common;
using HRAuto.Domain.Entities;

namespace HRAuto.Application.Abstractions;

// Contratos da manutenção de ponto (RF22, RF23) e dos holerites (RF29, RF30, RNF17).
// Consultas "ParaEdicao" devolvem entidades rastreadas; as demais são somente leitura.

/// <summary>Pedido de manutenção junto com o ponto do dia a que ele se refere.</summary>
public sealed record ManutencaoComPonto(ManutencaoPonto Pedido, Ponto Ponto);

/// <summary>
/// Filtros da lista de pedidos. IdFuncionario = só os dele; IdGestor = só dos subordinados diretos desse gestor
/// (escopo "equipe"); os dois nulos = todos. Status: Pendente, Aprovada ou Rejeitada; nulo = todos.
/// </summary>
public sealed record FiltroManutencoes(int? IdFuncionario, int? IdGestor, string? Status, Paginacao Paginacao);

public interface IManutencaoPontoRepositorio
{
    /// <summary>Lista paginada, do pedido mais recente para o mais antigo (somente leitura).</summary>
    Task<ResultadoPaginado<ManutencaoComPonto>> ListarAsync(FiltroManutencoes filtro, CancellationToken cancelamento = default);

    /// <summary>Pedido rastreado, para ser analisado.</summary>
    Task<ManutencaoPonto?> ObterParaEdicaoAsync(int idManutencao, CancellationToken cancelamento = default);

    /// <summary>Todos os pedidos já feitos para o ponto (somente leitura), para impedir dois pendentes ao mesmo tempo.</summary>
    Task<IReadOnlyList<ManutencaoPonto>> ListarDoPontoAsync(int idPonto, CancellationToken cancelamento = default);

    /// <summary>Ponto rastreado pelo id, para as batidas serem corrigidas na aprovação.</summary>
    Task<Ponto?> ObterPontoParaEdicaoAsync(int idPonto, CancellationToken cancelamento = default);

    void Adicionar(ManutencaoPonto pedido);
}

public interface IHoleriteRepositorio
{
    /// <summary>Holerite pelo id (somente leitura).</summary>
    Task<Holerite?> ObterAsync(int idHolerite, CancellationToken cancelamento = default);

    /// <summary>Holerite da competência, rastreado para ter o arquivo substituído; nulo se ainda não foi publicado.</summary>
    Task<Holerite?> ObterDaCompetenciaParaEdicaoAsync(int idFuncionario, int ano, int mes, CancellationToken cancelamento = default);

    /// <summary>Holerites do funcionário (opcionalmente de um ano), do mais recente para o mais antigo (somente leitura).</summary>
    Task<IReadOnlyList<Holerite>> ListarAsync(int idFuncionario, int? ano, CancellationToken cancelamento = default);

    void Adicionar(Holerite holerite);
}

/// <summary>
/// Pasta de arquivos dos holerites no servidor (RNF17). Quem usa só conhece o caminho RELATIVO devolvido por
/// <see cref="SalvarAsync"/>; o nome do arquivo é gerado pelo sistema, nunca vem do usuário.
/// </summary>
public interface IArmazenamentoHolerites
{
    /// <summary>Grava o PDF numa pasta nova e devolve o caminho relativo (o que vai para HOLERITE.arquivo).</summary>
    Task<string> SalvarAsync(int idFuncionario, int ano, int mes, ReadOnlyMemory<byte> conteudo, CancellationToken cancelamento = default);

    /// <summary>Conteúdo do arquivo, ou nulo se o caminho for inválido ou o arquivo não existir mais.</summary>
    Task<byte[]?> LerAsync(string caminhoRelativo, CancellationToken cancelamento = default);

    /// <summary>Apaga o arquivo (usado só para desfazer uma publicação que falhou). Ignora caminho inválido ou inexistente.</summary>
    Task RemoverAsync(string caminhoRelativo, CancellationToken cancelamento = default);
}
