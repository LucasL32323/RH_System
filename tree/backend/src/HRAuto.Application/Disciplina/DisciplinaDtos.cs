using HRAuto.Domain.Disciplina;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Disciplina;

// ---------- Entrada ----------

/// <summary>Tipo: Leve, Suspensao ou JustaCausa. Sem data, usa hoje.</summary>
public sealed record RegistrarAdvertenciaDto(string? CodigoFuncionario, string? Tipo, string? Motivo, string? Descricao, DateOnly? Data);

public sealed record CancelarAdvertenciaDto(string? Motivo);

/// <summary>Status: "Ativa" ou "Cancelada"; vazio = todas.</summary>
public sealed record FiltroListaAdvertenciasDto(string? Codigo, string? Status, int? Pagina, int? Tamanho);

// ---------- Saída ----------

public sealed record AdvertenciaDto(
    int Id, string CodigoFuncionario, string NomeFuncionario, string Tipo, DateOnly Data,
    string Motivo, string? Descricao, string Status, string CodigoResponsavel, string NomeResponsavel);

/// <summary>Cor (Verde, Amarelo ou Vermelho) e quantas advertências ativas a geraram (RF47).</summary>
public sealed record SemaforoDto(string Nivel, int QuantidadeAtivas);

/// <summary>Sugestão da régua (RF34): Nenhuma, Suspensao ou JustaCausa. Só o RH/Admin enxerga.</summary>
public sealed record SugestaoReguaDto(string Acao, int LevesDesdeUltimaSuspensao, int SuspensoesAtivas, string Mensagem);

public sealed record SituacaoDisciplinarDto(
    string Codigo, string Nome, SemaforoDto Semaforo, SugestaoReguaDto? Regua, IReadOnlyList<AdvertenciaDto> Advertencias);

public sealed record AdvertenciaRegistradaDto(AdvertenciaDto Advertencia, SemaforoDto Semaforo, SugestaoReguaDto Regua);

// ---------- Conversões ----------

internal static class MapeadorDisciplina
{
    public static SemaforoDto Semaforo(ResultadoSemaforo r) => new(r.Nivel.ToString(), r.QuantidadeAtivas);

    public static SugestaoReguaDto Regua(SugestaoConsequencia s) =>
        new(s.Acao.ToString(), s.LevesDesdeUltimaSuspensao, s.SuspensoesAtivas, s.Mensagem);

    public static AdvertenciaDto Advertencia(Advertencia a, IReadOnlyDictionary<int, string> nomes) =>
        new(a.Id, CodigoFuncionario.Formatar(a.IdFuncionario), NomeDe(nomes, a.IdFuncionario), a.Tipo, a.Data,
            a.Motivo, a.Descricao, a.Status, CodigoFuncionario.Formatar(a.IdResponsavel), NomeDe(nomes, a.IdResponsavel));

    private static string NomeDe(IReadOnlyDictionary<int, string> nomes, int id) =>
        nomes.TryGetValue(id, out var nome) ? nome : string.Empty;

    /// <summary>Lê o tipo pelo nome (Leve, Suspensao, JustaCausa). Números e nomes desconhecidos são recusados.</summary>
    public static bool TentarLerTipo(string? texto, out TipoAdvertencia tipo)
    {
        tipo = default;
        var t = texto?.Trim();
        if (string.IsNullOrEmpty(t) || char.IsDigit(t[0]) || t[0] == '-') return false;
        return Enum.TryParse(t, ignoreCase: true, out tipo) && Enum.IsDefined(tipo);
    }
}
