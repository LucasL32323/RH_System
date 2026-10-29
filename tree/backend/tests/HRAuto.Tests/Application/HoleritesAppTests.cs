using System.Text;
using HRAuto.Application.Common;
using HRAuto.Application.Holerites;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Holerites;

namespace HRAuto.Tests.Application;

/// <summary>
/// RF29 (publicar), RF30 (listar e baixar), RNF08 e RNF17. Relógio: quarta-feira, 21/10/2026.
/// Ana (FUNC-0004) foi admitida em 11/03/2024. Os PDFs de teste são só a assinatura "%PDF-" com algum texto.
/// </summary>
public class HoleritesAppTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF");
    private static readonly byte[] PdfNovo = Encoding.ASCII.GetBytes("%PDF-1.7\n%% versão corrigida\n%%EOF");

    private static PublicarHoleriteDto Dto(
        string codigo = "FUNC-0004", int mes = 9, int ano = 2026, string nome = "holerite-setembro.pdf",
        byte[]? conteudo = null, bool substituir = false) =>
        new(codigo, mes, ano, nome, conteudo ?? Pdf, substituir);

    // ---------- Publicar ----------

    [Fact]
    public async Task Publicar_Valido_GravaOArquivo_OCaminhoRelativo_AvisaEAudita()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto());

        Assert.True(r.Sucesso, r.Erro);
        Assert.False(r.Valor!.Substituido);
        Assert.Equal("FUNC-0004", r.Valor.Holerite.CodigoFuncionario);
        Assert.Equal("Ana Souza", r.Valor.Holerite.NomeFuncionario);
        Assert.Equal(9, r.Valor.Holerite.Mes);
        Assert.Equal(2026, r.Valor.Holerite.Ano);
        Assert.Equal(new DateOnly(2026, 10, 21), r.Valor.Holerite.DataPublicacao);
        Assert.Equal("holerite-2026-09.pdf", r.Valor.Holerite.NomeArquivo); // o nome original do RH não vira nome do arquivo
        Assert.True(r.Valor.Holerite.Id > 0);

        var holerite = Assert.Single(c.Banco.Holerites);
        Assert.True(RegrasHolerite.CaminhoEhSeguro(holerite.Arquivo));
        Assert.StartsWith("FUNC-0004/2026/09-", holerite.Arquivo);
        Assert.Equal(Pdf, c.Armazenamento.Arquivos[holerite.Arquivo]);

        var aviso = Assert.Single(c.Banco.Notificacoes);
        Assert.Equal(e.Ana.Id, aviso.IdFuncionario);
        Assert.Equal("Novo holerite disponível", aviso.Titulo);

        var log = Assert.Single(c.Banco.Logs);
        Assert.Equal("PublicarHolerite", log.Acao);
        Assert.Equal("HOLERITE", log.Tabela);
        Assert.Equal(holerite.Id, log.IdRegistro);
        Assert.DoesNotContain(holerite.Arquivo, log.Descricao); // o caminho no servidor não vai para o log
        Assert.Equal(1, c.Banco.TransacoesConfirmadas);
    }

    [Fact]
    public async Task Publicar_AdminTambemPode()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), Dto());

        Assert.True(r.Sucesso, r.Erro);
    }

    [Fact]
    public async Task Publicar_SemSerRhOuAdmin_Da403_ENadaEGravado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var gestor = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioGestor), Dto());
        var propria = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioAna), Dto());

        Assert.Equal(TipoErro.Proibido, gestor.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, propria.TipoDoErro);
        Assert.Equal(0, c.Armazenamento.Gravacoes);
        Assert.Empty(c.Banco.Holerites);
    }

    [Fact]
    public async Task Publicar_ParaSiMesmo_Da403_MesmoSendoRh()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto(codigo: "FUNC-0002"));

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
        Assert.Contains("seu próprio holerite", r.Erro);
        Assert.Equal(0, c.Armazenamento.Gravacoes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ana")]
    [InlineData("FUNC-ABC")]
    public async Task Publicar_CodigoInvalido_Da400(string? codigo)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto(codigo: codigo!));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Fact]
    public async Task Publicar_FuncionarioInexistente_Da404()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto(codigo: "FUNC-0099"));

        Assert.Equal(TipoErro.NaoEncontrado, r.TipoDoErro);
    }

    [Fact]
    public async Task Publicar_SemMesOuAno_Da400()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.PublicarHolerite().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), new PublicarHoleriteDto("FUNC-0004", null, 2026, "a.pdf", Pdf, false));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    [Theory]
    [InlineData(13, 2026)]
    [InlineData(0, 2026)]
    [InlineData(11, 2026)] // mês que ainda não começou
    [InlineData(2, 2024)] // antes da admissão da Ana
    public async Task Publicar_CompetenciaInvalida_Da400_ENadaEGravado(int mes, int ano)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto(mes: mes, ano: ano));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Equal(0, c.Armazenamento.Gravacoes);
        Assert.Empty(c.Banco.Holerites);
    }

    [Fact]
    public async Task Publicar_ArquivoInvalido_Da400_ENadaEGravado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var grande = new byte[RegrasHolerite.TamanhoMaximoBytes + 1];
        Pdf.CopyTo(grande, 0);

        var naoPdf = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto(nome: "holerite.docx"));
        var falsoPdf = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto(conteudo: Encoding.ASCII.GetBytes("MZ executável")));
        var vazio = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto(conteudo: Array.Empty<byte>()));
        var excedido = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto(conteudo: grande));

        Assert.All(new[] { naoPdf, falsoPdf, vazio, excedido }, r => Assert.Equal(TipoErro.Validacao, r.TipoDoErro));
        Assert.Contains("5 MB", excedido.Erro);
        Assert.Equal(0, c.Armazenamento.Gravacoes);
        Assert.Empty(c.Banco.Holerites);
    }

    [Fact]
    public async Task Publicar_CompetenciaRepetida_Da409_ComSubstituir_TrocaOArquivo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var original = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto());
        var caminhoAntigo = c.Banco.Holerites[0].Arquivo;

        var repetido = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto(conteudo: PdfNovo));
        Assert.Equal(TipoErro.Conflito, repetido.TipoDoErro);
        Assert.Equal(1, c.Armazenamento.Gravacoes); // o segundo arquivo nem chegou a ser gravado

        var substituido = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto(conteudo: PdfNovo, substituir: true));

        Assert.True(substituido.Sucesso, substituido.Erro);
        Assert.True(substituido.Valor!.Substituido);
        Assert.Equal(original.Valor!.Holerite.Id, substituido.Valor.Holerite.Id);
        var holerite = Assert.Single(c.Banco.Holerites);
        Assert.NotEqual(caminhoAntigo, holerite.Arquivo);
        Assert.Equal(PdfNovo, c.Armazenamento.Arquivos[holerite.Arquivo]);
        Assert.True(c.Armazenamento.Arquivos.ContainsKey(caminhoAntigo)); // a versão anterior fica guardada (RNF07)
        Assert.Equal("Holerite atualizado", c.Banco.Notificacoes[^1].Titulo);
        Assert.Equal("SubstituirHolerite", c.Banco.Logs[^1].Acao);
    }

    [Fact]
    public async Task Publicar_FalhaAoGravarNoBanco_ApagaOArquivoNovo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.Banco.ConflitoNoProximoSalvar = true;

        var r = await c.PublicarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), Dto());

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.Equal(1, c.Armazenamento.Gravacoes);
        Assert.Empty(c.Armazenamento.Arquivos); // nenhum arquivo órfão
        Assert.Equal(0, c.Banco.TransacoesConfirmadas);
    }

    // ---------- Listar ----------

    [Fact]
    public async Task Listar_SemCodigo_DevolveOsMeus_DoMaisRecenteAoMaisAntigo_SemExporOCaminho()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovoHolerite(e.Ana, 8, 2026);
        c.NovoHolerite(e.Ana, 9, 2026);
        c.NovoHolerite(e.Ana, 12, 2025);
        c.NovoHolerite(e.Bruno, 9, 2026);

        var r = await c.ListarHolerites().ExecutarAsync(c.Solicitante(e.UsuarioAna), null);

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(new[] { "2026-09", "2026-08", "2025-12" }, r.Valor!.Select(h => $"{h.Ano}-{h.Mes:D2}").ToArray());
        Assert.All(r.Valor, h => Assert.Equal("FUNC-0004", h.CodigoFuncionario));
        Assert.DoesNotContain(typeof(HoleriteDto).GetProperties(), p => p.Name.Contains("Arquivo") && p.Name != nameof(HoleriteDto.NomeArquivo));
    }

    [Fact]
    public async Task Listar_FiltraPorAno()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovoHolerite(e.Ana, 9, 2026);
        c.NovoHolerite(e.Ana, 12, 2025);

        var r = await c.ListarHolerites().ExecutarAsync(c.Solicitante(e.UsuarioAna), new FiltroListaHoleritesDto(null, 2025));

        Assert.Equal(2025, Assert.Single(r.Valor!).Ano);
    }

    [Fact]
    public async Task Listar_DeOutraPessoa_SoRhOuAdmin_ColaboradorEGestorRecebem403()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovoHolerite(e.Ana, 9, 2026);

        var colega = await c.ListarHolerites().ExecutarAsync(c.Solicitante(e.UsuarioBruno), new FiltroListaHoleritesDto("FUNC-0004", null));
        var gestor = await c.ListarHolerites().ExecutarAsync(c.Solicitante(e.UsuarioGestor), new FiltroListaHoleritesDto("FUNC-0004", null));
        var rh = await c.ListarHolerites().ExecutarAsync(c.Solicitante(e.UsuarioRh), new FiltroListaHoleritesDto("FUNC-0004", null));
        var proprioPeloCodigo = await c.ListarHolerites().ExecutarAsync(c.Solicitante(e.UsuarioAna), new FiltroListaHoleritesDto("FUNC-0004", null));

        Assert.Equal(TipoErro.Proibido, colega.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, gestor.TipoDoErro);
        Assert.Single(rh.Valor!);
        Assert.Single(proprioPeloCodigo.Valor!);
    }

    [Fact]
    public async Task Listar_CodigoInvalidoOuInexistente_Da400Ou404()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var invalido = await c.ListarHolerites().ExecutarAsync(c.Solicitante(e.UsuarioRh), new FiltroListaHoleritesDto("xyz", null));
        var inexistente = await c.ListarHolerites().ExecutarAsync(c.Solicitante(e.UsuarioRh), new FiltroListaHoleritesDto("FUNC-0099", null));

        Assert.Equal(TipoErro.Validacao, invalido.TipoDoErro);
        Assert.Equal(TipoErro.NaoEncontrado, inexistente.TipoDoErro);
    }

    // ---------- Baixar ----------

    [Fact]
    public async Task Baixar_ODono_RecebeOPdf_SemAuditoria()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var holerite = c.NovoHolerite(e.Ana, 9, 2026, Pdf);

        var r = await c.BaixarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioAna), holerite.Id);

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(Pdf, r.Valor!.Conteudo);
        Assert.Equal("holerite-2026-09.pdf", r.Valor.NomeArquivo);
        Assert.Equal("application/pdf", r.Valor.TipoConteudo);
        Assert.Empty(c.Banco.Logs);
    }

    [Fact]
    public async Task Baixar_Rh_RecebeOPdf_EFicaRegistradoNaAuditoria()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var holerite = c.NovoHolerite(e.Ana, 9, 2026, Pdf);

        var r = await c.BaixarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioRh), holerite.Id);

        Assert.True(r.Sucesso, r.Erro);
        var log = Assert.Single(c.Banco.Logs);
        Assert.Equal("ConsultaHolerite", log.Acao);
        Assert.Equal(e.UsuarioRh.Id, log.IdUsuario);
        Assert.Equal(holerite.Id, log.IdRegistro);
    }

    [Fact]
    public async Task Baixar_DeOutraPessoa_Da404_ENemChegaAoDisco()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var holerite = c.NovoHolerite(e.Ana, 9, 2026, Pdf);

        var colega = await c.BaixarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioBruno), holerite.Id);
        var gestor = await c.BaixarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioGestor), holerite.Id);

        Assert.Equal(TipoErro.NaoEncontrado, colega.TipoDoErro);
        Assert.Equal(TipoErro.NaoEncontrado, gestor.TipoDoErro);
        Assert.Equal(0, c.Armazenamento.Leituras);
    }

    [Fact]
    public async Task Baixar_IdInexistente_ArquivoSumido_OuCaminhoInseguro_Da404()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var semArquivo = c.NovoHolerite(e.Ana, 8, 2026, conteudo: null);
        var inseguro = c.NovoHolerite(e.Ana, 7, 2026, Pdf, caminho: "../../etc/passwd");

        var inexistente = await c.BaixarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioAna), 12345);
        var sumido = await c.BaixarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioAna), semArquivo.Id);
        var traversal = await c.BaixarHolerite().ExecutarAsync(c.Solicitante(e.UsuarioAna), inseguro.Id);

        Assert.Equal(TipoErro.NaoEncontrado, inexistente.TipoDoErro);
        Assert.Equal(TipoErro.NaoEncontrado, sumido.TipoDoErro);
        Assert.Equal(TipoErro.NaoEncontrado, traversal.TipoDoErro);
        Assert.Equal(1, c.Armazenamento.Leituras); // o caminho inseguro nunca foi entregue ao armazenamento
    }
}
