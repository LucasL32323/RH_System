using HRAuto.Application.Abstractions;
using HRAuto.Domain.Holerites;
using Microsoft.Extensions.Options;

namespace HRAuto.Infrastructure.Armazenamento;

/// <summary>Seção "Holerites" do appsettings. A pasta é relativa ao diretório de execução (como o banco SQLite).</summary>
public sealed class OpcoesHolerites
{
    public const string Secao = "Holerites";

    /// <summary>Pasta raiz dos PDFs. Fica fora do wwwroot: os arquivos só saem pela API, com autenticação (RNF08).</summary>
    public string Pasta { get; set; } = Path.Combine("dados", "holerites");
}

/// <summary>
/// Guarda os PDFs dos holerites em disco (RNF17). Os nomes são gerados pelo sistema (<see cref="RegrasHolerite.MontarCaminho"/>),
/// nunca pelo usuário, e todo caminho é conferido duas vezes antes de tocar no disco: formato permitido e dentro da pasta raiz.
/// </summary>
public sealed class ArmazenamentoHoleritesEmDisco : IArmazenamentoHolerites
{
    private readonly string _raiz;

    public ArmazenamentoHoleritesEmDisco(IOptions<OpcoesHolerites> opcoes)
    {
        var pasta = string.IsNullOrWhiteSpace(opcoes.Value.Pasta) ? new OpcoesHolerites().Pasta : opcoes.Value.Pasta;
        _raiz = Path.GetFullPath(pasta);
    }

    public async Task<string> SalvarAsync(
        int idFuncionario, int ano, int mes, ReadOnlyMemory<byte> conteudo, CancellationToken cancelamento = default)
    {
        var relativo = RegrasHolerite.MontarCaminho(idFuncionario, ano, mes, Guid.NewGuid());
        var completo = Resolver(relativo) ?? throw new InvalidOperationException("Caminho de holerite inválido.");

        Directory.CreateDirectory(Path.GetDirectoryName(completo)!);
        try
        {
            // CreateNew: nunca sobrescreve um arquivo existente.
            await using var arquivo = new FileStream(
                completo, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await arquivo.WriteAsync(conteudo, cancelamento);
        }
        catch
        {
            try { File.Delete(completo); } catch (IOException) { /* arquivo parcial fica; o erro original é repassado */ }
            throw;
        }

        return relativo;
    }

    public async Task<byte[]?> LerAsync(string caminhoRelativo, CancellationToken cancelamento = default)
    {
        var completo = Resolver(caminhoRelativo);
        if (completo is null || !File.Exists(completo)) return null;
        return await File.ReadAllBytesAsync(completo, cancelamento);
    }

    public Task RemoverAsync(string caminhoRelativo, CancellationToken cancelamento = default)
    {
        var completo = Resolver(caminhoRelativo);
        if (completo is not null && File.Exists(completo)) File.Delete(completo);
        return Task.CompletedTask;
    }

    // Caminho absoluto, ou nulo se o formato não é o permitido ou se escapa da pasta raiz.
    private string? Resolver(string caminhoRelativo)
    {
        if (!RegrasHolerite.CaminhoEhSeguro(caminhoRelativo)) return null;

        var completo = Path.GetFullPath(Path.Combine(_raiz, caminhoRelativo));
        var raizComSeparador = _raiz.EndsWith(Path.DirectorySeparatorChar) ? _raiz : _raiz + Path.DirectorySeparatorChar;
        return completo.StartsWith(raizComSeparador, StringComparison.Ordinal) ? completo : null;
    }
}
