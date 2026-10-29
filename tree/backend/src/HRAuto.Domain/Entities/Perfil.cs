using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela PERFIL: Colaborador, RH ou Admin (RF05). O Id segue o enum PerfilAcesso.</summary>
public class Perfil : EntidadeBase
{
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
}
