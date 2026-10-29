namespace HRAuto.Application.Common;

/// <summary>
/// Categoria da falha de um caso de uso. A camada Api converte cada uma em um código HTTP
/// (400, 401, 403, 404, 409), sem que a Application precise conhecer HTTP.
/// </summary>
public enum TipoErro
{
    /// <summary>Dado inválido ou regra de negócio violada (HTTP 400).</summary>
    Validacao = 1,
    /// <summary>Credencial inválida ou ausente (HTTP 401).</summary>
    NaoAutenticado = 2,
    /// <summary>Autenticado, mas sem permissão para a operação (HTTP 403).</summary>
    Proibido = 3,
    /// <summary>Registro inexistente (HTTP 404).</summary>
    NaoEncontrado = 4,
    /// <summary>Operação concorrente ou estado incompatível (HTTP 409).</summary>
    Conflito = 5
}

/// <summary>
/// Resultado de um caso de uso: sucesso com valor ou falha com mensagem e categoria.
/// Evita usar exceções para fluxos esperados (ex.: senha inválida, batida fora de ordem).
/// </summary>
public sealed class Resultado<T>
{
    public bool Sucesso { get; }
    public T? Valor { get; }
    public string? Erro { get; }
    public TipoErro? TipoDoErro { get; }

    private Resultado(bool sucesso, T? valor, string? erro, TipoErro? tipo)
    {
        Sucesso = sucesso;
        Valor = valor;
        Erro = erro;
        TipoDoErro = tipo;
    }

    public static Resultado<T> Ok(T valor) => new(true, valor, null, null);

    /// <summary>Falha de validação (a categoria mais comum).</summary>
    public static Resultado<T> Falha(string erro) => Falha(TipoErro.Validacao, erro);

    public static Resultado<T> Falha(TipoErro tipo, string erro)
    {
        if (string.IsNullOrWhiteSpace(erro))
            throw new ArgumentException("A mensagem de erro é obrigatória.", nameof(erro));
        return new(false, default, erro, tipo);
    }
}

/// <summary>Resultado de um caso de uso que não devolve valor (ex.: alterar senha).</summary>
public sealed class Resultado
{
    public bool Sucesso { get; }
    public string? Erro { get; }
    public TipoErro? TipoDoErro { get; }

    private Resultado(bool sucesso, string? erro, TipoErro? tipo)
    {
        Sucesso = sucesso;
        Erro = erro;
        TipoDoErro = tipo;
    }

    public static Resultado Ok() => new(true, null, null);

    public static Resultado Falha(string erro) => Falha(TipoErro.Validacao, erro);

    public static Resultado Falha(TipoErro tipo, string erro)
    {
        if (string.IsNullOrWhiteSpace(erro))
            throw new ArgumentException("A mensagem de erro é obrigatória.", nameof(erro));
        return new(false, erro, tipo);
    }
}
