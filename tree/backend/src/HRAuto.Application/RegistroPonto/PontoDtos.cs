using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;

namespace HRAuto.Application.RegistroPonto;

// DTOs do ponto. Durações vão em minutos inteiros (mais fácil para o app Flutter formatar);
// horários em HH:mm:ss; datas em AAAA-MM-DD (ISO 8601, RNF15).

public sealed record BatidaDto(TipoBatida Tipo, string Nome, TimeOnly Horario);

/// <summary>Estado do ponto de hoje: o app usa para mostrar o botão certo e a hora do servidor (RF17, RNF10).</summary>
public sealed record StatusPontoHojeDto(
    DateOnly Data,
    DateTimeOffset HoraServidor,
    IReadOnlyList<BatidaDto> Batidas,
    TipoBatida? ProximaBatida,
    string? ProximaBatidaNome);

public sealed record ResumoDiaDto(
    DateOnly Data,
    ContextoDia Contexto,
    SituacaoDia Situacao,
    int TrabalhadoMinutos,
    int CargaEsperadaMinutos,
    int ExtrasMinutos,
    int FaltantesMinutos,
    int AtrasoMinutos,
    int SaldoMinutos)
{
    public static ResumoDiaDto De(ResumoDia r) => new(
        r.Data, r.Contexto, r.Situacao,
        Minutos(r.Trabalhado), Minutos(r.CargaEsperada), Minutos(r.HorasExtras),
        Minutos(r.HorasFaltantes), Minutos(r.Atraso), Minutos(r.Saldo));

    public static int Minutos(TimeSpan duracao) => (int)Math.Round(duracao.TotalMinutes, MidpointRounding.AwayFromZero);
}

/// <summary>Resposta de POST /ponto/batidas. "FechamentoDoDia" só vem preenchido quando esta foi a 4ª batida.</summary>
public sealed record BatidaRegistradaDto(
    TipoBatida Tipo,
    string Nome,
    DateOnly Data,
    TimeOnly Horario,
    TipoBatida? ProximaBatida,
    ResumoDiaDto? FechamentoDoDia);

// ---- Sincronização offline (RF19, RNF09) ----

/// <summary>Uma batida guardada no aparelho sem internet. Campos opcionais para que a falta vire "Recusada" com motivo.</summary>
public sealed record BatidaOfflineDto(DateOnly? Data, TipoBatida? Tipo, TimeOnly? Horario);

public sealed record SincronizarBatidasDto(IReadOnlyList<BatidaOfflineDto>? Batidas);

public enum SituacaoItemOffline
{
    /// <summary>Gravada agora.</summary>
    Aplicada = 1,
    /// <summary>A mesma batida (tipo e horário) já estava gravada; nada foi duplicado.</summary>
    JaRegistrada = 2,
    /// <summary>Não aceita; veja o motivo. Pode exigir manutenção de ponto (RF22).</summary>
    Recusada = 3
}

public sealed record ResultadoBatidaOfflineDto(
    DateOnly? Data,
    TipoBatida? Tipo,
    TimeOnly? Horario,
    SituacaoItemOffline Situacao,
    string? Motivo);

// ---- Espelho de ponto (RF21) ----

public sealed record DiaEspelhoDto(
    DateOnly Data,
    ContextoDia Contexto,
    SituacaoDia Situacao,
    TimeOnly? Entrada,
    TimeOnly? SaidaIntervalo,
    TimeOnly? RetornoIntervalo,
    TimeOnly? Saida,
    int TrabalhadoMinutos,
    int ExtrasMinutos,
    int FaltantesMinutos,
    int AtrasoMinutos);

public sealed record TotaisEspelhoDto(
    int TrabalhadoMinutos,
    int ExtrasMinutos,
    int FaltantesMinutos,
    int AtrasosMinutos,
    int DiasTrabalhados,
    int Faltas,
    int DiasInconsistentes,
    int SaldoMinutos);

public sealed record EspelhoPontoDto(
    string Codigo,
    string Nome,
    DateOnly De,
    DateOnly Ate,
    IReadOnlyList<DiaEspelhoDto> Dias,
    TotaisEspelhoDto Totais,
    decimal SaldoBancoHorasHoras);

internal static class PontoMapeador
{
    /// <summary>Hora do servidor sem fração de segundo (a coluna guarda HH:MM:SS).</summary>
    public static TimeOnly Hora(DateTimeOffset agora) => new(agora.Hour, agora.Minute, agora.Second);

    public static DateOnly Dia(DateTimeOffset agora) => DateOnly.FromDateTime(agora.DateTime);

    public static IReadOnlyList<BatidaDto> Batidas(Ponto? ponto)
    {
        var lista = new List<BatidaDto>();
        if (ponto is null) return lista;

        foreach (var tipo in new[] { TipoBatida.Entrada, TipoBatida.SaidaIntervalo, TipoBatida.RetornoIntervalo, TipoBatida.Saida })
            if (ponto.Obter(tipo) is { } horario)
                lista.Add(new BatidaDto(tipo, Ponto.Nome(tipo), horario));
        return lista;
    }
}
