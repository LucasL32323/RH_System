using HRAuto.Domain.Common;
using HRAuto.Domain.Enums;

namespace HRAuto.Domain.Entities;

/// <summary>
/// Tabela PONTO (RF17, RF18, RF19, RNF09): um registro por funcionário por dia, com as quatro batidas.
/// Batidas nunca são excluídas (RNF07). Campos de horário nulos = batida ainda não feita.
/// As regras de sequência (RF18) e de sincronização offline (RF19) ficam aqui, no Domain.
/// Jornadas que viram a meia-noite não são tratadas (fora do escopo do TCC).
/// </summary>
public class Ponto : EntidadeBase
{
    /// <summary>Intervalo mínimo entre duas batidas do mesmo dia; abaixo disso a batida é considerada repetida.</summary>
    public static readonly TimeSpan IntervaloMinimoEntreBatidas = TimeSpan.FromMinutes(1);

    // Ordem obrigatória das batidas do dia.
    private static readonly TipoBatida[] Sequencia =
    {
        TipoBatida.Entrada, TipoBatida.SaidaIntervalo, TipoBatida.RetornoIntervalo, TipoBatida.Saida
    };

    public int IdFuncionario { get; set; }
    public DateOnly Data { get; set; }
    public TimeOnly? Entrada { get; set; }
    public TimeOnly? SaidaIntervalo { get; set; }
    public TimeOnly? RetornoIntervalo { get; set; }
    public TimeOnly? Saida { get; set; }

    /// <summary>Cria o registro (vazio) do dia para o funcionário.</summary>
    public static Ponto Criar(int idFuncionario, DateOnly data)
    {
        if (idFuncionario <= 0) throw new DominioException("O funcionário do ponto é obrigatório.");
        return new Ponto { IdFuncionario = idFuncionario, Data = data };
    }

    /// <summary>Horário gravado para o tipo de batida, ou nulo se ainda não foi feita.</summary>
    public TimeOnly? Obter(TipoBatida tipo) => tipo switch
    {
        TipoBatida.Entrada => Entrada,
        TipoBatida.SaidaIntervalo => SaidaIntervalo,
        TipoBatida.RetornoIntervalo => RetornoIntervalo,
        TipoBatida.Saida => Saida,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de batida desconhecido.")
    };

    /// <summary>Quantas das quatro batidas já foram feitas.</summary>
    public int TotalBatidas() => Sequencia.Count(t => Obter(t) is not null);

    /// <summary>Próxima batida esperada (a primeira ainda vazia), ou nulo se o dia já está completo.</summary>
    public TipoBatida? ProximaBatida()
    {
        foreach (var tipo in Sequencia)
            if (Obter(tipo) is null) return tipo;
        return null;
    }

    /// <summary>
    /// Registra a próxima batida do dia (RF17/RF18). O horário vem do servidor (relógio de Brasília).
    /// Bloqueia batida fora de ordem, repetida ou além da quarta batida.
    /// </summary>
    /// <returns>O tipo de batida que foi gravado.</returns>
    public TipoBatida RegistrarBatida(TimeOnly hora)
    {
        var tipo = ProximaBatida()
                   ?? throw new DominioException("As quatro batidas do dia já foram registradas.");

        ValidarOrdem(tipo, hora);
        Definir(tipo, hora);
        return tipo;
    }

    /// <summary>
    /// Aplica uma batida guardada offline no aparelho (RF19). É idempotente (RNF09): reenviar a mesma batida
    /// (mesmo tipo e mesmo horário) não duplica nada e devolve <see cref="ResultadoSincronizacao.JaRegistrada"/>.
    /// Se o tipo já tiver outro horário gravado, a batida é recusada: a correção passa por manutenção de ponto (RF22).
    /// </summary>
    public ResultadoSincronizacao AplicarBatidaSincronizada(TipoBatida tipo, TimeOnly hora)
    {
        var atual = Obter(tipo);
        if (atual is not null)
        {
            if (atual.Value == hora) return ResultadoSincronizacao.JaRegistrada;
            throw new DominioException(
                $"Já existe a batida de {Nome(tipo)} às {atual.Value:HH\\:mm\\:ss}. " +
                "Para corrigir, solicite a manutenção do ponto.");
        }

        // Todas as batidas anteriores precisam existir: a fila offline é enviada em ordem.
        foreach (var anterior in Sequencia.TakeWhile(t => t != tipo))
        {
            if (Obter(anterior) is null)
                throw new DominioException($"Falta a batida de {Nome(anterior)} antes da batida de {Nome(tipo)}.");
        }

        ValidarOrdem(tipo, hora);
        Definir(tipo, hora);
        return ResultadoSincronizacao.Aplicada;
    }

    /// <summary>Nome da batida em português, para mensagens.</summary>
    public static string Nome(TipoBatida tipo) => tipo switch
    {
        TipoBatida.Entrada => "entrada",
        TipoBatida.SaidaIntervalo => "saída para o intervalo",
        TipoBatida.RetornoIntervalo => "retorno do intervalo",
        TipoBatida.Saida => "saída",
        _ => tipo.ToString()
    };

    private void Definir(TipoBatida tipo, TimeOnly hora)
    {
        switch (tipo)
        {
            case TipoBatida.Entrada: Entrada = hora; break;
            case TipoBatida.SaidaIntervalo: SaidaIntervalo = hora; break;
            case TipoBatida.RetornoIntervalo: RetornoIntervalo = hora; break;
            case TipoBatida.Saida: Saida = hora; break;
            default: throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de batida desconhecido.");
        }
    }

    // A nova batida precisa ser posterior à batida anterior (com folga mínima) e anterior a uma eventual seguinte.
    private void ValidarOrdem(TipoBatida tipo, TimeOnly hora)
    {
        var indice = Array.IndexOf(Sequencia, tipo);

        for (var i = indice - 1; i >= 0; i--)
        {
            var anterior = Obter(Sequencia[i]);
            if (anterior is null) continue;

            var diferenca = hora.ToTimeSpan() - anterior.Value.ToTimeSpan();
            if (diferenca < TimeSpan.Zero)
                throw new DominioException(
                    $"Batida fora de ordem: a {Nome(tipo)} ({hora:HH\\:mm}) é anterior à {Nome(Sequencia[i])} ({anterior.Value:HH\\:mm}).");
            if (diferenca < IntervaloMinimoEntreBatidas)
                throw new DominioException(
                    $"Batida repetida: já houve a {Nome(Sequencia[i])} às {anterior.Value:HH\\:mm\\:ss}.");
            break;
        }

        for (var i = indice + 1; i < Sequencia.Length; i++)
        {
            var seguinte = Obter(Sequencia[i]);
            if (seguinte is null) continue;

            if (hora.ToTimeSpan() >= seguinte.Value.ToTimeSpan())
                throw new DominioException(
                    $"Batida fora de ordem: a {Nome(tipo)} ({hora:HH\\:mm}) não pode ser depois da {Nome(Sequencia[i])} ({seguinte.Value:HH\\:mm}).");
            break;
        }
    }
}
