using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Domain.Afastamentos;

/// <summary>Dados mínimos do funcionário que as regras de férias precisam (evita acoplar a entidade inteira).</summary>
public sealed record DadosFuncionario(int Id, DateOnly DataAdmissao, bool Ativo);

/// <summary>
/// Regras de férias (RF26, RF27), baseadas na CLT (arts. 130 e 134):
/// - O direito nasce após 12 meses de casa; cada ano completo de casa forma um período aquisitivo de 30 dias.
/// - Pode-se dividir em até 3 frações; cada fração tem no mínimo 5 dias e uma delas, no mínimo 14 dias.
/// - O início não pode cair em domingo, em feriado, nem nos 2 dias que antecedem domingo ou feriado.
/// Simplificações assumidas (a tabela FERIAS não tem coluna de período aquisitivo):
/// - Cada solicitação pertence ao período aquisitivo identificado pela DATA DE INÍCIO (anos completos de casa
///   nessa data). Férias vencidas de anos anteriores e abono pecuniário não são controlados.
/// - Solicitações Pendentes e Aprovadas consomem saldo; Rejeitadas não.
/// </summary>
public static class RegrasFerias
{
    public const int DiasPorPeriodo = 30;
    public const int MinimoDiasPorFracao = 5;
    public const int MinimoDiasFracaoPrincipal = 14;
    public const int MaximoFracoes = 3;
    public const int TamanhoMinimoMotivoRejeicao = 5;

    /// <summary>As férias estão aprovadas e cobrem o dia? (usado pelo cálculo de horas, RF20)</summary>
    public static bool CobreDia(Ferias ferias, DateOnly dia) =>
        ferias.Status == StatusSolicitacao.Aprovada && dia >= ferias.DataInicio && dia <= ferias.DataFim;

    /// <summary>Anos completos de casa na data (0 = ainda no primeiro ano, sem direito).</summary>
    public static int AnosCompletosDeCasa(DateOnly admissao, DateOnly data)
    {
        if (data < admissao) return 0;
        var anos = data.Year - admissao.Year;
        if (data < admissao.AddYears(anos)) anos--;
        return anos;
    }

    /// <summary>
    /// Verdadeiro se o início é permitido: o dia de início e os 2 dias seguintes não podem ser domingo nem feriado
    /// (CLT, art. 134 §3º: vedado iniciar nos 2 dias que antecedem feriado ou dia de repouso semanal).
    /// </summary>
    public static bool InicioPermitido(DateOnly inicio, IReadOnlySet<DateOnly> datasDeFeriado)
    {
        for (var i = 0; i <= 2; i++)
        {
            var dia = inicio.AddDays(i);
            if (dia.DayOfWeek == DayOfWeek.Sunday || datasDeFeriado.Contains(dia)) return false;
        }
        return true;
    }

    /// <summary>Cria a solicitação (status Pendente) ou lança <see cref="DominioException"/> com o motivo.</summary>
    /// <param name="dias">Quantidade informada pelo funcionário; precisa bater com o período (RF26).</param>
    /// <param name="hoje">Data de hoje em Brasília.</param>
    /// <param name="feriasDoFuncionario">Férias já cadastradas do funcionário (outros funcionários são ignorados).</param>
    /// <param name="datasDeFeriado">Feriados que valem para o funcionário (<see cref="Jornada.ResolvedorContextoDia"/>).</param>
    public static Ferias Solicitar(
        DadosFuncionario funcionario,
        DateOnly inicio,
        DateOnly fim,
        int dias,
        string? observacao,
        DateOnly hoje,
        IEnumerable<Ferias> feriasDoFuncionario,
        IReadOnlySet<DateOnly> datasDeFeriado)
    {
        if (!funcionario.Ativo) throw new DominioException("Funcionário inativo não pode solicitar férias.");
        if (fim < inicio) throw new DominioException("A data final não pode ser anterior à data inicial.");

        var diasDoPeriodo = fim.DayNumber - inicio.DayNumber + 1;
        if (dias != diasDoPeriodo)
            throw new DominioException($"A quantidade de dias ({dias}) não corresponde ao período informado ({diasDoPeriodo} dias).");
        if (inicio < hoje) throw new DominioException("O início das férias não pode ser no passado.");

        var periodo = AnosCompletosDeCasa(funcionario.DataAdmissao, inicio);
        if (periodo < 1)
            throw new DominioException("O direito a férias começa após 12 meses de trabalho; a data de início é anterior a isso.");

        if (dias < MinimoDiasPorFracao)
            throw new DominioException($"Cada período de férias deve ter no mínimo {MinimoDiasPorFracao} dias.");
        if (!InicioPermitido(inicio, datasDeFeriado))
            throw new DominioException("As férias não podem começar em domingo, feriado ou nos 2 dias que os antecedem.");

        var ativas = feriasDoFuncionario
            .Where(f => f.IdFuncionario == funcionario.Id &&
                        (f.Status == StatusSolicitacao.Pendente || f.Status == StatusSolicitacao.Aprovada))
            .ToList();

        if (ativas.Any(f => inicio <= f.DataFim && fim >= f.DataInicio))
            throw new DominioException("O período se sobrepõe a outra solicitação de férias do funcionário.");

        // Frações do mesmo período aquisitivo, já com a nova.
        var fracoes = ativas
            .Where(f => AnosCompletosDeCasa(funcionario.DataAdmissao, f.DataInicio) == periodo)
            .Select(f => f.Dias)
            .ToList();
        var jaUsados = fracoes.Sum();
        fracoes.Add(dias);

        var total = fracoes.Sum();
        if (total > DiasPorPeriodo)
            throw new DominioException($"Saldo insuficiente: restam {Math.Max(0, DiasPorPeriodo - jaUsados)} dias neste período aquisitivo.");
        if (fracoes.Count > MaximoFracoes)
            throw new DominioException($"As férias podem ser divididas em no máximo {MaximoFracoes} períodos.");

        // Uma das frações precisa ter 14 dias ou mais: já existe, ou ainda cabe uma fração com o saldo que sobra.
        var temFracaoPrincipal = fracoes.Max() >= MinimoDiasFracaoPrincipal;
        var aindaPossivel = fracoes.Count < MaximoFracoes && DiasPorPeriodo - total >= MinimoDiasFracaoPrincipal;
        if (!temFracaoPrincipal && !aindaPossivel)
            throw new DominioException(
                $"Ao dividir as férias, um dos períodos deve ter no mínimo {MinimoDiasFracaoPrincipal} dias.");

        return new Ferias
        {
            IdFuncionario = funcionario.Id,
            DataInicio = inicio,
            DataFim = fim,
            Dias = dias,
            Status = StatusSolicitacao.Pendente,
            DataSolicitacao = hoje,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim()
        };
    }

    /// <summary>
    /// Aprova ou rejeita a solicitação (RF27). Quem pode decidir (gestor do funcionário ou RH) é checado na API.
    /// Como a tabela FERIAS não guarda o decisor, ele é registrado no texto da observação (e no log de auditoria).
    /// </summary>
    public static void Decidir(Ferias ferias, bool aprovar, int idDecisor, string? observacao)
    {
        if (ferias.Status != StatusSolicitacao.Pendente)
            throw new DominioException("Esta solicitação de férias já foi decidida.");
        if (idDecisor <= 0) throw new DominioException("O responsável pela decisão é obrigatório.");
        if (idDecisor == ferias.IdFuncionario)
            throw new DominioException("Ninguém pode decidir a própria solicitação de férias.");

        var motivo = observacao?.Trim();
        if (!aprovar && (motivo is null || motivo.Length < TamanhoMinimoMotivoRejeicao))
            throw new DominioException("Informe o motivo da rejeição.");

        ferias.Status = aprovar ? StatusSolicitacao.Aprovada : StatusSolicitacao.Rejeitada;

        var registro = $"[{(aprovar ? "Aprovada" : "Rejeitada")} por {CodigoFuncionario.Formatar(idDecisor)}]" +
                       (string.IsNullOrEmpty(motivo) ? string.Empty : $" {motivo}");
        ferias.Observacao = string.IsNullOrEmpty(ferias.Observacao) ? registro : $"{ferias.Observacao}\n{registro}";
    }
}
