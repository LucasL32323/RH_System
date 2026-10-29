using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Calendario;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Application.Calendario;

// ---------- Entrada ----------

/// <summary>Tipo: Nacional, Estadual ou Municipal. Estado em sigla (SP).</summary>
public sealed record CriarFeriadoDto(DateOnly? Data, string? Nome, string? Tipo, string? Cidade, string? Estado);

// ---------- Saída ----------

public sealed record FeriadoDto(int Id, DateOnly Data, string Nome, string Tipo, string? Cidade, string? Estado);

internal static class MapeadorFeriados
{
    public static FeriadoDto Feriado(Feriado f) => new(f.Id, f.Data, f.Nome, f.Tipo, f.Cidade, f.Estado);
}

/// <summary>RF28: feriados de um ano (padrão: o ano atual), por data. Qualquer pessoa logada pode consultar.</summary>
public sealed class ListarFeriados
{
    public const int AnoMinimo = 2000;
    public const int AnoMaximo = 2100;

    private readonly IFeriadoRepositorio _feriados;
    private readonly IRelogio _relogio;

    public ListarFeriados(IFeriadoRepositorio feriados, IRelogio relogio)
    {
        _feriados = feriados;
        _relogio = relogio;
    }

    public async Task<Resultado<IReadOnlyList<FeriadoDto>>> ExecutarAsync(int? ano, CancellationToken cancelamento = default)
    {
        var anoConsulta = ano ?? _relogio.Hoje().Year;
        if (anoConsulta < AnoMinimo || anoConsulta > AnoMaximo)
            return Resultado<IReadOnlyList<FeriadoDto>>.Falha($"Informe um ano entre {AnoMinimo} e {AnoMaximo}.");

        var feriados = await _feriados.ListarPeriodoAsync(new DateOnly(anoConsulta, 1, 1), new DateOnly(anoConsulta, 12, 31), cancelamento);
        return Resultado<IReadOnlyList<FeriadoDto>>.Ok(
            feriados.OrderBy(f => f.Data).ThenBy(f => f.Nome).Select(MapeadorFeriados.Feriado).ToList());
    }
}

/// <summary>RF28: cadastro e exclusão de feriados. Somente RH e Admin; toda alteração fica no log de auditoria.</summary>
public sealed class GerenciarFeriados
{
    private readonly IFeriadoRepositorio _feriados;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;

    public GerenciarFeriados(IFeriadoRepositorio feriados, IAuditoria auditoria, IUnidadeDeTrabalho unidade)
    {
        _feriados = feriados;
        _auditoria = auditoria;
        _unidade = unidade;
    }

    public async Task<Resultado<FeriadoDto>> CriarAsync(Solicitante solicitante, CriarFeriadoDto? dto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<FeriadoDto>.Falha(TipoErro.Proibido, "Apenas o RH pode cadastrar feriados.");
        if (dto is null || dto.Data is null)
            return Resultado<FeriadoDto>.Falha("Informe a data do feriado.");
        if (dto.Data.Value.Year < ListarFeriados.AnoMinimo || dto.Data.Value.Year > ListarFeriados.AnoMaximo)
            return Resultado<FeriadoDto>.Falha($"Informe uma data entre os anos {ListarFeriados.AnoMinimo} e {ListarFeriados.AnoMaximo}.");

        Feriado feriado;
        try
        {
            feriado = RegrasFeriado.Criar(dto.Data.Value, dto.Nome, dto.Tipo, dto.Cidade, dto.Estado);
        }
        catch (DominioException e)
        {
            return Resultado<FeriadoDto>.Falha(e.Message);
        }

        if (await _feriados.ExisteAsync(feriado.Data, feriado.Tipo, feriado.Cidade, feriado.Estado, cancelamento))
            return Resultado<FeriadoDto>.Falha(TipoErro.Conflito, "Já existe um feriado igual nesta data.");

        _feriados.Adicionar(feriado);

        await using var transacao = await _unidade.IniciarTransacaoAsync(cancelamento);
        await _unidade.SalvarAsync(cancelamento); // gera o id do feriado
        _auditoria.Registrar(solicitante.IdUsuario, "CriarFeriado", "FERIADO", feriado.Id,
            $"{feriado.Tipo} {feriado.Data:yyyy-MM-dd}: {feriado.Nome}");
        await _unidade.SalvarAsync(cancelamento);
        await transacao.ConfirmarAsync(cancelamento);

        return Resultado<FeriadoDto>.Ok(MapeadorFeriados.Feriado(feriado));
    }

    public async Task<Resultado> ExcluirAsync(Solicitante solicitante, int idFeriado, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado.Falha(TipoErro.Proibido, "Apenas o RH pode excluir feriados.");

        var feriado = await _feriados.ObterParaEdicaoAsync(idFeriado, cancelamento);
        if (feriado is null)
            return Resultado.Falha(TipoErro.NaoEncontrado, "Feriado não encontrado.");

        _feriados.Remover(feriado);
        _auditoria.Registrar(solicitante.IdUsuario, "ExcluirFeriado", "FERIADO", feriado.Id,
            $"{feriado.Tipo} {feriado.Data:yyyy-MM-dd}: {feriado.Nome}");
        await _unidade.SalvarAsync(cancelamento);

        return Resultado.Ok();
    }
}
