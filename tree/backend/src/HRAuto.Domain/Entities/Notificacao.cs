using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela NOTIFICACAO (RF25, RF36).</summary>
public class Notificacao : EntidadeBase
{
    public int IdFuncionario { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public DateTime DataEnvio { get; set; }
    public bool Lida { get; set; }
}
