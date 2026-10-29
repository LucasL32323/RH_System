using HRAuto.Domain.Common;

namespace HRAuto.Domain.Entities;

/// <summary>Tabela LOG_AUDITORIA (RF08, RNF07).</summary>
public class LogAuditoria : EntidadeBase
{
    public int IdUsuario { get; set; }
    public DateTime DataHora { get; set; }
    public string Acao { get; set; } = string.Empty;
    public string? Tabela { get; set; }
    public int? IdRegistro { get; set; }
    public string? Descricao { get; set; }
}
