using HRAuto.Domain.Common;
using HRAuto.Domain.Disciplina;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Domain.Comunicacao;

/// <summary>
/// Notificações da disciplina (RF33, RF36): avisam o funcionário que uma advertência foi registrada ou cancelada.
/// A sugestão da régua para o RH continua em <see cref="FabricaNotificacoes.SugestaoDaRegua"/>.
/// </summary>
public static class NotificacoesDisciplina
{
    private const int TamanhoMaximoMotivoNaMensagem = 120;

    public static Notificacao AdvertenciaRegistrada(Advertencia advertencia, DateTime agora) =>
        Criar(advertencia.IdFuncionario, "Advertência registrada",
            $"Foi registrada uma advertência ({Rotulo(advertencia.Tipo)}) de {advertencia.Data:dd/MM/yyyy}: {Resumo(advertencia.Motivo)}", agora);

    public static Notificacao AdvertenciaCancelada(Advertencia advertencia, DateTime agora) =>
        Criar(advertencia.IdFuncionario, "Advertência cancelada",
            $"A advertência ({Rotulo(advertencia.Tipo)}) de {advertencia.Data:dd/MM/yyyy} foi cancelada.", agora);

    private static Notificacao Criar(int idDestinatario, string titulo, string mensagem, DateTime agora)
    {
        if (idDestinatario <= 0) throw new DominioException("O destinatário da notificação é obrigatório.");
        return new Notificacao
        {
            IdFuncionario = idDestinatario,
            Tipo = TiposNotificacao.Advertencia,
            Titulo = titulo,
            Mensagem = mensagem,
            DataEnvio = agora,
            Lida = false
        };
    }

    private static string Rotulo(string tipo) => RegrasAdvertencia.LerTipo(tipo) switch
    {
        TipoAdvertencia.Leve => "leve",
        TipoAdvertencia.Suspensao => "suspensão",
        _ => "justa causa"
    };

    private static string Resumo(string motivo) =>
        motivo.Length <= TamanhoMaximoMotivoNaMensagem ? motivo : motivo[..TamanhoMaximoMotivoNaMensagem] + "…";
}
