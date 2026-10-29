namespace HRAuto.Domain.Common;

/// <summary>
/// Violação de regra de negócio. A API converte esta exceção em HTTP 400/422.
/// </summary>
public class DominioException : Exception
{
    public DominioException(string mensagem) : base(mensagem) { }
}
