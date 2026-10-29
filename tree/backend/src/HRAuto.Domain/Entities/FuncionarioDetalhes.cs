namespace HRAuto.Domain.Entities;

/// <summary>
/// Tabela FUNCIONARIO_DETALHES (RF10, RNF08): documentos e endereço, relação 1:1 com FUNCIONARIO.
/// A chave primária é o próprio id_funcionario. Dados pessoais: acesso só do próprio funcionário e do RH.
/// </summary>
public class FuncionarioDetalhes
{
    public int IdFuncionario { get; set; }
    public string Cpf { get; set; } = string.Empty;
    public string Rg { get; set; } = string.Empty;
    public string? Pis { get; set; }
    public string? Ctps { get; set; }
    public string Endereco { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string? Complemento { get; set; }
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Cep { get; set; } = string.Empty;
}
