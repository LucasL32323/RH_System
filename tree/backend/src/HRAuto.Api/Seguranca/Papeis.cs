using HRAuto.Domain.Enums;

namespace HRAuto.Api.Seguranca;

/// <summary>Nomes dos perfis e das políticas de autorização usados nos atributos dos controllers (RF05, RF07).</summary>
public static class Papeis
{
    /// <summary>Para <c>[Authorize(Roles = ...)]</c>: RH ou Admin.</summary>
    public const string RhOuAdmin = NomesPerfil.RH + "," + NomesPerfil.Admin;

    /// <summary>Política registrada em <c>AddAutorizacao</c>: só RH ou Admin.</summary>
    public const string PoliticaRhOuAdmin = "RhOuAdmin";

    /// <summary>Política: só Admin.</summary>
    public const string PoliticaAdmin = "Admin";
}
