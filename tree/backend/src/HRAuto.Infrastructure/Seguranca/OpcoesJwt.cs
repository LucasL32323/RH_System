using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace HRAuto.Infrastructure.Seguranca;

/// <summary>
/// Configuração do JWT (seção "Jwt"). A chave NUNCA vai no appsettings.json versionado:
/// em desenvolvimento fica em appsettings.Development.json; em produção, na variável de ambiente Jwt__Chave.
/// </summary>
public sealed class OpcoesJwt
{
    public const string Secao = "Jwt";
    public const int TamanhoMinimoChaveBytes = 32;

    /// <summary>Segredo HMAC-SHA256 (mínimo de 32 caracteres/bytes).</summary>
    public string? Chave { get; set; }

    public string Emissor { get; set; } = "HR-Auto";

    /// <summary>Público do token de acesso (o app).</summary>
    public string PublicoAcesso { get; set; } = "hrauto-app";

    /// <summary>Público do token de redefinição de senha. Diferente do de acesso para que um nunca valha como o outro.</summary>
    public string PublicoRedefinicao { get; set; } = "hrauto-redefinicao-senha";

    /// <summary>Validade do token de acesso. Curta, porque o JWT sem estado não pode ser revogado antes de expirar.</summary>
    public int ExpiraMinutos { get; set; } = 480;

    /// <summary>Validade do link "Esqueci minha senha" (RF03: 30 minutos).</summary>
    public int RedefinicaoMinutos { get; set; } = 30;

    /// <summary>Falha cedo, na subida da API, se a configuração estiver incompleta ou insegura.</summary>
    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Chave) || Encoding.UTF8.GetByteCount(Chave) < TamanhoMinimoChaveBytes)
            throw new InvalidOperationException(
                $"Jwt:Chave deve ter pelo menos {TamanhoMinimoChaveBytes} caracteres. " +
                "Defina a variável de ambiente Jwt__Chave (ou use appsettings.Development.json em desenvolvimento).");
        if (ExpiraMinutos is < 5 or > 1440)
            throw new InvalidOperationException("Jwt:ExpiraMinutos deve estar entre 5 e 1440.");
        if (RedefinicaoMinutos is < 1 or > 60)
            throw new InvalidOperationException("Jwt:RedefinicaoMinutos deve estar entre 1 e 60.");
        if (string.IsNullOrWhiteSpace(Emissor) || string.IsNullOrWhiteSpace(PublicoAcesso) || string.IsNullOrWhiteSpace(PublicoRedefinicao))
            throw new InvalidOperationException("Jwt:Emissor, Jwt:PublicoAcesso e Jwt:PublicoRedefinicao são obrigatórios.");
        if (PublicoAcesso == PublicoRedefinicao)
            throw new InvalidOperationException("Jwt:PublicoAcesso e Jwt:PublicoRedefinicao devem ser diferentes.");
    }

    public SymmetricSecurityKey ChaveDeAcesso() => new(Encoding.UTF8.GetBytes(Chave!));

    /// <summary>Chave derivada (HMAC da chave principal): o token de redefinição é assinado com outra chave.</summary>
    public SymmetricSecurityKey ChaveDeRedefinicao() =>
        new(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Chave!), Encoding.UTF8.GetBytes("hrauto/redefinicao-senha")));
}
