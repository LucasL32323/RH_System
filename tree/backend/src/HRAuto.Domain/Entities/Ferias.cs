using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela FERIAS (RF26, RF27).</summary>
public class Ferias : EntidadeBase
{
    public int IdFuncionario { get; set; }
    public DateOnly DataInicio { get; set; }
    public DateOnly DataFim { get; set; }
    public int Dias { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly DataSolicitacao { get; set; }
    public string? Observacao { get; set; }
}
