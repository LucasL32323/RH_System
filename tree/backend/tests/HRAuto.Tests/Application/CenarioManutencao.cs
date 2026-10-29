using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Application.Holerites;
using HRAuto.Application.ManutencaoDePonto;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

// Dublês e atalhos da Fase 4 (parte 4): manutenção de ponto e holerites.
// Usa a empresa de CenarioRh: Admin (1), RH (2), gestor (3) com Ana (4) e Bruno (5) na equipe, e Carla (6) fora dela.

internal sealed class ManutencaoPontoRepositorioFake : IManutencaoPontoRepositorio
{
    private readonly BancoFake _banco;

    public ManutencaoPontoRepositorioFake(BancoFake banco) => _banco = banco;

    public Task<ResultadoPaginado<ManutencaoComPonto>> ListarAsync(FiltroManutencoes filtro, CancellationToken cancelamento = default)
    {
        var consulta = _banco.ManutencoesPonto.AsEnumerable();
        if (filtro.IdFuncionario is { } id) consulta = consulta.Where(m => m.IdFuncionario == id);
        if (filtro.IdGestor is { } gestor)
            consulta = consulta.Where(m => _banco.Funcionarios.Any(f => f.Id == m.IdFuncionario && f.IdGestor == gestor));
        if (filtro.Status is { } status) consulta = consulta.Where(m => m.Status == status);

        var todas = consulta.OrderByDescending(m => m.DataSolicitacao).ThenByDescending(m => m.Id).ToList();
        var pagina = todas.Skip(filtro.Paginacao.Pular).Take(filtro.Paginacao.Tamanho)
            .Select(m => new ManutencaoComPonto(m, _banco.Pontos.First(p => p.Id == m.IdPonto)))
            .ToList();
        return Task.FromResult(new ResultadoPaginado<ManutencaoComPonto>(pagina, todas.Count));
    }

    public Task<ManutencaoPonto?> ObterParaEdicaoAsync(int idManutencao, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.ManutencoesPonto.FirstOrDefault(m => m.Id == idManutencao));

    public Task<IReadOnlyList<ManutencaoPonto>> ListarDoPontoAsync(int idPonto, CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<ManutencaoPonto>>(_banco.ManutencoesPonto.Where(m => m.IdPonto == idPonto).ToList());

    public Task<Ponto?> ObterPontoParaEdicaoAsync(int idPonto, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Pontos.FirstOrDefault(p => p.Id == idPonto));

    public void Adicionar(ManutencaoPonto pedido) => _banco.ManutencoesPonto.Add(pedido);
}

internal sealed class HoleriteRepositorioFake : IHoleriteRepositorio
{
    private readonly BancoFake _banco;

    public HoleriteRepositorioFake(BancoFake banco) => _banco = banco;

    public Task<Holerite?> ObterAsync(int idHolerite, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Holerites.FirstOrDefault(h => h.Id == idHolerite));

    public Task<Holerite?> ObterDaCompetenciaParaEdicaoAsync(int idFuncionario, int ano, int mes, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Holerites.FirstOrDefault(h => h.IdFuncionario == idFuncionario && h.Ano == ano && h.Mes == mes));

    public Task<IReadOnlyList<Holerite>> ListarAsync(int idFuncionario, int? ano, CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<Holerite>>(_banco.Holerites
            .Where(h => h.IdFuncionario == idFuncionario && (ano is null || h.Ano == ano))
            .OrderByDescending(h => h.Ano).ThenByDescending(h => h.Mes).ThenByDescending(h => h.Id).ToList());

    public void Adicionar(Holerite holerite) => _banco.Holerites.Add(holerite);
}

/// <summary>Pasta de PDFs em memória: caminho relativo -> conteúdo.</summary>
internal sealed class ArmazenamentoHoleritesFalso : IArmazenamentoHolerites
{
    public Dictionary<string, byte[]> Arquivos { get; } = new();
    public int Gravacoes { get; private set; }
    public int Leituras { get; private set; }

    public Task<string> SalvarAsync(int idFuncionario, int ano, int mes, ReadOnlyMemory<byte> conteudo, CancellationToken cancelamento = default)
    {
        Gravacoes++;
        var caminho = HRAuto.Domain.Holerites.RegrasHolerite.MontarCaminho(idFuncionario, ano, mes, Guid.NewGuid());
        Arquivos[caminho] = conteudo.ToArray();
        return Task.FromResult(caminho);
    }

    public Task<byte[]?> LerAsync(string caminhoRelativo, CancellationToken cancelamento = default)
    {
        Leituras++;
        return Task.FromResult(Arquivos.TryGetValue(caminhoRelativo, out var bytes) ? bytes : null);
    }

    public Task RemoverAsync(string caminhoRelativo, CancellationToken cancelamento = default)
    {
        Arquivos.Remove(caminhoRelativo);
        return Task.CompletedTask;
    }
}

internal sealed partial class Cenario
{
    public ArmazenamentoHoleritesFalso Armazenamento { get; } = new();

    private ManutencaoPontoRepositorioFake RepositorioManutencoes => new(Banco);
    private HoleriteRepositorioFake RepositorioHolerites => new(Banco);

    // ---- Dados ----

    /// <summary>Ponto já gravado no banco de teste (use null para batida não feita).</summary>
    public Ponto NovoPontoGravado(Funcionario funcionario, string data, string? entrada, string? saidaIntervalo, string? retorno, string? saida)
    {
        var ponto = new Ponto
        {
            IdFuncionario = funcionario.Id,
            Data = ApoioApp.Data(data),
            Entrada = entrada is null ? null : ApoioApp.Hora(entrada),
            SaidaIntervalo = saidaIntervalo is null ? null : ApoioApp.Hora(saidaIntervalo),
            RetornoIntervalo = retorno is null ? null : ApoioApp.Hora(retorno),
            Saida = saida is null ? null : ApoioApp.Hora(saida)
        }.DefinirId(Banco.Pontos.Count + 100);
        Banco.Pontos.Add(ponto);
        return ponto;
    }

    public ManutencaoPonto NovaManutencao(
        Funcionario funcionario, Ponto ponto, string status = StatusSolicitacao.Pendente, string solicitadaEm = "2026-10-20T09:00:00")
    {
        var pedido = new ManutencaoPonto
        {
            IdPonto = ponto.Id, IdFuncionario = funcionario.Id, Justificativa = "Esqueci de bater a saída do dia",
            DataSolicitacao = DateTime.Parse(solicitadaEm, System.Globalization.CultureInfo.InvariantCulture), Status = status
        }.DefinirId(Banco.ManutencoesPonto.Count + 200);
        Banco.ManutencoesPonto.Add(pedido);
        return pedido;
    }

    public Holerite NovoHolerite(Funcionario funcionario, int mes, int ano, byte[]? conteudo = null, string? caminho = null)
    {
        caminho ??= HRAuto.Domain.Holerites.RegrasHolerite.MontarCaminho(funcionario.Id, ano, mes, Guid.NewGuid());
        if (conteudo is not null) Armazenamento.Arquivos[caminho] = conteudo;
        var holerite = new Holerite
        {
            IdFuncionario = funcionario.Id, Mes = mes, Ano = ano, DataPublicacao = new DateOnly(2026, 10, 5), Arquivo = caminho
        }.DefinirId(Banco.Holerites.Count + 300);
        Banco.Holerites.Add(holerite);
        return holerite;
    }

    // ---- Casos de uso ----

    public SolicitarManutencaoPonto SolicitarManutencao() => new(
        new FuncionarioRepositorioFake(Banco), Pontos, RepositorioManutencoes, UsuariosGestao,
        RepositorioNotificacoes, _auditoria, _unidade, Relogio);

    public ListarManutencoesPonto ListarManutencoes() =>
        new(new FuncionarioRepositorioFake(Banco), FuncionariosCadastro, RepositorioManutencoes);

    public DecidirManutencaoPonto DecidirManutencao() => new(
        RepositorioManutencoes, new FuncionarioRepositorioFake(Banco), FuncionariosCadastro, UsuariosGestao,
        Fechamento(), RepositorioNotificacoes, _auditoria, _unidade, Relogio);

    public PublicarHolerite PublicarHolerite() => new(
        new FuncionarioRepositorioFake(Banco), FuncionariosCadastro, RepositorioHolerites, Armazenamento,
        RepositorioNotificacoes, _auditoria, _unidade, Relogio);

    public ListarHolerites ListarHolerites() =>
        new(new FuncionarioRepositorioFake(Banco), FuncionariosCadastro, RepositorioHolerites);

    public BaixarHolerite BaixarHolerite() => new(RepositorioHolerites, Armazenamento, _auditoria, _unidade);
}
