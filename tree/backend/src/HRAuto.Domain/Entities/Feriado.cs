using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela FERIADO (RF28): nacional, estadual ou municipal.</summary>
public class Feriado : EntidadeBase
{
    public DateOnly Data { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
}
