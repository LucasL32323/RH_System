using HRAuto.Application;
using HRAuto.Application.Abstractions;
using HRAuto.Application.Holerites;
using HRAuto.Application.ManutencaoDePonto;
using Microsoft.Extensions.DependencyInjection;

namespace HRAuto.Tests.Application;

/// <summary>
/// Garante que a injeção de dependência (AddApplication) sabe montar os casos de uso da Fase 4 (parte 4),
/// para o erro não aparecer só quando a API recebe a primeira requisição.
/// </summary>
public class ComposicaoManutencaoTests
{
    [Fact]
    public void AddApplication_MontaOsCasosDeUsoDeManutencaoDePontoEHolerites()
    {
        var c = new Cenario();

        var servicos = new ServiceCollection();
        servicos.AddSingleton<IRelogio>(c.Relogio);
        servicos.AddSingleton<IFuncionarioRepositorio>(new FuncionarioRepositorioFake(c.Banco));
        servicos.AddSingleton<IFuncionarioCadastroRepositorio>(new FuncionarioCadastroRepositorioFake(c.Banco));
        servicos.AddSingleton<IUsuarioGestaoRepositorio>(new UsuarioGestaoRepositorioFake(c.Banco));
        servicos.AddSingleton<IPontoRepositorio>(new PontoRepositorioFake(c.Banco));
        servicos.AddSingleton<IFeriadoRepositorio>(new FeriadoRepositorioFake(c.Banco));
        servicos.AddSingleton<IFeriasRepositorio>(new FeriasRepositorioFake(c.Banco));
        servicos.AddSingleton<IBancoHorasRepositorio>(new BancoHorasRepositorioFake(c.Banco));
        servicos.AddSingleton<INotificacaoRepositorio>(new NotificacaoRepositorioFake(c.Banco));
        servicos.AddSingleton<IManutencaoPontoRepositorio>(new ManutencaoPontoRepositorioFake(c.Banco));
        servicos.AddSingleton<IHoleriteRepositorio>(new HoleriteRepositorioFake(c.Banco));
        servicos.AddSingleton<IArmazenamentoHolerites>(c.Armazenamento);
        servicos.AddSingleton<IAuditoria>(new AuditoriaFake(c.Banco, c.Relogio));
        servicos.AddSingleton<IUnidadeDeTrabalho>(new UnidadeDeTrabalhoFake(c.Banco));
        servicos.AddApplication();

        using var provedor = servicos.BuildServiceProvider();
        var tipos = new[]
        {
            typeof(SolicitarManutencaoPonto), typeof(ListarManutencoesPonto), typeof(DecidirManutencaoPonto),
            typeof(PublicarHolerite), typeof(ListarHolerites), typeof(BaixarHolerite)
        };

        foreach (var tipo in tipos)
            Assert.NotNull(provedor.GetRequiredService(tipo));
    }
}
