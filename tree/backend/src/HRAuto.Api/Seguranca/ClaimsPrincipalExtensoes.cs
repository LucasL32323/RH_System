using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using HRAuto.Application.Autenticacao;
using HRAuto.Application.Common;
using HRAuto.Domain.Enums;

namespace HRAuto.Api.Seguranca;

public static class ClaimsPrincipalExtensoes
{
    /// <summary>
    /// Monta o <see cref="Solicitante"/> a partir das claims do token já validado (sub, fid, role).
    /// Devolve false se alguma claim faltar ou for inválida: nesse caso o controller responde 401,
    /// em vez de deixar a Application trabalhar com identidade incompleta.
    /// </summary>
    public static bool TentarObterSolicitante(this ClaimsPrincipal principal, [NotNullWhen(true)] out Solicitante? solicitante)
    {
        solicitante = null;

        if (!TentarInteiro(principal.FindFirst(NomesClaims.Usuario)?.Value, out var idUsuario)) return false;
        if (!TentarInteiro(principal.FindFirst(NomesClaims.Funcionario)?.Value, out var idFuncionario)) return false;
        if (!PerfilAcessoExtensoes.TentarLer(principal.FindFirst(NomesClaims.Perfil)?.Value, out var perfil)) return false;

        solicitante = new Solicitante(idUsuario, idFuncionario, perfil);
        return true;
    }

    private static bool TentarInteiro(string? texto, out int valor) =>
        int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out valor) && valor > 0;
}
