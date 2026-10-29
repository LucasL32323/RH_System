using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Domain.Jornada;

/// <summary>
/// Manutenção de ponto (RF22, RF23): o colaborador pede ajuste de batida ou abono do dia com justificativa;
/// o gestor aprova ou rejeita. Quem pode analisar (gestor do funcionário) é checado na API; aqui ficam as regras
/// que não dependem de perfil. Observação: MANUTENCAO_PONTO.id_ponto é FK de PONTO, então para abonar um dia sem
/// nenhuma batida a API precisa criar antes o registro de ponto vazio do dia (Ponto.Criar).
/// </summary>
public static class RegrasManutencaoPonto
{
    public const int TamanhoMinimoJustificativa = 10;
    public const int TamanhoMaximoJustificativa = 500;
    public const int TamanhoMinimoObservacaoRejeicao = 5;
    public const int TamanhoMaximoObservacao = 500;

    /// <summary>Quantos dias para trás o colaborador ainda pode pedir manutenção (suposição do TCC; ajustável).</summary>
    public const int PrazoMaximoDias = 60;

    private static readonly TipoBatida[] Sequencia =
    {
        TipoBatida.Entrada, TipoBatida.SaidaIntervalo, TipoBatida.RetornoIntervalo, TipoBatida.Saida
    };

    /// <summary>
    /// Confere e limpa a justificativa (obrigatória, RF22). Devolve o texto sem espaços nas pontas.
    /// Como MANUTENCAO_PONTO não tem colunas para horários propostos, o colaborador descreve o que aconteceu aqui.
    /// </summary>
    public static string NormalizarJustificativa(string? justificativa)
    {
        var texto = justificativa?.Trim() ?? string.Empty;
        if (texto.Length < TamanhoMinimoJustificativa)
            throw new DominioException($"A justificativa é obrigatória (mínimo de {TamanhoMinimoJustificativa} caracteres).");
        if (texto.Length > TamanhoMaximoJustificativa)
            throw new DominioException($"A justificativa deve ter no máximo {TamanhoMaximoJustificativa} caracteres.");
        return texto;
    }

    /// <summary>
    /// O dia do pedido precisa existir para o funcionário: não pode ser futuro, anterior à admissão
    /// nem mais antigo que <see cref="PrazoMaximoDias"/> dias.
    /// </summary>
    public static void ValidarDiaDoPedido(DateOnly data, DateOnly admissao, DateOnly hoje)
    {
        if (data > hoje)
            throw new DominioException("Não é possível pedir manutenção de um dia que ainda não aconteceu.");
        if (data < admissao)
            throw new DominioException("Não é possível pedir manutenção de um dia anterior à admissão.");
        if (hoje.DayNumber - data.DayNumber > PrazoMaximoDias)
            throw new DominioException($"O pedido de manutenção só vale para os últimos {PrazoMaximoDias} dias.");
    }

    /// <summary>
    /// Aplica as batidas corrigidas pelo gestor ao aprovar o pedido (RF22/RF23). Campos nulos ficam como estão; uma batida
    /// nunca é apagada (RNF07). A correção inteira é validada antes de qualquer alteração: as batidas finais precisam ser
    /// contíguas (sem "buraco") e crescentes, com pelo menos <see cref="Ponto.IntervaloMinimoEntreBatidas"/> entre elas.
    /// </summary>
    /// <returns>As batidas que de fato mudaram (vazio se a correção repete o que já estava gravado).</returns>
    public static IReadOnlyList<AlteracaoBatida> AplicarCorrecao(Ponto ponto, CorrecaoBatidas correcao)
    {
        if (correcao.Vazia) throw new DominioException("Informe pelo menos um horário para corrigir.");

        var finais = Sequencia.ToDictionary(t => t, t => Proposto(correcao, t) ?? ponto.Obter(t));

        // Contíguas: depois da primeira batida vazia não pode haver nenhuma preenchida.
        var vazioVisto = false;
        foreach (var tipo in Sequencia)
        {
            if (finais[tipo] is null) { vazioVisto = true; continue; }
            if (vazioVisto)
                throw new DominioException(
                    $"Informe também as batidas anteriores à {Ponto.Nome(tipo)}: o dia não pode ter batidas faltando no meio.");
        }

        // Crescentes, com folga mínima entre uma e outra.
        TimeOnly? anterior = null;
        TipoBatida? tipoAnterior = null;
        foreach (var tipo in Sequencia)
        {
            if (finais[tipo] is not { } atual) break;
            if (anterior is { } ant)
            {
                var diferenca = atual.ToTimeSpan() - ant.ToTimeSpan();
                if (diferenca < Ponto.IntervaloMinimoEntreBatidas)
                    throw new DominioException(
                        $"A {Ponto.Nome(tipo)} ({atual:HH\\:mm}) precisa ser depois da {Ponto.Nome(tipoAnterior!.Value)} ({ant:HH\\:mm}).");
            }
            anterior = atual;
            tipoAnterior = tipo;
        }

        var alteracoes = new List<AlteracaoBatida>();
        foreach (var tipo in Sequencia)
        {
            if (Proposto(correcao, tipo) is not { } depois) continue;
            var antes = ponto.Obter(tipo);
            if (antes == depois) continue;

            alteracoes.Add(new AlteracaoBatida(tipo, antes, depois));
            Definir(ponto, tipo, depois);
        }
        return alteracoes;
    }

    private static TimeOnly? Proposto(CorrecaoBatidas c, TipoBatida tipo) => tipo switch
    {
        TipoBatida.Entrada => c.Entrada,
        TipoBatida.SaidaIntervalo => c.SaidaIntervalo,
        TipoBatida.RetornoIntervalo => c.RetornoIntervalo,
        TipoBatida.Saida => c.Saida,
        _ => null
    };

    private static void Definir(Ponto ponto, TipoBatida tipo, TimeOnly hora)
    {
        switch (tipo)
        {
            case TipoBatida.Entrada: ponto.Entrada = hora; break;
            case TipoBatida.SaidaIntervalo: ponto.SaidaIntervalo = hora; break;
            case TipoBatida.RetornoIntervalo: ponto.RetornoIntervalo = hora; break;
            case TipoBatida.Saida: ponto.Saida = hora; break;
        }
    }

    /// <param name="ponto">Registro de ponto que será ajustado (já salvo, com Id).</param>
    /// <param name="idSolicitante">Funcionário que está pedindo; só pode pedir do próprio ponto.</param>
    /// <param name="justificativa">Obrigatória.</param>
    /// <param name="agora">Data e hora de Brasília (do relógio do servidor).</param>
    /// <param name="existentes">Pedidos já feitos para este ponto, para impedir dois pendentes ao mesmo tempo.</param>
    public static ManutencaoPonto Solicitar(
        Ponto ponto, int idSolicitante, string? justificativa, DateTime agora, IEnumerable<ManutencaoPonto> existentes)
    {
        if (ponto.Id <= 0) throw new DominioException("O ponto precisa estar salvo antes de pedir manutenção.");
        if (ponto.IdFuncionario != idSolicitante)
            throw new DominioException("Só é possível pedir manutenção do próprio ponto.");
        if (ponto.Data > DateOnly.FromDateTime(agora))
            throw new DominioException("Não é possível pedir manutenção de um dia que ainda não aconteceu.");

        var texto = NormalizarJustificativa(justificativa);

        if (existentes.Any(m => m.IdPonto == ponto.Id && m.Status == StatusSolicitacao.Pendente))
            throw new DominioException("Já existe um pedido de manutenção pendente para este dia.");

        return new ManutencaoPonto
        {
            IdPonto = ponto.Id,
            IdFuncionario = idSolicitante,
            Justificativa = texto,
            DataSolicitacao = agora,
            Status = StatusSolicitacao.Pendente
        };
    }

    /// <summary>Aprova ou rejeita o pedido, registrando responsável e data da análise (RF23).</summary>
    public static void Analisar(ManutencaoPonto pedido, bool aprovar, int idResponsavel, string? observacao, DateTime agora)
    {
        if (pedido.Status != StatusSolicitacao.Pendente)
            throw new DominioException("Este pedido já foi analisado.");
        if (idResponsavel <= 0) throw new DominioException("O responsável pela análise é obrigatório.");
        if (idResponsavel == pedido.IdFuncionario)
            throw new DominioException("Ninguém pode analisar o próprio pedido de manutenção.");

        var texto = observacao?.Trim();
        if (texto is { Length: > TamanhoMaximoObservacao })
            throw new DominioException($"A observação deve ter no máximo {TamanhoMaximoObservacao} caracteres.");
        if (!aprovar && (texto is null || texto.Length < TamanhoMinimoObservacaoRejeicao))
            throw new DominioException("Informe o motivo da rejeição.");

        pedido.Status = aprovar ? StatusSolicitacao.Aprovada : StatusSolicitacao.Rejeitada;
        pedido.IdResponsavel = idResponsavel;
        pedido.DataAnalise = agora;
        pedido.Observacao = string.IsNullOrEmpty(texto) ? null : texto;
    }
}

/// <summary>
/// Horários corrigidos pelo gestor ao aprovar. Campo nulo = "não mexer". Para completar um dia, informe também as batidas
/// que já existiam se quiser conferi-las; as que não vierem continuam como estão.
/// </summary>
public sealed record CorrecaoBatidas(TimeOnly? Entrada, TimeOnly? SaidaIntervalo, TimeOnly? RetornoIntervalo, TimeOnly? Saida)
{
    public bool Vazia => Entrada is null && SaidaIntervalo is null && RetornoIntervalo is null && Saida is null;
}

/// <summary>Uma batida que mudou: <c>Antes</c> é nulo quando a batida não existia (ex.: esquecimento de saída).</summary>
public sealed record AlteracaoBatida(TipoBatida Tipo, TimeOnly? Antes, TimeOnly Depois);
