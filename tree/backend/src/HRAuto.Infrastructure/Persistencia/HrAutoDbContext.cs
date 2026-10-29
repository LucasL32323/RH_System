using System.Text.RegularExpressions;
using HRAuto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HRAuto.Infrastructure.Persistencia;

/// <summary>
/// Contexto do EF Core. Mapeia as 25 tabelas do modelo físico oficial (database/modelo_fisico_sqlite.sql).
/// Convenção de nomes: classe LogAuditoria -> tabela LOG_AUDITORIA; propriedade DataNascimento -> coluna data_nascimento;
/// Id -> id_&lt;tabela&gt;. Exceções ficam nas classes de Configuracoes.
/// </summary>
public class HrAutoDbContext : DbContext
{
    public HrAutoDbContext(DbContextOptions<HrAutoDbContext> opcoes) : base(opcoes) { }

    // --- Acesso e estrutura (M1/M2) ---
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Funcionario> Funcionarios => Set<Funcionario>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<LogAuditoria> LogsAuditoria => Set<LogAuditoria>();
    public DbSet<FuncionarioDetalhes> FuncionariosDetalhes => Set<FuncionarioDetalhes>();

    // --- Ponto, férias e holerites (M3/M4) ---
    public DbSet<Ponto> Pontos => Set<Ponto>();
    public DbSet<ManutencaoPonto> ManutencoesPonto => Set<ManutencaoPonto>();
    public DbSet<BancoHoras> BancoHoras => Set<BancoHoras>();
    public DbSet<Feriado> Feriados => Set<Feriado>();
    public DbSet<Ferias> Ferias => Set<Ferias>();
    public DbSet<Holerite> Holerites => Set<Holerite>();

    // --- Comunicação e disciplina (M5) + benefícios ---
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<Aviso> Avisos => Set<Aviso>();
    public DbSet<AvisoFuncionario> AvisosFuncionarios => Set<AvisoFuncionario>();
    public DbSet<Advertencia> Advertencias => Set<Advertencia>();
    public DbSet<Beneficio> Beneficios => Set<Beneficio>();
    public DbSet<FuncionarioBeneficio> FuncionariosBeneficios => Set<FuncionarioBeneficio>();

    // --- Treinamentos e formulários (M6) ---
    public DbSet<Treinamento> Treinamentos => Set<Treinamento>();
    public DbSet<TreinamentoFuncionario> TreinamentosFuncionarios => Set<TreinamentoFuncionario>();
    public DbSet<Certificado> Certificados => Set<Certificado>();
    public DbSet<Formulario> Formularios => Set<Formulario>();
    public DbSet<Pergunta> Perguntas => Set<Pergunta>();
    public DbSet<Resposta> Respostas => Set<Resposta>();

    // Chaves primárias cujo nome não segue "id_<tabela em minúsculas>".
    private static readonly Dictionary<string, string> ChavesEspeciais = new()
    {
        ["LOG_AUDITORIA"] = "id_log",
        ["MANUTENCAO_PONTO"] = "id_manutencao"
    };

    protected override void ConfigureConventions(ModelConfigurationBuilder configuracao)
    {
        // Datas/horas (DateTime) vão como texto ISO 8601; DateOnly e TimeOnly já são ISO no provider SQLite.
        configuracao.Properties<DateTime>().HaveConversion<ConversorDataHora>();
    }

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        // 1) Convenção de nomes (tabelas e colunas em snake_case)...
        foreach (var entidade in modelo.Model.GetEntityTypes())
        {
            var tabela = ParaSnake(entidade.ClrType.Name).ToUpperInvariant();
            entidade.SetTableName(tabela);

            foreach (var propriedade in entidade.GetProperties())
            {
                var coluna = propriedade.Name == "Id"
                    ? ChavesEspeciais.GetValueOrDefault(tabela, "id_" + tabela.ToLowerInvariant())
                    : ParaSnake(propriedade.Name);
                propriedade.SetColumnName(coluna);
            }
        }

        // 2) ...depois as configurações explícitas, que vencem a convenção quando houver conflito.
        modelo.ApplyConfigurationsFromAssembly(typeof(HrAutoDbContext).Assembly);

        // 3) Nenhuma exclusão em cascata: o modelo físico não tem ON DELETE e batidas de ponto nunca são excluídas (RNF07).
        foreach (var fk in modelo.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }

    private static string ParaSnake(string nome) =>
        Regex.Replace(nome, "(?<=[a-z0-9])(?=[A-Z])", "_").ToLowerInvariant();
}
