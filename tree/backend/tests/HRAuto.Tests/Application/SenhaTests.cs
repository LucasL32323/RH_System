using HRAuto.Application.Autenticacao;
using HRAuto.Application.Common;
using HRAuto.Domain.Entities;

namespace HRAuto.Tests.Application;

/// <summary>RF03 (esqueci minha senha, token de 30 min e uso único) e RF04 (alterar a própria senha).</summary>
public class SenhaTests
{
    private static (Cenario Cenario, Usuario Usuario) CenarioComAna()
    {
        var cenario = new Cenario();
        var ana = cenario.NovoFuncionario(7, "Ana Souza");
        return (cenario, cenario.NovoUsuario(ana, "ana@hrauto.local"));
    }

    // ---------- Alterar senha (RF04) ----------

    [Fact]
    public async Task AlterarSenha_Valida_TrocaOHashEAudita()
    {
        var (cenario, usuario) = CenarioComAna();

        var r = await cenario.AlterarSenha().ExecutarAsync(
            cenario.Solicitante(usuario), new AlterarSenhaDto(Cenario.SenhaPadrao, "NovaSenha@456"));

        Assert.True(r.Sucesso);
        Assert.Equal("hash:NovaSenha@456", usuario.Senha);
        Assert.Equal("AlterarSenha", cenario.Banco.Logs.Single().Acao);
    }

    [Fact]
    public async Task AlterarSenha_SenhaAtualErrada_EhRecusada_ESenhaContinuaIgual()
    {
        var (cenario, usuario) = CenarioComAna();

        var r = await cenario.AlterarSenha().ExecutarAsync(
            cenario.Solicitante(usuario), new AlterarSenhaDto("errada", "NovaSenha@456"));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro); // 400 e não 401: o token é válido
        Assert.Contains("atual", r.Erro!);
        Assert.Equal("hash:" + Cenario.SenhaPadrao, usuario.Senha);
        Assert.Equal("AlterarSenhaFalha", cenario.Banco.Logs.Single().Acao);
    }

    [Theory]
    [InlineData("curta1!")]          // menos de 8
    [InlineData("semnumero@Aa")]     // sem dígito
    [InlineData("SEMMINUSCULA1@")]   // sem minúscula
    [InlineData("semmaiuscula1@")]   // sem maiúscula
    [InlineData("SemSimbolo123")]    // sem símbolo
    public async Task AlterarSenha_NovaSenhaFraca_EhRecusada(string nova)
    {
        var (cenario, usuario) = CenarioComAna();

        var r = await cenario.AlterarSenha().ExecutarAsync(
            cenario.Solicitante(usuario), new AlterarSenhaDto(Cenario.SenhaPadrao, nova));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
        Assert.Equal("hash:" + Cenario.SenhaPadrao, usuario.Senha);
    }

    [Fact]
    public async Task AlterarSenha_NovaIgualAAtual_EhRecusada()
    {
        var (cenario, usuario) = CenarioComAna();

        var r = await cenario.AlterarSenha().ExecutarAsync(
            cenario.Solicitante(usuario), new AlterarSenhaDto(Cenario.SenhaPadrao, Cenario.SenhaPadrao));

        Assert.Contains("diferente", r.Erro!);
    }

    [Fact]
    public async Task AlterarSenha_UsuarioInativo_EhNaoAutenticado()
    {
        var (cenario, usuario) = CenarioComAna();
        usuario.Ativo = false;

        var r = await cenario.AlterarSenha().ExecutarAsync(
            cenario.Solicitante(usuario), new AlterarSenhaDto(Cenario.SenhaPadrao, "NovaSenha@456"));

        Assert.Equal(TipoErro.NaoAutenticado, r.TipoDoErro);
    }

    // ---------- Esqueci minha senha (RF03) ----------

    [Fact]
    public async Task Solicitar_EmailCadastrado_EnviaOLinkComToken()
    {
        var (cenario, usuario) = CenarioComAna();

        var r = await cenario.RecuperarSenha().SolicitarAsync(new SolicitarRecuperacaoDto("ANA@hrauto.local"));

        Assert.True(r.Sucesso);
        var email = cenario.Email.Enviados.Single();
        Assert.Equal("ana@hrauto.local", email.Para);
        Assert.Contains("https://hrauto.local/redefinir-senha?token=", email.Corpo);
        Assert.Contains("30 minutos", email.Corpo);
        Assert.Equal("SolicitarRecuperacaoSenha", cenario.Banco.Logs.Single().Acao);
        Assert.DoesNotContain(usuario.Senha, email.Corpo); // o hash nunca vai no e-mail
    }

    [Theory]
    [InlineData("ninguem@hrauto.local")]
    public async Task Solicitar_EmailNaoCadastrado_RespondeOk_SemEnviarNada(string email)
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.RecuperarSenha().SolicitarAsync(new SolicitarRecuperacaoDto(email));

        Assert.True(r.Sucesso); // mesma resposta de quem tem conta: não revela quem é cadastrado
        Assert.Empty(cenario.Email.Enviados);
    }

    [Fact]
    public async Task Solicitar_UsuarioInativo_RespondeOk_SemEnviarNada()
    {
        var (cenario, usuario) = CenarioComAna();
        usuario.Ativo = false;

        var r = await cenario.RecuperarSenha().SolicitarAsync(new SolicitarRecuperacaoDto("ana@hrauto.local"));

        Assert.True(r.Sucesso);
        Assert.Empty(cenario.Email.Enviados);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem-arroba")]
    public async Task Solicitar_EmailInvalido_EhErroDeValidacao(string? email)
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.RecuperarSenha().SolicitarAsync(new SolicitarRecuperacaoDto(email));

        Assert.Equal(TipoErro.Validacao, r.TipoDoErro);
    }

    // ---------- Redefinir (RF03) ----------

    private static string TokenDoEmail(Cenario cenario)
    {
        var corpo = cenario.Email.Enviados.Last().Corpo;
        var inicio = corpo.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
        var fim = corpo.IndexOfAny(new[] { '\n', ' ' }, inicio);
        return Uri.UnescapeDataString(corpo[inicio..fim]);
    }

    [Fact]
    public async Task Redefinir_ComOTokenDoEmail_TrocaASenha()
    {
        var (cenario, usuario) = CenarioComAna();
        await cenario.RecuperarSenha().SolicitarAsync(new SolicitarRecuperacaoDto("ana@hrauto.local"));

        var r = await cenario.RecuperarSenha().RedefinirAsync(new RedefinirSenhaDto(TokenDoEmail(cenario), "Nova#Senha9"));

        Assert.True(r.Sucesso);
        Assert.Equal("hash:Nova#Senha9", usuario.Senha);
        Assert.Contains(cenario.Banco.Logs, l => l.Acao == "RedefinirSenha");
    }

    [Fact]
    public async Task Redefinir_OMesmoLinkDuasVezes_SoFuncionaNaPrimeira()
    {
        var (cenario, _) = CenarioComAna();
        await cenario.RecuperarSenha().SolicitarAsync(new SolicitarRecuperacaoDto("ana@hrauto.local"));
        var token = TokenDoEmail(cenario);

        var primeira = await cenario.RecuperarSenha().RedefinirAsync(new RedefinirSenhaDto(token, "Nova#Senha9"));
        var segunda = await cenario.RecuperarSenha().RedefinirAsync(new RedefinirSenhaDto(token, "Outra#Senha8"));

        Assert.True(primeira.Sucesso);
        Assert.False(segunda.Sucesso);
        Assert.Equal(RecuperarSenha.MensagemLinkInvalido, segunda.Erro);
    }

    [Fact]
    public async Task Redefinir_TokenExpirado_EhRecusado()
    {
        var (cenario, usuario) = CenarioComAna();
        await cenario.RecuperarSenha().SolicitarAsync(new SolicitarRecuperacaoDto("ana@hrauto.local"));
        cenario.Tokens.RedefinicaoExpirada = true;

        var r = await cenario.RecuperarSenha().RedefinirAsync(new RedefinirSenhaDto(TokenDoEmail(cenario), "Nova#Senha9"));

        Assert.Equal(RecuperarSenha.MensagemLinkInvalido, r.Erro);
        Assert.Equal("hash:" + Cenario.SenhaPadrao, usuario.Senha);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lixo")]
    public async Task Redefinir_TokenInvalido_EhRecusado(string? token)
    {
        var (cenario, _) = CenarioComAna();

        var r = await cenario.RecuperarSenha().RedefinirAsync(new RedefinirSenhaDto(token, "Nova#Senha9"));

        Assert.Equal(RecuperarSenha.MensagemLinkInvalido, r.Erro);
    }

    [Fact]
    public async Task Redefinir_SenhaAtualMudouDepoisDoPedido_InvalidaOLink()
    {
        var (cenario, usuario) = CenarioComAna();
        await cenario.RecuperarSenha().SolicitarAsync(new SolicitarRecuperacaoDto("ana@hrauto.local"));
        var token = TokenDoEmail(cenario);
        await cenario.AlterarSenha().ExecutarAsync(cenario.Solicitante(usuario), new AlterarSenhaDto(Cenario.SenhaPadrao, "Troca#Manual7"));

        var r = await cenario.RecuperarSenha().RedefinirAsync(new RedefinirSenhaDto(token, "Nova#Senha9"));

        Assert.False(r.Sucesso);
        Assert.Equal("hash:Troca#Manual7", usuario.Senha);
    }

    [Fact]
    public async Task Redefinir_NovaSenhaFraca_EhRecusada_EOLinkContinuaValido()
    {
        var (cenario, usuario) = CenarioComAna();
        await cenario.RecuperarSenha().SolicitarAsync(new SolicitarRecuperacaoDto("ana@hrauto.local"));
        var token = TokenDoEmail(cenario);

        var fraca = await cenario.RecuperarSenha().RedefinirAsync(new RedefinirSenhaDto(token, "fraca"));
        var forte = await cenario.RecuperarSenha().RedefinirAsync(new RedefinirSenhaDto(token, "Nova#Senha9"));

        Assert.False(fraca.Sucesso);
        Assert.True(forte.Sucesso);
        Assert.Equal("hash:Nova#Senha9", usuario.Senha);
    }

    [Fact]
    public async Task Redefinir_UsuarioDesativadoNoMeioDoCaminho_EhRecusado()
    {
        var (cenario, usuario) = CenarioComAna();
        await cenario.RecuperarSenha().SolicitarAsync(new SolicitarRecuperacaoDto("ana@hrauto.local"));
        var token = TokenDoEmail(cenario);
        usuario.Ativo = false;

        var r = await cenario.RecuperarSenha().RedefinirAsync(new RedefinirSenhaDto(token, "Nova#Senha9"));

        Assert.False(r.Sucesso);
    }

    [Fact]
    public void ImpressaoSenha_MudaQuandoOHashMuda_ETemDezesseisCaracteres()
    {
        var a = ImpressaoSenha.De("hash-antigo");
        var b = ImpressaoSenha.De("hash-novo");

        Assert.Equal(16, a.Length);
        Assert.NotEqual(a, b);
        Assert.True(ImpressaoSenha.Confere("hash-antigo", a));
        Assert.False(ImpressaoSenha.Confere("hash-antigo", b));
    }
}
