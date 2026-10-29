namespace HRAuto.Infrastructure.Persistencia.Seed;

/// <summary>
/// Seção "Seed" do appsettings. O usuário administrador de TESTE só é criado quando
/// CriarAdminDeTeste = true (ligado apenas em appsettings.Development.json). Nunca use em produção.
/// </summary>
public sealed class OpcoesSeed
{
    public const string Secao = "Seed";

    public bool CriarAdminDeTeste { get; set; }
    public string AdminNome { get; set; } = "Administrador do Sistema";
    public string AdminEmail { get; set; } = "admin@hrauto.local";

    /// <summary>Senha do admin de teste; obrigatória quando CriarAdminDeTeste = true.</summary>
    public string? AdminSenha { get; set; }
}
