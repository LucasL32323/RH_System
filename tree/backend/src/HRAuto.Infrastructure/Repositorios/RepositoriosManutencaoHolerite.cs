using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Entities;
using HRAuto.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace HRAuto.Infrastructure.Repositorios;

/// <summary>Manutenção de ponto (RF22, RF23). A lista junta o pedido ao ponto do dia; o resto é por id.</summary>
public sealed class ManutencaoPontoRepositorio : IManutencaoPontoRepositorio
{
    private readonly HrAutoDbContext _db;

    public ManutencaoPontoRepositorio(HrAutoDbContext db) => _db = db;

    public async Task<ResultadoPaginado<ManutencaoComPonto>> ListarAsync(
        FiltroManutencoes filtro, CancellationToken cancelamento = default)
    {
        var consulta =
            from m in _db.ManutencoesPonto.AsNoTracking()
            join p in _db.Pontos.AsNoTracking() on m.IdPonto equals p.Id
            select new { Pedido = m, Ponto = p };

        if (filtro.IdFuncionario is { } id) consulta = consulta.Where(x => x.Pedido.IdFuncionario == id);
        if (filtro.IdGestor is { } gestor)
            consulta = consulta.Where(x => _db.Funcionarios.Any(f => f.Id == x.Pedido.IdFuncionario && f.IdGestor == gestor));
        if (filtro.Status is { } status) consulta = consulta.Where(x => x.Pedido.Status == status);

        var total = await consulta.CountAsync(cancelamento);
        var itens = await consulta
            .OrderByDescending(x => x.Pedido.DataSolicitacao).ThenByDescending(x => x.Pedido.Id)
            .Skip(filtro.Paginacao.Pular)
            .Take(filtro.Paginacao.Tamanho)
            .ToListAsync(cancelamento);

        return new ResultadoPaginado<ManutencaoComPonto>(
            itens.Select(x => new ManutencaoComPonto(x.Pedido, x.Ponto)).ToList(), total);
    }

    public Task<ManutencaoPonto?> ObterParaEdicaoAsync(int idManutencao, CancellationToken cancelamento = default) =>
        _db.ManutencoesPonto.FirstOrDefaultAsync(m => m.Id == idManutencao, cancelamento);

    public async Task<IReadOnlyList<ManutencaoPonto>> ListarDoPontoAsync(int idPonto, CancellationToken cancelamento = default) =>
        await _db.ManutencoesPonto.AsNoTracking().Where(m => m.IdPonto == idPonto).ToListAsync(cancelamento);

    public Task<Ponto?> ObterPontoParaEdicaoAsync(int idPonto, CancellationToken cancelamento = default) =>
        _db.Pontos.FirstOrDefaultAsync(p => p.Id == idPonto, cancelamento);

    public void Adicionar(ManutencaoPonto pedido) => _db.ManutencoesPonto.Add(pedido);
}

/// <summary>Holerites (RF29, RF30): só metadados; o PDF fica na pasta do servidor.</summary>
public sealed class HoleriteRepositorio : IHoleriteRepositorio
{
    private readonly HrAutoDbContext _db;

    public HoleriteRepositorio(HrAutoDbContext db) => _db = db;

    public Task<Holerite?> ObterAsync(int idHolerite, CancellationToken cancelamento = default) =>
        _db.Holerites.AsNoTracking().FirstOrDefaultAsync(h => h.Id == idHolerite, cancelamento);

    public Task<Holerite?> ObterDaCompetenciaParaEdicaoAsync(
        int idFuncionario, int ano, int mes, CancellationToken cancelamento = default) =>
        _db.Holerites.FirstOrDefaultAsync(h => h.IdFuncionario == idFuncionario && h.Ano == ano && h.Mes == mes, cancelamento);

    public async Task<IReadOnlyList<Holerite>> ListarAsync(int idFuncionario, int? ano, CancellationToken cancelamento = default)
    {
        var consulta = _db.Holerites.AsNoTracking().Where(h => h.IdFuncionario == idFuncionario);
        if (ano is { } a) consulta = consulta.Where(h => h.Ano == a);
        return await consulta.OrderByDescending(h => h.Ano).ThenByDescending(h => h.Mes).ThenByDescending(h => h.Id)
            .ToListAsync(cancelamento);
    }

    public void Adicionar(Holerite holerite) => _db.Holerites.Add(holerite);
}
