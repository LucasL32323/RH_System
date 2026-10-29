using HRAuto.Domain.Enums;

namespace HRAuto.Domain.Seguranca;

/// <summary>
/// Quem pode conceder e gerenciar cada perfil (RF06, RF07). Evita que o RH se promova a Admin
/// ou mexa na conta de um Admin (por exemplo, trocando o e-mail para tomar a conta pelo "esqueci minha senha").
/// - Admin: gerencia e concede qualquer perfil.
/// - RH: gerencia e concede só o perfil Colaborador.
/// - Colaborador: não gerencia acessos.
/// </summary>
public static class RegrasAcesso
{
    /// <summary>Pode atribuir este perfil a alguém?</summary>
    public static bool PodeConceder(PerfilAcesso quem, PerfilAcesso perfil) =>
        quem == PerfilAcesso.Admin || (quem == PerfilAcesso.RH && perfil == PerfilAcesso.Colaborador);

    /// <summary>Pode alterar a conta (e os dados) de alguém que hoje tem este perfil?</summary>
    public static bool PodeGerenciar(PerfilAcesso quem, PerfilAcesso alvo) => PodeConceder(quem, alvo);
}
