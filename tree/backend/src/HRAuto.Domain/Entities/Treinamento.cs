using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela TREINAMENTO (RF37).</summary>
public class Treinamento : EntidadeBase
{
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public DateOnly? DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public bool Obrigatorio { get; set; }
}
