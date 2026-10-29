using HRAuto.Application.Afastamentos;
using HRAuto.Application.Autenticacao;
using HRAuto.Application.BancoDeHoras;
using HRAuto.Application.Cadastro;
using HRAuto.Application.Calendario;
using HRAuto.Application.Comunicacao;
using HRAuto.Application.Disciplina;
using HRAuto.Application.Holerites;
using HRAuto.Application.ManutencaoDePonto;
using HRAuto.Application.RegistroPonto;
using HRAuto.Application.Sistema;
using HRAuto.Domain.Jornada;
using Microsoft.Extensions.DependencyInjection;

namespace HRAuto.Application;

public static class DependencyInjection
{
    /// <summary>Registra os casos de uso da camada Application.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IConsultarStatusSistema, ConsultarStatusSistema>();

        // Jornada única para todos (suposição da Fase 3): uma instância compartilhada pelos casos de uso e pela calculadora.
        services.AddSingleton(JornadaPadrao.Padrao);
        services.AddSingleton(provedor => new CalculadoraJornada(provedor.GetRequiredService<JornadaPadrao>()));

        // Acesso e segurança (RF01 a RF04, RF45)
        services.AddScoped<AutenticarUsuario>();
        services.AddScoped<AlterarSenha>();
        services.AddScoped<RecuperarSenha>();
        services.AddScoped<ConsultarMeusDados>();
        services.AddScoped<EncerrarSessao>();

        // Ponto (RF17 a RF21, RF24, RF25)
        services.AddScoped<FechamentoDoDia>();
        services.AddScoped<RegistrarBatida>();
        services.AddScoped<ConsultarStatusPontoHoje>();
        services.AddScoped<SincronizarBatidas>();
        services.AddScoped<ConsultarEspelhoPonto>();

        // Cadastro e acesso (RF06, RF07, RF09 a RF16)
        services.AddScoped<EnviadorDeConvite>();
        services.AddScoped<CadastrarFuncionario>();
        services.AddScoped<ListarFuncionarios>();
        services.AddScoped<ConsultarFuncionario>();
        services.AddScoped<AtualizarFuncionario>();
        services.AddScoped<AlterarSituacaoFuncionario>();
        services.AddScoped<GerenciarAcessoUsuario>();
        services.AddScoped<GerenciarCargos>();
        services.AddScoped<GerenciarDepartamentos>();

        // Disciplina (RF33, RF34, RF47)
        services.AddScoped<RegistrarAdvertencia>();
        services.AddScoped<CancelarAdvertencia>();
        services.AddScoped<ConsultarAdvertencias>();
        services.AddScoped<ConsultarSituacaoDisciplinar>();

        // Banco de horas (RF24)
        services.AddScoped<ConsultarBancoHoras>();
        services.AddScoped<AjustarBancoHoras>();

        // Notificações (RF36) e feriados (RF28)
        services.AddScoped<ListarNotificacoes>();
        services.AddScoped<ContarNotificacoesNaoLidas>();
        services.AddScoped<MarcarNotificacaoComoLida>();
        services.AddScoped<MarcarTodasNotificacoesComoLidas>();
        services.AddScoped<ListarFeriados>();
        services.AddScoped<GerenciarFeriados>();

        // Férias (RF26, RF27)
        services.AddScoped<SolicitarFerias>();
        services.AddScoped<ListarFerias>();
        services.AddScoped<DecidirFerias>();
        services.AddScoped<CancelarSolicitacaoFerias>();

        // Manutenção de ponto (RF22, RF23) e holerites (RF29, RF30)
        services.AddScoped<SolicitarManutencaoPonto>();
        services.AddScoped<ListarManutencoesPonto>();
        services.AddScoped<DecidirManutencaoPonto>();
        services.AddScoped<PublicarHolerite>();
        services.AddScoped<ListarHolerites>();
        services.AddScoped<BaixarHolerite>();

        return services;
    }
}
