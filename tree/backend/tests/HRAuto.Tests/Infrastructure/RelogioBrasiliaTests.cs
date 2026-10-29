using HRAuto.Infrastructure.Tempo;

namespace HRAuto.Tests.Infrastructure;

public class RelogioBrasiliaTests
{
    [Fact]
    public void Agora_ConverteUtcParaBrasilia_MenosTresHoras()
    {
        // 15/10/2026 15:00 UTC => 12:00 em Brasília (UTC-3, sem horário de verão)
        var utc = new DateTimeOffset(2026, 10, 15, 15, 0, 0, TimeSpan.Zero);
        var relogio = new RelogioBrasilia(() => utc);

        var agora = relogio.Agora();

        Assert.Equal(TimeSpan.FromHours(-3), agora.Offset);
        Assert.Equal(12, agora.Hour);
    }

    [Fact]
    public void Hoje_UsaADataDeBrasilia_NaVirada()
    {
        // 16/10/2026 01:30 UTC ainda é 15/10 22:30 em Brasília
        var utc = new DateTimeOffset(2026, 10, 16, 1, 30, 0, TimeSpan.Zero);
        var relogio = new RelogioBrasilia(() => utc);

        Assert.Equal(new DateOnly(2026, 10, 15), relogio.Hoje());
    }
}
