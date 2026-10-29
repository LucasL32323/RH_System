using HRAuto.Application.Abstractions;

namespace HRAuto.Application.Sistema;

/// <summary>Caso de uso: informa que a API está no ar e qual é a hora oficial do servidor.</summary>
public interface IConsultarStatusSistema
{
    StatusSistemaDto Executar(string ambiente);
}

public sealed class ConsultarStatusSistema : IConsultarStatusSistema
{
    public const string NomeAplicacao = "HR-Auto API";
    public const string VersaoAtual = "0.1.0";

    private readonly IRelogio _relogio;

    public ConsultarStatusSistema(IRelogio relogio) => _relogio = relogio;

    public StatusSistemaDto Executar(string ambiente) =>
        new(NomeAplicacao, VersaoAtual, ambiente, _relogio.Agora());
}
