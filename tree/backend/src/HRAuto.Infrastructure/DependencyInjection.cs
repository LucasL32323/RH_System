using HRAuto.Application.Abstractions;
using HRAuto.Infrastructure.Armazenamento;
using HRAuto.Infrastructure.Comunicacao;
using HRAuto.Infrastructure.Persistencia;
using HRAuto.Infrastructure.Persistencia.Seed;
using HRAuto.Infrastructure.Repositorios;
using HRAuto.Infrastructure.Seguranca;
using HRAuto.Infrastructure.Tempo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HRAuto.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registra as implementações da camada Infrastructure.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuracao)
    {
        services.Configure<OpcoesBanco>(configuracao.GetSection(OpcoesBanco.Secao));
        services.Configure<OpcoesSeed>(configuracao.GetSection(OpcoesSeed.Secao));
        services.Configure<OpcoesJwt>(configuracao.GetSection(OpcoesJwt.Secao));
        services.Configure<OpcoesEmail>(configuracao.GetSection(OpcoesEmail.Secao));
        services.Configure<OpcoesHolerites>(configuracao.GetSection(OpcoesHolerites.Secao));

        services.AddSingleton<IRelogio>(_ => new RelogioBrasilia());
        services.AddSingleton<IHashSenha>(_ => new HashSenhaBCrypt());
        services.AddSingleton<IServicoToken, ServicoTokenJwt>();

        // Com Email:Host vazio usa o enviador de log. A escolha é feita na primeira vez que o serviço é pedido
        // (não na subida), para que configurações de teste e variáveis de ambiente já estejam carregadas.
        services.AddSingleton<IEnviadorEmail>(provedor =>
        {
            var opcoes = provedor.GetRequiredService<IOptions<OpcoesEmail>>();
            return string.IsNullOrWhiteSpace(opcoes.Value.Host)
                ? ActivatorUtilities.CreateInstance<EnviadorEmailLog>(provedor)
                : ActivatorUtilities.CreateInstance<EnviadorEmailSmtp>(provedor);
        });

        // SQLite via EF Core. A connection string é lida só quando o contexto é criado (facilita os testes).
        services.AddDbContext<HrAutoDbContext>((provedor, opcoes) =>
            opcoes.UseSqlite(provedor.GetRequiredService<IOptions<OpcoesBanco>>().Value.ConnectionString));

        services.AddScoped<IInicializadorBanco, InicializadorBanco>();
        services.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();
        services.AddScoped<IAuditoria, AuditoriaEf>();

        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<IFuncionarioRepositorio, FuncionarioRepositorio>();
        services.AddScoped<IPontoRepositorio, PontoRepositorio>();
        services.AddScoped<IFeriadoRepositorio, FeriadoRepositorio>();
        services.AddScoped<IFeriasRepositorio, FeriasRepositorio>();
        services.AddScoped<IBancoHorasRepositorio, BancoHorasRepositorio>();
        services.AddScoped<INotificacaoRepositorio, NotificacaoRepositorio>();

        // Cadastro e disciplina (Fase 4, parte 2)
        services.AddScoped<IFuncionarioCadastroRepositorio, FuncionarioCadastroRepositorio>();
        services.AddScoped<IUsuarioGestaoRepositorio, UsuarioGestaoRepositorio>();
        services.AddScoped<ICargoRepositorio, CargoRepositorio>();
        services.AddScoped<IDepartamentoRepositorio, DepartamentoRepositorio>();
        services.AddScoped<IAdvertenciaRepositorio, AdvertenciaRepositorio>();

        // Manutenção de ponto e holerites (Fase 4, parte 4). A pasta dos PDFs é lida quando o serviço é criado (facilita os testes).
        services.AddScoped<IManutencaoPontoRepositorio, ManutencaoPontoRepositorio>();
        services.AddScoped<IHoleriteRepositorio, HoleriteRepositorio>();
        services.AddSingleton<IArmazenamentoHolerites, ArmazenamentoHoleritesEmDisco>();
        return services;
    }
}
