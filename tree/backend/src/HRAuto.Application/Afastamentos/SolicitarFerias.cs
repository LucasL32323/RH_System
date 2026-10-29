using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Afastamentos;
using HRAuto.Domain.Comunicacao;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Jornada;

namespace HRAuto.Application.Afastamentos;

/// <summary>
/// RF26: o funcionário solicita as próprias férias (CLT: 12 meses de casa, até 3 frações, uma de 14+ dias, início fora de
/// domingo/feriado/2 dias antes). A solicitação nasce Pendente e o gestor é avisado; se o funcionário não tem gestor
/// ativo, o aviso vai para o RH.
/// </summary>
public sealed class SolicitarFerias
{
    public const int TamanhoMaximoObservacao = 500;

    private readonly IFuncionarioRepositorio _funcionarios;
    private readonly IFeriadoRepositorio _feriados;
    private readonly IFeriasRepositorio _ferias;
    private readonly IUsuarioGestaoRepositorio _usuarios;
    private readonly INotificacaoRepositorio _notificacoes;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public SolicitarFerias(
        IFuncionarioRepositorio funcionarios,
        IFeriadoRepositorio feriados,
        IFeriasRepositorio ferias,
        IUsuarioGestaoRepositorio usuarios,
        INotificacaoRepositorio notificacoes,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio)
    {
        _funcionarios = funcionarios;
        _feriados = feriados;
        _ferias = ferias;
        _usuarios = usuarios;
        _notificacoes = notificacoes;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<Resultado<FeriasDto>> ExecutarAsync(
        Solicitante solicitante, SolicitarFeriasDto? dto, CancellationToken cancelamento = default)
    {
        if (dto is null || dto.Inicio is null || dto.Fim is null)
            return Resultado<FeriasDto>.Falha("Informe a data de início e a data final das férias.");
        if (dto.Observacao is { Length: > TamanhoMaximoObservacao })
            return Resultado<FeriasDto>.Falha($"A observação deve ter no máximo {TamanhoMaximoObservacao} caracteres.");

        var inicio = dto.Inicio.Value;
        var fim = dto.Fim.Value;

        var funcionario = await _funcionarios.ObterPorIdAsync(solicitante.IdFuncionario, cancelamento);
        if (funcionario is null)
            return Resultado<FeriasDto>.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado.");

        var agora = _relogio.Agora();
        var hoje = _relogio.Hoje();

        // Só o início e os 2 dias seguintes precisam ser conferidos contra os feriados (CLT, art. 134 §3º).
        var detalhes = await _funcionarios.ObterDetalhesAsync(funcionario.Id, cancelamento);
        var feriados = await _feriados.ListarPeriodoAsync(inicio, inicio.AddDays(2), cancelamento);
        var datasDeFeriado = ResolvedorContextoDia.DatasDeFeriadoAplicaveis(feriados, detalhes?.Cidade, detalhes?.Estado);
        var existentes = await _ferias.ListarAtivasDoFuncionarioAsync(funcionario.Id, cancelamento);

        var dias = dto.Dias ?? (fim.DayNumber - inicio.DayNumber + 1);

        Ferias solicitacao;
        try
        {
            solicitacao = RegrasFerias.Solicitar(
                new DadosFuncionario(funcionario.Id, funcionario.DataAdmissao, funcionario.Ativo),
                inicio, fim, dias, dto.Observacao, hoje, existentes, datasDeFeriado);
        }
        catch (DominioException e)
        {
            return Resultado<FeriasDto>.Falha(e.Message);
        }

        _ferias.Adicionar(solicitacao);
        foreach (var idDestinatario in await DestinatariosDoAvisoAsync(funcionario, cancelamento))
            _notificacoes.Adicionar(NotificacoesJornada.NovaSolicitacaoDeFerias(idDestinatario, solicitacao, agora.DateTime));

        await using var transacao = await _unidade.IniciarTransacaoAsync(cancelamento);
        await _unidade.SalvarAsync(cancelamento); // gera o id da solicitação
        _auditoria.Registrar(solicitante.IdUsuario, "SolicitarFerias", "FERIAS", solicitacao.Id,
            $"{funcionario.Codigo}: {inicio:yyyy-MM-dd} a {fim:yyyy-MM-dd} ({dias} dias)");
        await _unidade.SalvarAsync(cancelamento);
        await transacao.ConfirmarAsync(cancelamento);

        var nomes = new Dictionary<int, string> { [funcionario.Id] = funcionario.Nome };
        return Resultado<FeriasDto>.Ok(MapeadorFerias.Ferias(solicitacao, nomes));
    }

    // Gestor ativo; sem gestor (ou gestor desligado), todos os RH ativos. Ninguém é avisado da própria solicitação.
    private async Task<IReadOnlyList<int>> DestinatariosDoAvisoAsync(Funcionario funcionario, CancellationToken cancelamento)
    {
        if (funcionario.IdGestor is { } idGestor)
        {
            var gestor = await _funcionarios.ObterPorIdAsync(idGestor, cancelamento);
            if (gestor is { Ativo: true }) return new[] { idGestor };
        }

        return (await _usuarios.ListarIdsFuncionariosRhAtivosAsync(cancelamento))
            .Where(id => id != funcionario.Id)
            .Distinct()
            .ToList();
    }
}
