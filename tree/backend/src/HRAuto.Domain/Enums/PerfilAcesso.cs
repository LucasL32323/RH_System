namespace HRAuto.Domain.Enums;

/// <summary>
/// Perfis de acesso do sistema (tabela PERFIL). O papel de "gestor" não é um perfil:
/// é qualquer funcionário que apareça como id_gestor de outros (FUNCIONARIO.id_gestor).
/// </summary>
public enum PerfilAcesso
{
    Colaborador = 1,
    RH = 2,
    Admin = 3
}

/// <summary>Nomes gravados em PERFIL.nome e usados como "role" no JWT.</summary>
public static class NomesPerfil
{
    public const string Colaborador = "Colaborador";
    public const string RH = "RH";
    public const string Admin = "Admin";

    /// <summary>Converte o enum para o nome gravado no banco.</summary>
    public static string De(PerfilAcesso perfil) => perfil switch
    {
        PerfilAcesso.Colaborador => Colaborador,
        PerfilAcesso.RH => RH,
        PerfilAcesso.Admin => Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(perfil), perfil, "Perfil desconhecido.")
    };
}
