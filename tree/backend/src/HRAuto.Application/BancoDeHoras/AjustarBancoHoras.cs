using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Comunicacao;
using HRAuto.Domain.Common;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;
using HRAuto.Domain.Seguranca;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.BancoDeHoras;

/// <summary>
/// RF24: o RH/Admin lança um crédito ou débito manual no banco de horas, com observação obrigatória (RNF07).
/// Regras de acesso: ninguém ajusta o próprio banco, e só um Admin ajusta quem tem acesso RH ou Admin
/// (mesma hierarquia do gerenciamento de acessos). O funcionário é avisado.
/// </summary>
public sealed class AjustarBancoHoras
{
    private readonly IFuncionarioCadastroRepositorio _funcionarios;
    private readonly IUsuarioGestaoRepositorio _usuarios;
    private readonly IBancoHorasRepositorio _bancoHoras;
    private readonly INotificacaoRepositorio _notificacoes;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public AjustarBancoHoras(
        IFuncionarioCadastroRepositorio funcionarios,
        IUsuarioGestaoRepositorio usuarios,
        IBancoHorasRepositorio bancoHoras,
        INotificacaoRepositorio notificacoes,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio)
    {
        _funcionarios = funcionarios;
        _usuarios = usuarios;
        _bancoHoras = bancoHoras;
        _notificacoes = notificacoes;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<Resultado<AjusteRegistradoDto>> ExecutarAsync(
        Solicitante solicitante, AjustarBancoHorasDto? dto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<AjusteRegistradoDto>.Falha(TipoErro.Proibido, "Apenas o RH pode ajustar o banco de horas.");
        if (dto is null)
            return Resultado<AjusteRegistradoDto>.Falha("Envie os dados do ajuste.");
        if (!CodigoFuncionario.TentarConverter(dto.CodigoFuncionario, out var codigo))
            return Resultado<AjusteRegistradoDto>.Falha("Código do funcionário inválido. Use o formato FUNC-0001.");
        if (dto.Horas is null)
            return Resultado<AjusteRegistradoDto>.Falha("Informe a quantidade de horas do ajuste.");

        if (codigo.Id == solicitante.IdFuncionario)
            return Resultado<AjusteRegistradoDto>.Falha(TipoErro.Proibido, "Você não pode ajustar o seu próprio banco de horas.");

        var funcionario = await _funcionarios.ObterAsync(codigo.Id, cancelamento);
        if (funcionario is null)
            return Resultado<AjusteRegistradoDto>.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado.");
        if (!funcionario.Ativo)
            return Resultado<AjusteRegistradoDto>.Falha(TipoErro.Conflito, "O funcionário está desligado.");

        var usuarioAlvo = await _usuarios.ObterPorFuncionarioAsync(funcionario.Id, cancelamento);
        if (usuarioAlvo is not null && !RegrasAcesso.PodeGerenciar(solicitante.Perfil, (PerfilAcesso)usuarioAlvo.IdPerfil))
            return Resultado<AjusteRegistradoDto>.Falha(TipoErro.Proibido, "Apenas um Admin pode ajustar o banco de horas de um usuário RH ou Admin.");

        var agora = _relogio.Agora();
        var hoje = _relogio.Hoje();

        Domain.Entities.BancoHoras ajuste;
        decimal saldo;
        try
        {
            ajuste = RegrasBancoHoras.AjusteManual(
                funcionario.Id, dto.Data ?? hoje, NormalizarTipo(dto.Tipo), dto.Horas.Value, dto.Observacao, hoje);

            var existentes = await _bancoHoras.ListarTodosAsync(funcionario.Id, cancelamento);
            saldo = RegrasBancoHoras.Saldo(existentes.Append(ajuste));
        }
        catch (DominioException e)
        {
            return Resultado<AjusteRegistradoDto>.Falha(e.Message);
        }

        _bancoHoras.Adicionar(ajuste);
        _notificacoes.Adicionar(NotificacoesJornada.AjusteBancoHoras(ajuste, agora.DateTime));

        await using var transacao = await _unidade.IniciarTransacaoAsync(cancelamento);
        await _unidade.SalvarAsync(cancelamento); // gera o id do lançamento
        _auditoria.Registrar(
            solicitante.IdUsuario, "AjustarBancoHoras", "BANCO_HORAS", ajuste.Id,
            $"{funcionario.Codigo}: {ajuste.Tipo} de {ajuste.Quantidade}h em {ajuste.Data:yyyy-MM-dd}");
        await _unidade.SalvarAsync(cancelamento);
        await transacao.ConfirmarAsync(cancelamento);

        return Resultado<AjusteRegistradoDto>.Ok(new AjusteRegistradoDto(
            MapeadorBancoHoras.Lancamento(ajuste), saldo, MapeadorBancoHoras.FormatarSaldo(saldo)));
    }

    // Aceita "credito", "Debito"... sem diferenciar maiúsculas; qualquer outro texto segue e a regra de negócio o recusa.
    private static string NormalizarTipo(string? tipo) => tipo?.Trim().ToLowerInvariant() switch
    {
        "credito" => TiposBancoHoras.Credito,
        "debito" => TiposBancoHoras.Debito,
        _ => tipo?.Trim() ?? string.Empty
    };
}
