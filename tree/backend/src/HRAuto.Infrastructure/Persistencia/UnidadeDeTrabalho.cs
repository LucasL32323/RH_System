using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRAuto.Infrastructure.Persistencia;

/// <summary>
/// Unidade de trabalho sobre o <see cref="HrAutoDbContext"/>. Os repositórios, a auditoria e esta classe
/// compartilham o mesmo contexto (escopo da requisição), então um SaveChanges grava tudo junto.
/// </summary>
public sealed class UnidadeDeTrabalho : IUnidadeDeTrabalho
{
    // Códigos estendidos do SQLite para violação de chave única e de chave estrangeira.
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;
    private const int SqliteConstraintForeignKey = 787;

    private readonly HrAutoDbContext _db;

    public UnidadeDeTrabalho(HrAutoDbContext db) => _db = db;

    public async Task SalvarAsync(CancellationToken cancelamento = default)
    {
        try
        {
            await _db.SaveChangesAsync(cancelamento);
        }
        catch (DbUpdateException e) when (e.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: SqliteConstraintUnique or SqliteConstraintPrimaryKey or SqliteConstraintForeignKey
        })
        {
            // Ex.: dois toques simultâneos tentando criar o PONTO do mesmo dia (índice único ux_ponto_func_data, RNF09),
            // dois cadastros com o mesmo e-mail, ou a exclusão de um cargo que acabou de ser usado (chave estrangeira).
            throw new ConflitoDePersistenciaException("Violação de chave única ou estrangeira ao gravar.", e);
        }
    }

    public async Task<ITransacao> IniciarTransacaoAsync(CancellationToken cancelamento = default) =>
        new TransacaoEf(await _db.Database.BeginTransactionAsync(cancelamento));

    private sealed class TransacaoEf : ITransacao
    {
        private readonly IDbContextTransaction _transacao;

        public TransacaoEf(IDbContextTransaction transacao) => _transacao = transacao;

        public Task ConfirmarAsync(CancellationToken cancelamento = default) => _transacao.CommitAsync(cancelamento);

        // Descartar sem confirmar desfaz tudo (rollback).
        public ValueTask DisposeAsync() => _transacao.DisposeAsync();
    }
}
