using HRAuto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRAuto.Infrastructure.Persistencia.Configuracoes;

/// <summary>Módulos M1/M2: perfis, funcionários, usuários, auditoria e documentos.</summary>
public sealed class FuncionarioConfiguracao : IEntityTypeConfiguration<Funcionario>
{
    public void Configure(EntityTypeBuilder<Funcionario> b)
    {
        b.HasOne(f => f.Cargo).WithMany().HasForeignKey(f => f.IdCargo);
        b.HasOne(f => f.Departamento).WithMany().HasForeignKey(f => f.IdDepartamento);
        b.HasOne(f => f.Gestor).WithMany().HasForeignKey(f => f.IdGestor); // auto-relacionamento
    }
}

public sealed class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.HasOne(u => u.Funcionario).WithMany().HasForeignKey(u => u.IdFuncionario);
        b.HasOne(u => u.Perfil).WithMany().HasForeignKey(u => u.IdPerfil);

        // E-mail do login: único e sem diferenciar maiúsculas/minúsculas.
        b.Property(u => u.Email).UseCollation("NOCASE");
        b.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ux_usuario_email");
    }
}

public sealed class LogAuditoriaConfiguracao : IEntityTypeConfiguration<LogAuditoria>
{
    public void Configure(EntityTypeBuilder<LogAuditoria> b)
    {
        b.HasOne<Usuario>().WithMany().HasForeignKey(l => l.IdUsuario);
        b.HasIndex(l => new { l.IdUsuario, l.DataHora }).HasDatabaseName("idx_log_usuario");
    }
}

public sealed class FuncionarioDetalhesConfiguracao : IEntityTypeConfiguration<FuncionarioDetalhes>
{
    public void Configure(EntityTypeBuilder<FuncionarioDetalhes> b)
    {
        // Relação 1:1: a PK é o próprio id_funcionario (não é autoincremento).
        b.HasKey(d => d.IdFuncionario);
        b.Property(d => d.IdFuncionario).ValueGeneratedNever();
        b.HasOne<Funcionario>().WithOne().HasForeignKey<FuncionarioDetalhes>(d => d.IdFuncionario);
    }
}
