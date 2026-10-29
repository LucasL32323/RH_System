using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Enums;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.ManutencaoDePonto;

/// <summary>
/// RF22, RF23: lista de pedidos de manutenção de ponto. "minhas" (padrão) é de qualquer pessoa; "equipe" mostra os
/// subordinados diretos e só vale para quem é gestor; "todas" é do RH/Admin. Do mais recente para o mais antigo.
/// </summary>
public sealed class ListarManutencoesPonto
{
    private readonly IFuncionarioRepositorio _funcionarios;
    private readonly IFuncionarioCadastroRepositorio _cadastro;
    private readonly IManutencaoPontoRepositorio _manutencoes;

    public ListarManutencoesPonto(
        IFuncionarioRepositorio funcionarios, IFuncionarioCadastroRepositorio cadastro, IManutencaoPontoRepositorio manutencoes)
    {
        _funcionarios = funcionarios;
        _cadastro = cadastro;
        _manutencoes = manutencoes;
    }

    public async Task<Resultado<PaginaDto<ManutencaoPontoDto>>> ExecutarAsync(
        Solicitante solicitante, FiltroListaManutencoesDto? filtro, CancellationToken cancelamento = default)
    {
        filtro ??= new FiltroListaManutencoesDto(null, null, null, null, null);

        var escopo = (filtro.Escopo ?? "minhas").Trim().ToLowerInvariant();
        if (escopo is not ("minhas" or "equipe" or "todas"))
            return Resultado<PaginaDto<ManutencaoPontoDto>>.Falha("Escopo inválido. Use minhas, equipe ou todas.");

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
                return Resultado<PaginaDto<ManutencaoPontoDto>>.Falha("Status inválido. Use Pendente, Aprovada ou Rejeitada.");
        }

        int? idCodigo = null;
        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
        {
            if (!CodigoFuncionario.TentarConverter(filtro.Codigo, out var codigo))
                return Resultado<PaginaDto<ManutencaoPontoDto>>.Falha("Código inválido. Use o formato FUNC-0001.");
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
                    return Resultado<PaginaDto<ManutencaoPontoDto>>.Falha(
                        TipoErro.Proibido, "Use o escopo equipe ou todas para ver outro funcionário.");
                break;
            case "equipe":
                if (!await _funcionarios.EhGestorAsync(solicitante.IdFuncionario, cancelamento))
                    return Resultado<PaginaDto<ManutencaoPontoDto>>.Falha(TipoErro.Proibido, "Você não é gestor de nenhuma equipe.");
                idGestor = solicitante.IdFuncionario;
                break;
            case "todas":
                if (!solicitante.Perfil.EhRhOuAdmin())
                    return Resultado<PaginaDto<ManutencaoPontoDto>>.Falha(
                        TipoErro.Proibido, "Apenas o RH pode listar os pedidos de todos.");
                break;
        }

        var paginacao = Paginacao.De(filtro.Pagina, filtro.Tamanho);
        var pagina = await _manutencoes.ListarAsync(new FiltroManutencoes(idFuncionario, idGestor, status, paginacao), cancelamento);

        var ids = pagina.Itens.Select(i => i.Pedido.IdFuncionario).Distinct().ToList();
        var nomes = await _cadastro.ObterNomesAsync(ids, cancelamento);

        var itens = pagina.Itens.Select(i => MapeadorManutencao.Pedido(i.Pedido, i.Ponto, nomes)).ToList();
        return Resultado<PaginaDto<ManutencaoPontoDto>>.Ok(PaginaDto<ManutencaoPontoDto>.De(itens, paginacao, pagina.Total));
    }
}
