namespace HRAuto.Application.Common;

/// <summary>Pedido de página de uma listagem. Padrão: página 1 com 20 itens; máximo de 100 por página.</summary>
public sealed record Paginacao
{
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;

    public int Pagina { get; }
    public int Tamanho { get; }

    /// <summary>Quantos itens pular antes da página pedida.</summary>
    public int Pular => (Pagina - 1) * Tamanho;

    private Paginacao(int pagina, int tamanho)
    {
        Pagina = pagina;
        Tamanho = tamanho;
    }

    /// <summary>Corrige valores ausentes ou fora da faixa em vez de falhar (página mínima 1, tamanho entre 1 e 100).</summary>
    public static Paginacao De(int? pagina, int? tamanho) =>
        new(Math.Max(1, pagina ?? 1), Math.Clamp(tamanho ?? TamanhoPadrao, 1, TamanhoMaximo));
}

/// <summary>Resposta paginada enviada ao app.</summary>
public sealed record PaginaDto<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, int Total)
{
    public int TotalPaginas => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamanho);

    public static PaginaDto<T> De(IReadOnlyList<T> itens, Paginacao paginacao, int total) =>
        new(itens, paginacao.Pagina, paginacao.Tamanho, total);
}
