using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela FORMULARIO (RF41, RF43, RF44): pesquisa, teste, avaliação, clima, feedback ou denúncia.</summary>
public class Formulario : EntidadeBase
{
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int IdAutor { get; set; }
    public DateTime DataCriacao { get; set; }
    public bool Ativo { get; set; } = true;
}
