using HRAuto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRAuto.Infrastructure.Persistencia.Configuracoes;

/// <summary>Módulo M5 e benefícios: notificações, avisos, advertências.</summary>
public sealed class NotificacaoConfiguracao : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> b)
    {
        b.HasOne<Funcionario>().WithMany().HasForeignKey(n => n.IdFuncionario);
        b.HasIndex(n => new { n.IdFuncionario, n.Lida }).HasDatabaseName("idx_notificacao_func");
    }
}

public sealed class AvisoConfiguracao : IEntityTypeConfiguration<Aviso>
{
    public void Configure(EntityTypeBuilder<Aviso> b) =>
        b.HasOne<Funcionario>().WithMany().HasForeignKey(a => a.IdAutor);
}

public sealed class AvisoFuncionarioConfiguracao : IEntityTypeConfiguration<AvisoFuncionario>
{
    public void Configure(EntityTypeBuilder<AvisoFuncionario> b)
    {
        b.HasKey(x => new { x.IdAviso, x.IdFuncionario });
        b.HasOne<Aviso>().WithMany().HasForeignKey(x => x.IdAviso);
        b.HasOne<Funcionario>().WithMany().HasForeignKey(x => x.IdFuncionario);
    }
}

public sealed class AdvertenciaConfiguracao : IEntityTypeConfiguration<Advertencia>
{
    public void Configure(EntityTypeBuilder<Advertencia> b)
    {
        b.HasOne<Funcionario>().WithMany().HasForeignKey(a => a.IdFuncionario);
        b.HasOne<Funcionario>().WithMany().HasForeignKey(a => a.IdResponsavel);
    }
}

public sealed class FuncionarioBeneficioConfiguracao : IEntityTypeConfiguration<FuncionarioBeneficio>
{
    public void Configure(EntityTypeBuilder<FuncionarioBeneficio> b)
    {
        b.HasKey(x => new { x.IdFuncionario, x.IdBeneficio });
        b.HasOne<Funcionario>().WithMany().HasForeignKey(x => x.IdFuncionario);
        b.HasOne<Beneficio>().WithMany().HasForeignKey(x => x.IdBeneficio);
    }
}
