using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Domain.Calendario;

/// <summary>
/// Regras do cadastro de feriados (RF28):
/// - Nacional vale para todos e não tem cidade nem estado.
/// - Estadual exige o estado (sigla de 2 letras) e não tem cidade.
/// - Municipal exige cidade e estado.
/// Estado é gravado em maiúsculas e a cidade com os espaços das pontas removidos.
/// </summary>
public static class RegrasFeriado
{
    public const int TamanhoMinimoNome = 3;
    public const int TamanhoMaximoNome = 100;
    public const int TamanhoMaximoCidade = 100;

    /// <summary>Cria o feriado ou lança <see cref="DominioException"/> com o motivo.</summary>
    public static Feriado Criar(DateOnly data, string? nome, string? tipo, string? cidade, string? estado)
    {
        var nomeLimpo = nome?.Trim() ?? string.Empty;
        if (nomeLimpo.Length < TamanhoMinimoNome || nomeLimpo.Length > TamanhoMaximoNome)
            throw new DominioException($"O nome do feriado deve ter de {TamanhoMinimoNome} a {TamanhoMaximoNome} caracteres.");

        var tipoNormalizado = NormalizarTipo(tipo)
            ?? throw new DominioException("Tipo inválido. Use Nacional, Estadual ou Municipal.");

        var cidadeLimpa = string.IsNullOrWhiteSpace(cidade) ? null : cidade.Trim();
        var estadoLimpo = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();

        if (estadoLimpo is not null && !EstadoValido(estadoLimpo))
            throw new DominioException("Estado inválido. Use a sigla de 2 letras (ex.: SP).");
        if (cidadeLimpa is { Length: > TamanhoMaximoCidade })
            throw new DominioException($"A cidade deve ter no máximo {TamanhoMaximoCidade} caracteres.");

        switch (tipoNormalizado)
        {
            case TiposFeriado.Nacional:
                if (cidadeLimpa is not null || estadoLimpo is not null)
                    throw new DominioException("Feriado nacional não tem cidade nem estado.");
                break;
            case TiposFeriado.Estadual:
                if (estadoLimpo is null) throw new DominioException("Informe o estado do feriado estadual.");
                if (cidadeLimpa is not null) throw new DominioException("Feriado estadual não tem cidade.");
                break;
            case TiposFeriado.Municipal:
                if (estadoLimpo is null || cidadeLimpa is null)
                    throw new DominioException("Informe a cidade e o estado do feriado municipal.");
                break;
        }

        return new Feriado
        {
            Data = data,
            Nome = nomeLimpo,
            Tipo = tipoNormalizado,
            Cidade = cidadeLimpa,
            Estado = estadoLimpo
        };
    }

    /// <summary>"nacional", "ESTADUAL"... para o texto oficial gravado no banco; nulo se não for um tipo conhecido.</summary>
    public static string? NormalizarTipo(string? tipo) => tipo?.Trim().ToLowerInvariant() switch
    {
        "nacional" => TiposFeriado.Nacional,
        "estadual" => TiposFeriado.Estadual,
        "municipal" => TiposFeriado.Municipal,
        _ => null
    };

    private static bool EstadoValido(string estado) => estado.Length == 2 && estado.All(c => c is >= 'A' and <= 'Z');
}
