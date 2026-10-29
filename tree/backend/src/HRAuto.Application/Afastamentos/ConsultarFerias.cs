using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Enums;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Afastamentos;

/// <summary>
/// RF26, RF27: lista de solicitações de férias. "minhas" (padrão) é de qualquer pessoa; "equipe" mostra os subordinados
/// diretos e só vale para quem é gestor; "todas" é do RH/Admin. Da mais recente para a mais antiga.
/// </summary>
public sealed class ListarFerias
{
    private readonly IFuncionarioRepositorio _funcionarios;
    private readonly IFuncionarioCadastroRepositorio _cadastro;
    private readonly IFeriasRepositorio _ferias;

    public ListarFerias(IFuncionarioRepositorio funcionarios, IFuncionarioCadastroRepositorio cadastro, IFeriasRepositorio ferias)
    {
        _funcionarios = funcionarios;
        _cadastro = cadastro;
        _ferias = ferias;
    }

    public async Task<Resultado<PaginaDto<FeriasDto>>> ExecutarAsync(
        Solicitante solicitante, FiltroListaFeriasDto? filtro, CancellationToken cancelamento = default)
    {
        filtro ??= new FiltroListaFeriasDto(null, null, null, null, null);

        var escopo = (filtro.Escopo ?? "minhas").Trim().ToLowerInvariant();
        if (escopo is not ("minhas" or "equipe" or "todas"))
            return Resultado<PaginaDto<FeriasDto>>.Falha("Escopo inválido. Use minhas, equipe ou todas.");

        string? status = null;
        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            status = filtro.Status.Trim().ToLowerInvariant() switch
            {
                "pendente" => StatusSolicitacao.Pendente,
                "aprovada" => StatusSolicitacao.Aprovada,
                "rejeitada" => StatusSolicitacao.Rejeitada,
                _ => null
            };
            if (status is null)
                return Resultado<PaginaDto<FeriasDto>>.Falha("Status inválido. Use Pendente, Aprovada ou Rejeitada.");
        }

        int? idCodigo = null;
        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
        {
            if (!CodigoFuncionario.TentarConverter(filtro.Codigo, out var codigo))
                return Resultado<PaginaDto<FeriasDto>>.Falha("Código inválido. Use o formato FUNC-0001.");
            idCodigo = codigo.Id;
        }

        int? idFuncionario = idCodigo;
        int? idGestor = null;
        switch (escopo)
        {
            case "minhas":
                // O código só poderia mostrar outra pessoa; nas "minhas" ele nunca amplia o acesso.
                idFuncionario = solicitante.IdFuncionario;
                if (idCodigo is { } c && c != solicitante.IdFuncionario)
                    return Resultado<PaginaDto<FeriasDto>>.Falha(TipoErro.Proibido, "Use o escopo equipe ou todas para ver outro funcionário.");
                break;
            case "equipe":
                if (!await _funcionarios.EhGestorAsync(solicitante.IdFuncionario, cancelamento))
                    return Resultado<PaginaDto<FeriasDto>>.Falha(TipoErro.Proibido, "Você não é gestor de nenhuma equipe.");
                idGestor = solicitante.IdFuncionario;
                break;
            case "todas":
                if (!solicitante.Perfil.EhRhOuAdmin())
                    return Resultado<PaginaDto<FeriasDto>>.Falha(TipoErro.Proibido, "Apenas o RH pode listar as férias de todos.");
                break;
        }

        var paginacao = Paginacao.De(filtro.Pagina, filtro.Tamanho);
        var pagina = await _ferias.ListarAsync(new FiltroFerias(idFuncionario, idGestor, status, paginacao), cancelamento);

        var ids = pagina.Itens.Select(f => f.IdFuncionario).Distinct().ToList();
        var nomes = await _cadastro.ObterNomesAsync(ids, cancelamento);

        var itens = pagina.Itens.Select(f => MapeadorFerias.Ferias(f, nomes)).ToList();
        return Resultado<PaginaDto<FeriasDto>>.Ok(PaginaDto<FeriasDto>.De(itens, paginacao, pagina.Total));
    }
}
