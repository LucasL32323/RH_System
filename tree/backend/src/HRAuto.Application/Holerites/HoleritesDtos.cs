using HRAuto.Domain.Entities;
using HRAuto.Domain.Holerites;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Holerites;

// ---------- Entrada ----------

/// <summary>
/// RF29: o RH publica o PDF do mês. O conteúdo vem da API (multipart) já limitado a 5 MB + 1 byte, para o Domain
/// recusar o excesso com mensagem clara. "Substituir" = true troca o PDF de uma competência já publicada.
/// </summary>
public sealed record PublicarHoleriteDto(
    string? Codigo, int? Mes, int? Ano, string? NomeArquivo, ReadOnlyMemory<byte> Conteudo, bool Substituir);

/// <summary>Sem código = os holerites de quem está logado. Com código, só RH/Admin. Ano é opcional.</summary>
public sealed record FiltroListaHoleritesDto(string? Codigo, int? Ano);

// ---------- Saída ----------

/// <summary>Nunca expõe o caminho do arquivo no servidor (RNF08).</summary>
public sealed record HoleriteDto(
    int Id, string CodigoFuncionario, string NomeFuncionario, int Mes, int Ano, DateOnly DataPublicacao, string NomeArquivo);

public sealed record HoleritePublicadoDto(HoleriteDto Holerite, bool Substituido);

/// <summary>PDF pronto para devolver ao app.</summary>
public sealed record ArquivoHoleriteDto(byte[] Conteudo, string NomeArquivo, string TipoConteudo);

// ---------- Conversões ----------

internal static class MapeadorHolerite
{
    public static HoleriteDto Holerite(Holerite h, IReadOnlyDictionary<int, string> nomes) =>
        new(h.Id,
            CodigoFuncionario.Formatar(h.IdFuncionario),
            nomes.TryGetValue(h.IdFuncionario, out var nome) ? nome : string.Empty,
            h.Mes, h.Ano, h.DataPublicacao,
            RegrasHolerite.NomeDoArquivoParaDownload(h));
}
