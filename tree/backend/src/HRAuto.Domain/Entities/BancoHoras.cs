using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela BANCO_HORAS (RF24): lançamentos de crédito/débito em horas.</summary>
public class BancoHoras : EntidadeBase
{
    public int IdFuncionario { get; set; }
    public DateOnly Data { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public double Quantidade { get; set; }
    public string Origem { get; set; } = string.Empty;
    public string? Observacao { get; set; }
}
