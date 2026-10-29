namespace HRAuto.Domain.Enums;

/// <summary>Conversões e atalhos sobre <see cref="PerfilAcesso"/> usados na autorização (RF07, RNF06).</summary>
public static class PerfilAcessoExtensoes
{
    /// <summary>Converte o nome do perfil (claim "role" do JWT) para o enum. Retorna false se for desconhecido.</summary>
    public static bool TentarLer(string? nome, out PerfilAcesso perfil)
    {
        switch (nome)
        {
            case NomesPerfil.Colaborador: perfil = PerfilAcesso.Colaborador; return true;
            case NomesPerfil.RH: perfil = PerfilAcesso.RH; return true;
            case NomesPerfil.Admin: perfil = PerfilAcesso.Admin; return true;
            default: perfil = default; return false;
        }
    }

    /// <summary>RH e Admin enxergam dados de todos os funcionários.</summary>
    public static bool EhRhOuAdmin(this PerfilAcesso perfil) => perfil is PerfilAcesso.RH or PerfilAcesso.Admin;
}
