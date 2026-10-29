using HRAuto.Domain.Enums;

namespace HRAuto.Application.Common;

/// <summary>
/// Quem está executando o caso de uso, lido do token JWT pela camada Api.
/// A Application só usa estes três dados; nunca confia em nome ou perfil vindos do corpo da requisição.
/// </summary>
/// <param name="IdUsuario">USUARIO.id_usuario (claim "sub").</param>
/// <param name="IdFuncionario">FUNCIONARIO.id_funcionario (claim "fid").</param>
/// <param name="Perfil">Colaborador, RH ou Admin (claim "role").</param>
public sealed record Solicitante(int IdUsuario, int IdFuncionario, PerfilAcesso Perfil);
