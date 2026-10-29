using HRAuto.Api.Seguranca;
using HRAuto.Application.Common;
using HRAuto.Application.Holerites;
using HRAuto.Domain.Holerites;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Controllers;

/// <summary>Formulário multipart da publicação do holerite (RF29).</summary>
public sealed class PublicarHoleriteForm
{
    /// <summary>Código do funcionário (FUNC-0001).</summary>
    public string? Codigo { get; set; }
    public int? Mes { get; set; }
    public int? Ano { get; set; }
    /// <summary>Verdadeiro para trocar o PDF de uma competência já publicada.</summary>
    public bool Substituir { get; set; }
    /// <summary>PDF de até 5 MB.</summary>
    public IFormFile? Arquivo { get; set; }
}

/// <summary>
/// Holerites (RF29, RF30, RNF17). O RH/Admin publica o PDF; cada funcionário lista e baixa os próprios. Os arquivos ficam em
/// uma pasta do servidor e só saem por aqui, com login (RNF08). O caminho do arquivo nunca aparece nas respostas.
/// </summary>
[Route("api/v1/holerites")]
public class HoleritesController : ApiControllerBase
{
    // 5 MB do PDF + folga para os campos do formulário e o cabeçalho multipart.
    private const long LimiteDaRequisicao = RegrasHolerite.TamanhoMaximoBytes + 64 * 1024;

    /// <summary>Holerites de quem está logado. RH/Admin podem informar <c>codigo</c> para ver os de outro funcionário.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HoleriteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Listar(
        [FromQuery] string? codigo, [FromQuery] int? ano, [FromServices] ListarHolerites caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, new FiltroListaHoleritesDto(codigo, ano), ct));
    }

    /// <summary>Baixa o PDF do holerite (o dono ou o RH/Admin).</summary>
    [HttpGet("{id:int}/arquivo")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Baixar(int id, [FromServices] BaixarHolerite caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();

        var resultado = await caso.ExecutarAsync(solicitante, id, ct);
        if (!resultado.Sucesso) return Falha(resultado.TipoDoErro, resultado.Erro);

        // Documento pessoal: o navegador e os proxies não devem guardar cópia.
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(resultado.Valor!.Conteudo, resultado.Valor.TipoConteudo, resultado.Valor.NomeArquivo);
    }

    /// <summary>Publica (ou substitui) o PDF do holerite de um funcionário/mês/ano. Somente RH/Admin.</summary>
    [HttpPost]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimiteDaRequisicao)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteDaRequisicao)]
    [ProducesResponseType(typeof(HoleritePublicadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(HoleritePublicadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Publicar([FromForm] PublicarHoleriteForm form, [FromServices] PublicarHolerite caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();

        if (form.Arquivo is null || form.Arquivo.Length == 0)
            return Falha(TipoErro.Validacao, "Envie o arquivo PDF do holerite.");
        if (form.Arquivo.Length > RegrasHolerite.TamanhoMaximoBytes)
            return Falha(TipoErro.Validacao, "O arquivo do holerite deve ter no máximo 5 MB.");

        // O tamanho já foi conferido; o buffer tem exatamente o tamanho do arquivo.
        var conteudo = new byte[form.Arquivo.Length];
        await using (var leitura = form.Arquivo.OpenReadStream())
            await leitura.ReadExactlyAsync(conteudo, ct);

        var resultado = await caso.ExecutarAsync(
            solicitante,
            new PublicarHoleriteDto(form.Codigo, form.Mes, form.Ano, form.Arquivo.FileName, conteudo, form.Substituir),
            ct);
        if (!resultado.Sucesso) return Falha(resultado.TipoDoErro, resultado.Erro);

        return StatusCode(resultado.Valor!.Substituido ? StatusCodes.Status200OK : StatusCodes.Status201Created, resultado.Valor);
    }
}
