namespace HRAuto.Domain.ValueObjects;

/// <summary>
/// CPF (RF10, RNF08). Dado pessoal: no banco fica só com os 11 dígitos; nas listagens aparece mascarado.
/// A validação confere o tamanho e os dois dígitos verificadores (módulo 11).
/// </summary>
public static class Cpf
{
    public const int Tamanho = 11;

    /// <summary>Aceita com ou sem máscara (123.456.789-09). Devolve só os 11 dígitos. Retorna false se for inválido.</summary>
    public static bool TentarNormalizar(string? texto, out string digitos)
    {
        digitos = string.Empty;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var limpo = new System.Text.StringBuilder(Tamanho);
        foreach (var c in texto.Trim())
        {
            if (c is >= '0' and <= '9') limpo.Append(c);
            else if (c is '.' or '-' or ' ') continue; // só estes símbolos de máscara são tolerados
            else return false;
        }

        var d = limpo.ToString();
        if (d.Length != Tamanho) return false;
        if (d.All(c => c == d[0])) return false; // 111.111.111-11 passa na conta, mas não existe

        if (DigitoVerificador(d, 9) != d[9] - '0') return false;
        if (DigitoVerificador(d, 10) != d[10] - '0') return false;

        digitos = d;
        return true;
    }

    /// <summary>12345678909 -> 123.456.789-09. Entrada deve ter 11 dígitos.</summary>
    public static string Formatar(string digitos) =>
        digitos.Length == Tamanho
            ? $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..]}"
            : digitos;

    /// <summary>12345678909 -> ***.456.789-** (LGPD: listagens não mostram o CPF inteiro).</summary>
    public static string Mascarar(string digitos) =>
        digitos.Length == Tamanho ? $"***.{digitos[3..6]}.{digitos[6..9]}-**" : "***";

    // Soma ponderada dos 'quantos' primeiros dígitos, pesos de quantos+1 até 2.
    private static int DigitoVerificador(string d, int quantos)
    {
        var soma = 0;
        for (var i = 0; i < quantos; i++) soma += (d[i] - '0') * (quantos + 1 - i);
        var resto = soma * 10 % 11;
        return resto == 10 ? 0 : resto;
    }
}
