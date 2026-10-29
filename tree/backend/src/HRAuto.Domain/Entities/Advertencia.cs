using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela ADVERTENCIA (RF33 a RF35, RF47): base do semáforo e da régua de consequências.</summary>
public class Advertencia : EntidadeBase
{
    public int IdFuncionario { get; set; }
    public int IdResponsavel { get; set; }
    public DateOnly Data { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string Status { get; set; } = string.Empty;
}
