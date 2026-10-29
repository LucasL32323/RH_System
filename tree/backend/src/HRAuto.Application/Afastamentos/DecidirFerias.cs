using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Afastamentos;
using HRAuto.Domain.Comunicacao;
using HRAuto.Domain.Common;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Seguranca;

namespace HRAuto.Application.Afastamentos;

/// <summary>
/// RF27: aprova ou rejeita uma solicitação de férias pendente. Quem decide: o gestor direto do funcionário ou o RH/Admin
/// (o RH não decide as férias de quem tem acesso RH ou Admin, salvo se for o gestor direto). Ninguém decide a própria
/// solicitação. Como a tabela FERIAS não guarda o decisor, ele fica no texto da observação e no log de auditoria.
/// O funcionário é avisado da decisão.
/// </summary>
public sealed class DecidirFerias
{
    private readonly IFeriasRepositorio _ferias;
    private readonly IFuncionarioCadastroRepositorio _funcionarios;
    private readonly IUsuarioGestaoRepositorio _usuarios;
    private readonly INotificacaoRepositorio _notificacoes;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public DecidirFerias(
        IFeriasRepositorio ferias,
        IFuncionarioCadastroRepositorio funcionarios,
        IUsuarioGestaoRepositorio usuarios,
        INotificacaoRepositorio notificacoes,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio)
    {
        _ferias = ferias;
        _funcionarios = funcionarios;
        _usuarios = usuarios;
        _notificacoes = notificacoes;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<Resultado<FeriasDto>> ExecutarAsync(
        Solicitante solicitante, int idFerias, DecidirFeriasDto? dto, CancellationToken cancelamento = default)
    {
        if (dto is null || dto.Aprovar is null)
            return Resultado<FeriasDto>.Falha("Informe se a solicitação foi aprovada ou rejeitada.");
        if (dto.Observacao is { Length: > SolicitarFerias.TamanhoMaximoObservacao })
            return Resultado<FeriasDto>.Falha($"A observação deve ter no máximo {SolicitarFerias.TamanhoMaximoObservacao} caracteres.");

        var ferias = await _ferias.ObterParaEdicaoAsync(idFerias, cancelamento);
        if (ferias is null)
            return Resultado<FeriasDto>.Falha(TipoErro.NaoEncontrado, "Solicitação de férias não encontrada.");

        if (ferias.IdFuncionario == solicitante.IdFuncionario)
            return Resultado<FeriasDto>.Falha(TipoErro.Proibido, "Você não pode decidir a sua própria solicitação de férias.");

        if (!await PodeDecidirAsync(solicitante, ferias.IdFuncionario, cancelamento))
            return Resultado<FeriasDto>.Falha(TipoErro.Proibido, "Você não pode decidir as férias deste funcionário.");

        if (ferias.Status != StatusSolicitacao.Pendente)
            return Resultado<FeriasDto>.Falha(TipoErro.Conflito, "Esta solicitação de férias já foi decidida.");

        var agora = _relogio.Agora();
        try
        {
            RegrasFerias.Decidir(ferias, dto.Aprovar.Value, solicitante.IdFuncionario, dto.Observacao);
        }
        catch (DominioException e)
        {
            return Resultado<FeriasDto>.Falha(e.Message);
        }

        _notificacoes.Adicionar(FabricaNotificacoes.DecisaoDeFerias(ferias, agora.DateTime));
        _auditoria.Registrar(solicitante.IdUsuario, "DecidirFerias", "FERIAS", ferias.Id,
            $"{HRAuto.Domain.ValueObjects.CodigoFuncionario.Formatar(ferias.IdFuncionario)}: {ferias.Status}");
        await _unidade.SalvarAsync(cancelamento);

        var nomes = await _funcionarios.ObterNomesAsync(new[] { ferias.IdFuncionario }, cancelamento);
        return Resultado<FeriasDto>.Ok(MapeadorFerias.Ferias(ferias, nomes));
    }

    private async Task<bool> PodeDecidirAsync(Solicitante solicitante, int idDono, CancellationToken cancelamento)
    {
        if (await _funcionarios.EhSubordinadoDiretoAsync(solicitante.IdFuncionario, idDono, cancelamento)) return true;
        if (!solicitante.Perfil.EhRhOuAdmin()) return false;

        var usuarioDono = await _usuarios.ObterPorFuncionarioAsync(idDono, cancelamento);
        return usuarioDono is null || RegrasAcesso.PodeGerenciar(solicitante.Perfil, (PerfilAcesso)usuarioDono.IdPerfil);
    }
}

/// <summary>
/// O funcionário desiste de uma solicitação ainda Pendente (a linha é removida; fica o registro na auditoria).
/// Solicitação já decidida não pode ser cancelada por aqui: é assunto para o RH.
/// </summary>
public sealed class CancelarSolicitacaoFerias
{
    private readonly IFeriasRepositorio _ferias;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;

    public CancelarSolicitacaoFerias(IFeriasRepositorio ferias, IAuditoria auditoria, IUnidadeDeTrabalho unidade)
    {
        _ferias = ferias;
        _auditoria = auditoria;
        _unidade = unidade;
    }

    public async Task<Resultado> ExecutarAsync(Solicitante solicitante, int idFerias, CancellationToken cancelamento = default)
    {
        var ferias = await _ferias.ObterParaEdicaoAsync(idFerias, cancelamento);
        if (ferias is null)
            return Resultado.Falha(TipoErro.NaoEncontrado, "Solicitação de férias não encontrada.");
        if (ferias.IdFuncionario != solicitante.IdFuncionario)
            return Resultado.Falha(TipoErro.Proibido, "Você só pode cancelar as suas próprias solicitações.");
        if (ferias.Status != StatusSolicitacao.Pendente)
            return Resultado.Falha(TipoErro.Conflito, "Só é possível cancelar solicitações pendentes. Procure o RH.");

        _ferias.Remover(ferias);
        _auditoria.Registrar(solicitante.IdUsuario, "CancelarSolicitacaoFerias", "FERIAS", ferias.Id,
            $"{ferias.DataInicio:yyyy-MM-dd} a {ferias.DataFim:yyyy-MM-dd}");
        await _unidade.SalvarAsync(cancelamento);

        return Resultado.Ok();
    }
}
