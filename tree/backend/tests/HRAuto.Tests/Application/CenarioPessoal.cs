using HRAuto.Application.Afastamentos;
using HRAuto.Application.BancoDeHoras;
using HRAuto.Application.Calendario;
using HRAuto.Application.Comunicacao;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Tests.Application;

// Atalhos da Fase 4 (parte 3): banco de horas, notificações, feriados e férias.
// Usa a empresa de CenarioRh: Admin (1), RH (2), gestor (3) com Ana (4) e Bruno (5) na equipe, e Carla (6) fora dela.

internal sealed partial class Cenario
{
    private FeriadoRepositorioFake RepositorioFeriados => new(Banco);
    private FeriasRepositorioFake RepositorioFerias => new(Banco);
    private BancoHorasRepositorioFake RepositorioBancoHoras => new(Banco);
    private NotificacaoRepositorioFake RepositorioNotificacoes => new(Banco);

    // ---- Dados ----

    public BancoHoras NovoLancamento(Funcionario funcionario, string data, string tipo, double horas, string origem = OrigensBancoHoras.Ponto)
    {
        var lancamento = new BancoHoras
        {
            IdFuncionario = funcionario.Id, Data = ApoioApp.Data(data), Tipo = tipo, Quantidade = horas,
            Origem = origem, Observacao = origem == OrigensBancoHoras.AjusteRH ? "Ajuste de teste" : null
        }.DefinirId(Banco.BancoHoras.Count + 1);
        Banco.BancoHoras.Add(lancamento);
        return lancamento;
    }

    public Notificacao NovaNotificacao(Funcionario destinatario, string titulo, bool lida = false, string envio = "2026-10-20T10:00:00")
    {
        var notificacao = new Notificacao
        {
            IdFuncionario = destinatario.Id, Tipo = TiposNotificacao.Aviso, Titulo = titulo, Mensagem = $"Mensagem de {titulo}",
            DataEnvio = DateTime.Parse(envio, System.Globalization.CultureInfo.InvariantCulture), Lida = lida
        }.DefinirId(Banco.Notificacoes.Count + 1);
        Banco.Notificacoes.Add(notificacao);
        return notificacao;
    }

    public Feriado NovoFeriado(string data, string nome, string tipo = TiposFeriado.Nacional, string? cidade = null, string? estado = null)
    {
        var feriado = new Feriado { Data = ApoioApp.Data(data), Nome = nome, Tipo = tipo, Cidade = cidade, Estado = estado }
            .DefinirId(Banco.Feriados.Count + 1);
        Banco.Feriados.Add(feriado);
        return feriado;
    }

    public Ferias NovasFerias(Funcionario funcionario, string inicio, string fim, string status = StatusSolicitacao.Pendente, string solicitadaEm = "2026-10-01")
    {
        var i = ApoioApp.Data(inicio);
        var f = ApoioApp.Data(fim);
        var ferias = new Ferias
        {
            IdFuncionario = funcionario.Id, DataInicio = i, DataFim = f, Dias = f.DayNumber - i.DayNumber + 1,
            Status = status, DataSolicitacao = ApoioApp.Data(solicitadaEm)
        }.DefinirId(Banco.Ferias.Count + 1);
        Banco.Ferias.Add(ferias);
        return ferias;
    }

    // ---- Casos de uso ----

    public ConsultarBancoHoras ConsultarBancoHoras() => new(FuncionariosCadastro, RepositorioBancoHoras, Relogio);

    public AjustarBancoHoras AjustarBancoHoras() =>
        new(FuncionariosCadastro, UsuariosGestao, RepositorioBancoHoras, RepositorioNotificacoes, _auditoria, _unidade, Relogio);

    public ListarNotificacoes ListarNotificacoes() => new(RepositorioNotificacoes);

    public ContarNotificacoesNaoLidas ContarNaoLidas() => new(RepositorioNotificacoes);

    public MarcarNotificacaoComoLida MarcarComoLida() => new(RepositorioNotificacoes, _unidade);

    public MarcarTodasNotificacoesComoLidas MarcarTodasComoLidas() => new(RepositorioNotificacoes, _unidade);

    public ListarFeriados ListarFeriados() => new(RepositorioFeriados, Relogio);

    public GerenciarFeriados GerenciarFeriados() => new(RepositorioFeriados, _auditoria, _unidade);

    public SolicitarFerias SolicitarFerias() => new(
        new FuncionarioRepositorioFake(Banco), RepositorioFeriados, RepositorioFerias, UsuariosGestao,
        RepositorioNotificacoes, _auditoria, _unidade, Relogio);

    public ListarFerias ListarFerias() => new(new FuncionarioRepositorioFake(Banco), FuncionariosCadastro, RepositorioFerias);

    public DecidirFerias DecidirFerias() => new(
        RepositorioFerias, FuncionariosCadastro, UsuariosGestao, RepositorioNotificacoes, _auditoria, _unidade, Relogio);

    public CancelarSolicitacaoFerias CancelarSolicitacaoFerias() => new(RepositorioFerias, _auditoria, _unidade);
}
