using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Application.RegistroPonto;
using HRAuto.Domain.Comunicacao;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;
using HRAuto.Domain.Seguranca;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.ManutencaoDePonto;

/// <summary>
/// RF23: aprova ou rejeita um pedido de manutenção de ponto pendente, registrando responsável e data da análise.
/// Quem decide: o gestor direto do funcionário ou o RH/Admin (o RH não decide o pedido de quem tem acesso RH ou Admin,
/// salvo se for o gestor direto). Ninguém decide o próprio pedido.
/// Ao aprovar, o gestor pode informar as batidas corrigidas: elas são validadas (ordem, sem buraco, nada é apagado),
/// gravadas e auditadas com o horário antigo e o novo (RNF07); se o dia ficou com as quatro batidas, o banco de horas é
/// reajustado pela diferença. Aprovar sem batidas é um abono: só registra a decisão. O funcionário é avisado.
/// </summary>
public sealed class DecidirManutencaoPonto
{
    private readonly IManutencaoPontoRepositorio _manutencoes;
    private readonly IFuncionarioRepositorio _funcionarios;
    private readonly IFuncionarioCadastroRepositorio _cadastro;
    private readonly IUsuarioGestaoRepositorio _usuarios;
    private readonly FechamentoDoDia _fechamento;
    private readonly INotificacaoRepositorio _notificacoes;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public DecidirManutencaoPonto(
        IManutencaoPontoRepositorio manutencoes,
        IFuncionarioRepositorio funcionarios,
        IFuncionarioCadastroRepositorio cadastro,
        IUsuarioGestaoRepositorio usuarios,
        FechamentoDoDia fechamento,
        INotificacaoRepositorio notificacoes,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio)
    {
        _manutencoes = manutencoes;
        _funcionarios = funcionarios;
        _cadastro = cadastro;
        _usuarios = usuarios;
        _fechamento = fechamento;
        _notificacoes = notificacoes;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<Resultado<ManutencaoDecididaDto>> ExecutarAsync(
        Solicitante solicitante, int idManutencao, DecidirManutencaoPontoDto? dto, CancellationToken cancelamento = default)
    {
        if (dto is null || dto.Aprovar is null)
            return Resultado<ManutencaoDecididaDto>.Falha("Informe se o pedido foi aprovado ou rejeitado.");
        var aprovar = dto.Aprovar.Value;
        if (!aprovar && dto.Batidas is not null)
            return Resultado<ManutencaoDecididaDto>.Falha("Não é possível corrigir batidas ao rejeitar o pedido.");

        var pedido = await _manutencoes.ObterParaEdicaoAsync(idManutencao, cancelamento);
        if (pedido is null)
            return Resultado<ManutencaoDecididaDto>.Falha(TipoErro.NaoEncontrado, "Pedido de manutenção de ponto não encontrado.");

        if (pedido.IdFuncionario == solicitante.IdFuncionario)
            return Resultado<ManutencaoDecididaDto>.Falha(TipoErro.Proibido, "Você não pode decidir o seu próprio pedido de manutenção.");
        if (!await PodeDecidirAsync(solicitante, pedido.IdFuncionario, cancelamento))
            return Resultado<ManutencaoDecididaDto>.Falha(TipoErro.Proibido, "Você não pode decidir os pedidos deste funcionário.");
        if (pedido.Status != StatusSolicitacao.Pendente)
            return Resultado<ManutencaoDecididaDto>.Falha(TipoErro.Conflito, "Este pedido de manutenção já foi decidido.");

        var ponto = await _manutencoes.ObterPontoParaEdicaoAsync(pedido.IdPonto, cancelamento);
        var dono = await _funcionarios.ObterPorIdAsync(pedido.IdFuncionario, cancelamento);
        if (ponto is null || dono is null)
            return Resultado<ManutencaoDecididaDto>.Falha(TipoErro.NaoEncontrado, "O ponto deste pedido não foi encontrado.");

        var agora = _relogio.Agora();
        IReadOnlyList<AlteracaoBatida> alteracoes = Array.Empty<AlteracaoBatida>();

        try
        {
            // Primeiro as batidas (a correção é validada inteira antes de mexer em qualquer horário); a análise vem depois
            // e não falha mais neste ponto, então nunca sobra batida alterada com pedido ainda pendente.
            if (aprovar && dto.Batidas is { } b)
            {
                alteracoes = RegrasManutencaoPonto.AplicarCorrecao(
                    ponto, new CorrecaoBatidas(b.Entrada, b.SaidaIntervalo, b.RetornoIntervalo, b.Saida));
            }
            RegrasManutencaoPonto.Analisar(pedido, aprovar, solicitante.IdFuncionario, dto.Observacao, agora.DateTime);
        }
        catch (DominioException e)
        {
            return Resultado<ManutencaoDecididaDto>.Falha(e.Message);
        }

        // Dia com as quatro batidas depois da correção: o banco de horas acompanha (só a diferença é lançada).
        ResumoDia? fechamento = null;
        if (alteracoes.Count > 0 && ponto.TotalBatidas() == 4)
            fechamento = await _fechamento.ReajustarAsync(dono, ponto, cancelamento);

        _notificacoes.Adicionar(FabricaNotificacoes.DecisaoDeManutencao(pedido, agora.DateTime));

        var codigo = CodigoFuncionario.Formatar(pedido.IdFuncionario);
        _auditoria.Registrar(solicitante.IdUsuario, "DecidirManutencaoPonto", "MANUTENCAO_PONTO", pedido.Id,
            $"{codigo}: {pedido.Status} (ponto de {ponto.Data:yyyy-MM-dd})");
        if (alteracoes.Count > 0)
        {
            var detalhes = string.Join("; ", alteracoes.Select(a =>
                $"{Ponto.Nome(a.Tipo)} {(a.Antes is { } antes ? antes.ToString("HH\\:mm\\:ss") : "(vazia)")} -> {a.Depois:HH\\:mm\\:ss}"));
            _auditoria.Registrar(solicitante.IdUsuario, "AjustarBatida", "PONTO", ponto.Id,
                $"{codigo} em {ponto.Data:yyyy-MM-dd}: {detalhes}");
        }

        try
        {
            await _unidade.SalvarAsync(cancelamento);
        }
        catch (ConflitoDePersistenciaException)
        {
            return Resultado<ManutencaoDecididaDto>.Falha(
                TipoErro.Conflito, "O ponto foi alterado ao mesmo tempo. Atualize a tela e tente novamente.");
        }

        var nomes = await _cadastro.ObterNomesAsync(new[] { pedido.IdFuncionario }, cancelamento);
        return Resultado<ManutencaoDecididaDto>.Ok(new ManutencaoDecididaDto(
            MapeadorManutencao.Pedido(pedido, ponto, nomes),
            alteracoes.Count > 0,
            alteracoes.Select(MapeadorManutencao.Alteracao).ToList(),
            fechamento is null ? null : ResumoDiaDto.De(fechamento)));
    }

    // Mesma hierarquia das férias: gestor direto sempre pode; RH/Admin só se puder gerenciar o perfil do dono.
    private async Task<bool> PodeDecidirAsync(Solicitante solicitante, int idDono, CancellationToken cancelamento)
    {
        if (await _cadastro.EhSubordinadoDiretoAsync(solicitante.IdFuncionario, idDono, cancelamento)) return true;
        if (!solicitante.Perfil.EhRhOuAdmin()) return false;

        var usuarioDono = await _usuarios.ObterPorFuncionarioAsync(idDono, cancelamento);
        return usuarioDono is null || RegrasAcesso.PodeGerenciar(solicitante.Perfil, (PerfilAcesso)usuarioDono.IdPerfil);
    }
}
