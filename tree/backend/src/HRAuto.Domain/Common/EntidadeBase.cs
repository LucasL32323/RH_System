namespace HRAuto.Domain.Common;

/// <summary>
/// Base das entidades persistidas. Todas as tabelas do modelo físico usam
/// chave primária inteira autoincrementada.
/// </summary>
public abstract class EntidadeBase
{
    /// <summary>Chave primária (id_* da tabela correspondente).</summary>
    public int Id { get; protected set; }

    // Igualdade por identidade: duas entidades são iguais se tiverem o mesmo tipo e o mesmo Id (> 0).
    public override bool Equals(object? obj)
    {
        if (obj is not EntidadeBase outra) return false;
        if (ReferenceEquals(this, outra)) return true;
        if (GetType() != outra.GetType()) return false;
        return Id != 0 && Id == outra.Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
