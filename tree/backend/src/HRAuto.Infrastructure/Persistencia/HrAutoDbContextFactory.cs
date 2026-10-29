using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HRAuto.Infrastructure.Persistencia;

/// <summary>
/// Usada só pelas ferramentas em tempo de design (dotnet ef migrations add / database update),
/// para não depender da API estar configurada.
/// </summary>
public sealed class HrAutoDbContextFactory : IDesignTimeDbContextFactory<HrAutoDbContext>
{
    public HrAutoDbContext CreateDbContext(string[] args)
    {
        var opcoes = new DbContextOptionsBuilder<HrAutoDbContext>()
            .UseSqlite(new OpcoesBanco().ConnectionString)
            .Options;
        return new HrAutoDbContext(opcoes);
    }
}
