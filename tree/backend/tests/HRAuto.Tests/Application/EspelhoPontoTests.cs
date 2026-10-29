using HRAuto.Application.Common;
using HRAuto.Application.RegistroPonto;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

/// <summary>RF21, RNF06, RNF08: espelho de ponto com permissão por perfil (próprio, equipe do gestor, RH/Admin).</summary>
public class EspelhoPontoTests
{
    // Hoje: quarta-feira, 21/10/2026, 10:00. Gil (id 2) lidera Ana (7); Bruno (8) é de outra equipe; Rita (3) é RH.
    private sealed record Elenco(Cenario Cenario, Usuario Gil, Usuario Ana, Usuario Bruno, Usuario Rita);

    private static Elenco Montar()
    {
        var cenario = new Cenario("2026-10-21T10:00:00");
        var gil = cenario.NovoFuncionario(2, "Gil Prado");
        var rita = cenario.NovoFuncionario(3, "Rita Alves");
        var ana = cenario.NovoFuncionario(7, "Ana Souza", idGestor: gil.Id);
        var bruno = cenario.NovoFuncionario(8, "Bruno Reis");

        return new Elenco(
            cenario,
            cenario.NovoUsuario(gil, "gil@hrauto.local"),
            cenario.NovoUsuario(ana, "ana@hrauto.local"),
            cenario.NovoUsuario(bruno, "bruno@hrauto.local"),
            cenario.NovoUsuario(rita, "rita@hrauto.local", perfil: PerfilAcesso.RH));
    }

    private static void GravarPonto(Cenario c, int idFuncionario, string data, string e, string si, string r, string s) =>
        c.Banco.Pontos.Add(new Ponto
        {
            IdFuncionario = idFuncionario,
            Data = ApoioApp.Data(data),
            Entrada = ApoioApp.Hora(e),
            SaidaIntervalo = ApoioApp.Hora(si),
            RetornoIntervalo = ApoioApp.Hora(r),
            Saida = ApoioApp.Hora(s)
        }.DefinirId(c.Banco.Pontos.Count + 1));

    [Fact]
    public async Task EspelhoProprio_TraBatidasTotaisESaldoDoBanco()
    {
        var e = Montar();
        GravarPonto(e.Cenario, 7, "2026-10-19", "08:00", "12:00", "13:00", "17:30"); // segunda: +30 min
        // terça 20/10 sem batida: falta; quarta 21/10 (hoje) em andamento
        e.Cenario.Banco.BancoHoras.Add(new BancoHoras { IdFuncionario = 7, Tipo = TiposBancoHoras.Credito, Quantidade = 0.5, Data = ApoioApp.Data("2026-10-19") });

        var r = await e.Cenario.Espelho().ExecutarAsync(
            e.Cenario.Solicitante(e.Ana), null, ApoioApp.Data("2026-10-19"), ApoioApp.Data("2026-10-21"));

        Assert.True(r.Sucesso);
        Assert.Equal("FUNC-0007", r.Valor!.Codigo);
        Assert.Equal(3, r.Valor.Dias.Count);

        var segunda = r.Valor.Dias[0];
        Assert.Equal(SituacaoDia.Completo, segunda.Situacao);
        Assert.Equal(new TimeOnly(8, 0), segunda.Entrada);
        Assert.Equal(30, segunda.ExtrasMinutos);

        Assert.Equal(SituacaoDia.Falta, r.Valor.Dias[1].Situacao);
        Assert.Equal(SituacaoDia.EmAndamento, r.Valor.Dias[2].Situacao); // hoje ainda não acabou

        Assert.Equal(30, r.Valor.Totais.ExtrasMinutos);
        Assert.Equal(1, r.Valor.Totais.Faltas);
        Assert.Equal(1, r.Valor.Totais.DiasTrabalhados);
        Assert.Equal(0.5m, r.Valor.SaldoBancoHorasHoras);
    }

    [Fact]
    public async Task SemDatas_DevolveDoPrimeiroDiaDoMesAteHoje()
    {
        var e = Montar();

        var r = await e.Cenario.Espelho().ExecutarAsync(e.Cenario.Solicitante(e.Ana), null, null, null);

        Assert.Equal(ApoioApp.Data("2026-10-01"), r.Valor!.De);
        Assert.Equal(ApoioApp.Data("2026-10-21"), r.Valor.Ate);
        Assert.Equal(21, r.Valor.Dias.Count);
        Assert.Equal(ContextoDia.DescansoSemanal, r.Valor.Dias[3].Contexto); // sábado, 03/10
    }

    [Fact]
    public async Task DiasAntesDaAdmissaoEDiasFuturos_NaoAparecem()
    {
        var cenario = new Cenario("2026-10-21T10:00:00");
        var novo = cenario.NovoFuncionario(8, "Novato", admissao: "2026-10-20");
        var usuario = cenario.NovoUsuario(novo, "novato@hrauto.local");

        var r = await cenario.Espelho().ExecutarAsync(
            cenario.Solicitante(usuario), null, ApoioApp.Data("2026-10-15"), ApoioApp.Data("2026-10-30"));

        Assert.Equal(new[] { ApoioApp.Data("2026-10-20"), ApoioApp.Data("2026-10-21") }, r.Valor!.Dias.Select(d => d.Data).ToArray());
    }

    [Fact]
    public async Task Gestor_VeAEquipe()
    {
        var e = Montar();
        GravarPonto(e.Cenario, 7, "2026-10-19", "08:00", "12:00", "13:00", "17:00");

        var r = await e.Cenario.Espelho().ExecutarAsync(
            e.Cenario.Solicitante(e.Gil), "FUNC-0007", ApoioApp.Data("2026-10-19"), ApoioApp.Data("2026-10-19"));

        Assert.True(r.Sucesso);
        Assert.Equal("Ana Souza", r.Valor!.Nome);
    }

    [Fact]
    public async Task Gestor_NaoVeQuemEhDeOutraEquipe()
    {
        var e = Montar();

        var r = await e.Cenario.Espelho().ExecutarAsync(e.Cenario.Solicitante(e.Gil), "FUNC-0008", null, null);

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }

    [Fact]
    public async Task Colaborador_NaoVeOPontoDeOutroColaborador()
    {
        var e = Montar();

        var r = await e.Cenario.Espelho().ExecutarAsync(e.Cenario.Solicitante(e.Bruno), "FUNC-0007", null, null);

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
    }

    [Fact]
    public async Task Rh_VeQualquerFuncionario()
    {
        var e = Montar();

        var r = await e.Cenario.Espelho().ExecutarAsync(e.Cenario.Solicitante(e.Rita), "func-0008", null, null);

        Assert.True(r.Sucesso); // código aceita minúsculas
        Assert.Equal("FUNC-0008", r.Valor!.Codigo);
    }

    [Fact]
    public async Task ColaboradorPodeConsultarOProprioCodigoExplicitamente()
    {
        var e = Montar();

        var r = await e.Cenario.Espelho().ExecutarAsync(e.Cenario.Solicitante(e.Ana), "FUNC-0007", null, null);

        Assert.True(r.Sucesso);
    }

    [Theory]
    [InlineData("Ana")]        // a busca é pelo código, não pelo nome
    [InlineData("FUNC-1")]
    [InlineData("FUNC-0000")]
    public async Task CodigoInvalido_EhErroDeValidacao(string codigo)
    {
        var e = Montar();

        var r = await e.Cenario.Espelho().ExecutarAsync(e.Cenario.Solicitante(e.Rita), codigo, null, null);

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("FUNC-0001", r.Erro!);
    }

    [Fact]
    public async Task CodigoInexistente_EhNaoEncontrado()
    {
        var e = Montar();

        var r = await e.Cenario.Espelho().ExecutarAsync(e.Cenario.Solicitante(e.Rita), "FUNC-0999", null, null);

        Assert.Equal(TipoErro.NaoEncontrado, r.TipoDoErro);
    }

    [Fact]
    public async Task PeriodoInvertido_OuMuitoLongo_EhErroDeValidacao()
    {
        var e = Montar();
        var solicitante = e.Cenario.Solicitante(e.Ana);

        var invertido = await e.Cenario.Espelho().ExecutarAsync(solicitante, null, ApoioApp.Data("2026-10-20"), ApoioApp.Data("2026-10-19"));
        var longo = await e.Cenario.Espelho().ExecutarAsync(solicitante, null, ApoioApp.Data("2025-01-01"), ApoioApp.Data("2026-10-21"));
        var limite = await e.Cenario.Espelho().ExecutarAsync(solicitante, null, ApoioApp.Data("2025-10-21"), ApoioApp.Data("2026-10-21"));

        Assert.Equal(TipoErro.Validacao, invertido.TipoDoErro);
        Assert.Equal(TipoErro.Validacao, longo.TipoDoErro);
        Assert.True(limite.Sucesso); // 366 dias exatos é o máximo aceito
    }

    [Fact]
    public async Task FeriadoEFerias_AparecemComOContextoCerto()
    {
        var e = Montar();
        e.Cenario.Banco.Feriados.Add(new Feriado { Data = ApoioApp.Data("2026-10-19"), Nome = "Feriado", Tipo = TiposFeriado.Nacional });
        e.Cenario.Banco.Ferias.Add(new Ferias
        {
            IdFuncionario = 7, DataInicio = ApoioApp.Data("2026-10-20"), DataFim = ApoioApp.Data("2026-10-20"),
            Dias = 1, Status = StatusSolicitacao.Aprovada
        });

        var r = await e.Cenario.Espelho().ExecutarAsync(
            e.Cenario.Solicitante(e.Ana), null, ApoioApp.Data("2026-10-19"), ApoioApp.Data("2026-10-20"));

        Assert.Equal(ContextoDia.Feriado, r.Valor!.Dias[0].Contexto);
        Assert.Equal(SituacaoDia.SemExpediente, r.Valor.Dias[0].Situacao); // feriado sem batida: nada a cobrar
        Assert.Equal(ContextoDia.Ferias, r.Valor.Dias[1].Contexto);
        Assert.Equal(0, r.Valor.Totais.Faltas);
    }
}
