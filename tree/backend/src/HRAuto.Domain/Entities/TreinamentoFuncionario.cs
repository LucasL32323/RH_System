namespace HRAuto.Domain.Entities;

/// <summary>Tabela TREINAMENTO_FUNCIONARIO (RF38): inscrição, status, conclusão e nota. Chave composta.</summary>
public class TreinamentoFuncionario
{
    public int IdTreinamento { get; set; }
    public int IdFuncionario { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? DataInicio { get; set; }
    public DateOnly? DataConclusao { get; set; }
    public double? Nota { get; set; }
}
