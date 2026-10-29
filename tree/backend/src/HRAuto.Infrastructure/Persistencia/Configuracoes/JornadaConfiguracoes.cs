using HRAuto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRAuto.Infrastructure.Persistencia.Configuracoes;

/// <summary>Módulos M3/M4: ponto, banco de horas, férias e holerites.</summary>
public sealed class PontoConfiguracao : IEntityTypeConfiguration<Ponto>
{
    public void Configure(EntityTypeBuilder<Ponto> b)
    {
        b.HasOne<Funcionario>().WithMany().HasForeignKey(p => p.IdFuncionario);

        // RNF09: um único registro de ponto por funcionário por dia.
        // Substitui o índice não único idx_ponto_func_data do modelo físico (mesmas colunas).
        b.HasIndex(p => new { p.IdFuncionario, p.Data }).IsUnique().HasDatabaseName("ux_ponto_func_data");
    }
}

public sealed class ManutencaoPontoConfiguracao : IEntityTypeConfiguration<ManutencaoPonto>
{
    public void Configure(EntityTypeBuilder<ManutencaoPonto> b)
    {
        b.HasOne<Ponto>().WithMany().HasForeignKey(m => m.IdPonto);
        b.HasOne<Funcionario>().WithMany().HasForeignKey(m => m.IdFuncionario);
        b.HasOne<Funcionario>().WithMany().HasForeignKey(m => m.IdResponsavel);
    }
}

public sealed class BancoHorasConfiguracao : IEntityTypeConfiguration<BancoHoras>
{
    public void Configure(EntityTypeBuilder<BancoHoras> b) =>
        b.HasOne<Funcionario>().WithMany().HasForeignKey(x => x.IdFuncionario);
}

public sealed class FeriasConfiguracao : IEntityTypeConfiguration<Ferias>
{
    public void Configure(EntityTypeBuilder<Ferias> b)
    {
        b.HasOne<Funcionario>().WithMany().HasForeignKey(f => f.IdFuncionario);
        b.HasIndex(f => f.IdFuncionario).HasDatabaseName("idx_ferias_func");
    }
}

public sealed class HoleriteConfiguracao : IEntityTypeConfiguration<Holerite>
{
    public void Configure(EntityTypeBuilder<Holerite> b)
    {
        b.HasOne<Funcionario>().WithMany().HasForeignKey(h => h.IdFuncionario);
        b.HasIndex(h => new { h.IdFuncionario, h.Ano, h.Mes }).HasDatabaseName("idx_holerite_func");
    }
}
