namespace HRAuto.Application.Abstractions;

/// <summary>
/// Fonte única de data/hora do sistema (horário de Brasília, RNF11).
/// O ponto sempre usa o horário do servidor (RF17); abstrair o relógio permite testar regras de jornada.
/// </summary>
public interface IRelogio
{
    /// <summary>Data e hora atuais em Brasília, com o deslocamento (offset) correto.</summary>
    DateTimeOffset Agora();

    /// <summary>Data de hoje em Brasília.</summary>
    DateOnly Hoje();
}
