using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela HOLERITE (RF29, RF30, RNF17): o banco guarda só o caminho do PDF.</summary>
public class Holerite : EntidadeBase
{
    public int IdFuncionario { get; set; }
    public int Mes { get; set; }
    public int Ano { get; set; }
    public DateOnly DataPublicacao { get; set; }
    public string Arquivo { get; set; } = string.Empty;
}
