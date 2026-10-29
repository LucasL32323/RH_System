using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela CERTIFICADO (RF39, RF40): código de validação único.</summary>
public class Certificado : EntidadeBase
{
    public int IdTreinamento { get; set; }
    public int IdFuncionario { get; set; }
    public DateOnly DataEmissao { get; set; }
    public string CodigoValidacao { get; set; } = string.Empty;
}
