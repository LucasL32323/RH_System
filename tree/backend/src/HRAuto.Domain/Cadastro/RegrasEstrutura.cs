using HRAuto.Domain.Common;

namespace HRAuto.Domain.Cadastro;

/// <summary>Regras comuns de cargos e departamentos (RF14, RF15): nome obrigatório e descrição opcional.</summary>
public static class RegrasEstrutura
{
    public const int TamanhoMinimoNome = 2;
    public const int TamanhoMaximoNome = 80;
    public const int TamanhoMaximoDescricao = 200;

    /// <summary>Tira espaços repetidos nas pontas e no meio. Falha se vazio ou longo demais.</summary>
    public static string NormalizarNome(string? nome, string rotulo)
    {
        var texto = string.Join(' ', (nome ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (texto.Length < TamanhoMinimoNome)
            throw new DominioException($"O nome do {rotulo} é obrigatório (mínimo de {TamanhoMinimoNome} caracteres).");
        if (texto.Length > TamanhoMaximoNome)
            throw new DominioException($"O nome do {rotulo} pode ter no máximo {TamanhoMaximoNome} caracteres.");
        if (texto.Any(char.IsControl)) throw new DominioException($"O nome do {rotulo} contém caracteres inválidos.");
        return texto;
    }

    public static string? NormalizarDescricao(string? descricao)
    {
        var texto = descricao?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (texto.Length > TamanhoMaximoDescricao)
            throw new DominioException($"A descrição pode ter no máximo {TamanhoMaximoDescricao} caracteres.");
        return texto;
    }
}
