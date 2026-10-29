using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.RegistroPonto;

/// <summary>
/// Registrar ponto (RF17, RF18): grava a próxima batida do dia com a hora do SERVIDOR (o app não envia horário).
/// A sequência entrada -> saída p/ intervalo -> retorno -> saída é imposta pelo Domain (<see cref="Ponto.RegistrarBatida"/>).
/// Na 4ª batida o dia é fechado: banco de horas (RF24) e alerta de hora extra ao gestor (RF25).
/// </summary>
public sealed class RegistrarBatida
{
    private readonly IFuncionarioRepositorio _funcionarios;
    private readonly IPontoRepositorio _pontos;
    private readonly FechamentoDoDia _fechamento;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public RegistrarBatida(
        IFuncionarioRepositorio funcionarios,
        IPontoRepositorio pontos,
        FechamentoDoDia fechamento,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio)
    {
        _funcionarios = funcionarios;
        _pontos = pontos;
        _fechamento = fechamento;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<Resultado<BatidaRegistradaDto>> ExecutarAsync(Solicitante solicitante, CancellationToken cancelamento = default)
    {
        var funcionario = await _funcionarios.ObterPorIdAsync(solicitante.IdFuncionario, cancelamento);
        if (funcionario is null || !funcionario.Ativo)
            return Resultado<BatidaRegistradaDto>.Falha(TipoErro.Proibido, "Funcionário inativo ou não encontrado.");

        var agora = _relogio.Agora();
        var hoje = PontoMapeador.Dia(agora);
        var hora = PontoMapeador.Hora(agora);

        var ponto = await _pontos.ObterDoDiaAsync(funcionario.Id, hoje, cancelamento);
        var ehNovo = ponto is null;
        ponto ??= Ponto.Criar(funcionario.Id, hoje);

        TipoBatida tipo;
        try
        {
            tipo = ponto.RegistrarBatida(hora);
        }
        catch (DominioException e)
        {
            return Resultado<BatidaRegistradaDto>.Falha(e.Message); // fora de ordem, repetida ou 5ª batida
        }

        if (ehNovo) _pontos.Adicionar(ponto);

        ResumoDia? fechamento = null;
        try
        {
            // Duas gravações na mesma transação: a primeira gera o id do ponto, a segunda grava o log que o referencia.
            await using var transacao = await _unidade.IniciarTransacaoAsync(cancelamento);

            if (ponto.TotalBatidas() == 4)
                fechamento = await _fechamento.ExecutarAsync(funcionario, ponto, cancelamento);
            await _unidade.SalvarAsync(cancelamento);

            _auditoria.Registrar(
                solicitante.IdUsuario, "RegistrarPonto", "PONTO", ponto.Id,
                $"{CodigoFuncionario.Formatar(funcionario.Id)}: {Ponto.Nome(tipo)} às {hora:HH\\:mm\\:ss} de {hoje:dd/MM/yyyy}");
            await _unidade.SalvarAsync(cancelamento);

            await transacao.ConfirmarAsync(cancelamento);
        }
        catch (ConflitoDePersistenciaException)
        {
            return Resultado<BatidaRegistradaDto>.Falha(
                TipoErro.Conflito, "Outra batida foi registrada ao mesmo tempo. Atualize a tela e confira o ponto de hoje.");
        }

        return Resultado<BatidaRegistradaDto>.Ok(new BatidaRegistradaDto(
            tipo,
            Ponto.Nome(tipo),
            hoje,
            hora,
            ponto.ProximaBatida(),
            fechamento is null ? null : ResumoDiaDto.De(fechamento)));
    }
}

/// <summary>Estado do ponto de hoje (RF17, RNF10: bater o ponto com 1 toque depois do login).</summary>
public sealed class ConsultarStatusPontoHoje
{
    private readonly IPontoRepositorio _pontos;
    private readonly IRelogio _relogio;

    public ConsultarStatusPontoHoje(IPontoRepositorio pontos, IRelogio relogio)
    {
        _pontos = pontos;
        _relogio = relogio;
    }

    public async Task<Resultado<StatusPontoHojeDto>> ExecutarAsync(Solicitante solicitante, CancellationToken cancelamento = default)
    {
        var agora = _relogio.Agora();
        var hoje = PontoMapeador.Dia(agora);
        var ponto = await _pontos.ObterDoDiaAsync(solicitante.IdFuncionario, hoje, cancelamento);

        var proxima = ponto is null ? TipoBatida.Entrada : ponto.ProximaBatida();
        return Resultado<StatusPontoHojeDto>.Ok(new StatusPontoHojeDto(
            hoje,
            agora,
            PontoMapeador.Batidas(ponto),
            proxima,
            proxima is null ? null : Ponto.Nome(proxima.Value)));
    }
}
