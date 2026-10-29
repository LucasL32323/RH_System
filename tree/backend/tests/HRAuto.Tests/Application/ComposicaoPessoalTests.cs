using HRAuto.Application;
using HRAuto.Application.Abstractions;
using HRAuto.Application.Afastamentos;
using HRAuto.Application.BancoDeHoras;
using HRAuto.Application.Calendario;
using HRAuto.Application.Comunicacao;
using Microsoft.Extensions.DependencyInjection;

namespace HRAuto.Tests.Application;

/// <summary>
/// Garante que a injeção de dependência (AddApplication) sabe montar todos os casos de uso da Fase 4 (parte 3),
/// para o erro não aparecer só quando a API recebe a primeira requisição.
/// </summary>
public class ComposicaoPessoalTests
{
    [Fact]
    public void AddApplication_MontaOsCasosDeUsoDeBancoDeHorasNotificacoesFeriadosEFerias()
    {
        var banco = new BancoFake();
        var relogio = new RelogioFixo("2026-10-21T08:00:00");

        var servicos = new ServiceCollection();
        servicos.AddSingleton<IRelogio>(relogio);
        servicos.AddSingleton<IFuncionarioRepositorio>(new FuncionarioRepositorioFake(banco));
        servicos.AddSingleton<IFuncionarioCadastroRepositorio>(new FuncionarioCadastroRepositorioFake(banco));
        servicos.AddSingleton<IUsuarioGestaoRepositorio>(new UsuarioGestaoRepositorioFake(banco));
        servicos.AddSingleton<IFeriadoRepositorio>(new FeriadoRepositorioFake(banco));
        servicos.AddSingleton<IFeriasRepositorio>(new FeriasRepositorioFake(banco));
        servicos.AddSingleton<IBancoHorasRepositorio>(new BancoHorasRepositorioFake(banco));
        servicos.AddSingleton<INotificacaoRepositorio>(new NotificacaoRepositorioFake(banco));
        servicos.AddSingleton<IAuditoria>(new AuditoriaFake(banco, relogio));
        servicos.AddSingleton<IUnidadeDeTrabalho>(new UnidadeDeTrabalhoFake(banco));
        servicos.AddApplication();

        using var provedor = servicos.BuildServiceProvider();
        var tipos = new[]
        {
            typeof(ConsultarBancoHoras), typeof(AjustarBancoHoras),
            typeof(ListarNotificacoes), typeof(ContarNotificacoesNaoLidas),
            typeof(MarcarNotificacaoComoLida), typeof(MarcarTodasNotificacoesComoLidas),
            typeof(ListarFeriados), typeof(GerenciarFeriados),
            typeof(SolicitarFerias), typeof(ListarFerias), typeof(DecidirFerias), typeof(CancelarSolicitacaoFerias)
        };

        foreach (var tipo in tipos)
            Assert.NotNull(provedor.GetRequiredService(tipo));
    }
}
