using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HRAuto.Application.Abstractions;
using HRAuto.Application.Autenticacao;
using HRAuto.Domain.Enums;
using HRAuto.Infrastructure.Seguranca;
using HRAuto.Tests.Application;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HRAuto.Tests.Infrastructure;

/// <summary>RF01, RF03, RNF04: tokens JWT de acesso e de redefinição de senha (assinatura, validade, público e finalidade).</summary>
public class ServicoTokenJwtTests
{
    private const string ChaveDeTeste = "chave-de-teste-com-pelo-menos-32-caracteres!!";

    private static OpcoesJwt Opcoes(string? chave = ChaveDeTeste) => new() { Chave = chave };

    private static (ServicoTokenJwt Servico, RelogioFixo Relogio, OpcoesJwt Opcoes) Montar(string? chave = ChaveDeTeste)
    {
        var opcoes = Opcoes(chave);
        var relogio = new RelogioFixo("2026-10-21T10:00:00");
        return (new ServicoTokenJwt(Options.Create(opcoes), relogio), relogio, opcoes);
    }

    private static readonly DadosToken Dados = new(501, 7, "FUNC-0007", "Ana Souza", "ana@hrauto.local", PerfilAcesso.RH);

    // Valida o token de acesso do mesmo jeito que o middleware JwtBearer da Api faz (mesmos parâmetros).
    private static ClaimsPrincipal ValidarComoAcesso(string token, OpcoesJwt opcoes, string? publico = null)
    {
        var manipulador = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return manipulador.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opcoes.Emissor,
            ValidateAudience = true,
            ValidAudience = publico ?? opcoes.PublicoAcesso,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = opcoes.ChaveDeAcesso(),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateLifetime = false,
            NameClaimType = NomesClaims.Nome,
            RoleClaimType = NomesClaims.Perfil
        }, out _);
    }

    // ---------- Token de acesso ----------

    [Fact]
    public void Acesso_TrazAsClaimsDoUsuario_EValidaComAChave()
    {
        var (servico, _, opcoes) = Montar();

        var token = servico.GerarAcesso(Dados);
        var principal = ValidarComoAcesso(token.Token, opcoes);

        Assert.Equal("501", principal.FindFirst(NomesClaims.Usuario)!.Value);
        Assert.Equal("7", principal.FindFirst(NomesClaims.Funcionario)!.Value);
        Assert.Equal("FUNC-0007", principal.FindFirst(NomesClaims.Codigo)!.Value);
        Assert.Equal("Ana Souza", principal.FindFirst(NomesClaims.Nome)!.Value);
        Assert.Equal("ana@hrauto.local", principal.FindFirst(NomesClaims.Email)!.Value);
        Assert.Equal("RH", principal.FindFirst(NomesClaims.Perfil)!.Value);
        Assert.True(principal.IsInRole("RH"));
        Assert.False(principal.IsInRole("Admin"));
    }

    [Fact]
    public void Acesso_ExpiraEm8Horas_EUsaHs256()
    {
        var (servico, _, _) = Montar();

        var token = servico.GerarAcesso(Dados);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);

        Assert.Equal(new DateTimeOffset(2026, 10, 21, 18, 0, 0, TimeSpan.FromHours(-3)), token.ExpiraEm);
        Assert.Equal(token.ExpiraEm.UtcDateTime, jwt.ValidTo);
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
        Assert.Equal("HR-Auto", jwt.Issuer);
        Assert.Equal("hrauto-app", jwt.Audiences.Single());
    }

    [Fact]
    public void Acesso_DoisTokensParaOMesmoUsuario_SaoDiferentes()
    {
        var (servico, _, _) = Montar();

        Assert.NotEqual(servico.GerarAcesso(Dados).Token, servico.GerarAcesso(Dados).Token); // claim jti
    }

    [Fact]
    public void Acesso_AssinadoComOutraChave_EhRecusado()
    {
        var (servico, _, _) = Montar();
        var token = servico.GerarAcesso(Dados);

        Assert.ThrowsAny<SecurityTokenException>(() => ValidarComoAcesso(token.Token, Opcoes("outra-chave-totalmente-diferente-0123456789")));
    }

    [Fact]
    public void Acesso_AdulteradoNoPayload_EhRecusado()
    {
        var (servico, _, opcoes) = Montar();
        var partes = servico.GerarAcesso(Dados).Token.Split('.');
        var payload = partes[1].ToCharArray();
        payload[10] = payload[10] == 'A' ? 'B' : 'A'; // vira outro perfil/usuário, mas a assinatura não confere
        var adulterado = $"{partes[0]}.{new string(payload)}.{partes[2]}";

        Assert.ThrowsAny<Exception>(() => ValidarComoAcesso(adulterado, opcoes));
    }

    // ---------- Token de redefinição de senha ----------

    [Fact]
    public void Redefinicao_VaiEVolta_ComUsuarioEImpressao()
    {
        var (servico, _, _) = Montar();

        var token = servico.GerarRedefinicaoSenha(501, "abcdef0123456789");

        Assert.True(servico.TentarLerRedefinicaoSenha(token, out var id, out var impressao));
        Assert.Equal(501, id);
        Assert.Equal("abcdef0123456789", impressao);
    }

    [Fact]
    public void Redefinicao_ValeAte30Minutos_DepoisExpira()
    {
        var (servico, relogio, _) = Montar();
        var token = servico.GerarRedefinicaoSenha(501, "abcdef0123456789");

        relogio.Definir("2026-10-21T10:29:59");
        Assert.True(servico.TentarLerRedefinicaoSenha(token, out _, out _));

        relogio.Definir("2026-10-21T10:30:01");
        Assert.False(servico.TentarLerRedefinicaoSenha(token, out var id, out var impressao));
        Assert.Equal(0, id);
        Assert.Equal(string.Empty, impressao);
    }

    [Fact]
    public void Redefinicao_NaoEhAceitaComoTokenDeAcesso()
    {
        var (servico, _, opcoes) = Montar();
        var token = servico.GerarRedefinicaoSenha(501, "abcdef0123456789");

        // Público e chave diferentes: o middleware de login recusa este token.
        Assert.ThrowsAny<SecurityTokenException>(() => ValidarComoAcesso(token, opcoes));
    }

    [Fact]
    public void Acesso_NaoEhAceitoComoTokenDeRedefinicao()
    {
        var (servico, _, _) = Montar();
        var acesso = servico.GerarAcesso(Dados).Token;

        Assert.False(servico.TentarLerRedefinicaoSenha(acesso, out _, out _));
    }

    [Fact]
    public void Redefinicao_AssinadaComOutraChave_EhRecusada()
    {
        var (servico, _, _) = Montar();
        var (outro, _, _) = Montar("outra-chave-totalmente-diferente-0123456789");
        var token = outro.GerarRedefinicaoSenha(501, "abcdef0123456789");

        Assert.False(servico.TentarLerRedefinicaoSenha(token, out _, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("lixo")]
    [InlineData("a.b.c")]
    // alg=none, sem assinatura (ataque clássico): {"alg":"none","typ":"JWT"} . {"sub":"1"} .
    [InlineData("eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiIxIn0.")]
    public void Redefinicao_TokenInvalido_DevolveFalso_SemLancarExcecao(string token)
    {
        var (servico, _, _) = Montar();

        Assert.False(servico.TentarLerRedefinicaoSenha(token, out _, out _));
    }

    [Fact]
    public void Redefinicao_TokenGiganteEhRecusadoSemProcessar()
    {
        var (servico, _, _) = Montar();

        Assert.False(servico.TentarLerRedefinicaoSenha(new string('a', 5000), out _, out _));
    }

    // ---------- Configuração (OpcoesJwt) ----------

    [Fact]
    public void Configuracao_Valida_Passa()
    {
        Opcoes().Validar();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curta-demais")]
    public void Configuracao_ChaveAusenteOuCurta_FalhaNaSubida(string? chave)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => Opcoes(chave).Validar());

        Assert.Contains("Jwt__Chave", erro.Message);
    }

    [Fact]
    public void Configuracao_PublicosIguais_Falha()
    {
        var opcoes = Opcoes();
        opcoes.PublicoRedefinicao = opcoes.PublicoAcesso;

        Assert.Throws<InvalidOperationException>(() => opcoes.Validar());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(1441)]
    public void Configuracao_ValidadeDoAcessoForaDoLimite_Falha(int minutos)
    {
        var opcoes = Opcoes();
        opcoes.ExpiraMinutos = minutos;

        Assert.Throws<InvalidOperationException>(() => opcoes.Validar());
    }

    [Fact]
    public void Servico_ComConfiguracaoInvalida_NaoNasce()
    {
        Assert.Throws<InvalidOperationException>(() => Montar(chave: "curta"));
    }
}
