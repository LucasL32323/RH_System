using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Common;
using HRAuto.Domain.Disciplina;
using HRAuto.Domain.Enums;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Disciplina;

/// <summary>RF33: lista geral de advertências, com filtro por funcionário (código) e status. Somente RH e Admin.</summary>
public sealed class ConsultarAdvertencias
{
    private readonly IAdvertenciaRepositorio _advertencias;
    private readonly IFuncionarioCadastroRepositorio _funcionarios;

    public ConsultarAdvertencias(IAdvertenciaRepositorio advertencias, IFuncionarioCadastroRepositorio funcionarios)
    {
        _advertencias = advertencias;
        _funcionarios = funcionarios;
    }

    public async Task<Resultado<PaginaDto<AdvertenciaDto>>> ExecutarAsync(
        Solicitante solicitante, FiltroListaAdvertenciasDto? filtro, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<PaginaDto<AdvertenciaDto>>.Falha(TipoErro.Proibido, "Apenas o RH pode listar advertências.");
        filtro ??= new FiltroListaAdvertenciasDto(null, null, null, null);

        int? idFuncionario = null;
        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
        {
            if (!CodigoFuncionario.TentarConverter(filtro.Codigo, out var codigo))
                return Resultado<PaginaDto<AdvertenciaDto>>.Falha("Código inválido. Use o formato FUNC-0001.");
            idFuncionario = codigo.Id;
        }

        string? status = null;
        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            status = filtro.Status.Trim().ToLowerInvariant() switch
            {
                "ativa" => StatusAdvertencia.Ativa,
                "cancelada" => StatusAdvertencia.Cancelada,
                _ => null
            };
            if (status is null)
                return Resultado<PaginaDto<AdvertenciaDto>>.Falha("Status inválido. Use Ativa ou Cancelada.");
        }

        var paginacao = Paginacao.De(filtro.Pagina, filtro.Tamanho);
        var pagina = await _advertencias.ListarAsync(new FiltroAdvertencias(idFuncionario, status, paginacao), cancelamento);

        var ids = pagina.Itens.SelectMany(a => new[] { a.IdFuncionario, a.IdResponsavel }).Distinct().ToList();
        var nomes = await _funcionarios.ObterNomesAsync(ids, cancelamento);

        var itens = pagina.Itens.Select(a => MapeadorDisciplina.Advertencia(a, nomes)).ToList();
        return Resultado<PaginaDto<AdvertenciaDto>>.Ok(PaginaDto<AdvertenciaDto>.De(itens, paginacao, pagina.Total));
    }
}

/// <summary>
/// RF47: semáforo de um funcionário com o histórico. O próprio funcionário vê o seu (sem código = o meu); o gestor vê o da
/// sua equipe; RH e Admin veem de todos e, só eles, também a sugestão da régua (RF34).
/// </summary>
public sealed class ConsultarSituacaoDisciplinar
{
    private readonly IAdvertenciaRepositorio _advertencias;
    private readonly IFuncionarioCadastroRepositorio _funcionarios;

    public ConsultarSituacaoDisciplinar(IAdvertenciaRepositorio advertencias, IFuncionarioCadastroRepositorio funcionarios)
    {
        _advertencias = advertencias;
        _funcionarios = funcionarios;
    }

    public async Task<Resultado<SituacaoDisciplinarDto>> ExecutarAsync(
        Solicitante solicitante, string? codigoTexto, CancellationToken cancelamento = default)
    {
        var idAlvo = solicitante.IdFuncionario;
        if (!string.IsNullOrWhiteSpace(codigoTexto))
        {
            if (!CodigoFuncionario.TentarConverter(codigoTexto, out var codigo))
                return Resultado<SituacaoDisciplinarDto>.Falha("Código inválido. Use o formato FUNC-0001.");
            idAlvo = codigo.Id;
        }

        var ehRhOuAdmin = solicitante.Perfil.EhRhOuAdmin();
        if (!ehRhOuAdmin && idAlvo != solicitante.IdFuncionario
            && !await _funcionarios.EhSubordinadoDiretoAsync(solicitante.IdFuncionario, idAlvo, cancelamento))
            return Resultado<SituacaoDisciplinarDto>.Falha(TipoErro.Proibido, "Você não tem acesso a este funcionário.");

        var funcionario = await _funcionarios.ObterAsync(idAlvo, cancelamento);
        if (funcionario is null)
            return Resultado<SituacaoDisciplinarDto>.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado.");

        var advertencias = await _advertencias.ListarDoFuncionarioAsync(idAlvo, cancelamento);

        SemaforoDto semaforo;
        SugestaoReguaDto? regua = null;
        try
        {
            semaforo = MapeadorDisciplina.Semaforo(SemaforoAdvertencias.Calcular(advertencias));
            if (ehRhOuAdmin) regua = MapeadorDisciplina.Regua(ReguaConsequencias.Avaliar(advertencias));
        }
        catch (DominioException)
        {
            // Registro com tipo desconhecido no banco: erro de dados, não do usuário.
            return Resultado<SituacaoDisciplinarDto>.Falha(TipoErro.Conflito, "Há uma advertência com tipo inválido. Procure o administrador.");
        }

        var ids = advertencias.Select(a => a.IdResponsavel).Append(funcionario.Id).Distinct().ToList();
        var nomes = await _funcionarios.ObterNomesAsync(ids, cancelamento);

        return Resultado<SituacaoDisciplinarDto>.Ok(new SituacaoDisciplinarDto(
            funcionario.Codigo, funcionario.Nome, semaforo, regua,
            advertencias.Select(a => MapeadorDisciplina.Advertencia(a, nomes)).ToList()));
    }
}
