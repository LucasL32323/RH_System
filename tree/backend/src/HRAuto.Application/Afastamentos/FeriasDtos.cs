using HRAuto.Domain.Entities;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Afastamentos;

// ---------- Entrada ----------

/// <summary>Dias é opcional: sem ele, é calculado pelo período. Se vier, precisa bater com o período (RF26).</summary>
public sealed record SolicitarFeriasDto(DateOnly? Inicio, DateOnly? Fim, int? Dias, string? Observacao);

/// <summary>Aprovar = true aprova; false rejeita (e então a observação com o motivo é obrigatória).</summary>
public sealed record DecidirFeriasDto(bool? Aprovar, string? Observacao);

/// <summary>
/// Escopo da lista: "minhas" (padrão), "equipe" (subordinados diretos; só quem é gestor) ou "todas" (RH/Admin).
/// Status: Pendente, Aprovada ou Rejeitada; vazio = todos. Código só filtra dentro do escopo.
/// </summary>
public sealed record FiltroListaFeriasDto(string? Escopo, string? Codigo, string? Status, int? Pagina, int? Tamanho);

// ---------- Saída ----------

public sealed record FeriasDto(
    int Id, string CodigoFuncionario, string NomeFuncionario, DateOnly DataInicio, DateOnly DataFim, int Dias,
    string Status, DateOnly DataSolicitacao, string? Observacao);

// ---------- Conversões ----------

internal static class MapeadorFerias
{
    public static FeriasDto Ferias(Ferias f, IReadOnlyDictionary<int, string> nomes) =>
        new(f.Id, CodigoFuncionario.Formatar(f.IdFuncionario), nomes.TryGetValue(f.IdFuncionario, out var nome) ? nome : string.Empty,
            f.DataInicio, f.DataFim, f.Dias, f.Status, f.DataSolicitacao, f.Observacao);
}
