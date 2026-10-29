using HRAuto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRAuto.Infrastructure.Persistencia.Configuracoes;

/// <summary>Módulo M6: treinamentos, certificados e formulários.</summary>
public sealed class TreinamentoFuncionarioConfiguracao : IEntityTypeConfiguration<TreinamentoFuncionario>
{
    public void Configure(EntityTypeBuilder<TreinamentoFuncionario> b)
    {
        b.HasKey(x => new { x.IdTreinamento, x.IdFuncionario });
        b.HasOne<Treinamento>().WithMany().HasForeignKey(x => x.IdTreinamento);
        b.HasOne<Funcionario>().WithMany().HasForeignKey(x => x.IdFuncionario);
    }
}

public sealed class CertificadoConfiguracao : IEntityTypeConfiguration<Certificado>
{
    public void Configure(EntityTypeBuilder<Certificado> b)
    {
        b.HasOne<Treinamento>().WithMany().HasForeignKey(c => c.IdTreinamento);
        b.HasOne<Funcionario>().WithMany().HasForeignKey(c => c.IdFuncionario);

        // RF39: código de validação único, para consulta de autenticidade (RF40).
        b.HasIndex(c => c.CodigoValidacao).IsUnique().HasDatabaseName("ux_certificado_codigo");
    }
}

public sealed class FormularioConfiguracao : IEntityTypeConfiguration<Formulario>
{
    public void Configure(EntityTypeBuilder<Formulario> b) =>
        b.HasOne<Funcionario>().WithMany().HasForeignKey(f => f.IdAutor);
}

public sealed class PerguntaConfiguracao : IEntityTypeConfiguration<Pergunta>
{
    public void Configure(EntityTypeBuilder<Pergunta> b)
    {
        b.Property(p => p.Enunciado).HasColumnName("pergunta"); // a classe se chama Pergunta; a coluna também
        b.HasOne<Formulario>().WithMany().HasForeignKey(p => p.IdFormulario);
    }
}

public sealed class RespostaConfiguracao : IEntityTypeConfiguration<Resposta>
{
    public void Configure(EntityTypeBuilder<Resposta> b)
    {
        b.Property(r => r.Conteudo).HasColumnName("resposta");
        b.HasOne<Pergunta>().WithMany().HasForeignKey(r => r.IdPergunta);
        // id_funcionario nulo = resposta anônima (RNF08).
        b.HasOne<Funcionario>().WithMany().HasForeignKey(r => r.IdFuncionario);
    }
}
