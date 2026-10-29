using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Domain.Disciplina;

/// <summary>Cor do semáforo e quantas advertências ativas a geraram.</summary>
public sealed record ResultadoSemaforo(NivelSemaforo Nivel, int QuantidadeAtivas);

/// <summary>
/// Semáforo de advertências (RF47): 0 ativas = verde, 1 a 2 = amarelo, 3 ou mais = vermelho.
/// Contam todas as advertências com status Ativa, de qualquer tipo.
/// </summary>
public static class SemaforoAdvertencias
{
    public const int LimiteVermelho = 3;

    public static NivelSemaforo NivelPara(int quantidadeAtivas) => quantidadeAtivas switch
    {
        <= 0 => NivelSemaforo.Verde,
        < LimiteVermelho => NivelSemaforo.Amarelo,
        _ => NivelSemaforo.Vermelho
    };

    public static ResultadoSemaforo Calcular(IEnumerable<Advertencia> advertenciasDoFuncionario)
    {
        var ativas = advertenciasDoFuncionario.Count(a => a.Status == StatusAdvertencia.Ativa);
        return new ResultadoSemaforo(NivelPara(ativas), ativas);
    }
}
