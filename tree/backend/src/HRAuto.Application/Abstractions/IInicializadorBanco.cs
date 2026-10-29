namespace HRAuto.Application.Abstractions;

/// <summary>
/// Prepara o banco na subida da API: aplica as migrations (RNF15) e grava os dados iniciais (seed).
/// </summary>
public interface IInicializadorBanco
{
    Task ExecutarAsync(CancellationToken cancelamento = default);
}
