namespace HRAuto.Domain.Entities;

/// <summary>Tabela FUNCIONARIO_BENEFICIO (RF51): adesão. Chave composta (funcionário, benefício).</summary>
public class FuncionarioBeneficio
{
    public int IdFuncionario { get; set; }
    public int IdBeneficio { get; set; }
    public DateOnly DataAdesao { get; set; }
}
