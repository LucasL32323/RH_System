namespace HRAuto.Infrastructure.Persistencia;

/// <summary>Configuração do banco (seção "Banco" do appsettings.json).</summary>
public sealed class OpcoesBanco
{
    public const string Secao = "Banco";

    /// <summary>String de conexão SQLite. Ex.: Data Source=hrauto.db</summary>
    public string ConnectionString { get; set; } = "Data Source=hrauto.db";
}
