using HRAuto.Application.Abstractions;

namespace HRAuto.Infrastructure.Tempo;

/// <summary>
/// Relógio do servidor no fuso de Brasília. Aceita um "provedor de hora UTC" para permitir testes.
/// </summary>
public sealed class RelogioBrasilia : IRelogio
{
    private readonly Func<DateTimeOffset> _utcAgora;
    private readonly TimeZoneInfo _fuso;

    public RelogioBrasilia() : this(() => DateTimeOffset.UtcNow) { }

    public RelogioBrasilia(Func<DateTimeOffset> utcAgora)
    {
        _utcAgora = utcAgora;
        _fuso = ObterFusoBrasilia();
    }

    public DateTimeOffset Agora() => TimeZoneInfo.ConvertTime(_utcAgora(), _fuso);

    public DateOnly Hoje() => DateOnly.FromDateTime(Agora().DateTime);

    // Linux/macOS usam o id IANA; Windows usa o id próprio. Tenta os dois.
    private static TimeZoneInfo ObterFusoBrasilia()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { /* tenta o próximo id */ }
            catch (InvalidTimeZoneException) { /* tenta o próximo id */ }
        }

        // Último recurso: UTC-3 fixo (o Brasil não usa horário de verão desde 2019).
        return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "Brasília", "Brasília");
    }
}
