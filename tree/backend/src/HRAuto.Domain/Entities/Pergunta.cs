using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela PERGUNTA (RF41). O texto da pergunta fica na coluna "pergunta" (propriedade Enunciado).</summary>
public class Pergunta : EntidadeBase
{
    public int IdFormulario { get; set; }
    public string Enunciado { get; set; } = string.Empty;
    public string TipoResposta { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public bool Obrigatoria { get; set; }
}
