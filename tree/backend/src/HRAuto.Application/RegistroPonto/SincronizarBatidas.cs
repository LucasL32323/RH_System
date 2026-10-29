using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.RegistroPonto;

/// <summary>
/// Sincroniza a fila de batidas feitas sem internet (RF19, RNF09). Cada item é avaliado sozinho: os válidos entram,
/// os inválidos voltam como "Recusada" com o motivo, e reenviar uma batida já gravada devolve "JaRegistrada" sem duplicar.
/// O horário vem do aparelho (o servidor não estava acessível), por isso há limites de segurança:
/// nada de data/horário futuro (com 2 min de folga para diferença de relógio) e nada com mais de 7 dias.
/// Batida mais antiga ou diferente da já gravada exige manutenção de ponto (RF22).
/// </summary>
public sealed class SincronizarBatidas
{
    public const int MaximoBatidasPorEnvio = 100;
    public const int DiasMaximosOffline = 7;
    public static readonly TimeSpan FolgaRelogioAparelho = TimeSpan.FromMinutes(2);

    private readonly IFuncionarioRepositorio _funcionarios;
    private readonly IPontoRepositorio _pontos;
    private readonly FechamentoDoDia _fechamento;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public SincronizarBatidas(
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

    // Estado de um dia durante a sincronização.
    private sealed class DiaEmSincronizacao
    {
        public required Ponto Ponto { get; init; }
        public required bool Novo { get; init; }
        public int Aplicadas { get; set; }
    }

    public async Task<Resultado<IReadOnlyList<ResultadoBatidaOfflineDto>>> ExecutarAsync(
        Solicitante solicitante, SincronizarBatidasDto? dto, CancellationToken cancelamento = default)
    {
        if (dto?.Batidas is null || dto.Batidas.Count == 0)
            return Resultado<IReadOnlyList<ResultadoBatidaOfflineDto>>.Falha("Envie ao menos uma batida.");
        if (dto.Batidas.Count > MaximoBatidasPorEnvio)
            return Resultado<IReadOnlyList<ResultadoBatidaOfflineDto>>.Falha(
                $"Envie no máximo {MaximoBatidasPorEnvio} batidas por vez.");

        var funcionario = await _funcionarios.ObterPorIdAsync(solicitante.IdFuncionario, cancelamento);
        if (funcionario is null || !funcionario.Ativo)
            return Resultado<IReadOnlyList<ResultadoBatidaOfflineDto>>.Falha(
                TipoErro.Proibido, "Funcionário inativo ou não encontrado.");

        var agora = _relogio.Agora();
        var hoje = PontoMapeador.Dia(agora);
        var horaAgora = PontoMapeador.Hora(agora);

        var resultados = new ResultadoBatidaOfflineDto[dto.Batidas.Count];
        var dias = new Dictionary<DateOnly, DiaEmSincronizacao>();

        // Ordem cronológica e, no mesmo dia, a ordem da sequência (entrada ... saída), guardando a posição original.
        var ordem = dto.Batidas
            .Select((item, indice) => (item, indice))
            .OrderBy(x => x.item.Data ?? DateOnly.MaxValue)
            .ThenBy(x => x.item.Tipo ?? TipoBatida.Saida)
            .ThenBy(x => x.item.Horario ?? TimeOnly.MaxValue)
            .ToList();

        foreach (var (item, indice) in ordem)
        {
            resultados[indice] = await ProcessarAsync(item, funcionario, hoje, horaAgora, dias, cancelamento);
        }

        // Só grava os dias que mudaram. Pontos novos que ficaram vazios (todas as batidas recusadas) são descartados.
        var alterados = dias.Where(d => d.Value.Aplicadas > 0).OrderBy(d => d.Key).Select(d => d.Value).ToList();
        if (alterados.Count == 0)
            return Resultado<IReadOnlyList<ResultadoBatidaOfflineDto>>.Ok(resultados);

        try
        {
            await using var transacao = await _unidade.IniciarTransacaoAsync(cancelamento);

            foreach (var dia in alterados)
            {
                if (dia.Novo) _pontos.Adicionar(dia.Ponto);
                if (dia.Ponto.TotalBatidas() == 4)
                    await _fechamento.ExecutarAsync(funcionario, dia.Ponto, cancelamento);
            }
            await _unidade.SalvarAsync(cancelamento);

            foreach (var dia in alterados)
            {
                _auditoria.Registrar(
                    solicitante.IdUsuario, "SincronizarPonto", "PONTO", dia.Ponto.Id,
                    $"{CodigoFuncionario.Formatar(funcionario.Id)}: {dia.Aplicadas} batida(s) offline de {dia.Ponto.Data:dd/MM/yyyy}");
            }
            await _unidade.SalvarAsync(cancelamento);

            await transacao.ConfirmarAsync(cancelamento);
        }
        catch (ConflitoDePersistenciaException)
        {
            return Resultado<IReadOnlyList<ResultadoBatidaOfflineDto>>.Falha(
                TipoErro.Conflito, "O ponto foi alterado ao mesmo tempo. Tente sincronizar novamente.");
        }

        return Resultado<IReadOnlyList<ResultadoBatidaOfflineDto>>.Ok(resultados);
    }

    private async Task<ResultadoBatidaOfflineDto> ProcessarAsync(
        BatidaOfflineDto item,
        Funcionario funcionario,
        DateOnly hoje,
        TimeOnly horaAgora,
        Dictionary<DateOnly, DiaEmSincronizacao> dias,
        CancellationToken cancelamento)
    {
        ResultadoBatidaOfflineDto Recusar(string motivo) =>
            new(item.Data, item.Tipo, item.Horario, SituacaoItemOffline.Recusada, motivo);

        if (item.Data is null || item.Tipo is null || item.Horario is null)
            return Recusar("Informe data, tipo e horário da batida.");

        var data = item.Data.Value;
        var tipo = item.Tipo.Value;
        var horario = item.Horario.Value;

        if (!Enum.IsDefined(typeof(TipoBatida), tipo))
            return Recusar("Tipo de batida inválido.");
        if (data > hoje)
            return Recusar("A data da batida está no futuro.");
        if (data == hoje && horario.ToTimeSpan() > horaAgora.ToTimeSpan() + FolgaRelogioAparelho)
            return Recusar("O horário da batida está no futuro. Confira o relógio do aparelho.");
        if (data < hoje.AddDays(-DiasMaximosOffline))
            return Recusar($"A batida tem mais de {DiasMaximosOffline} dias. Solicite a manutenção de ponto.");
        if (data < funcionario.DataAdmissao)
            return Recusar("A data da batida é anterior à admissão.");

        if (!dias.TryGetValue(data, out var dia))
        {
            var existente = await _pontos.ObterDoDiaAsync(funcionario.Id, data, cancelamento);
            dia = new DiaEmSincronizacao
            {
                Ponto = existente ?? Ponto.Criar(funcionario.Id, data),
                Novo = existente is null
            };
            dias[data] = dia;
        }

        try
        {
            var resultado = dia.Ponto.AplicarBatidaSincronizada(tipo, horario);
            if (resultado == ResultadoSincronizacao.JaRegistrada)
                return new(data, tipo, horario, SituacaoItemOffline.JaRegistrada, null);

            dia.Aplicadas++;
            return new(data, tipo, horario, SituacaoItemOffline.Aplicada, null);
        }
        catch (DominioException e)
        {
            return Recusar(e.Message);
        }
    }
}
