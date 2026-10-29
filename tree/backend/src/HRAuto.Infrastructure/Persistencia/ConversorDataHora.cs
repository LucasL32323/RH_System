using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HRAuto.Infrastructure.Persistencia;

/// <summary>
/// Grava DateTime como texto ISO 8601 sem fração (yyyy-MM-ddTHH:mm:ss), no horário de Brasília (RNF11/RNF15).
/// O formato permite ordenar e filtrar por intervalo direto no SQLite (comparação de texto).
/// </summary>
public sealed class ConversorDataHora : ValueConverter<DateTime, string>
{
    private const string Formato = "yyyy-MM-dd'T'HH:mm:ss";

    public ConversorDataHora()
        : base(
            valor => valor.ToString(Formato, CultureInfo.InvariantCulture),
            texto => DateTime.ParseExact(texto, Formato, CultureInfo.InvariantCulture, DateTimeStyles.None))
    {
    }
}
