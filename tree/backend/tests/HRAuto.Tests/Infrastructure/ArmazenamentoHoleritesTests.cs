using System.Text;
using HRAuto.Domain.Holerites;
using HRAuto.Infrastructure.Armazenamento;
using Microsoft.Extensions.Options;

namespace HRAuto.Tests.Infrastructure;

/// <summary>RNF17 e RNF08: o armazenamento grava só em nomes gerados pelo sistema e nunca sai da pasta raiz.</summary>
public sealed class ArmazenamentoHoleritesTests : IDisposable
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF");

    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"hrauto-holerites-{Guid.NewGuid():N}");
    private readonly ArmazenamentoHoleritesEmDisco _armazenamento;

    public ArmazenamentoHoleritesTests()
    {
        _armazenamento = new ArmazenamentoHoleritesEmDisco(Options.Create(new OpcoesHolerites { Pasta = Path.Combine(_raiz, "pdfs") }));
    }

    public void Dispose()
    {
        try { Directory.Delete(_raiz, recursive: true); } catch (IOException) { /* a pasta temp é limpa pelo SO */ }
    }

    [Fact]
    public async Task Salvar_CriaOArquivoNoCaminhoRelativoSeguro_ELerDevolveOConteudo()
    {
        var caminho = await _armazenamento.SalvarAsync(4, 2026, 9, Pdf);

        Assert.True(RegrasHolerite.CaminhoEhSeguro(caminho));
        Assert.StartsWith("FUNC-0004/2026/09-", caminho);
        Assert.True(File.Exists(Path.Combine(_raiz, "pdfs", caminho.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Equal(Pdf, await _armazenamento.LerAsync(caminho));
    }

    [Fact]
    public async Task SalvarDuasVezes_NaoSobrescreve()
    {
        var primeiro = await _armazenamento.SalvarAsync(4, 2026, 9, Pdf);
        var segundo = await _armazenamento.SalvarAsync(4, 2026, 9, Encoding.ASCII.GetBytes("%PDF-1.7 outro"));

        Assert.NotEqual(primeiro, segundo);
        Assert.Equal(Pdf, await _armazenamento.LerAsync(primeiro));
    }

    [Fact]
    public async Task Remover_ApagaOArquivo_EIgnoraInexistente()
    {
        var caminho = await _armazenamento.SalvarAsync(4, 2026, 9, Pdf);

        await _armazenamento.RemoverAsync(caminho);
        await _armazenamento.RemoverAsync(caminho); // segunda vez não falha

        Assert.Null(await _armazenamento.LerAsync(caminho));
    }

    [Theory]
    [InlineData("../fora.pdf")]
    [InlineData("FUNC-0004/2026/../../../fora.pdf")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\system.ini")]
    [InlineData("FUNC-0004/2026/09-curto.pdf")]
    public async Task CaminhoForaDoPadrao_NuncaTocaNoDisco(string caminho)
    {
        // Um arquivo "alvo" fora da pasta raiz não pode ser lido nem apagado por nenhum caminho forjado.
        var alvo = Path.Combine(_raiz, "fora.pdf");
        Directory.CreateDirectory(_raiz);
        await File.WriteAllBytesAsync(alvo, Pdf);

        Assert.Null(await _armazenamento.LerAsync(caminho));
        await _armazenamento.RemoverAsync(caminho);

        Assert.True(File.Exists(alvo));
    }

    [Fact]
    public void PastaPadrao_FicaEmDadosHoleritesFogaDoWwwroot()
    {
        Assert.Equal(Path.Combine("dados", "holerites"), new OpcoesHolerites().Pasta);
    }
}
