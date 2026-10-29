using System.Text;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Holerites;
using static HRAuto.Tests.Domain.Apoio;

namespace HRAuto.Tests.Domain;

/// <summary>RF29, RF30, RNF17: competência, arquivo PDF até 5 MB e caminho seguro.</summary>
public class HoleriteTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF");

    // ---------- Competência ----------

    [Fact]
    public void Competencia_MesAtualEMesDaAdmissao_SaoAceitos()
    {
        RegrasHolerite.ValidarCompetencia(10, 2026, D("2026-10-21"), D("2024-03-11"));
        RegrasHolerite.ValidarCompetencia(3, 2024, D("2026-10-21"), D("2024-03-11"));
    }

    [Theory]
    [InlineData(0, 2026)]
    [InlineData(13, 2026)]
    [InlineData(9, 1999)]
    [InlineData(11, 2026)] // mês que ainda não começou
    [InlineData(1, 2027)]
    [InlineData(2, 2024)] // antes da admissão (março/2024)
    public void Competencia_Invalida_EhRecusada(int mes, int ano)
    {
        Assert.Throws<DominioException>(() => RegrasHolerite.ValidarCompetencia(mes, ano, D("2026-10-21"), D("2024-03-11")));
    }

    // ---------- Arquivo ----------

    [Fact]
    public void Arquivo_PdfValido_EhAceito_InclusiveComExtensaoMaiuscula()
    {
        RegrasHolerite.ValidarArquivo("holerite.pdf", Pdf);
        RegrasHolerite.ValidarArquivo("HOLERITE.PDF", Pdf);
    }

    [Fact]
    public void Arquivo_Vazio_ForaDeTipo_OuSemAssinatura_SaoRecusados()
    {
        Assert.Throws<DominioException>(() => RegrasHolerite.ValidarArquivo("a.pdf", ReadOnlySpan<byte>.Empty));
        Assert.Throws<DominioException>(() => RegrasHolerite.ValidarArquivo("a.exe", Pdf));
        Assert.Throws<DominioException>(() => RegrasHolerite.ValidarArquivo(null, Pdf));
        Assert.Throws<DominioException>(() => RegrasHolerite.ValidarArquivo("a.pdf", Encoding.ASCII.GetBytes("MZ qualquer coisa")));
    }

    [Fact]
    public void Arquivo_ComExatamente5Mb_PassaE5MbMaisUmByte_NaoPassa()
    {
        var limite = new byte[RegrasHolerite.TamanhoMaximoBytes];
        Pdf.CopyTo(limite, 0);
        RegrasHolerite.ValidarArquivo("a.pdf", limite);

        var excedido = new byte[RegrasHolerite.TamanhoMaximoBytes + 1];
        Pdf.CopyTo(excedido, 0);
        var erro = Assert.Throws<DominioException>(() => RegrasHolerite.ValidarArquivo("a.pdf", excedido));
        Assert.Contains("5 MB", erro.Message);
    }

    // ---------- Caminho ----------

    [Fact]
    public void Caminho_Montado_EhSeguro_ESemTextoDoUsuario()
    {
        var caminho = RegrasHolerite.MontarCaminho(4, 2026, 9, Guid.Parse("0123456789abcdef0123456789abcdef"));

        Assert.Equal("FUNC-0004/2026/09-0123456789abcdef0123456789abcdef.pdf", caminho);
        Assert.True(RegrasHolerite.CaminhoEhSeguro(caminho));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../segredo.pdf")]
    [InlineData("FUNC-0004/2026/../../../etc/passwd")]
    [InlineData("FUNC-0004\\2026\\09-0123456789abcdef0123456789abcdef.pdf")]
    [InlineData("/FUNC-0004/2026/09-0123456789abcdef0123456789abcdef.pdf")]
    [InlineData("C:/FUNC-0004/2026/09-0123456789abcdef0123456789abcdef.pdf")]
    [InlineData("FUNC-0004/2026/09-0123456789ABCDEF0123456789ABCDEF.pdf")]
    [InlineData("FUNC-0004/2026/09-0123456789abcdef0123456789abcdef.exe")]
    [InlineData("FUNC-0004/2026/09-curto.pdf")]
    public void Caminho_ForaDoPadrao_EhRejeitado(string? caminho)
    {
        Assert.False(RegrasHolerite.CaminhoEhSeguro(caminho));
    }

    // ---------- Criar e substituir ----------

    [Fact]
    public void Criar_PreencheOsCampos_ERecusaCaminhoInseguro()
    {
        var caminho = RegrasHolerite.MontarCaminho(4, 2026, 9, Guid.NewGuid());

        var holerite = RegrasHolerite.Criar(4, 9, 2026, D("2026-10-21"), caminho);

        Assert.Equal(4, holerite.IdFuncionario);
        Assert.Equal(9, holerite.Mes);
        Assert.Equal(2026, holerite.Ano);
        Assert.Equal(D("2026-10-21"), holerite.DataPublicacao);
        Assert.Equal(caminho, holerite.Arquivo);
        Assert.Throws<DominioException>(() => RegrasHolerite.Criar(4, 9, 2026, D("2026-10-21"), "../x.pdf"));
        Assert.Throws<DominioException>(() => RegrasHolerite.Criar(0, 9, 2026, D("2026-10-21"), caminho));
    }

    [Fact]
    public void Substituir_TrocaOArquivoEADataDePublicacao()
    {
        var antigo = RegrasHolerite.MontarCaminho(4, 2026, 9, Guid.NewGuid());
        var novo = RegrasHolerite.MontarCaminho(4, 2026, 9, Guid.NewGuid());
        var holerite = new Holerite { IdFuncionario = 4, Mes = 9, Ano = 2026, Arquivo = antigo, DataPublicacao = D("2026-10-05") };

        RegrasHolerite.Substituir(holerite, D("2026-10-21"), novo);

        Assert.Equal(novo, holerite.Arquivo);
        Assert.Equal(D("2026-10-21"), holerite.DataPublicacao);
        Assert.Throws<DominioException>(() => RegrasHolerite.Substituir(holerite, D("2026-10-21"), "../x.pdf"));
        Assert.Equal(novo, holerite.Arquivo);
    }

    [Fact]
    public void NomeParaDownload_NaoTemDadosPessoais()
    {
        var holerite = new Holerite { IdFuncionario = 4, Mes = 9, Ano = 2026 };

        Assert.Equal("holerite-2026-09.pdf", RegrasHolerite.NomeDoArquivoParaDownload(holerite));
    }
}
