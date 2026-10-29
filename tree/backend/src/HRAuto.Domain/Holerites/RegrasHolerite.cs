using System.Text;
using System.Text.RegularExpressions;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Domain.Holerites;

/// <summary>
/// Holerites (RF29, RF30, RNF17): o RH publica um PDF por funcionário/mês/ano; o funcionário consulta e baixa o próprio.
/// O arquivo fica em uma pasta do servidor e o banco guarda só o caminho RELATIVO. Aqui ficam as regras que não dependem
/// de disco nem de HTTP: competência válida, tamanho e tipo do arquivo e formato seguro do caminho.
/// </summary>
public static partial class RegrasHolerite
{
    /// <summary>RNF17: no máximo 5 MB por arquivo.</summary>
    public const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public const int AnoMinimo = 2000;

    private static readonly byte[] AssinaturaPdf = Encoding.ASCII.GetBytes("%PDF-");

    // FUNC-0001/2026/09-<32 hex>.pdf — nada de "..", barras invertidas, unidade de disco ou nome vindo do usuário.
    [GeneratedRegex(@"^FUNC-\d{4,}/\d{4}/\d{2}-[0-9a-f]{32}\.pdf$", RegexOptions.CultureInvariant)]
    private static partial Regex PadraoDeCaminho();

    /// <summary>Mês de 1 a 12, ano a partir de 2000, não pode ser futuro nem anterior ao mês de admissão.</summary>
    public static void ValidarCompetencia(int mes, int ano, DateOnly hoje, DateOnly admissao)
    {
        if (mes is < 1 or > 12) throw new DominioException("O mês do holerite deve estar entre 1 e 12.");
        if (ano < AnoMinimo) throw new DominioException($"O ano do holerite deve ser {AnoMinimo} ou posterior.");

        var competencia = ano * 12 + mes;
        if (competencia > hoje.Year * 12 + hoje.Month)
            throw new DominioException("Não é possível publicar o holerite de um mês que ainda não começou.");
        if (competencia < admissao.Year * 12 + admissao.Month)
            throw new DominioException("O funcionário ainda não havia sido admitido nessa competência.");
    }

    /// <summary>Só PDF (extensão e assinatura "%PDF-"), não vazio e com no máximo 5 MB (RNF17).</summary>
    public static void ValidarArquivo(string? nomeOriginal, ReadOnlySpan<byte> conteudo)
    {
        if (conteudo.IsEmpty) throw new DominioException("O arquivo do holerite está vazio.");
        if (conteudo.Length > TamanhoMaximoBytes)
            throw new DominioException($"O arquivo do holerite deve ter no máximo {TamanhoMaximoBytes / (1024 * 1024)} MB.");

        var extensao = Path.GetExtension(nomeOriginal ?? string.Empty);
        if (!string.Equals(extensao, ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new DominioException("O holerite deve ser um arquivo PDF.");
        if (!conteudo.StartsWith(AssinaturaPdf))
            throw new DominioException("O arquivo enviado não é um PDF válido.");
    }

    /// <summary>Monta o caminho relativo no padrão seguro. O nome do arquivo é sorteado: nunca usa texto do usuário.</summary>
    public static string MontarCaminho(int idFuncionario, int ano, int mes, Guid unico) =>
        $"{CodigoFuncionario.Formatar(idFuncionario)}/{ano:D4}/{mes:D2}-{unico:N}.pdf";

    /// <summary>Confere o formato do caminho gravado no banco antes de tocar no disco (defesa contra path traversal).</summary>
    public static bool CaminhoEhSeguro(string? caminho) =>
        !string.IsNullOrEmpty(caminho) && PadraoDeCaminho().IsMatch(caminho);

    public static Holerite Criar(int idFuncionario, int mes, int ano, DateOnly hoje, string caminho)
    {
        if (idFuncionario <= 0) throw new DominioException("O funcionário do holerite é obrigatório.");
        ExigirCaminhoSeguro(caminho);
        return new Holerite { IdFuncionario = idFuncionario, Mes = mes, Ano = ano, DataPublicacao = hoje, Arquivo = caminho };
    }

    /// <summary>Troca o arquivo da competência por uma nova versão (o RH corrigiu o PDF).</summary>
    public static void Substituir(Holerite existente, DateOnly hoje, string novoCaminho)
    {
        ExigirCaminhoSeguro(novoCaminho);
        existente.Arquivo = novoCaminho;
        existente.DataPublicacao = hoje;
    }

    /// <summary>Nome sugerido ao baixar: sem dados pessoais, por exemplo "holerite-2026-09.pdf".</summary>
    public static string NomeDoArquivoParaDownload(Holerite holerite) => $"holerite-{holerite.Ano:D4}-{holerite.Mes:D2}.pdf";

    private static void ExigirCaminhoSeguro(string caminho)
    {
        if (!CaminhoEhSeguro(caminho)) throw new DominioException("Caminho de arquivo inválido para o holerite.");
    }
}
