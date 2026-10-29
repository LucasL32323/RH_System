namespace HRAuto.Domain.Entities;

/// <summary>Tabela AVISO_FUNCIONARIO (RF31, RF32): destinatário e confirmação de leitura. Chave composta.</summary>
public class AvisoFuncionario
{
    public int IdAviso { get; set; }
    public int IdFuncionario { get; set; }
    public bool Visualizado { get; set; }
    public DateTime? DataVisualizacao { get; set; }
}
