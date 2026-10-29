using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela USUARIO (RF01, RF06): credencial de acesso de um funcionário.</summary>
public class Usuario : EntidadeBase
{
    public int IdFuncionario { get; set; }
    public Funcionario? Funcionario { get; set; }

    public int IdPerfil { get; set; }
    public Perfil? Perfil { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>Hash BCrypt da senha (RNF05). Nunca texto puro.</summary>
    public string Senha { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime? UltimoLogin { get; set; }
}
