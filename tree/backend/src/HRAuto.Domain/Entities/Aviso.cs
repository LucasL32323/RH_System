using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela AVISO (RF31). O autor é um funcionário (coluna id_autor).</summary>
public class Aviso : EntidadeBase
{
    public int IdAutor { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Conteudo { get; set; } = string.Empty;
    public DateTime DataPublicacao { get; set; }
    public DateOnly? DataExpiracao { get; set; }
    public string Tipo { get; set; } = string.Empty;
}
