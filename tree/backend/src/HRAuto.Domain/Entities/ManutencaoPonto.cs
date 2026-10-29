using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela MANUTENCAO_PONTO (RF22, RF23): pedido de ajuste/abono analisado pelo gestor.</summary>
public class ManutencaoPonto : EntidadeBase
{
    public int IdPonto { get; set; }
    public int IdFuncionario { get; set; }
    public string Justificativa { get; set; } = string.Empty;
    public DateTime DataSolicitacao { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DataAnalise { get; set; }
    public int? IdResponsavel { get; set; }
    public string? Observacao { get; set; }
}
