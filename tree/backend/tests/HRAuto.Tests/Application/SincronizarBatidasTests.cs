using HRAuto.Application.Common;
using HRAuto.Application.RegistroPonto;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

/// <summary>RF19 e RNF09: fila de batidas offline sincronizada sem duplicar, com limites de segurança.</summary>
public class SincronizarBatidasTests
{
    // "Hoje" no relógio do servidor: quarta-feira, 21/10/2026, 10:00.
    private static (Cenario Cenario, Usuario Ana) CenarioPadrao()
    {
        var cenario = new Cenario("2026-10-21T10:00:00");
        var gestor = cenario.NovoFuncionario(2, "Gil Prado");
        var ana = cenario.NovoFuncionario(7, "Ana Souza", idGestor: gestor.Id);
        return (cenario, cenario.NovoUsuario(ana, "ana@hrauto.local"));
    }

    private static BatidaOfflineDto Item(string data, TipoBatida tipo, string hora) =>
        new(ApoioApp.Data(data), tipo, ApoioApp.Hora(hora));

    private static SincronizarBatidasDto Fila(params BatidaOfflineDto[] itens) => new(itens);

    private static BatidaOfflineDto[] DiaCompleto(string data, string saida = "17:00") => new[]
    {
        Item(data, TipoBatida.Entrada, "08:00"),
        Item(data, TipoBatida.SaidaIntervalo, "12:00"),
        Item(data, TipoBatida.RetornoIntervalo, "13:00"),
        Item(data, TipoBatida.Saida, saida)
    };

    [Fact]
    public async Task FilaDeUmDiaCompleto_EhAplicada_EFechaODia()
    {
        var (cenario, ana) = CenarioPadrao();

        var r = await cenario.Sincronizar().ExecutarAsync(cenario.Solicitante(ana), Fila(DiaCompleto("2026-10-20", "17:30")));

        Assert.True(r.Sucesso);
        Assert.All(r.Valor!, i => Assert.Equal(SituacaoItemOffline.Aplicada, i.Situacao));
        var ponto = cenario.Banco.Pontos.Single();
        Assert.Equal(4, ponto.TotalBatidas());
        Assert.Equal(ApoioApp.Data("2026-10-20"), ponto.Data);
        Assert.Equal(0.5, cenario.Banco.BancoHoras.Single().Quantidade); // fechamento do dia, como na batida online
        Assert.Contains(cenario.Banco.Logs, l => l.Acao == "SincronizarPonto" && l.IdRegistro == ponto.Id);
    }

    [Fact]
    public async Task ReenviarAMesmaFila_NaoDuplicaNada()
    {
        var (cenario, ana) = CenarioPadrao();
        var fila = Fila(DiaCompleto("2026-10-20"));
        await cenario.Sincronizar().ExecutarAsync(cenario.Solicitante(ana), fila);
        var salvamentos = cenario.Banco.Salvamentos;
        var lancamentos = cenario.Banco.BancoHoras.Count;

        var r = await cenario.Sincronizar().ExecutarAsync(cenario.Solicitante(ana), fila);

        Assert.True(r.Sucesso);
        Assert.All(r.Valor!, i => Assert.Equal(SituacaoItemOffline.JaRegistrada, i.Situacao));
        Assert.Single(cenario.Banco.Pontos);
        Assert.Equal(lancamentos, cenario.Banco.BancoHoras.Count);
        Assert.Equal(salvamentos, cenario.Banco.Salvamentos); // nada novo para gravar
    }

    [Fact]
    public async Task FilaEnviadaForaDeOrdem_EhReordenadaPeloServidor()
    {
        var (cenario, ana) = CenarioPadrao();
        var itens = DiaCompleto("2026-10-20").Reverse().ToArray();

        var r = await cenario.Sincronizar().ExecutarAsync(cenario.Solicitante(ana), Fila(itens));

        Assert.All(r.Valor!, i => Assert.Equal(SituacaoItemOffline.Aplicada, i.Situacao));
        Assert.Equal(TipoBatida.Saida, r.Valor![0].Tipo); // a resposta mantém a ordem em que o app enviou
        Assert.Equal(4, cenario.Banco.Pontos.Single().TotalBatidas());
    }

    [Fact]
    public async Task ItemInvalido_NaoImpedeOsValidos_EVoltaComMotivo()
    {
        var (cenario, ana) = CenarioPadrao();
        var fila = Fila(
            Item("2026-10-20", TipoBatida.Entrada, "08:00"),
            Item("2026-10-22", TipoBatida.Entrada, "08:00"), // amanhã
            new BatidaOfflineDto(null, TipoBatida.Entrada, null));

        var r = await cenario.Sincronizar().ExecutarAsync(cenario.Solicitante(ana), fila);

        Assert.True(r.Sucesso);
        Assert.Equal(SituacaoItemOffline.Aplicada, r.Valor![0].Situacao);
        Assert.Equal(SituacaoItemOffline.Recusada, r.Valor[1].Situacao);
        Assert.Contains("futuro", r.Valor[1].Motivo!);
        Assert.Equal(SituacaoItemOffline.Recusada, r.Valor[2].Situacao);
        Assert.Contains("Informe", r.Valor[2].Motivo!);
        Assert.Single(cenario.Banco.Pontos); // o dia de amanhã não virou ponto vazio
    }

    [Fact]
    public async Task HorarioFuturoHoje_EhRecusado_MasComFolgaDeRelogio()
    {
        var (cenario, ana) = CenarioPadrao(); // servidor: 10:00

        var r = await cenario.Sincronizar().ExecutarAsync(cenario.Solicitante(ana), Fila(
            Item("2026-10-21", TipoBatida.Entrada, "10:01"),   // 1 min à frente: dentro da folga de 2 min
            Item("2026-10-21", TipoBatida.SaidaIntervalo, "10:30"))); // 30 min à frente: recusada

        Assert.Equal(SituacaoItemOffline.Aplicada, r.Valor![0].Situacao);
        Assert.Equal(SituacaoItemOffline.Recusada, r.Valor[1].Situacao);
        Assert.Contains("relógio", r.Valor[1].Motivo!);
    }

    [Fact]
    public async Task BatidaComMaisDeSeteDias_ExigeManutencaoDePonto()
    {
        var (cenario, ana) = CenarioPadrao();

        var r = await cenario.Sincronizar().ExecutarAsync(cenario.Solicitante(ana), Fila(
            Item("2026-10-14", TipoBatida.Entrada, "08:00"),   // exatamente 7 dias: aceita
            Item("2026-10-13", TipoBatida.Entrada, "08:00"))); // 8 dias: recusada

        Assert.Equal(SituacaoItemOffline.Aplicada, r.Valor![0].Situacao);
        Assert.Equal(SituacaoItemOffline.Recusada, r.Valor[1].Situacao);
        Assert.Contains("manutenção de ponto", r.Valor[1].Motivo!);
    }

    [Fact]
    public async Task BatidaAnteriorAAdmissao_EhRecusada()
    {
        var cenario = new Cenario("2026-10-21T10:00:00");
        var novo = cenario.NovoFuncionario(8, "Novato", admissao: "2026-10-20");
        var usuario = cenario.NovoUsuario(novo, "novato@hrauto.local");

        var r = await cenario.Sincronizar().ExecutarAsync(
            cenario.Solicitante(usuario), Fila(Item("2026-10-19", TipoBatida.Entrada, "08:00")));

        Assert.Equal(SituacaoItemOffline.Recusada, r.Valor!.Single().Situacao);
        Assert.Contains("admissão", r.Valor.Single().Motivo!);
    }

    [Fact]
    public async Task OutroHorarioParaUmaBatidaJaGravada_EhRecusado_ComOrientacao()
    {
        var (cenario, ana) = CenarioPadrao();
        await cenario.Sincronizar().ExecutarAsync(cenario.Solicitante(ana), Fila(Item("2026-10-20", TipoBatida.Entrada, "08:00")));

        var r = await cenario.Sincronizar().ExecutarAsync(
            cenario.Solicitante(ana), Fila(Item("2026-10-20", TipoBatida.Entrada, "08:20")));

        Assert.Equal(SituacaoItemOffline.Recusada, r.Valor!.Single().Situacao);
        Assert.Contains("manutenção", r.Valor.Single().Motivo!);
        Assert.Equal(new TimeOnly(8, 0), cenario.Banco.Pontos.Single().Entrada); // a batida original permanece
    }

    [Fact]
    public async Task BatidaSemAAnterior_EhRecusada()
    {
        var (cenario, ana) = CenarioPadrao();

        var r = await cenario.Sincronizar().ExecutarAsync(
            cenario.Solicitante(ana), Fila(Item("2026-10-20", TipoBatida.RetornoIntervalo, "13:00")));

        Assert.Equal(SituacaoItemOffline.Recusada, r.Valor!.Single().Situacao);
        Assert.Contains("Falta a batida", r.Valor.Single().Motivo!);
        Assert.Empty(cenario.Banco.Pontos);
    }

    [Fact]
    public async Task ContinuaUmDiaJaIniciadoOnline()
    {
        var (cenario, ana) = CenarioPadrao();
        cenario.Relogio.Definir("2026-10-21T08:00:00");
        await cenario.RegistrarBatida().ExecutarAsync(cenario.Solicitante(ana)); // entrada online às 08:00
        cenario.Relogio.Definir("2026-10-21T10:00:00");

        var r = await cenario.Sincronizar().ExecutarAsync(cenario.Solicitante(ana), Fila(
            Item("2026-10-21", TipoBatida.Entrada, "08:00"),          // a mesma que já está gravada
            Item("2026-10-21", TipoBatida.SaidaIntervalo, "09:30")));  // feita offline

        Assert.Equal(SituacaoItemOffline.JaRegistrada, r.Valor![0].Situacao);
        Assert.Equal(SituacaoItemOffline.Aplicada, r.Valor[1].Situacao);
        Assert.Equal(2, cenario.Banco.Pontos.Single().TotalBatidas());
    }

    [Fact]
    public async Task ConflitoNoBanco_VirouFalhaDeConflito()
    {
        var (cenario, ana) = CenarioPadrao();
        cenario.Banco.ConflitoNoProximoSalvar = true;

        var r = await cenario.Sincronizar().ExecutarAsync(
            cenario.Solicitante(ana), Fila(Item("2026-10-20", TipoBatida.Entrada, "08:00")));

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
        Assert.Equal(1, cenario.Banco.TransacoesDescartadas);
    }

    [Fact]
    public async Task FilaVazia_OuGrandeDemais_EhErroDeValidacao()
    {
        var (cenario, ana) = CenarioPadrao();
        var muitas = Enumerable.Repeat(Item("2026-10-20", TipoBatida.Entrada, "08:00"), SincronizarBatidas.MaximoBatidasPorEnvio + 1).ToArray();

        var vazia = await cenario.Sincronizar().ExecutarAsync(cenario.Solicitante(ana), Fila());
        var nula = await cenario.Sincronizar().ExecutarAsync(cenario.Solicitante(ana), null);
        var grande = await cenario.Sincronizar().ExecutarAsync(cenario.Solicitante(ana), Fila(muitas));

        Assert.Equal(TipoErro.Validacao, vazia.TipoDoErro);
        Assert.Equal(TipoErro.Validacao, nula.TipoDoErro);
        Assert.Equal(TipoErro.Validacao, grande.TipoDoErro);
    }

    [Fact]
    public async Task FuncionarioDesligado_NaoSincroniza()
    {
        var cenario = new Cenario("2026-10-21T10:00:00");
        var ex = cenario.NovoFuncionario(9, "Ex Funcionario", ativo: false);
        var usuario = cenario.NovoUsuario(ex, "ex@hrauto.local");

        var r = await cenario.Sincronizar().ExecutarAsync(
            cenario.Solicitante(usuario), Fila(Item("2026-10-20", TipoBatida.Entrada, "08:00")));

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }
}
