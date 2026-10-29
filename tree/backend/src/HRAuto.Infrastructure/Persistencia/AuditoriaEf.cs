using HRAuto.Application.Abstractions;
using HRAuto.Domain.Entities;

namespace HRAuto.Infrastructure.Persistencia;

/// <summary>
/// Log de auditoria (RF08, RNF07). Apenas adiciona o registro ao contexto; ele é gravado no mesmo
/// SaveChanges das demais mudanças. Nunca recebe senha, hash nem token (o chamador só passa texto seguro).
/// </summary>
public sealed class AuditoriaEf : IAuditoria
{
    private const int TamanhoMaximoDescricao = 500;

    private readonly HrAutoDbContext _db;
    private readonly IRelogio _relogio;

    public AuditoriaEf(HrAutoDbContext db, IRelogio relogio)
    {
        _db = db;
        _relogio = relogio;
    }

    public void Registrar(int idUsuario, string acao, string? tabela, int? idRegistro, string? descricao)
    {
        _db.LogsAuditoria.Add(new LogAuditoria
        {
            IdUsuario = idUsuario,
            DataHora = _relogio.Agora().DateTime, // horário de Brasília, sem fuso (ISO 8601 local, como as demais datas)
            Acao = acao,
            Tabela = tabela,
            IdRegistro = idRegistro,
            Descricao = descricao is { Length: > TamanhoMaximoDescricao } ? descricao[..TamanhoMaximoDescricao] : descricao
        });
    }
}
