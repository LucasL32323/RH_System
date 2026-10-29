using HRAuto.Domain.Common;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela FUNCIONARIO (RF09, RF12, RF16). O código FUNC-0001 é derivado do Id (RF11).</summary>
public class Funcionario : EntidadeBase
{
    public string Nome { get; set; } = string.Empty;
    public DateOnly DataNascimento { get; set; }
    public DateOnly DataAdmissao { get; set; }
    /// <summary>Preenchida no desligamento, que também inativa o funcionário (RF12).</summary>
    public DateOnly? DataDesligamento { get; set; }
    public bool Ativo { get; set; } = true;
    public string Email { get; set; } = string.Empty;
    public string? Telefone { get; set; }

    public int IdCargo { get; set; }
    public Cargo? Cargo { get; set; }

    public int IdDepartamento { get; set; }
    public Departamento? Departamento { get; set; }

    /// <summary>Gestor direto (auto-relacionamento). Quem aparece aqui para outros funcionários é "gestor".</summary>
    public int? IdGestor { get; set; }
    public Funcionario? Gestor { get; set; }

    /// <summary>Código exibido ao usuário (FUNC-0001). Só existe depois que o Id for gerado.</summary>
    public string Codigo => CodigoFuncionario.Formatar(Id);
}
