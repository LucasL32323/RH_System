using System.Globalization;
using HRAuto.Domain.Entities;

namespace HRAuto.Application.BancoDeHoras;

// ---------- Entrada ----------

/// <summary>Ajuste manual do RH (RF24). Tipo: Credito ou Debito. Sem data, usa hoje.</summary>
public sealed record AjustarBancoHorasDto(string? CodigoFuncionario, string? Tipo, double? Horas, DateOnly? Data, string? Observacao);

// ---------- Saída ----------

/// <summary>Um lançamento do extrato. Horas é sempre positivo; o sinal vem do tipo (Credito/Debito).</summary>
public sealed record LancamentoBancoHorasDto(int Id, DateOnly Data, string Tipo, double Horas, string Origem, string? Observacao);

/// <summary>Saldo total (todos os lançamentos) e extrato do período pedido.</summary>
public sealed record BancoHorasDto(
    string Codigo, string Nome, decimal SaldoHoras, string SaldoFormatado,
    DateOnly Inicio, DateOnly Fim, IReadOnlyList<LancamentoBancoHorasDto> Lancamentos);

public sealed record AjusteRegistradoDto(LancamentoBancoHorasDto Lancamento, decimal SaldoHoras, string SaldoFormatado);

// ---------- Conversões ----------

internal static class MapeadorBancoHoras
{
    public static LancamentoBancoHorasDto Lancamento(BancoHoras l) =>
        new(l.Id, l.Data, l.Tipo, l.Quantidade, l.Origem, l.Observacao);

    /// <summary>1,5 -> "+1h30min"; -0,25 -> "-0h15min"; 0 -> "0h00min".</summary>
    public static string FormatarSaldo(decimal horas)
    {
        var minutos = (int)Math.Round(Math.Abs(horas) * 60, MidpointRounding.AwayFromZero);
        var sinal = minutos == 0 ? string.Empty : horas > 0 ? "+" : "-";
        return string.Create(CultureInfo.InvariantCulture, $"{sinal}{minutos / 60}h{minutos % 60:D2}min");
    }
}
