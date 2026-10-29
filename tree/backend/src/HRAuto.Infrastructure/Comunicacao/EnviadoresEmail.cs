using System.Net;
using System.Net.Mail;
using HRAuto.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRAuto.Infrastructure.Comunicacao;

/// <summary>Configuração de e-mail (seção "Email"). Sem Host, o sistema usa <see cref="EnviadorEmailLog"/>.</summary>
public sealed class OpcoesEmail
{
    public const string Secao = "Email";

    public string? Host { get; set; }
    public int Porta { get; set; } = 587;
    public bool UsarSsl { get; set; } = true;
    public string? Usuario { get; set; }
    public string? Senha { get; set; }
    public string Remetente { get; set; } = "nao-responder@hrauto.local";

    /// <summary>Só para desenvolvimento: escreve o corpo da mensagem (com o link) no log. Nunca ligue em produção.</summary>
    public bool LogarCorpo { get; set; }
}

/// <summary>
/// Substitui o envio quando não há servidor SMTP configurado (desenvolvimento e testes): registra no log que a
/// mensagem "seria enviada". O corpo (que contém o link com o token) só é registrado se Email:LogarCorpo = true.
/// </summary>
public sealed class EnviadorEmailLog : IEnviadorEmail
{
    private readonly ILogger<EnviadorEmailLog> _log;
    private readonly OpcoesEmail _opcoes;

    public EnviadorEmailLog(ILogger<EnviadorEmailLog> log, IOptions<OpcoesEmail> opcoes)
    {
        _log = log;
        _opcoes = opcoes.Value;
    }

    public Task EnviarAsync(string destinatario, string assunto, string corpo, CancellationToken cancelamento = default)
    {
        if (_opcoes.LogarCorpo)
            _log.LogWarning("E-mail (simulado) para {Destinatario} — {Assunto}\n{Corpo}", destinatario, assunto, corpo);
        else
            _log.LogInformation("E-mail (simulado) para {Destinatario} — {Assunto}. Configure Email:Host para envio real.", destinatario, assunto);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Envio real por SMTP (System.Net.Mail). Nunca lança exceção para o chamador: a resposta de "Esqueci minha senha"
/// não pode mudar conforme o envio dê certo (isso revelaria quais e-mails existem). Falhas vão para o log, sem o corpo.
/// </summary>
public sealed class EnviadorEmailSmtp : IEnviadorEmail
{
    private readonly ILogger<EnviadorEmailSmtp> _log;
    private readonly OpcoesEmail _opcoes;

    public EnviadorEmailSmtp(ILogger<EnviadorEmailSmtp> log, IOptions<OpcoesEmail> opcoes)
    {
        _log = log;
        _opcoes = opcoes.Value;
    }

    public async Task EnviarAsync(string destinatario, string assunto, string corpo, CancellationToken cancelamento = default)
    {
        try
        {
            using var cliente = new SmtpClient(_opcoes.Host, _opcoes.Porta) { EnableSsl = _opcoes.UsarSsl };
            if (!string.IsNullOrWhiteSpace(_opcoes.Usuario))
                cliente.Credentials = new NetworkCredential(_opcoes.Usuario, _opcoes.Senha);

            using var mensagem = new MailMessage(_opcoes.Remetente, destinatario, assunto, corpo);
            await cliente.SendMailAsync(mensagem, cancelamento);
        }
        catch (Exception e)
        {
            _log.LogError(e, "Falha ao enviar e-mail para {Destinatario} — {Assunto}.", destinatario, assunto);
        }
    }
}
