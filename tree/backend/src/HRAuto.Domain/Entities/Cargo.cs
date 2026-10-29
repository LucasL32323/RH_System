using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela CARGO (RF14). Coluna nome_cargo.</summary>
public class Cargo : EntidadeBase
{
    public string NomeCargo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
}
