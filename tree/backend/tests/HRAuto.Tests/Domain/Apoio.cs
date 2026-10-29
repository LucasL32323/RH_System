using System.Globalization;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Domain;

/// <summary>Atalhos usados pelos testes de regras de negócio (datas, horas e entidades prontas).</summary>
internal static class Apoio
{
    public static DateOnly D(string data) => DateOnly.ParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static TimeOnly H(string hora) => TimeOnly.ParseExact(hora, "HH:mm", CultureInfo.InvariantCulture);

    /// <summary>O Id das entidades tem setter protegido (só o banco define); nos testes usamos reflexão.</summary>
    public static T ComId<T>(this T entidade, int id) where T : EntidadeBase
    {
        typeof(EntidadeBase).GetProperty(nameof(EntidadeBase.Id))!.SetValue(entidade, id);
        return entidade;
    }

    /// <summary>Ponto do dia com as batidas informadas; use null para batida não feita.</summary>
    public static Ponto NovoPonto(string data, string? entrada, string? saidaIntervalo, string? retorno, string? saida, int idFuncionario = 1) =>
        new()
        {
            IdFuncionario = idFuncionario,
            Data = D(data),
            Entrada = entrada is null ? null : H(entrada),
            SaidaIntervalo = saidaIntervalo is null ? null : H(saidaIntervalo),
            RetornoIntervalo = retorno is null ? null : H(retorno),
            Saida = saida is null ? null : H(saida)
        };

    public static Ponto PontoCompleto(string data, string entrada, string saidaIntervalo, string retorno, string saida) =>
        NovoPonto(data, entrada, saidaIntervalo, retorno, saida);

    public static Ferias NovasFerias(int idFuncionario, string inicio, string fim, string status) =>
        new()
        {
            IdFuncionario = idFuncionario,
            DataInicio = D(inicio),
            DataFim = D(fim),
            Dias = D(fim).DayNumber - D(inicio).DayNumber + 1,
            Status = status,
            DataSolicitacao = D("2026-10-01")
        };

    public static Advertencia NovaAdvertencia(TipoAdvertencia tipo, string data, string status = StatusAdvertencia.Ativa, int id = 0)
    {
        var a = new Advertencia
        {
            IdFuncionario = 1,
            IdResponsavel = 2,
            Data = D(data),
            Tipo = tipo.ToString(),
            Motivo = "Motivo de teste",
            Status = status
        };
        return id == 0 ? a : a.ComId(id);
    }

    public static readonly DateTime Agora = new(2026, 10, 20, 9, 30, 0);
    public static readonly DateOnly Hoje = new(2026, 10, 20);
}
