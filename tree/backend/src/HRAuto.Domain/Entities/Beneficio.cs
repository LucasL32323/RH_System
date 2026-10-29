using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela BENEFICIO (RF50).</summary>
public class Beneficio : EntidadeBase
{
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;
}
