using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HRAuto.Application.Abstractions;
using HRAuto.Application.Autenticacao;
using HRAuto.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HRAuto.Infrastructure.Seguranca;

/// <summary>
/// Emite e lê os JWT do sistema (HS256). Há dois tipos de token, impossíveis de confundir:
/// - acesso: público "hrauto-app", chave principal, claims do usuário e perfil (validado pelo middleware JwtBearer);
/// - redefinição de senha: público próprio, chave derivada, claim de finalidade e a impressão da senha atual (uso único).
/// </summary>
public sealed class ServicoTokenJwt : IServicoToken
{
    private readonly OpcoesJwt _opcoes;
    private readonly IRelogio _relogio;
    private readonly JwtSecurityTokenHandler _manipulador = new();

    public ServicoTokenJwt(IOptions<OpcoesJwt> opcoes, IRelogio relogio)
    {
        _opcoes = opcoes.Value;
        _opcoes.Validar();
        _relogio = relogio;
    }

    public TokenAcesso GerarAcesso(DadosToken dados)
    {
        var emitidoEm = _relogio.Agora();
        var expiraEm = emitidoEm.AddMinutes(_opcoes.ExpiraMinutos);

        var claims = new[]
        {
            new Claim(NomesClaims.Usuario, dados.IdUsuario.ToString(CultureInfo.InvariantCulture)),
            new Claim(NomesClaims.Funcionario, dados.IdFuncionario.ToString(CultureInfo.InvariantCulture)),
            new Claim(NomesClaims.Codigo, dados.Codigo),
            new Claim(NomesClaims.Nome, dados.Nome),
            new Claim(NomesClaims.Email, dados.Email),
            new Claim(NomesClaims.Perfil, NomesPerfil.De(dados.Perfil)),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var token = Criar(claims, _opcoes.PublicoAcesso, _opcoes.ChaveDeAcesso(), emitidoEm, expiraEm);
        return new TokenAcesso(token, expiraEm);
    }

    public string GerarRedefinicaoSenha(int idUsuario, string impressaoSenha)
    {
        var emitidoEm = _relogio.Agora();
        var expiraEm = emitidoEm.AddMinutes(_opcoes.RedefinicaoMinutos);

        var claims = new[]
        {
            new Claim(NomesClaims.Usuario, idUsuario.ToString(CultureInfo.InvariantCulture)),
            new Claim(NomesClaims.Finalidade, NomesClaims.FinalidadeRedefinicaoSenha),
            new Claim(NomesClaims.ImpressaoSenha, impressaoSenha)
        };

        return Criar(claims, _opcoes.PublicoRedefinicao, _opcoes.ChaveDeRedefinicao(), emitidoEm, expiraEm);
    }

    public bool TentarLerRedefinicaoSenha(string token, out int idUsuario, out string impressaoSenha)
    {
        idUsuario = 0;
        impressaoSenha = string.Empty;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return false;

        // A validade é conferida à mão, com o relógio do sistema (IRelogio), para ser testável.
        var parametros = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _opcoes.Emissor,
            ValidateAudience = true,
            ValidAudience = _opcoes.PublicoRedefinicao,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _opcoes.ChaveDeRedefinicao(),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = false,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            _manipulador.ValidateToken(token, parametros, out var validado);
            if (validado is not JwtSecurityToken jwt) return false;
            if (jwt.ValidTo <= _relogio.Agora().UtcDateTime) return false;

            string? Valor(string tipo) => jwt.Claims.FirstOrDefault(c => c.Type == tipo)?.Value;

            if (Valor(NomesClaims.Finalidade) != NomesClaims.FinalidadeRedefinicaoSenha) return false;
            if (!int.TryParse(Valor(NomesClaims.Usuario), NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                return false;
            var impressao = Valor(NomesClaims.ImpressaoSenha);
            if (string.IsNullOrEmpty(impressao)) return false;

            idUsuario = id;
            impressaoSenha = impressao;
            return true;
        }
        catch (Exception e) when (e is SecurityTokenException or ArgumentException)
        {
            return false; // assinatura, público, formato ou algoritmo inválidos
        }
    }

    private string Criar(IEnumerable<Claim> claims, string publico, SecurityKey chave, DateTimeOffset emitidoEm, DateTimeOffset expiraEm)
    {
        var descritor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _opcoes.Emissor,
            Audience = publico,
            IssuedAt = emitidoEm.UtcDateTime,
            NotBefore = emitidoEm.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            SigningCredentials = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256)
        };
        return _manipulador.WriteToken(_manipulador.CreateToken(descritor));
    }
}
