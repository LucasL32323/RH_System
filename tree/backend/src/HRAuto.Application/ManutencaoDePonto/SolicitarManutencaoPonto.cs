using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Comunicacao;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Jornada;

namespace HRAuto.Application.ManutencaoDePonto;

/// <summary>
/// RF22: o colaborador pede a manutenção do ponto de um dia (ajuste de batida ou abono) com justificativa obrigatória.
/// O pedido nasce Pendente e o gestor é avisado; sem gestor ativo, o aviso vai para o RH. Se o dia não tem nenhuma batida,
/// o registro de ponto vazio é criado antes (MANUTENCAO_PONTO.id_ponto é FK de PONTO), na mesma transação do pedido.
/// </summary>
public sealed class SolicitarManutencaoPonto
{
    private readonly IFuncionarioRepositorio _funcionarios;
    private readonly IPontoRepositorio _pontos;
    private readonly IManutencaoPontoRepositorio _manutencoes;
    private readonly IUsuarioGestaoRepositorio _usuarios;
    private readonly INotificacaoRepositorio _notificacoes;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public SolicitarManutencaoPonto(
        IFuncionarioRepositorio funcionarios,
        IPontoRepositorio pontos,
        IManutencaoPontoRepositorio manutencoes,
        IUsuarioGestaoRepositorio usuarios,
        INotificacaoRepositorio notificacoes,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio)
    {
        _funcionarios = funcionarios;
        _pontos = pontos;
        _manutencoes = manutencoes;
        _usuarios = usuarios;
        _notificacoes = notificacoes;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<Resultado<ManutencaoPontoDto>> ExecutarAsync(
        Solicitante solicitante, SolicitarManutencaoPontoDto? dto, CancellationToken cancelamento = default)
    {
        if (dto is null || dto.Data is null)
            return Resultado<ManutencaoPontoDto>.Falha("Informe o dia do ponto que precisa de manutenção.");

        var funcionario = await _funcionarios.ObterPorIdAsync(solicitante.IdFuncionario, cancelamento);
        if (funcionario is null || !funcionario.Ativo)
            return Resultado<ManutencaoPontoDto>.Falha(TipoErro.Proibido, "Funcionário inativo ou não encontrado.");

        var data = dto.Data.Value;
        var agora = _relogio.Agora();
        var hoje = _relogio.Hoje();

        // Tudo que dá para conferir sem gravar nada é conferido antes de abrir a transação.
        string justificativa;
        try
        {
            justificativa = RegrasManutencaoPonto.NormalizarJustificativa(dto.Justificativa);
            RegrasManutencaoPonto.ValidarDiaDoPedido(data, funcionario.DataAdmissao, hoje);
        }
        catch (DominioException e)
        {
            return Resultado<ManutencaoPontoDto>.Falha(e.Message);
        }

        var ponto = await _pontos.ObterDoDiaAsync(funcionario.Id, data, cancelamento);
        var existentes = ponto is null
            ? Array.Empty<ManutencaoPonto>()
            : await _manutencoes.ListarDoPontoAsync(ponto.Id, cancelamento);
        var destinatarios = await DestinatariosDeAviso.ObterAsync(funcionario, _funcionarios, _usuarios, cancelamento);

        ManutencaoPonto pedido;
        try
        {
            await using var transacao = await _unidade.IniciarTransacaoAsync(cancelamento);

            if (ponto is null)
            {
                // Dia sem nenhuma batida: cria o ponto vazio e grava para obter o id (a FK exige).
                ponto = Ponto.Criar(funcionario.Id, data);
                _pontos.Adicionar(ponto);
                await _unidade.SalvarAsync(cancelamento);
            }

            try
            {
                pedido = RegrasManutencaoPonto.Solicitar(ponto, funcionario.Id, justificativa, agora.DateTime, existentes);
            }
            catch (DominioException e)
            {
                return Resultado<ManutencaoPontoDto>.Falha(e.Message); // a transação é descartada (rollback)
            }

            _manutencoes.Adicionar(pedido);
            foreach (var idDestinatario in destinatarios)
                _notificacoes.Adicionar(NotificacoesJornada.NovaManutencaoDePonto(idDestinatario, pedido, data, agora.DateTime));
            await _unidade.SalvarAsync(cancelamento); // gera o id do pedido

            _auditoria.Registrar(solicitante.IdUsuario, "SolicitarManutencaoPonto", "MANUTENCAO_PONTO", pedido.Id,
                $"{funcionario.Codigo}: ponto de {data:yyyy-MM-dd}");
            await _unidade.SalvarAsync(cancelamento);

            await transacao.ConfirmarAsync(cancelamento);
        }
        catch (ConflitoDePersistenciaException)
        {
            return Resultado<ManutencaoPontoDto>.Falha(
                TipoErro.Conflito, "Outra operação alterou o ponto deste dia ao mesmo tempo. Tente novamente.");
        }

        var nomes = new Dictionary<int, string> { [funcionario.Id] = funcionario.Nome };
        return Resultado<ManutencaoPontoDto>.Ok(MapeadorManutencao.Pedido(pedido, ponto, nomes));
    }
}
