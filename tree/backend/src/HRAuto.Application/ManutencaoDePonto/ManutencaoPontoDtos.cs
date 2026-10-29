using HRAuto.Application.Abstractions;
using HRAuto.Application.RegistroPonto;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.ManutencaoDePonto;

// ---------- Entrada ----------

/// <summary>RF22: dia que precisa de ajuste/abono e a justificativa (obrigatória). Descreva ali o que aconteceu e os horários certos.</summary>
public sealed record SolicitarManutencaoPontoDto(DateOnly? Data, string? Justificativa);

/// <summary>Horários corrigidos pelo gestor ao aprovar. Campo vazio = não mexer naquela batida.</summary>
public sealed record BatidasCorrigidasDto(TimeOnly? Entrada, TimeOnly? SaidaIntervalo, TimeOnly? RetornoIntervalo, TimeOnly? Saida);

/// <summary>
/// RF23: Aprovar = true aprova (e pode corrigir as batidas); false rejeita e então a observação com o motivo é obrigatória.
/// Batidas só são aceitas junto com a aprovação.
/// </summary>
public sealed record DecidirManutencaoPontoDto(bool? Aprovar, string? Observacao, BatidasCorrigidasDto? Batidas);

/// <summary>
/// Escopo da lista: "minhas" (padrão), "equipe" (subordinados diretos; só quem é gestor) ou "todas" (RH/Admin).
/// Status: Pendente, Aprovada ou Rejeitada; vazio = todos. Código só filtra dentro do escopo.
/// </summary>
public sealed record FiltroListaManutencoesDto(string? Escopo, string? Codigo, string? Status, int? Pagina, int? Tamanho);

// ---------- Saída ----------

public sealed record ManutencaoPontoDto(
    int Id,
    string CodigoFuncionario,
    string NomeFuncionario,
    DateOnly DataPonto,
    IReadOnlyList<BatidaDto> Batidas,
    string Justificativa,
    DateTime DataSolicitacao,
    string Status,
    DateTime? DataAnalise,
    string? CodigoResponsavel,
    string? Observacao);

public sealed record AlteracaoBatidaDto(TipoBatida Tipo, string Nome, TimeOnly? Antes, TimeOnly Depois);

/// <summary>
/// Resposta da decisão. "FechamentoDoDia" só vem preenchido quando as batidas foram corrigidas e o dia ficou com as quatro
/// batidas (o banco de horas foi reajustado).
/// </summary>
public sealed record ManutencaoDecididaDto(
    ManutencaoPontoDto Manutencao,
    bool BatidasAlteradas,
    IReadOnlyList<AlteracaoBatidaDto> Alteracoes,
    ResumoDiaDto? FechamentoDoDia);

// ---------- Conversões ----------

internal static class MapeadorManutencao
{
    public static ManutencaoPontoDto Pedido(ManutencaoPonto m, Ponto ponto, IReadOnlyDictionary<int, string> nomes) =>
        new(m.Id,
            CodigoFuncionario.Formatar(m.IdFuncionario),
            nomes.TryGetValue(m.IdFuncionario, out var nome) ? nome : string.Empty,
            ponto.Data,
            PontoMapeador.Batidas(ponto),
            m.Justificativa,
            m.DataSolicitacao,
            m.Status,
            m.DataAnalise,
            m.IdResponsavel is { } r ? CodigoFuncionario.Formatar(r) : null,
            m.Observacao);

    public static AlteracaoBatidaDto Alteracao(AlteracaoBatida a) => new(a.Tipo, Ponto.Nome(a.Tipo), a.Antes, a.Depois);
}
