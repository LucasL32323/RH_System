namespace HRAuto.Domain.Jornada;

/// <summary>
/// Jornada padrão usada no cálculo de horas (RF20). Valores iniciais: segunda a sexta, entrada às 08:00,
/// 8 h de trabalho efetivo por dia (mais 1 h de intervalo), tolerância de 10 min por dia (CLT, art. 58 §1º)
/// e alerta de hora extra acima de 2 h no dia (RF25).
/// A tabela de modelo físico não tem jornada por funcionário; por isso há uma única jornada para todos.
/// </summary>
public sealed record JornadaPadrao
{
    /// <summary>Horário previsto de entrada; atrasos são medidos a partir dele.</summary>
    public TimeOnly HoraEntrada { get; init; } = new(8, 0);

    /// <summary>Horas de trabalho efetivo esperadas por dia útil (sem o intervalo).</summary>
    public TimeSpan CargaDiaria { get; init; } = TimeSpan.FromHours(8);

    /// <summary>Variação diária que não conta como atraso, hora extra nem hora faltante.</summary>
    public TimeSpan ToleranciaDiaria { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Acima disto, em horas extras no dia, o gestor é notificado (RF25).</summary>
    public TimeSpan LimiteHoraExtraDiaria { get; init; } = TimeSpan.FromHours(2);

    /// <summary>Dias da semana com expediente.</summary>
    public IReadOnlyCollection<DayOfWeek> DiasUteis { get; init; } = new[]
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
    };

    /// <summary>Jornada padrão da empresa.</summary>
    public static JornadaPadrao Padrao => new();

    /// <summary>Verdadeiro se o dia da semana tem expediente (não olha feriados nem férias).</summary>
    public bool TemExpediente(DateOnly data) => DiasUteis.Contains(data.DayOfWeek);
}
