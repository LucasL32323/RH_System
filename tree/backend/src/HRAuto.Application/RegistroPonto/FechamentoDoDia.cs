using HRAuto.Application.Abstractions;
using HRAuto.Domain.Comunicacao;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Jornada;

namespace HRAuto.Application.RegistroPonto;

/// <summary>
/// Fecha o dia quando a 4ª batida é registrada: calcula horas (RF20), lança o crédito/débito no banco de horas (RF24)
/// e avisa o gestor se as horas extras passaram do limite (RF25). Só marca as mudanças nos repositórios;
/// quem grava é o caso de uso que chamou, dentro da sua transação.
/// </summary>
public sealed class FechamentoDoDia
{
    private readonly IFuncionarioRepositorio _funcionarios;
    private readonly IFeriadoRepositorio _feriados;
    private readonly IFeriasRepositorio _ferias;
    private readonly IBancoHorasRepositorio _bancoHoras;
    private readonly INotificacaoRepositorio _notificacoes;
    private readonly JornadaPadrao _jornada;
    private readonly CalculadoraJornada _calculadora;
    private readonly IRelogio _relogio;

    public FechamentoDoDia(
        IFuncionarioRepositorio funcionarios,
        IFeriadoRepositorio feriados,
        IFeriasRepositorio ferias,
        IBancoHorasRepositorio bancoHoras,
        INotificacaoRepositorio notificacoes,
        JornadaPadrao jornada,
        CalculadoraJornada calculadora,
        IRelogio relogio)
    {
        _funcionarios = funcionarios;
        _feriados = feriados;
        _ferias = ferias;
        _bancoHoras = bancoHoras;
        _notificacoes = notificacoes;
        _jornada = jornada;
        _calculadora = calculadora;
        _relogio = relogio;
    }

    /// <param name="funcionario">Dono do ponto.</param>
    /// <param name="ponto">Ponto do dia, com as quatro batidas.</param>
    public async Task<ResumoDia> ExecutarAsync(Funcionario funcionario, Ponto ponto, CancellationToken cancelamento = default)
    {
        var dia = ponto.Data;
        var resumo = await CalcularAsync(funcionario, ponto, cancelamento);

        // Idempotente: se o dia já tem lançamento de origem "Ponto", não cria outro.
        var existentes = await _bancoHoras.ListarDoDiaAsync(funcionario.Id, dia, cancelamento);
        var lancamento = RegrasBancoHoras.LancamentoDoDia(funcionario.Id, resumo, existentes);
        if (lancamento is not null) _bancoHoras.Adicionar(lancamento);

        if (funcionario.IdGestor is int idGestor && _calculadora.ExcedeLimiteHoraExtra(resumo))
        {
            _notificacoes.Adicionar(FabricaNotificacoes.HoraExtra(
                idGestor, funcionario.Id, resumo.HorasExtras, dia, _relogio.Agora().DateTime));
        }

        return resumo;
    }

    /// <summary>
    /// Refaz o fechamento de um dia cujas batidas foram corrigidas por uma manutenção de ponto aprovada (RF22, RF24).
    /// Os lançamentos antigos ficam como estão (RNF07); entra só a diferença (<see cref="RegrasBancoHoras.LancamentoDeCorrecao"/>).
    /// Não reenvia o alerta de hora extra: o gestor que aprovou a correção já está ciente.
    /// </summary>
    public async Task<ResumoDia> ReajustarAsync(Funcionario funcionario, Ponto ponto, CancellationToken cancelamento = default)
    {
        var resumo = await CalcularAsync(funcionario, ponto, cancelamento);

        var existentes = await _bancoHoras.ListarDoDiaAsync(funcionario.Id, ponto.Data, cancelamento);
        var correcao = RegrasBancoHoras.LancamentoDeCorrecao(funcionario.Id, resumo, existentes);
        if (correcao is not null) _bancoHoras.Adicionar(correcao);

        return resumo;
    }

    // Contexto do dia (feriado conforme a cidade/estado do funcionário, férias aprovadas) e cálculo das horas (RF20).
    private async Task<ResumoDia> CalcularAsync(Funcionario funcionario, Ponto ponto, CancellationToken cancelamento)
    {
        var dia = ponto.Data;

        var detalhes = await _funcionarios.ObterDetalhesAsync(funcionario.Id, cancelamento);
        var feriados = await _feriados.ListarPeriodoAsync(dia, dia, cancelamento);
        var datasDeFeriado = ResolvedorContextoDia.DatasDeFeriadoAplicaveis(feriados, detalhes?.Cidade, detalhes?.Estado);
        var ferias = await _ferias.ListarAprovadasNoPeriodoAsync(funcionario.Id, dia, dia, cancelamento);

        var contexto = ResolvedorContextoDia.Resolver(dia, _jornada, ferias, datasDeFeriado);
        return _calculadora.Calcular(ponto, dia, contexto, diaEncerrado: true);
    }
}
