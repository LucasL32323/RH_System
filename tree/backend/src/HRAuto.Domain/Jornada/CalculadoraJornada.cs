using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Domain.Jornada;

/// <summary>
/// Calcula horas trabalhadas, extras e atrasos do dia pela jornada padrão (RF20).
/// Regras:
/// - Feriado, férias aprovadas e descanso semanal não têm carga: não geram atraso nem hora faltante;
///   se a pessoa trabalhar nesses dias, todo o tempo trabalhado vira hora extra.
/// - A tolerância diária (padrão 10 min) vale para hora extra, hora faltante e atraso. Passou dela, conta tudo.
/// - Só dia COMPLETO (4 batidas) gera horas extras/faltantes. Dia aberto fica "em andamento"; dia encerrado
///   com batida faltando fica "inconsistente" até haver manutenção de ponto (RF22).
/// - Falta (dia útil encerrado sem batida) é contada à parte e NÃO debita o banco de horas.
/// </summary>
public sealed class CalculadoraJornada
{
    private readonly JornadaPadrao _jornada;

    public CalculadoraJornada() : this(JornadaPadrao.Padrao) { }

    public CalculadoraJornada(JornadaPadrao jornada) => _jornada = jornada;

    /// <param name="ponto">Registro do dia (nulo se não há nenhuma batida).</param>
    /// <param name="data">Dia calculado.</param>
    /// <param name="contexto">Natureza do dia (ver <see cref="ResolvedorContextoDia"/>).</param>
    /// <param name="diaEncerrado">Verdadeiro se o dia já acabou (data anterior a hoje no horário de Brasília).</param>
    public ResumoDia Calcular(Ponto? ponto, DateOnly data, ContextoDia contexto, bool diaEncerrado)
    {
        var util = contexto == ContextoDia.DiaUtil;
        var carga = util ? _jornada.CargaDiaria : TimeSpan.Zero;
        var batidas = ponto?.TotalBatidas() ?? 0;
        var trabalhado = Trabalhado(ponto);
        var atraso = util ? Atraso(ponto) : TimeSpan.Zero;

        var situacao = batidas switch
        {
            0 when !util => SituacaoDia.SemExpediente,
            0 => diaEncerrado ? SituacaoDia.Falta : SituacaoDia.EmAndamento,
            4 => SituacaoDia.Completo,
            _ => diaEncerrado ? SituacaoDia.Inconsistente : SituacaoDia.EmAndamento
        };

        var extras = TimeSpan.Zero;
        var faltantes = TimeSpan.Zero;

        if (situacao == SituacaoDia.Completo)
        {
            if (!util)
            {
                extras = trabalhado; // trabalho em feriado, férias ou descanso: tudo é extra
            }
            else
            {
                var diferenca = trabalhado - carga;
                if (diferenca.Duration() > _jornada.ToleranciaDiaria)
                {
                    if (diferenca > TimeSpan.Zero) extras = diferenca;
                    else faltantes = -diferenca;
                }
            }
        }

        return new ResumoDia(data, contexto, situacao, trabalhado, carga, extras, faltantes, atraso);
    }

    /// <summary>RF25: o dia passou do limite de horas extras que obriga a notificar o gestor?</summary>
    public bool ExcedeLimiteHoraExtra(ResumoDia resumo) => resumo.HorasExtras > _jornada.LimiteHoraExtraDiaria;

    /// <summary>Soma os dias de um período (espelho de ponto e indicadores).</summary>
    public static ResumoPeriodo Totalizar(IEnumerable<ResumoDia> dias)
    {
        var trabalhado = TimeSpan.Zero;
        var extras = TimeSpan.Zero;
        var faltantes = TimeSpan.Zero;
        var atrasos = TimeSpan.Zero;
        var diasTrabalhados = 0;
        var faltas = 0;
        var inconsistentes = 0;

        foreach (var dia in dias)
        {
            trabalhado += dia.Trabalhado;
            extras += dia.HorasExtras;
            faltantes += dia.HorasFaltantes;
            atrasos += dia.Atraso;
            if (dia.Trabalhado > TimeSpan.Zero) diasTrabalhados++;
            if (dia.Situacao == SituacaoDia.Falta) faltas++;
            if (dia.Situacao == SituacaoDia.Inconsistente) inconsistentes++;
        }

        return new ResumoPeriodo(trabalhado, extras, faltantes, atrasos, diasTrabalhados, faltas, inconsistentes);
    }

    // Soma os dois períodos do dia que estiverem fechados (entrada->saída p/ intervalo e retorno->saída).
    private static TimeSpan Trabalhado(Ponto? ponto)
    {
        if (ponto is null) return TimeSpan.Zero;
        return Periodo(ponto.Entrada, ponto.SaidaIntervalo) + Periodo(ponto.RetornoIntervalo, ponto.Saida);
    }

    private static TimeSpan Periodo(TimeOnly? inicio, TimeOnly? fim)
    {
        if (inicio is null || fim is null) return TimeSpan.Zero;
        var duracao = fim.Value.ToTimeSpan() - inicio.Value.ToTimeSpan();
        return duracao > TimeSpan.Zero ? duracao : TimeSpan.Zero; // dado inconsistente nunca gera horas negativas
    }

    private TimeSpan Atraso(Ponto? ponto)
    {
        if (ponto?.Entrada is null) return TimeSpan.Zero;
        var atraso = ponto.Entrada.Value.ToTimeSpan() - _jornada.HoraEntrada.ToTimeSpan();
        return atraso > _jornada.ToleranciaDiaria ? atraso : TimeSpan.Zero;
    }
}
