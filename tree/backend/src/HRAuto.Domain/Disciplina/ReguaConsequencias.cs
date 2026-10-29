using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Domain.Disciplina;

/// <summary>Resultado da régua: o que o sistema sugere ao RH, com os números que levaram a isso.</summary>
public sealed record SugestaoConsequencia(
    AcaoSugerida Acao,
    int LevesDesdeUltimaSuspensao,
    int SuspensoesAtivas,
    string Mensagem);

/// <summary>
/// Régua de consequências automatizada (RF34):
/// - 3 advertências leves ativas desde a última suspensão = sugerir 1 suspensão;
/// - 2 suspensões ativas = sugerir justa causa (tem prioridade sobre a suspensão).
/// O sistema apenas SUGERE; o RH decide e registra a suspensão ou a justa causa como nova advertência.
/// Uma suspensão "consome" as leves anteriores a ela (a contagem das leves recomeça depois dela), por isso a
/// sugestão não se repete depois que o RH registra a suspensão. Advertências canceladas não contam.
/// A ordem é a da data da advertência (empate: ordem de cadastro).
/// </summary>
public static class ReguaConsequencias
{
    public const int LimiteLeves = 3;
    public const int LimiteSuspensoes = 2;

    public static SugestaoConsequencia Avaliar(IEnumerable<Advertencia> advertenciasDoFuncionario)
    {
        var leves = 0;
        var suspensoes = 0;
        var justaCausa = false;

        foreach (var advertencia in RegrasAdvertencia.Ativas(advertenciasDoFuncionario))
        {
            switch (RegrasAdvertencia.LerTipo(advertencia.Tipo))
            {
                case TipoAdvertencia.Leve:
                    leves++;
                    break;
                case TipoAdvertencia.Suspensao:
                    suspensoes++;
                    leves = 0; // a suspensão consome as leves anteriores
                    break;
                case TipoAdvertencia.JustaCausa:
                    justaCausa = true;
                    break;
            }
        }

        if (justaCausa)
            return new(AcaoSugerida.Nenhuma, leves, suspensoes, "Justa causa já aplicada: nenhuma nova consequência a sugerir.");

        if (suspensoes >= LimiteSuspensoes)
            return new(AcaoSugerida.JustaCausa, leves, suspensoes,
                $"Sugestão: avaliar justa causa ({suspensoes} suspensões ativas).");

        if (leves >= LimiteLeves)
            return new(AcaoSugerida.Suspensao, leves, suspensoes,
                $"Sugestão: aplicar suspensão ({leves} advertências leves desde a última suspensão).");

        return new(AcaoSugerida.Nenhuma, leves, suspensoes, "Nenhuma consequência a sugerir.");
    }
}
