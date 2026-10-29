using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela DEPARTAMENTO (RF15).</summary>
public class Departamento : EntidadeBase
{
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
}
