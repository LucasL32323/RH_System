using HRAuto.Domain.Enums;

namespace HRAuto.Domain.Jornada;

/// <summary>Resultado do cálculo de horas de um dia (RF20).</summary>
/// <param name="Data">Dia calculado.</param>
/// <param name="Contexto">Dia útil, descanso semanal, feriado ou férias.</param>
/// <param name="Situacao">Completo, em andamento, inconsistente, falta ou sem expediente.</param>
/// <param name="Trabalhado">Horas efetivamente trabalhadas (soma dos dois períodos, sem o intervalo).</param>
/// <param name="CargaEsperada">Horas devidas no dia (zero em feriado, férias e descanso).</param>
/// <param name="HorasExtras">Excedente da carga, fora da tolerância; em dia sem expediente é todo o trabalhado.</param>
/// <param name="HorasFaltantes">Carga não cumprida, fora da tolerância.</param>
/// <param name="Atraso">Atraso na entrada, fora da tolerância.</param>
public sealed record ResumoDia(
    DateOnly Data,
    ContextoDia Contexto,
    SituacaoDia Situacao,
    TimeSpan Trabalhado,
    TimeSpan CargaEsperada,
    TimeSpan HorasExtras,
    TimeSpan HorasFaltantes,
    TimeSpan Atraso)
{
    /// <summary>Saldo do dia para o banco de horas: extras menos faltantes.</summary>
    public TimeSpan Saldo => HorasExtras - HorasFaltantes;
}

/// <summary>Totais de um período (espelho de ponto, RF21, e indicadores, RF48/RF49).</summary>
public sealed record ResumoPeriodo(
    TimeSpan Trabalhado,
    TimeSpan HorasExtras,
    TimeSpan HorasFaltantes,
    TimeSpan Atrasos,
    int DiasTrabalhados,
    int Faltas,
    int DiasInconsistentes)
{
    /// <summary>Saldo do período: extras menos faltantes.</summary>
    public TimeSpan Saldo => HorasExtras - HorasFaltantes;
}
