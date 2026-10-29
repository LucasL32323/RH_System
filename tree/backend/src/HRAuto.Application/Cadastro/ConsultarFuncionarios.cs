using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Enums;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Cadastro;

/// <summary>
/// RF13: lista de funcionários, buscando pelo código (FUNC-0001). RH e Admin veem todos; um gestor vê só a sua equipe
/// (quem tem ele como id_gestor); os demais não têm acesso. A lista nunca mostra CPF, RG nem endereço.
/// </summary>
public sealed class ListarFuncionarios
{
    private readonly IFuncionarioCadastroRepositorio _funcionarios;
    private readonly IFuncionarioRepositorio _consultas;

    public ListarFuncionarios(IFuncionarioCadastroRepositorio funcionarios, IFuncionarioRepositorio consultas)
    {
        _funcionarios = funcionarios;
        _consultas = consultas;
    }

    public async Task<Resultado<PaginaDto<FuncionarioResumoDto>>> ExecutarAsync(
        Solicitante solicitante, FiltroListaFuncionariosDto? filtro, CancellationToken cancelamento = default)
    {
        filtro ??= new FiltroListaFuncionariosDto(null, null, null, null, null, null);

        int? idGestor = null;
        if (!solicitante.Perfil.EhRhOuAdmin())
        {
            if (!await _consultas.EhGestorAsync(solicitante.IdFuncionario, cancelamento))
                return Resultado<PaginaDto<FuncionarioResumoDto>>.Falha(TipoErro.Proibido, "Você não tem acesso à lista de funcionários.");
            idGestor = solicitante.IdFuncionario; // gestor: só a própria equipe
        }

        int? idFuncionario = null;
        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
        {
            if (!CodigoFuncionario.TentarConverter(filtro.Codigo, out var codigo))
                return Resultado<PaginaDto<FuncionarioResumoDto>>.Falha("Código inválido. Use o formato FUNC-0001.");
            idFuncionario = codigo.Id;
        }

        var paginacao = Paginacao.De(filtro.Pagina, filtro.Tamanho);
        var pagina = await _funcionarios.ListarAsync(
            new FiltroFuncionarios(idFuncionario, filtro.IdCargo, filtro.IdDepartamento, filtro.Ativo, idGestor, paginacao),
            cancelamento);

        var itens = pagina.Itens.Select(MapeadorCadastro.Resumo).ToList();
        return Resultado<PaginaDto<FuncionarioResumoDto>>.Ok(PaginaDto<FuncionarioResumoDto>.De(itens, paginacao, pagina.Total));
    }
}

/// <summary>
/// RF10, RF11: ficha de um funcionário. O próprio funcionário e o RH/Admin veem tudo (documentos, endereço, acesso);
/// o gestor vê a ficha de quem é da sua equipe SEM documentos, endereço e nascimento; ninguém mais vê.
/// Quando o RH/Admin abre a ficha de OUTRA pessoa, a consulta dos dados pessoais é registrada na auditoria (LGPD).
/// </summary>
public sealed class ConsultarFuncionario
{
    private readonly IFuncionarioCadastroRepositorio _funcionarios;
    private readonly IFuncionarioRepositorio _consultas;
    private readonly IUsuarioGestaoRepositorio _usuarios;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;

    public ConsultarFuncionario(
        IFuncionarioCadastroRepositorio funcionarios,
        IFuncionarioRepositorio consultas,
        IUsuarioGestaoRepositorio usuarios,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade)
    {
        _funcionarios = funcionarios;
        _consultas = consultas;
        _usuarios = usuarios;
        _auditoria = auditoria;
        _unidade = unidade;
    }

    public async Task<Resultado<FuncionarioDetalheDto>> ExecutarAsync(
        Solicitante solicitante, string? codigoTexto, CancellationToken cancelamento = default)
    {
        if (!CodigoFuncionario.TentarConverter(codigoTexto, out var codigo))
            return Resultado<FuncionarioDetalheDto>.Falha("Código inválido. Use o formato FUNC-0001.");

        var ehRhOuAdmin = solicitante.Perfil.EhRhOuAdmin();
        var ehOProprio = codigo.Id == solicitante.IdFuncionario;
        var ehDaMinhaEquipe = false;

        if (!ehRhOuAdmin && !ehOProprio)
        {
            ehDaMinhaEquipe = await _funcionarios.EhSubordinadoDiretoAsync(solicitante.IdFuncionario, codigo.Id, cancelamento);
            if (!ehDaMinhaEquipe) // 403 antes de saber se existe: não revela quais códigos existem
                return Resultado<FuncionarioDetalheDto>.Falha(TipoErro.Proibido, "Você não tem acesso a este funcionário.");
        }

        var funcionario = await _funcionarios.ObterAsync(codigo.Id, cancelamento);
        if (funcionario is null)
            return Resultado<FuncionarioDetalheDto>.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado.");

        var verTudo = ehRhOuAdmin || ehOProprio;
        DocumentosDto? documentos = null;
        EnderecoDto? endereco = null;
        if (verTudo)
        {
            var detalhes = await _consultas.ObterDetalhesAsync(funcionario.Id, cancelamento);
            if (detalhes is not null)
            {
                documentos = new DocumentosDto(Cpf.Formatar(detalhes.Cpf), detalhes.Rg, detalhes.Pis, detalhes.Ctps);
                endereco = MapeadorCadastro.Endereco(detalhes);
            }
        }

        AcessoDto? acesso = null;
        if (ehRhOuAdmin)
            acesso = MapeadorCadastro.Acesso(await _usuarios.ObterPorFuncionarioAsync(funcionario.Id, cancelamento));

        if (ehRhOuAdmin && !ehOProprio)
        {
            _auditoria.Registrar(solicitante.IdUsuario, "ConsultaDadosPessoais", "FUNCIONARIO", funcionario.Id, funcionario.Codigo);
            await _unidade.SalvarAsync(cancelamento);
        }

        return Resultado<FuncionarioDetalheDto>.Ok(new FuncionarioDetalheDto(
            funcionario.Codigo, funcionario.Nome, funcionario.Email, funcionario.Telefone,
            verTudo ? funcionario.DataNascimento : null, funcionario.DataAdmissao, funcionario.DataDesligamento, funcionario.Ativo,
            funcionario.IdCargo, funcionario.Cargo?.NomeCargo ?? string.Empty,
            funcionario.IdDepartamento, funcionario.Departamento?.Nome ?? string.Empty,
            funcionario.IdGestor is { } g ? CodigoFuncionario.Formatar(g) : null, funcionario.Gestor?.Nome,
            documentos, endereco, acesso));
    }
}
