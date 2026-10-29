using HRAuto.Application.BancoDeHoras;
using HRAuto.Application.Common;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

/// <summary>RF24: saldo, extrato e ajuste manual do banco de horas. Relógio: quarta-feira, 21/10/2026.</summary>
public class BancoHorasAppTests
{
    // ---------- Consulta ----------

    [Fact]
    public async Task Meu_SemCodigo_MostraSaldoTotalEExtratoDosUltimos90Dias()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovoLancamento(e.Ana, "2026-05-01", TiposBancoHoras.Credito, 1);   // fora dos 90 dias, mas conta no saldo
        c.NovoLancamento(e.Ana, "2026-10-01", TiposBancoHoras.Credito, 2);
        c.NovoLancamento(e.Ana, "2026-10-05", TiposBancoHoras.Debito, 0.5);
        c.NovoLancamento(e.Bruno, "2026-10-02", TiposBancoHoras.Credito, 9); // de outra pessoa

        var r = await c.ConsultarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioAna), null, null, null);

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal("FUNC-0004", r.Valor!.Codigo);
        Assert.Equal("Ana Souza", r.Valor.Nome);
        Assert.Equal(2.5m, r.Valor.SaldoHoras);
        Assert.Equal("+2h30min", r.Valor.SaldoFormatado);
        Assert.Equal(new DateOnly(2026, 7, 24), r.Valor.Inicio);
        Assert.Equal(new DateOnly(2026, 10, 21), r.Valor.Fim);
        Assert.Equal(2, r.Valor.Lancamentos.Count);
        Assert.Equal(new DateOnly(2026, 10, 5), r.Valor.Lancamentos[0].Data); // mais recente primeiro
        Assert.Equal("Debito", r.Valor.Lancamentos[0].Tipo);
        Assert.Equal(0.5, r.Valor.Lancamentos[0].Horas);
    }

    [Fact]
    public async Task Meu_SaldoNegativo_ApareceComSinal()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovoLancamento(e.Ana, "2026-10-01", TiposBancoHoras.Credito, 1);
        c.NovoLancamento(e.Ana, "2026-10-02", TiposBancoHoras.Debito, 2.25);

        var r = await c.ConsultarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioAna), null, null, null);

        Assert.Equal(-1.25m, r.Valor!.SaldoHoras);
        Assert.Equal("-1h15min", r.Valor.SaldoFormatado);
    }

    [Fact]
    public async Task Meu_SemLancamentos_SaldoZero()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ConsultarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioAna), null, null, null);

        Assert.Equal(0m, r.Valor!.SaldoHoras);
        Assert.Equal("0h00min", r.Valor.SaldoFormatado);
        Assert.Empty(r.Valor.Lancamentos);
    }

    [Fact]
    public async Task Periodo_FiltraOExtratoMasNaoOSaldo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovoLancamento(e.Ana, "2026-09-10", TiposBancoHoras.Credito, 1);
        c.NovoLancamento(e.Ana, "2026-10-10", TiposBancoHoras.Credito, 2);

        var r = await c.ConsultarBancoHoras().ExecutarAsync(
            c.Solicitante(e.UsuarioAna), null, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        Assert.Equal(3m, r.Valor!.SaldoHoras);
        var unico = Assert.Single(r.Valor.Lancamentos);
        Assert.Equal(new DateOnly(2026, 10, 10), unico.Data);
    }

    [Fact]
    public async Task Gestor_VeAEquipe_MasNaoQuemEstaForaDela()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovoLancamento(e.Ana, "2026-10-01", TiposBancoHoras.Credito, 2);

        var daEquipe = await c.ConsultarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioGestor), "FUNC-0004", null, null);
        var deFora = await c.ConsultarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioGestor), "FUNC-0006", null, null);

        Assert.True(daEquipe.Sucesso, daEquipe.Erro);
        Assert.Equal(2m, daEquipe.Valor!.SaldoHoras);
        Assert.Equal(TipoErro.Proibido, deFora.TipoDoErro);
    }

    [Fact]
    public async Task Colaborador_NaoVeOutraPessoa_NemDescobreSeOCodigoExiste()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var existente = await c.ConsultarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioAna), "FUNC-0005", null, null);
        var inexistente = await c.ConsultarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioAna), "FUNC-0999", null, null);

        Assert.Equal(TipoErro.Proibido, existente.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, inexistente.TipoDoErro);
        Assert.Equal(existente.Erro, inexistente.Erro);
    }

    [Fact]
    public async Task Rh_VeQualquerUm_ECodigoInexistenteDa404()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovoLancamento(e.Carla, "2026-10-01", TiposBancoHoras.Credito, 1);

        var carla = await c.ConsultarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioRh), "func-0006", null, null);
        var fantasma = await c.ConsultarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioRh), "FUNC-0999", null, null);

        Assert.True(carla.Sucesso, carla.Erro);
        Assert.Equal(1m, carla.Valor!.SaldoHoras);
        Assert.Equal(TipoErro.NaoEncontrado, fantasma.TipoDoErro);
    }

    [Theory]
    [InlineData("0006")]
    [InlineData("Carla")]
    [InlineData("FUNC-0")]
    public async Task CodigoInvalido_Da400(string codigo)
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.ConsultarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioRh), codigo, null, null);

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Contains("FUNC-0001", r.Erro);
    }

    [Fact]
    public async Task PeriodoInvalido_Da400()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var ana = c.Solicitante(e.UsuarioAna);

        var invertido = await c.ConsultarBancoHoras().ExecutarAsync(ana, null, new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 1));
        var longo = await c.ConsultarBancoHoras().ExecutarAsync(ana, null, new DateOnly(2025, 1, 1), new DateOnly(2026, 10, 1));
        var noLimite = await c.ConsultarBancoHoras().ExecutarAsync(ana, null, new DateOnly(2025, 10, 1), new DateOnly(2026, 10, 1));

        Assert.Equal(TipoErro.Validacao, invertido.TipoDoErro);
        Assert.Equal(TipoErro.Validacao, longo.TipoDoErro);
        Assert.True(noLimite.Sucesso, noLimite.Erro); // 366 dias
    }

    [Fact]
    public async Task LancamentoComTipoDesconhecido_Da409_SemExplodir()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovoLancamento(e.Ana, "2026-10-01", "Estorno", 1);

        var r = await c.ConsultarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioAna), null, null, null);

        Assert.Equal(TipoErro.Conflito, r.TipoDoErro);
    }

    // ---------- Ajuste ----------

    private static AjustarBancoHorasDto Credito(string codigo = "FUNC-0004", double horas = 2, string? obs = "Compensação autorizada pelo gestor") =>
        new(codigo, "Credito", horas, null, obs);

    [Fact]
    public async Task Ajustar_Credito_GravaNotificaAuditaEDevolveONovoSaldo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovoLancamento(e.Ana, "2026-10-01", TiposBancoHoras.Credito, 1);

        var r = await c.AjustarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioRh), Credito());

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(3m, r.Valor!.SaldoHoras);
        Assert.Equal("+3h00min", r.Valor.SaldoFormatado);
        Assert.Equal("AjusteRH", r.Valor.Lancamento.Origem);
        Assert.Equal(new DateOnly(2026, 10, 21), r.Valor.Lancamento.Data);

        var gravado = c.Banco.BancoHoras.Single(b => b.Origem == OrigensBancoHoras.AjusteRH);
        Assert.Equal(e.Ana.Id, gravado.IdFuncionario);
        Assert.Equal("Compensação autorizada pelo gestor", gravado.Observacao);

        var aviso = Assert.Single(c.Banco.Notificacoes);
        Assert.Equal(e.Ana.Id, aviso.IdFuncionario);
        Assert.Equal("Ajuste no banco de horas", aviso.Titulo);

        var log = Assert.Single(c.Banco.Logs);
        Assert.Equal("AjustarBancoHoras", log.Acao);
        Assert.Equal("BANCO_HORAS", log.Tabela);
        Assert.Equal(gravado.Id, log.IdRegistro);
        Assert.Equal(e.UsuarioRh.Id, log.IdUsuario);
        Assert.Equal(1, c.Banco.TransacoesConfirmadas);
    }

    [Fact]
    public async Task Ajustar_Debito_ReduzOSaldo()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        c.NovoLancamento(e.Ana, "2026-10-01", TiposBancoHoras.Credito, 1);

        var r = await c.AjustarBancoHoras().ExecutarAsync(
            c.Solicitante(e.UsuarioRh), new AjustarBancoHorasDto("FUNC-0004", "debito", 3, new DateOnly(2026, 10, 15), "Saída antecipada sem compensação"));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(-2m, r.Valor!.SaldoHoras);
        Assert.Equal("-2h00min", r.Valor.SaldoFormatado);
        Assert.Equal("Debito", r.Valor.Lancamento.Tipo);
        Assert.Equal(new DateOnly(2026, 10, 15), r.Valor.Lancamento.Data);
    }

    [Fact]
    public async Task Ajustar_ColaboradorEGestor_Recusado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var colaborador = await c.AjustarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioBruno), Credito());
        var gestor = await c.AjustarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioGestor), Credito());

        Assert.Equal(TipoErro.Proibido, colaborador.TipoDoErro);
        Assert.Equal(TipoErro.Proibido, gestor.TipoDoErro);
        Assert.Empty(c.Banco.BancoHoras);
    }

    [Fact]
    public async Task Ajustar_OProprioBanco_Recusado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();

        var r = await c.AjustarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioRh), Credito("FUNC-0002"));

        Assert.Equal(TipoErro.Proibido, r.TipoDoErro);
        Assert.Contains("próprio", r.Erro);
        Assert.Empty(c.Banco.BancoHoras);
    }

    [Fact]
    public async Task Ajustar_QuemTemAcessoRh_ExigeAdmin()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var outroRh = c.NovoFuncionario(20, "Rui RH");
        c.NovoUsuario(outroRh, "rui@hrauto.local", perfil: PerfilAcesso.RH);

        var porRh = await c.AjustarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioRh), Credito("FUNC-0020"));
        var porAdmin = await c.AjustarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioAdmin), Credito("FUNC-0020"));

        Assert.Equal(TipoErro.Proibido, porRh.TipoDoErro);
        Assert.True(porAdmin.Sucesso, porAdmin.Erro);
    }

    [Fact]
    public async Task Ajustar_DadosInvalidos_Da400_ENadaEGravado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var rh = c.Solicitante(e.UsuarioRh);
        var caso = c.AjustarBancoHoras();

        var semDto = await caso.ExecutarAsync(rh, null);
        var codigoRuim = await caso.ExecutarAsync(rh, Credito("Ana"));
        var semHoras = await caso.ExecutarAsync(rh, new AjustarBancoHorasDto("FUNC-0004", "Credito", null, null, "Observação válida"));
        var horasZero = await caso.ExecutarAsync(rh, Credito(horas: 0));
        var horasNegativas = await caso.ExecutarAsync(rh, Credito(horas: -1));
        var tipoRuim = await caso.ExecutarAsync(rh, new AjustarBancoHorasDto("FUNC-0004", "Estorno", 1, null, "Observação válida"));
        var obsCurta = await caso.ExecutarAsync(rh, Credito(obs: "ok"));
        var semObs = await caso.ExecutarAsync(rh, Credito(obs: null));
        var futuro = await caso.ExecutarAsync(rh, new AjustarBancoHorasDto("FUNC-0004", "Credito", 1, new DateOnly(2026, 10, 22), "Observação válida"));

        foreach (var r in new[] { semDto, codigoRuim, semHoras, horasZero, horasNegativas, tipoRuim, obsCurta, semObs, futuro })
            Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Empty(c.Banco.BancoHoras);
        Assert.Empty(c.Banco.Notificacoes);
        Assert.Empty(c.Banco.Logs);
    }

    [Fact]
    public async Task Ajustar_FuncionarioInexistenteOuDesligado()
    {
        var c = new Cenario();
        var e = c.NovaEmpresa();
        var desligado = c.NovoFuncionario(30, "Dani Desligada", ativo: false);

        var inexistente = await c.AjustarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioRh), Credito("FUNC-0999"));
        var inativo = await c.AjustarBancoHoras().ExecutarAsync(c.Solicitante(e.UsuarioRh), Credito("FUNC-0030"));

        Assert.Equal(TipoErro.NaoEncontrado, inexistente.TipoDoErro);
        Assert.Equal(TipoErro.Conflito, inativo.TipoDoErro);
        Assert.Equal(30, desligado.Id);
    }
}
