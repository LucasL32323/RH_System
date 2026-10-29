using HRAuto.Application.Common;
using HRAuto.Application.RegistroPonto;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

/// <summary>RF17, RF18, RF20, RF24, RF25: bater o ponto com a hora do servidor, fechar o dia na 4ª batida.</summary>
public class RegistrarBatidaTests
{
    // Quarta-feira, 21/10/2026, dia útil. O gestor (id 2) lidera a Ana (id 7).
    private static (Cenario Cenario, Usuario Ana) CenarioPadrao()
    {
        var cenario = new Cenario();
        var gestor = cenario.NovoFuncionario(2, "Gil Prado");
        var ana = cenario.NovoFuncionario(7, "Ana Souza", idGestor: gestor.Id);
        return (cenario, cenario.NovoUsuario(ana, "ana@hrauto.local"));
    }

    private static async Task<Resultado<BatidaRegistradaDto>> Bater(Cenario cenario, Usuario usuario, string hora)
    {
        cenario.Relogio.Definir($"2026-10-21T{hora}:00");
        return await cenario.RegistrarBatida().ExecutarAsync(cenario.Solicitante(usuario));
    }

    [Fact]
    public async Task PrimeiraBatida_CriaOPontoDoDia_ComAHoraDoServidor()
    {
        var (cenario, ana) = CenarioPadrao();

        var r = await Bater(cenario, ana, "08:03");

        Assert.True(r.Sucesso);
        Assert.Equal(TipoBatida.Entrada, r.Valor!.Tipo);
        Assert.Equal(TipoBatida.SaidaIntervalo, r.Valor.ProximaBatida);
        Assert.Null(r.Valor.FechamentoDoDia);

        var ponto = cenario.Banco.Pontos.Single();
        Assert.Equal(7, ponto.IdFuncionario);
        Assert.Equal(ApoioApp.Data("2026-10-21"), ponto.Data);
        Assert.Equal(new TimeOnly(8, 3, 0), ponto.Entrada);
        Assert.True(ponto.Id > 0);
    }

    [Fact]
    public async Task Batida_FicaNaAuditoria_ComOIdDoPonto_NaMesmaTransacao()
    {
        var (cenario, ana) = CenarioPadrao();

        await Bater(cenario, ana, "08:00");

        var log = cenario.Banco.Logs.Single();
        Assert.Equal("RegistrarPonto", log.Acao);
        Assert.Equal("PONTO", log.Tabela);
        Assert.Equal(cenario.Banco.Pontos.Single().Id, log.IdRegistro);
        Assert.Contains("FUNC-0007", log.Descricao!);
        Assert.Equal(1, cenario.Banco.TransacoesConfirmadas);
        Assert.Equal(0, cenario.Banco.TransacoesDescartadas);
    }

    [Fact]
    public async Task QuatroBatidas_FecharamODia_ELancamExtraNoBancoDeHoras()
    {
        var (cenario, ana) = CenarioPadrao();

        await Bater(cenario, ana, "08:00");
        await Bater(cenario, ana, "12:00");
        await Bater(cenario, ana, "13:00");
        var ultima = await Bater(cenario, ana, "17:30");

        Assert.True(ultima.Sucesso);
        Assert.Null(ultima.Valor!.ProximaBatida);
        var fechamento = ultima.Valor.FechamentoDoDia!;
        Assert.Equal(SituacaoDia.Completo, fechamento.Situacao);
        Assert.Equal(8 * 60 + 30, fechamento.TrabalhadoMinutos);
        Assert.Equal(30, fechamento.ExtrasMinutos);

        var lancamento = cenario.Banco.BancoHoras.Single();
        Assert.Equal(TiposBancoHoras.Credito, lancamento.Tipo);
        Assert.Equal(0.5, lancamento.Quantidade);
        Assert.Equal(OrigensBancoHoras.Ponto, lancamento.Origem);
        Assert.Empty(cenario.Banco.Notificacoes); // 30 min não passa do limite de 2 h
    }

    [Fact]
    public async Task HorasExtrasAcimaDeDuasHoras_NotificamOGestor()
    {
        var (cenario, ana) = CenarioPadrao();

        await Bater(cenario, ana, "08:00");
        await Bater(cenario, ana, "12:00");
        await Bater(cenario, ana, "13:00");
        await Bater(cenario, ana, "19:30"); // 8 h + 2h30 de extra

        var aviso = cenario.Banco.Notificacoes.Single();
        Assert.Equal(2, aviso.IdFuncionario); // destinatário: o gestor
        Assert.Equal(TiposNotificacao.HoraExtra, aviso.Tipo);
        Assert.Contains("FUNC-0007", aviso.Mensagem);
        Assert.Contains("2h30min", aviso.Mensagem);
    }

    [Fact]
    public async Task HorasExtrasAcimaDoLimite_SemGestor_NaoGeramNotificacao()
    {
        var cenario = new Cenario();
        var solo = cenario.NovoFuncionario(5, "Sem Gestor");
        var usuario = cenario.NovoUsuario(solo, "solo@hrauto.local");

        await Bater(cenario, usuario, "08:00");
        await Bater(cenario, usuario, "12:00");
        await Bater(cenario, usuario, "13:00");
        await Bater(cenario, usuario, "19:30");

        Assert.Empty(cenario.Banco.Notificacoes);
        Assert.Single(cenario.Banco.BancoHoras);
    }

    [Fact]
    public async Task SaidaAntecipada_LancaDebitoNoBancoDeHoras()
    {
        var (cenario, ana) = CenarioPadrao();

        await Bater(cenario, ana, "08:00");
        await Bater(cenario, ana, "12:00");
        await Bater(cenario, ana, "13:00");
        await Bater(cenario, ana, "16:00"); // trabalhou 7 h

        var lancamento = cenario.Banco.BancoHoras.Single();
        Assert.Equal(TiposBancoHoras.Debito, lancamento.Tipo);
        Assert.Equal(1.0, lancamento.Quantidade);
    }

    [Fact]
    public async Task TrabalhoEmFeriadoNacional_ViraTodoHoraExtra()
    {
        var (cenario, ana) = CenarioPadrao();
        cenario.Banco.Feriados.Add(new Feriado { Data = ApoioApp.Data("2026-10-21"), Nome = "Feriado de teste", Tipo = TiposFeriado.Nacional });

        await Bater(cenario, ana, "09:00");
        await Bater(cenario, ana, "11:00");
        await Bater(cenario, ana, "12:00");
        var ultima = await Bater(cenario, ana, "14:00"); // 4 h trabalhadas

        Assert.Equal(ContextoDia.Feriado, ultima.Valor!.FechamentoDoDia!.Contexto);
        Assert.Equal(4 * 60, ultima.Valor.FechamentoDoDia.ExtrasMinutos);
        Assert.Equal(4.0, cenario.Banco.BancoHoras.Single().Quantidade);
    }

    [Fact]
    public async Task FeriadoMunicipalDeOutraCidade_NaoAfetaOFuncionario()
    {
        var (cenario, ana) = CenarioPadrao();
        cenario.Banco.Detalhes.Add(new FuncionarioDetalhes { IdFuncionario = 7, Cidade = "Santos", Estado = "SP" });
        cenario.Banco.Feriados.Add(new Feriado
        {
            Data = ApoioApp.Data("2026-10-21"), Nome = "Aniversário da cidade", Tipo = TiposFeriado.Municipal, Cidade = "Guarujá", Estado = "SP"
        });

        await Bater(cenario, ana, "08:00");
        await Bater(cenario, ana, "12:00");
        await Bater(cenario, ana, "13:00");
        var ultima = await Bater(cenario, ana, "17:00");

        Assert.Equal(ContextoDia.DiaUtil, ultima.Valor!.FechamentoDoDia!.Contexto);
        Assert.Empty(cenario.Banco.BancoHoras); // 8 h exatas: nada a lançar
    }

    [Fact]
    public async Task BatidaNoMesmoMinuto_EhRepetida_ENadaEhGravado()
    {
        var (cenario, ana) = CenarioPadrao();
        await Bater(cenario, ana, "08:00");
        var salvamentos = cenario.Banco.Salvamentos;

        var r = await Bater(cenario, ana, "08:00");

        Assert.False(r.Sucesso);
        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("repetida", r.Erro!);
        Assert.Equal(1, cenario.Banco.Pontos.Single().TotalBatidas());
        Assert.Equal(salvamentos, cenario.Banco.Salvamentos);
    }

    [Fact]
    public async Task QuintaBatida_EhRecusada()
    {
        var (cenario, ana) = CenarioPadrao();
        await Bater(cenario, ana, "08:00");
        await Bater(cenario, ana, "12:00");
        await Bater(cenario, ana, "13:00");
        await Bater(cenario, ana, "17:00");

        var r = await Bater(cenario, ana, "18:00");

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("quatro batidas", r.Erro!);
        Assert.Single(cenario.Banco.Logs, l => l.Acao == "RegistrarPonto" && l.Descricao!.Contains("saída às 17:00"));
        Assert.Equal(4, cenario.Banco.Logs.Count);
    }

    [Fact]
    public async Task ConflitoNoBanco_VirouFalhaDeConflito_ETransacaoDesfeita()
    {
        var (cenario, ana) = CenarioPadrao();
        cenario.Banco.ConflitoNoProximoSalvar = true; // simula dois toques simultâneos criando o ponto do dia

        var r = await Bater(cenario, ana, "08:00");

        Assert.False(r.Sucesso);
        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.Equal(1, cenario.Banco.TransacoesDescartadas);
        Assert.Equal(0, cenario.Banco.TransacoesConfirmadas);
    }

    [Fact]
    public async Task FuncionarioDesligado_NaoBateOPonto()
    {
        var cenario = new Cenario();
        var ex = cenario.NovoFuncionario(9, "Ex Funcionario", ativo: false);
        var usuario = cenario.NovoUsuario(ex, "ex@hrauto.local");

        var r = await Bater(cenario, usuario, "08:00");

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
        Assert.Empty(cenario.Banco.Pontos);
    }

    [Fact]
    public async Task Admin_TambemBateOProprioPonto()
    {
        var cenario = new Cenario();
        var adm = cenario.NovoFuncionario(1, "Administrador");
        var usuario = cenario.NovoUsuario(adm, "admin@hrauto.local", perfil: PerfilAcesso.Admin);

        var r = await Bater(cenario, usuario, "08:00");

        Assert.True(r.Sucesso);
    }

    // ---------- Status de hoje ----------

    [Fact]
    public async Task StatusDeHoje_SemBatidas_PedeAEntrada()
    {
        var (cenario, ana) = CenarioPadrao();

        var r = await cenario.StatusPontoHoje().ExecutarAsync(cenario.Solicitante(ana));

        Assert.Equal(TipoBatida.Entrada, r.Valor!.ProximaBatida);
        Assert.Equal("entrada", r.Valor.ProximaBatidaNome);
        Assert.Empty(r.Valor.Batidas);
        Assert.Equal(ApoioApp.Data("2026-10-21"), r.Valor.Data);
        Assert.Equal(TimeSpan.FromHours(-3), r.Valor.HoraServidor.Offset);
    }

    [Fact]
    public async Task StatusDeHoje_DepoisDeUmaBatida_PedeASaidaParaOIntervalo()
    {
        var (cenario, ana) = CenarioPadrao();
        await Bater(cenario, ana, "08:00");

        var r = await cenario.StatusPontoHoje().ExecutarAsync(cenario.Solicitante(ana));

        Assert.Equal(TipoBatida.SaidaIntervalo, r.Valor!.ProximaBatida);
        Assert.Equal(TipoBatida.Entrada, r.Valor.Batidas.Single().Tipo);
    }

    [Fact]
    public async Task StatusDeHoje_DiaCompleto_NaoTemProximaBatida()
    {
        var (cenario, ana) = CenarioPadrao();
        await Bater(cenario, ana, "08:00");
        await Bater(cenario, ana, "12:00");
        await Bater(cenario, ana, "13:00");
        await Bater(cenario, ana, "17:00");

        var r = await cenario.StatusPontoHoje().ExecutarAsync(cenario.Solicitante(ana));

        Assert.Null(r.Valor!.ProximaBatida);
        Assert.Null(r.Valor.ProximaBatidaNome);
        Assert.Equal(4, r.Valor.Batidas.Count);
    }
}
