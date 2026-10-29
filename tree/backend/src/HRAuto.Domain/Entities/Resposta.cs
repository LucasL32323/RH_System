using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>
/// Tabela RESPOSTA (RF42, RF44). O texto fica na coluna "resposta" (propriedade Conteudo).
/// IdFuncionario nulo = resposta anônima (RNF08: denúncia anônima não grava o funcionário).
/// </summary>
public class Resposta : EntidadeBase
{
    public int IdPergunta { get; set; }
    public int? IdFuncionario { get; set; }
    public string Conteudo { get; set; } = string.Empty;
    public DateTime DataResposta { get; set; }
}
