using System.Globalization;
using System.Text.RegularExpressions;
using HRAuto.Domain.Common;

namespace HRAuto.Domain.ValueObjects;

/// <summary>
/// Código do funcionário no formato FUNC-0001 (RF11/RF13).
/// É derivado de FUNCIONARIO.id_funcionario: por isso é único, sequencial e imutável.
/// A busca na tela de funcionários é feita pelo código, não pelo nome.
/// </summary>
public readonly record struct CodigoFuncionario
{
    private const string Prefixo = "FUNC-";
    private static readonly Regex Padrao =
        new(@"^FUNC-(\d{4,9})$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Id do funcionário (id_funcionario).</summary>
    public int Id { get; }

    /// <summary>Texto no formato FUNC-0001.</summary>
    public string Valor => Formatar(Id);

    public CodigoFuncionario(int id)
    {
        if (id <= 0) throw new DominioException("O id do funcionário deve ser maior que zero.");
        Id = id;
    }

    /// <summary>Gera o texto FUNC-NNNN (mínimo de 4 dígitos) a partir do id.</summary>
    public static string Formatar(int id)
    {
        if (id <= 0) throw new DominioException("O id do funcionário deve ser maior que zero.");
        return Prefixo + id.ToString("D4", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Lê um texto digitado na busca (aceita minúsculas e espaços nas pontas).
    /// Retorna false se o formato não for FUNC- seguido de 4 a 9 dígitos, ou se o id for zero.
    /// </summary>
    public static bool TentarConverter(string? texto, out CodigoFuncionario codigo)
    {
        codigo = default;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var m = Padrao.Match(texto.Trim());
        if (!m.Success) return false;

        if (!int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            return false;

        codigo = new CodigoFuncionario(id);
        return true;
    }

    public override string ToString() => Valor;
}
