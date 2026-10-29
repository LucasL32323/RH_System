using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Domain.Disciplina;

/// <summary>
/// Registro e cancelamento de advertências (RF33). Quem pode registrar (perfil RH) é checado na API.
/// ADVERTENCIA.tipo é gravado como texto com o nome do enum (Leve, Suspensao, JustaCausa).
/// </summary>
public static class RegrasAdvertencia
{
    public const int TamanhoMinimoMotivo = 3;
    public const int TamanhoMinimoMotivoCancelamento = 5;

    public static Advertencia Registrar(
        int idFuncionario,
        int idResponsavel,
        TipoAdvertencia tipo,
        string? motivo,
        string? descricao,
        DateOnly data,
        DateOnly hoje)
    {
        if (idFuncionario <= 0) throw new DominioException("O funcionário da advertência é obrigatório.");
        if (idResponsavel <= 0) throw new DominioException("O responsável pela advertência é obrigatório.");
        if (idResponsavel == idFuncionario)
            throw new DominioException("O responsável não pode registrar uma advertência para si mesmo.");
        if (!Enum.IsDefined(tipo)) throw new DominioException("Tipo de advertência inválido.");
        if (data > hoje) throw new DominioException("A data da advertência não pode ser futura.");

        var textoMotivo = motivo?.Trim() ?? string.Empty;
        if (textoMotivo.Length < TamanhoMinimoMotivo)
            throw new DominioException("O motivo da advertência é obrigatório.");

        return new Advertencia
        {
            IdFuncionario = idFuncionario,
            IdResponsavel = idResponsavel,
            Data = data,
            Tipo = tipo.ToString(),
            Motivo = textoMotivo,
            Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim(),
            Status = StatusAdvertencia.Ativa
        };
    }

    /// <summary>
    /// Cancela uma advertência registrada por engano ou revista. Ela deixa de contar no semáforo e na régua,
    /// mas continua no histórico (nada é apagado, RNF07).
    /// </summary>
    public static void Cancelar(Advertencia advertencia, string? motivoDoCancelamento)
    {
        if (advertencia.Status != StatusAdvertencia.Ativa)
            throw new DominioException("Só é possível cancelar uma advertência ativa.");

        var motivo = motivoDoCancelamento?.Trim() ?? string.Empty;
        if (motivo.Length < TamanhoMinimoMotivoCancelamento)
            throw new DominioException("Informe o motivo do cancelamento.");

        advertencia.Status = StatusAdvertencia.Cancelada;
        var registro = $"[Cancelada] {motivo}";
        advertencia.Descricao = string.IsNullOrEmpty(advertencia.Descricao) ? registro : $"{advertencia.Descricao}\n{registro}";
    }

    /// <summary>Converte o texto gravado no banco para o enum; falha alto se vier um valor desconhecido.</summary>
    public static TipoAdvertencia LerTipo(string texto)
    {
        if (Enum.TryParse<TipoAdvertencia>(texto, ignoreCase: false, out var tipo) && Enum.IsDefined(tipo))
            return tipo;
        throw new DominioException($"Tipo de advertência desconhecido: '{texto}'.");
    }

    /// <summary>Somente as advertências com status Ativa, da mais antiga para a mais nova.</summary>
    public static IReadOnlyList<Advertencia> Ativas(IEnumerable<Advertencia> advertencias) =>
        advertencias
            .Where(a => a.Status == StatusAdvertencia.Ativa)
            .OrderBy(a => a.Data)
            .ThenBy(a => a.Id)
            .ToList();
}
