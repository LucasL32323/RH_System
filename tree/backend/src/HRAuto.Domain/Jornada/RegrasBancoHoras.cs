using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Domain.Jornada;

/// <summary>
/// Banco de horas (RF24): créditos e débitos diários gerados pelo ponto e ajustes manuais do RH.
/// Quantidade é sempre positiva e em horas decimais (4 casas); o sinal vem do tipo (Credito/Debito).
/// </summary>
public static class RegrasBancoHoras
{
    private const int CasasDecimais = 4;
    public const int TamanhoMinimoObservacao = 5;

    /// <summary>
    /// Gera o lançamento do fechamento do dia, ou nulo quando não há o que lançar:
    /// dia que não está completo (em andamento, inconsistente, falta, sem expediente), saldo zero,
    /// ou dia que já tem lançamento de origem "Ponto" (idempotente: rodar o fechamento duas vezes não duplica).
    /// </summary>
    public static BancoHoras? LancamentoDoDia(int idFuncionario, ResumoDia resumo, IEnumerable<BancoHoras> lancamentosExistentes)
    {
        if (idFuncionario <= 0) throw new DominioException("O funcionário do lançamento é obrigatório.");
        if (resumo.Situacao != SituacaoDia.Completo) return null;

        var jaLancado = lancamentosExistentes.Any(l =>
            l.IdFuncionario == idFuncionario && l.Data == resumo.Data && l.Origem == OrigensBancoHoras.Ponto);
        if (jaLancado) return null;

        var horas = Math.Round(resumo.Saldo.TotalHours, CasasDecimais, MidpointRounding.AwayFromZero);
        if (horas == 0) return null;

        return new BancoHoras
        {
            IdFuncionario = idFuncionario,
            Data = resumo.Data,
            Tipo = horas > 0 ? TiposBancoHoras.Credito : TiposBancoHoras.Debito,
            Quantidade = Math.Abs(horas),
            Origem = OrigensBancoHoras.Ponto,
            Observacao = $"Fechamento do ponto de {resumo.Data:dd/MM/yyyy}"
        };
    }

    /// <summary>
    /// Reajusta o banco de horas de um dia que já foi fechado quando uma manutenção de ponto aprovada muda as batidas (RF22, RF24).
    /// Não apaga nem altera lançamentos antigos (RNF07): lança só a DIFERENÇA entre o saldo novo do dia e o que os
    /// lançamentos de origem "Ponto" desse dia já somam. Se o dia não estava fechado, a diferença é o saldo inteiro.
    /// Devolve nulo quando o dia não está completo ou quando não há diferença (idempotente).
    /// </summary>
    public static BancoHoras? LancamentoDeCorrecao(int idFuncionario, ResumoDia resumo, IEnumerable<BancoHoras> lancamentosExistentes)
    {
        if (idFuncionario <= 0) throw new DominioException("O funcionário do lançamento é obrigatório.");
        if (resumo.Situacao != SituacaoDia.Completo) return null;

        var jaLancado = Saldo(lancamentosExistentes.Where(l =>
            l.IdFuncionario == idFuncionario && l.Data == resumo.Data && l.Origem == OrigensBancoHoras.Ponto));
        var alvo = Math.Round((decimal)resumo.Saldo.TotalHours, CasasDecimais, MidpointRounding.AwayFromZero);
        var diferenca = Math.Round(alvo - jaLancado, CasasDecimais, MidpointRounding.AwayFromZero);
        if (diferenca == 0) return null;

        return new BancoHoras
        {
            IdFuncionario = idFuncionario,
            Data = resumo.Data,
            Tipo = diferenca > 0 ? TiposBancoHoras.Credito : TiposBancoHoras.Debito,
            Quantidade = (double)Math.Abs(diferenca),
            Origem = OrigensBancoHoras.Ponto,
            Observacao = $"Correção do ponto de {resumo.Data:dd/MM/yyyy} (manutenção aprovada)"
        };
    }

    /// <summary>
    /// Ajuste manual do RH. Exige observação (rastreabilidade, RNF07) e não aceita data futura.
    /// </summary>
    public static BancoHoras AjusteManual(
        int idFuncionario, DateOnly data, string tipo, double horas, string? observacao, DateOnly hoje)
    {
        if (idFuncionario <= 0) throw new DominioException("O funcionário do ajuste é obrigatório.");
        if (tipo != TiposBancoHoras.Credito && tipo != TiposBancoHoras.Debito)
            throw new DominioException("O tipo do ajuste deve ser Credito ou Debito.");
        if (double.IsNaN(horas) || double.IsInfinity(horas) || horas <= 0)
            throw new DominioException("A quantidade de horas do ajuste deve ser maior que zero.");
        if (data > hoje) throw new DominioException("O ajuste não pode ter data futura.");

        var texto = observacao?.Trim() ?? string.Empty;
        if (texto.Length < TamanhoMinimoObservacao)
            throw new DominioException($"Informe a observação do ajuste (mínimo de {TamanhoMinimoObservacao} caracteres).");

        return new BancoHoras
        {
            IdFuncionario = idFuncionario,
            Data = data,
            Tipo = tipo,
            Quantidade = Math.Round(horas, CasasDecimais, MidpointRounding.AwayFromZero),
            Origem = OrigensBancoHoras.AjusteRH,
            Observacao = texto
        };
    }

    /// <summary>Saldo em horas: soma dos créditos menos a soma dos débitos (pode ser negativo).</summary>
    public static decimal Saldo(IEnumerable<BancoHoras> lancamentos)
    {
        decimal saldo = 0;
        foreach (var l in lancamentos)
        {
            var quantidade = (decimal)l.Quantidade;
            saldo += l.Tipo switch
            {
                TiposBancoHoras.Credito => quantidade,
                TiposBancoHoras.Debito => -quantidade,
                _ => throw new DominioException($"Tipo de lançamento desconhecido no banco de horas: '{l.Tipo}'.")
            };
        }
        return Math.Round(saldo, CasasDecimais, MidpointRounding.AwayFromZero);
    }
}
