namespace HRAuto.Application.Abstractions;

/// <summary>Transação aberta. Se for descartada sem <see cref="ConfirmarAsync"/>, tudo é desfeito (rollback).</summary>
public interface ITransacao : IAsyncDisposable
{
    Task ConfirmarAsync(CancellationToken cancelamento = default);
}

/// <summary>
/// Grava de uma vez tudo o que os repositórios e a auditoria acumularam.
/// Os repositórios apenas marcam as mudanças; quem grava é a unidade de trabalho.
/// </summary>
public interface IUnidadeDeTrabalho
{
    /// <exception cref="Common.ConflitoDePersistenciaException">Violação de chave única (ex.: ponto do dia duplicado).</exception>
    Task SalvarAsync(CancellationToken cancelamento = default);

    /// <summary>
    /// Abre uma transação para gravar em duas etapas de forma atômica (ex.: gravar o ponto e,
    /// depois de conhecer o id gerado, gravar o log de auditoria que o referencia).
    /// </summary>
    Task<ITransacao> IniciarTransacaoAsync(CancellationToken cancelamento = default);
}
