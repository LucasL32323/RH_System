using HRAuto.Api.Seguranca;
using HRAuto.Application.Cadastro;
using HRAuto.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRAuto.Api.Controllers;

/// <summary>
/// Funcionários (RF09 a RF16) e acessos (RF06, RF07). Quem pode o quê é decidido na Application, pelo perfil do token:
/// cadastro, edição, desligamento e acessos são do RH/Admin; a lista é do RH/Admin e dos gestores (só a equipe);
/// a ficha é do próprio funcionário, do RH/Admin e do gestor direto. As rotas usam o código (FUNC-0001), nunca o id interno.
/// </summary>
[Route("api/v1/funcionarios")]
public class FuncionariosController : ApiControllerBase
{
    private const int LimiteCorpo = 16 * 1024;

    /// <summary>Lista paginada. Busca pelo código (FUNC-0001). Gestor vê só a equipe; RH/Admin veem todos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaDto<FuncionarioResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Listar(
        [FromQuery] string? codigo, [FromQuery] int? idCargo, [FromQuery] int? idDepartamento, [FromQuery] bool? ativo,
        [FromQuery] int? pagina, [FromQuery] int? tamanho,
        [FromServices] ListarFuncionarios caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        var filtro = new FiltroListaFuncionariosDto(codigo, idCargo, idDepartamento, ativo, pagina, tamanho);
        return Responder(await caso.ExecutarAsync(solicitante, filtro, ct));
    }

    /// <summary>Cadastra o funcionário (CPF, RG, residência) e, por padrão, cria o acesso com convite por e-mail.</summary>
    [HttpPost]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [RequestSizeLimit(LimiteCorpo)]
    [ProducesResponseType(typeof(FuncionarioCadastradoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Cadastrar(
        [FromBody] CadastrarFuncionarioDto dto, [FromServices] CadastrarFuncionario caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, dto, ct), StatusCodes.Status201Created);
    }

    /// <summary>Ficha do funcionário. Documentos e endereço só para o próprio e para RH/Admin.</summary>
    [HttpGet("{codigo}")]
    [ProducesResponseType(typeof(FuncionarioDetalheDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Obter(string codigo, [FromServices] ConsultarFuncionario caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, codigo, ct));
    }

    /// <summary>Atualização completa dos dados do funcionário.</summary>
    [HttpPut("{codigo}")]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [RequestSizeLimit(LimiteCorpo)]
    [ProducesResponseType(typeof(FuncionarioResumoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Atualizar(
        string codigo, [FromBody] AtualizarFuncionarioDto dto, [FromServices] AtualizarFuncionario caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExecutarAsync(solicitante, codigo, dto, ct));
    }

    /// <summary>Desliga o funcionário (data opcional, padrão hoje) e bloqueia o acesso. Nada é apagado.</summary>
    [HttpPost("{codigo}/desligamento")]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [RequestSizeLimit(LimiteCorpo)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Desligar(
        string codigo, [FromBody] DesligarFuncionarioDto? dto, [FromServices] AlterarSituacaoFuncionario caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.DesligarAsync(solicitante, codigo, dto, ct));
    }

    /// <summary>Reativa o funcionário. O acesso (login) continua desativado até o RH reativá-lo.</summary>
    [HttpPost("{codigo}/reativacao")]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Reativar(string codigo, [FromServices] AlterarSituacaoFuncionario caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ReativarAsync(solicitante, codigo, ct));
    }

    // ---- Acesso (usuário) ----

    [HttpGet("{codigo}/acesso")]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [ProducesResponseType(typeof(AcessoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ConsultarAcesso(string codigo, [FromServices] GerenciarAcessoUsuario caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ConsultarAsync(solicitante, codigo, ct));
    }

    /// <summary>Cria o login (e-mail do cadastro) e envia o convite para o funcionário definir a senha.</summary>
    [HttpPost("{codigo}/acesso")]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [RequestSizeLimit(LimiteCorpo)]
    [ProducesResponseType(typeof(AcessoCriadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CriarAcesso(
        string codigo, [FromBody] CriarAcessoDto? dto, [FromServices] GerenciarAcessoUsuario caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.CriarAsync(solicitante, codigo, dto, ct), StatusCodes.Status201Created);
    }

    /// <summary>Troca o perfil e/ou ativa e desativa o login. O novo perfil vale a partir do próximo login.</summary>
    [HttpPatch("{codigo}/acesso")]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [RequestSizeLimit(LimiteCorpo)]
    [ProducesResponseType(typeof(AcessoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> AlterarAcesso(
        string codigo, [FromBody] AlterarAcessoDto dto, [FromServices] GerenciarAcessoUsuario caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.AlterarAsync(solicitante, codigo, dto, ct));
    }

    /// <summary>Reenvia o link de definição de senha (válido por 30 minutos).</summary>
    [HttpPost("{codigo}/acesso/convite")]
    [Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> ReenviarConvite(string codigo, [FromServices] GerenciarAcessoUsuario caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ReenviarConviteAsync(solicitante, codigo, ct));
    }
}

/// <summary>Cargos (RF14). Só RH e Admin.</summary>
[Route("api/v1/cargos")]
[Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
public class CargosController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EstruturaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> Listar([FromServices] GerenciarCargos caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ListarAsync(solicitante, ct));
    }

    [HttpPost]
    [RequestSizeLimit(4 * 1024)]
    [ProducesResponseType(typeof(EstruturaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Criar([FromBody] SalvarEstruturaDto dto, [FromServices] GerenciarCargos caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.CriarAsync(solicitante, dto, ct), StatusCodes.Status201Created);
    }

    [HttpPut("{id:int}")]
    [RequestSizeLimit(4 * 1024)]
    [ProducesResponseType(typeof(EstruturaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Atualizar(int id, [FromBody] SalvarEstruturaDto dto, [FromServices] GerenciarCargos caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.AtualizarAsync(solicitante, id, dto, ct));
    }

    /// <summary>Exclui o cargo se nenhum funcionário o usar.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Excluir(int id, [FromServices] GerenciarCargos caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExcluirAsync(solicitante, id, ct));
    }
}

/// <summary>Departamentos (RF15). Só RH e Admin.</summary>
[Route("api/v1/departamentos")]
[Authorize(Policy = Papeis.PoliticaRhOuAdmin)]
public class DepartamentosController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EstruturaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> Listar([FromServices] GerenciarDepartamentos caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ListarAsync(solicitante, ct));
    }

    [HttpPost]
    [RequestSizeLimit(4 * 1024)]
    [ProducesResponseType(typeof(EstruturaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Criar([FromBody] SalvarEstruturaDto dto, [FromServices] GerenciarDepartamentos caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.CriarAsync(solicitante, dto, ct), StatusCodes.Status201Created);
    }

    [HttpPut("{id:int}")]
    [RequestSizeLimit(4 * 1024)]
    [ProducesResponseType(typeof(EstruturaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Atualizar(int id, [FromBody] SalvarEstruturaDto dto, [FromServices] GerenciarDepartamentos caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.AtualizarAsync(solicitante, id, dto, ct));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Excluir(int id, [FromServices] GerenciarDepartamentos caso, CancellationToken ct)
    {
        if (!TentarObterSolicitante(out var solicitante)) return SessaoInvalida();
        return Responder(await caso.ExcluirAsync(solicitante, id, ct));
    }
}
