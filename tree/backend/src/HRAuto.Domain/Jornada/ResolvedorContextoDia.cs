using HRAuto.Domain.Afastamentos;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Domain.Jornada;

/// <summary>
/// Decide se um dia é útil, descanso semanal, feriado ou férias (RF20, RF28).
/// Prioridade quando mais de um se aplica: férias aprovadas, feriado, descanso semanal.
/// </summary>
public static class ResolvedorContextoDia
{
    /// <summary>
    /// Datas de feriado que valem para o funcionário: nacionais valem para todos; estaduais, só para quem mora no
    /// mesmo estado; municipais, só para quem mora na mesma cidade e estado. Cidade e estado vêm de
    /// FUNCIONARIO_DETALHES. Feriado com tipo desconhecido é ignorado.
    /// </summary>
    public static HashSet<DateOnly> DatasDeFeriadoAplicaveis(IEnumerable<Feriado> feriados, string? cidade, string? estado)
    {
        var datas = new HashSet<DateOnly>();
        foreach (var feriado in feriados)
        {
            var vale = feriado.Tipo switch
            {
                TiposFeriado.Nacional => true,
                TiposFeriado.Estadual => Igual(feriado.Estado, estado),
                TiposFeriado.Municipal => Igual(feriado.Estado, estado) && Igual(feriado.Cidade, cidade),
                _ => false
            };
            if (vale) datas.Add(feriado.Data);
        }
        return datas;
    }

    /// <param name="data">Dia a classificar.</param>
    /// <param name="jornada">Jornada que define os dias úteis da semana.</param>
    /// <param name="feriasDoFuncionario">Férias do funcionário (só as aprovadas contam).</param>
    /// <param name="datasDeFeriado">Resultado de <see cref="DatasDeFeriadoAplicaveis"/>.</param>
    public static ContextoDia Resolver(
        DateOnly data,
        JornadaPadrao jornada,
        IEnumerable<Ferias> feriasDoFuncionario,
        IReadOnlySet<DateOnly> datasDeFeriado)
    {
        if (feriasDoFuncionario.Any(f => RegrasFerias.CobreDia(f, data))) return ContextoDia.Ferias;
        if (datasDeFeriado.Contains(data)) return ContextoDia.Feriado;
        return jornada.TemExpediente(data) ? ContextoDia.DiaUtil : ContextoDia.DescansoSemanal;
    }

    private static bool Igual(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
