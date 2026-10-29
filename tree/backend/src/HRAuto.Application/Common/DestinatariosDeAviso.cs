using HRAuto.Application.Abstractions;
using HRAuto.Domain.Entities;

namespace HRAuto.Application.Common;

/// <summary>
/// Quem recebe o aviso de um pedido feito por um funcionário (férias, manutenção de ponto): o gestor, se estiver ativo;
/// senão, todos os RH ativos. Ninguém é avisado do próprio pedido.
/// </summary>
internal static class DestinatariosDeAviso
{
    public static async Task<IReadOnlyList<int>> ObterAsync(
        Funcionario solicitante,
        IFuncionarioRepositorio funcionarios,
        IUsuarioGestaoRepositorio usuarios,
        CancellationToken cancelamento)
    {
        if (solicitante.IdGestor is { } idGestor)
        {
            var gestor = await funcionarios.ObterPorIdAsync(idGestor, cancelamento);
            if (gestor is { Ativo: true } && gestor.Id != solicitante.Id) return new[] { idGestor };
        }

        return (await usuarios.ListarIdsFuncionariosRhAtivosAsync(cancelamento))
            .Where(id => id != solicitante.Id)
            .Distinct()
            .ToList();
    }
}
