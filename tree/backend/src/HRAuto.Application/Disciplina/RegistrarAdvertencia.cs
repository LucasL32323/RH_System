using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Comunicacao;
using HRAuto.Domain.Common;
using HRAuto.Domain.Disciplina;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Seguranca;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Disciplina;

/// <summary>
/// RF33, RF34, RF47: o RH/Admin registra uma advertência (leve, suspensão ou justa causa).
/// O sistema avisa o funcionário, recalcula o semáforo e a régua e, se a régua sugerir uma consequência, avisa o RH.
/// Quem tem acesso RH/Admin só pode ser advertido por um Admin (mesma hierarquia do gerenciamento de acessos).
/// </summary>
public sealed class RegistrarAdvertencia
{
    private readonly IFuncionarioCadastroRepositorio _funcionarios;
    private readonly IUsuarioGestaoRepositorio _usuarios;
    private readonly IAdvertenciaRepositorio _advertencias;
    private readonly INotificacaoRepositorio _notificacoes;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public RegistrarAdvertencia(
        IFuncionarioCadastroRepositorio funcionarios,
        IUsuarioGestaoRepositorio usuarios,
        IAdvertenciaRepositorio advertencias,
        INotificacaoRepositorio notificacoes,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio)
    {
        _funcionarios = funcionarios;
        _usuarios = usuarios;
        _advertencias = advertencias;
        _notificacoes = notificacoes;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<Resultado<AdvertenciaRegistradaDto>> ExecutarAsync(
        Solicitante solicitante, RegistrarAdvertenciaDto? dto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<AdvertenciaRegistradaDto>.Falha(TipoErro.Proibido, "Apenas o RH pode registrar advertências.");
        if (dto is null)
            return Resultado<AdvertenciaRegistradaDto>.Falha("Envie os dados da advertência.");
        if (!CodigoFuncionario.TentarConverter(dto.CodigoFuncionario, out var codigo))
            return Resultado<AdvertenciaRegistradaDto>.Falha("Código do funcionário inválido. Use o formato FUNC-0001.");
        if (!MapeadorDisciplina.TentarLerTipo(dto.Tipo, out var tipo))
            return Resultado<AdvertenciaRegistradaDto>.Falha("Tipo inválido. Use Leve, Suspensao ou JustaCausa.");

        var funcionario = await _funcionarios.ObterAsync(codigo.Id, cancelamento);
        if (funcionario is null)
            return Resultado<AdvertenciaRegistradaDto>.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado.");
        if (!funcionario.Ativo)
            return Resultado<AdvertenciaRegistradaDto>.Falha(TipoErro.Conflito, "O funcionário está desligado.");

        var usuarioAlvo = await _usuarios.ObterPorFuncionarioAsync(funcionario.Id, cancelamento);
        if (usuarioAlvo is not null && !RegrasAcesso.PodeGerenciar(solicitante.Perfil, (PerfilAcesso)usuarioAlvo.IdPerfil))
            return Resultado<AdvertenciaRegistradaDto>.Falha(TipoErro.Proibido, "Apenas um Admin pode advertir um usuário RH ou Admin.");

        var agora = _relogio.Agora();
        var hoje = _relogio.Hoje();

        Advertencia nova;
        IReadOnlyList<Advertencia> todas;
        SugestaoConsequencia regua;
        ResultadoSemaforo semaforo;
        try
        {
            nova = RegrasAdvertencia.Registrar(
                funcionario.Id, solicitante.IdFuncionario, tipo, dto.Motivo, dto.Descricao, dto.Data ?? hoje, hoje);

            var existentes = await _advertencias.ListarDoFuncionarioAsync(funcionario.Id, cancelamento);
            todas = existentes.Append(nova).ToList();
            regua = ReguaConsequencias.Avaliar(todas);
            semaforo = SemaforoAdvertencias.Calcular(todas);
        }
        catch (DominioException e)
        {
            return Resultado<AdvertenciaRegistradaDto>.Falha(e.Message);
        }

        _advertencias.Adicionar(nova);
        _notificacoes.Adicionar(NotificacoesDisciplina.AdvertenciaRegistrada(nova, agora.DateTime));

        // Avisa o RH quando a régua sugere suspensão ou justa causa. Ao registrar a própria justa causa não há mais o que sugerir.
        if (tipo != TipoAdvertencia.JustaCausa)
        {
            var destinatarios = (await _usuarios.ListarIdsFuncionariosRhAtivosAsync(cancelamento))
                .Append(solicitante.IdFuncionario)
                .Where(id => id != funcionario.Id)
                .Distinct();
            foreach (var idRh in destinatarios)
            {
                var aviso = FabricaNotificacoes.SugestaoDaRegua(idRh, funcionario.Id, regua, agora.DateTime);
                if (aviso is not null) _notificacoes.Adicionar(aviso);
            }
        }

        await using var transacao = await _unidade.IniciarTransacaoAsync(cancelamento);
        await _unidade.SalvarAsync(cancelamento); // gera o id da advertência
        _auditoria.Registrar(
            solicitante.IdUsuario, "RegistrarAdvertencia", "ADVERTENCIA", nova.Id,
            $"{funcionario.Codigo}: {tipo} (semáforo {semaforo.Nivel})");
        await _unidade.SalvarAsync(cancelamento);
        await transacao.ConfirmarAsync(cancelamento);

        var nomes = new Dictionary<int, string> { [funcionario.Id] = funcionario.Nome };
        if (!nomes.ContainsKey(solicitante.IdFuncionario))
        {
            var responsavel = await _funcionarios.ObterNomesAsync(new[] { solicitante.IdFuncionario }, cancelamento);
            if (responsavel.TryGetValue(solicitante.IdFuncionario, out var nomeResponsavel))
                nomes[solicitante.IdFuncionario] = nomeResponsavel;
        }

        return Resultado<AdvertenciaRegistradaDto>.Ok(new AdvertenciaRegistradaDto(
            MapeadorDisciplina.Advertencia(nova, nomes), MapeadorDisciplina.Semaforo(semaforo), MapeadorDisciplina.Regua(regua)));
    }
}

/// <summary>
/// Cancela uma advertência registrada por engano ou revista: ela sai do semáforo e da régua, mas fica no histórico (RNF07).
/// Exige motivo, avisa o funcionário e vai para a auditoria. Mesma hierarquia de perfis do registro.
/// </summary>
public sealed class CancelarAdvertencia
{
    private readonly IAdvertenciaRepositorio _advertencias;
    private readonly IFuncionarioCadastroRepositorio _funcionarios;
    private readonly IUsuarioGestaoRepositorio _usuarios;
    private readonly INotificacaoRepositorio _notificacoes;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public CancelarAdvertencia(
        IAdvertenciaRepositorio advertencias,
        IFuncionarioCadastroRepositorio funcionarios,
        IUsuarioGestaoRepositorio usuarios,
        INotificacaoRepositorio notificacoes,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio)
    {
        _advertencias = advertencias;
        _funcionarios = funcionarios;
        _usuarios = usuarios;
        _notificacoes = notificacoes;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<Resultado<AdvertenciaDto>> ExecutarAsync(
        Solicitante solicitante, int idAdvertencia, CancelarAdvertenciaDto? dto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<AdvertenciaDto>.Falha(TipoErro.Proibido, "Apenas o RH pode cancelar advertências.");

        var advertencia = await _advertencias.ObterParaEdicaoAsync(idAdvertencia, cancelamento);
        if (advertencia is null)
            return Resultado<AdvertenciaDto>.Falha(TipoErro.NaoEncontrado, "Advertência não encontrada.");

        var usuarioAlvo = await _usuarios.ObterPorFuncionarioAsync(advertencia.IdFuncionario, cancelamento);
        if (usuarioAlvo is not null && !RegrasAcesso.PodeGerenciar(solicitante.Perfil, (PerfilAcesso)usuarioAlvo.IdPerfil))
            return Resultado<AdvertenciaDto>.Falha(TipoErro.Proibido, "Apenas um Admin pode cancelar a advertência de um usuário RH ou Admin.");

        try
        {
            RegrasAdvertencia.Cancelar(advertencia, dto?.Motivo);
            _notificacoes.Adicionar(NotificacoesDisciplina.AdvertenciaCancelada(advertencia, _relogio.Agora().DateTime));
        }
        catch (DominioException e)
        {
            return Resultado<AdvertenciaDto>.Falha(e.Message);
        }

        _auditoria.Registrar(
            solicitante.IdUsuario, "CancelarAdvertencia", "ADVERTENCIA", advertencia.Id,
            CodigoFuncionario.Formatar(advertencia.IdFuncionario));
        await _unidade.SalvarAsync(cancelamento);

        var nomes = await _funcionarios.ObterNomesAsync(new[] { advertencia.IdFuncionario, advertencia.IdResponsavel }, cancelamento);
        return Resultado<AdvertenciaDto>.Ok(MapeadorDisciplina.Advertencia(advertencia, nomes));
    }
}
