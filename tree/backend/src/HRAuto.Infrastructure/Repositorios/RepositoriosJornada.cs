using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HRAuto.Infrastructure.Repositorios;

/// <summary>Ponto (RF17 a RF21). O dia atual é rastreado para a batida ser gravada por UPDATE; o espelho é somente leitura.</summary>
public sealed class PontoRepositorio : IPontoRepositorio
{
    private readonly HrAutoDbContext _db;

    public PontoRepositorio(HrAutoDbContext db) => _db = db;

    public Task<Ponto?> ObterDoDiaAsync(int idFuncionario, DateOnly data, CancellationToken cancelamento = default) =>
        _db.Pontos.FirstOrDefaultAsync(p => p.IdFuncionario == idFuncionario && p.Data == data, cancelamento);

    public async Task<IReadOnlyList<Ponto>> ListarPeriodoAsync(
        int idFuncionario, DateOnly inicio, DateOnly fim, CancellationToken cancelamento = default) =>
        await _db.Pontos
            .AsNoTracking()
            .Where(p => p.IdFuncionario == idFuncionario && p.Data >= inicio && p.Data <= fim)
            .OrderBy(p => p.Data)
            .ToListAsync(cancelamento);

    public void Adicionar(Ponto ponto) => _db.Pontos.Add(ponto);
}

public sealed class FeriadoRepositorio : IFeriadoRepositorio
{
    private readonly HrAutoDbContext _db;

    public FeriadoRepositorio(HrAutoDbContext db) => _db = db;

    public async Task<IReadOnlyList<Feriado>> ListarPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken cancelamento = default) =>
        await _db.Feriados
            .AsNoTracking()
            .Where(f => f.Data >= inicio && f.Data <= fim)
            .OrderBy(f => f.Data)
            .ToListAsync(cancelamento);

    public Task<Feriado?> ObterParaEdicaoAsync(int idFeriado, CancellationToken cancelamento = default) =>
        _db.Feriados.FirstOrDefaultAsync(f => f.Id == idFeriado, cancelamento);

    public Task<bool> ExisteAsync(DateOnly data, string tipo, string? cidade, string? estado, CancellationToken cancelamento = default)
    {
        // Cidade e estado já chegam normalizados; a comparação ignora maiúsculas para a cidade.
        var cidadeMinuscula = cidade?.ToLower();
        return _db.Feriados.AnyAsync(
            f => f.Data == data && f.Tipo == tipo && f.Estado == estado &&
                 (cidadeMinuscula == null ? f.Cidade == null : f.Cidade != null && f.Cidade.ToLower() == cidadeMinuscula),
            cancelamento);
    }

    public void Adicionar(Feriado feriado) => _db.Feriados.Add(feriado);

    public void Remover(Feriado feriado) => _db.Feriados.Remove(feriado);
}

public sealed class FeriasRepositorio : IFeriasRepositorio
{
    private readonly HrAutoDbContext _db;

    public FeriasRepositorio(HrAutoDbContext db) => _db = db;

    public async Task<IReadOnlyList<Ferias>> ListarAprovadasNoPeriodoAsync(
        int idFuncionario, DateOnly inicio, DateOnly fim, CancellationToken cancelamento = default) =>
        await _db.Ferias
            .AsNoTracking()
            .Where(f => f.IdFuncionario == idFuncionario
                        && f.Status == StatusSolicitacao.Aprovada
                        && f.DataInicio <= fim
                        && f.DataFim >= inicio)
            .ToListAsync(cancelamento);

    public async Task<IReadOnlyList<Ferias>> ListarAtivasDoFuncionarioAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        await _db.Ferias
            .AsNoTracking()
            .Where(f => f.IdFuncionario == idFuncionario
                        && (f.Status == StatusSolicitacao.Pendente || f.Status == StatusSolicitacao.Aprovada))
            .ToListAsync(cancelamento);

    public Task<Ferias?> ObterParaEdicaoAsync(int idFerias, CancellationToken cancelamento = default) =>
        _db.Ferias.FirstOrDefaultAsync(f => f.Id == idFerias, cancelamento);

    public async Task<ResultadoPaginado<Ferias>> ListarAsync(FiltroFerias filtro, CancellationToken cancelamento = default)
    {
        var consulta = _db.Ferias.AsNoTracking().AsQueryable();
        if (filtro.IdFuncionario is { } id) consulta = consulta.Where(f => f.IdFuncionario == id);
        if (filtro.IdGestor is { } gestor)
            consulta = consulta.Where(f => _db.Funcionarios.Any(x => x.Id == f.IdFuncionario && x.IdGestor == gestor));
        if (filtro.Status is { } status) consulta = consulta.Where(f => f.Status == status);

        var total = await consulta.CountAsync(cancelamento);
        var itens = await consulta
            .OrderByDescending(f => f.DataSolicitacao).ThenByDescending(f => f.Id)
            .Skip(filtro.Paginacao.Pular)
            .Take(filtro.Paginacao.Tamanho)
            .ToListAsync(cancelamento);

        return new ResultadoPaginado<Ferias>(itens, total);
    }

    public void Adicionar(Ferias ferias) => _db.Ferias.Add(ferias);

    public void Remover(Ferias ferias) => _db.Ferias.Remove(ferias);
}

public sealed class BancoHorasRepositorio : IBancoHorasRepositorio
{
    private readonly HrAutoDbContext _db;

    public BancoHorasRepositorio(HrAutoDbContext db) => _db = db;

    public async Task<IReadOnlyList<BancoHoras>> ListarDoDiaAsync(
        int idFuncionario, DateOnly data, CancellationToken cancelamento = default) =>
        await _db.BancoHoras
            .AsNoTracking()
            .Where(b => b.IdFuncionario == idFuncionario && b.Data == data)
            .ToListAsync(cancelamento);

    public async Task<IReadOnlyList<BancoHoras>> ListarTodosAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        await _db.BancoHoras
            .AsNoTracking()
            .Where(b => b.IdFuncionario == idFuncionario)
            .ToListAsync(cancelamento);

    public async Task<IReadOnlyList<BancoHoras>> ListarPeriodoAsync(
        int idFuncionario, DateOnly inicio, DateOnly fim, CancellationToken cancelamento = default) =>
        await _db.BancoHoras
            .AsNoTracking()
            .Where(b => b.IdFuncionario == idFuncionario && b.Data >= inicio && b.Data <= fim)
            .OrderByDescending(b => b.Data).ThenByDescending(b => b.Id)
            .ToListAsync(cancelamento);

    public void Adicionar(BancoHoras lancamento) => _db.BancoHoras.Add(lancamento);
}

public sealed class NotificacaoRepositorio : INotificacaoRepositorio
{
    private readonly HrAutoDbContext _db;

    public NotificacaoRepositorio(HrAutoDbContext db) => _db = db;

    public void Adicionar(Notificacao notificacao) => _db.Notificacoes.Add(notificacao);

    public async Task<ResultadoPaginado<Notificacao>> ListarAsync(
        int idFuncionario, bool somenteNaoLidas, Paginacao paginacao, CancellationToken cancelamento = default)
    {
        var consulta = _db.Notificacoes.AsNoTracking().Where(n => n.IdFuncionario == idFuncionario);
        if (somenteNaoLidas) consulta = consulta.Where(n => !n.Lida);

        var total = await consulta.CountAsync(cancelamento);
        var itens = await consulta
            .OrderByDescending(n => n.DataEnvio).ThenByDescending(n => n.Id)
            .Skip(paginacao.Pular)
            .Take(paginacao.Tamanho)
            .ToListAsync(cancelamento);

        return new ResultadoPaginado<Notificacao>(itens, total);
    }

    public Task<int> ContarNaoLidasAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        _db.Notificacoes.CountAsync(n => n.IdFuncionario == idFuncionario && !n.Lida, cancelamento);

    public Task<Notificacao?> ObterParaEdicaoAsync(int idNotificacao, CancellationToken cancelamento = default) =>
        _db.Notificacoes.FirstOrDefaultAsync(n => n.Id == idNotificacao, cancelamento);

    public async Task<IReadOnlyList<Notificacao>> ListarNaoLidasParaEdicaoAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        await _db.Notificacoes.Where(n => n.IdFuncionario == idFuncionario && !n.Lida).ToListAsync(cancelamento);
}
