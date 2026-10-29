using System.Globalization;
using HRAuto.Application.Abstractions;
using HRAuto.Application.Autenticacao;
using HRAuto.Application.Common;
using HRAuto.Application.RegistroPonto;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;

namespace HRAuto.Tests.Application;

// Dublês em memória para testar os casos de uso da Application sem banco, sem HTTP e sem BCrypt.

internal static class ApoioApp
{
    /// <summary>O Id das entidades tem setter protegido (só o banco define); nos testes usamos reflexão.</summary>
    public static T DefinirId<T>(this T entidade, int id) where T : EntidadeBase
    {
        typeof(EntidadeBase).GetProperty(nameof(EntidadeBase.Id))!.SetValue(entidade, id);
        return entidade;
    }

    public static DateOnly Data(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static TimeOnly Hora(string texto) => TimeOnly.ParseExact(texto, "HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>Relógio que o teste move à vontade (sempre em Brasília, UTC-3).</summary>
internal sealed class RelogioFixo : IRelogio
{
    private DateTimeOffset _agora;

    public RelogioFixo(string isoBrasilia) => Definir(isoBrasilia);

    public void Definir(string isoBrasilia) =>
        _agora = DateTimeOffset.Parse(isoBrasilia + "-03:00", CultureInfo.InvariantCulture);

    public DateTimeOffset Agora() => _agora;

    public DateOnly Hoje() => DateOnly.FromDateTime(_agora.DateTime);
}

internal sealed class HashFalso : IHashSenha
{
    public string Gerar(string senha) => "hash:" + senha;

    public bool Verificar(string senha, string hash) => hash == "hash:" + senha;
}

internal sealed class ServicoTokenFalso : IServicoToken
{
    private readonly IRelogio _relogio;
    public bool RedefinicaoExpirada { get; set; }

    public ServicoTokenFalso(IRelogio relogio) => _relogio = relogio;

    public TokenAcesso GerarAcesso(DadosToken dados) =>
        new($"jwt-{dados.IdUsuario}-{dados.Perfil}", _relogio.Agora().AddHours(8));

    public string GerarRedefinicaoSenha(int idUsuario, string impressaoSenha) => $"reset|{idUsuario}|{impressaoSenha}";

    public bool TentarLerRedefinicaoSenha(string token, out int idUsuario, out string impressaoSenha)
    {
        idUsuario = 0;
        impressaoSenha = string.Empty;
        var partes = token.Split('|');
        if (RedefinicaoExpirada || partes.Length != 3 || partes[0] != "reset" || !int.TryParse(partes[1], out idUsuario))
            return false;
        impressaoSenha = partes[2];
        return true;
    }
}

internal sealed class EnviadorEmailFalso : IEnviadorEmail
{
    public List<(string Para, string Assunto, string Corpo)> Enviados { get; } = new();

    /// <summary>Quando verdadeiro, o envio falha (simula o servidor de e-mail fora do ar).</summary>
    public bool Falhar { get; set; }

    public Task EnviarAsync(string destinatario, string assunto, string corpo, CancellationToken cancelamento = default)
    {
        if (Falhar) throw new InvalidOperationException("SMTP indisponível");
        Enviados.Add((destinatario, assunto, corpo));
        return Task.CompletedTask;
    }
}

/// <summary>"Banco" em memória: as listas fazem o papel das tabelas e SalvarAsync atribui os ids.</summary>
internal sealed class BancoFake
{
    private int _proximoId = 1000;

    public List<Usuario> Usuarios { get; } = new();
    public List<Funcionario> Funcionarios { get; } = new();
    public List<FuncionarioDetalhes> Detalhes { get; } = new();
    public List<Ponto> Pontos { get; } = new();
    public List<Feriado> Feriados { get; } = new();
    public List<Ferias> Ferias { get; } = new();
    public List<BancoHoras> BancoHoras { get; } = new();
    public List<Notificacao> Notificacoes { get; } = new();
    public List<LogAuditoria> Logs { get; } = new();
    public List<Cargo> Cargos { get; } = new();
    public List<Departamento> Departamentos { get; } = new();
    public List<Advertencia> Advertencias { get; } = new();
    public List<ManutencaoPonto> ManutencoesPonto { get; } = new();
    public List<Holerite> Holerites { get; } = new();

    public int Salvamentos { get; set; }
    public int TransacoesConfirmadas { get; set; }
    public int TransacoesDescartadas { get; set; }
    public bool ConflitoNoProximoSalvar { get; set; }

    public void AtribuirIds()
    {
        void Atribuir<T>(IEnumerable<T> lista) where T : EntidadeBase
        {
            foreach (var item in lista.Where(i => i.Id == 0)) item.DefinirId(++_proximoId);
        }

        Atribuir(Funcionarios);
        Atribuir(Usuarios);
        Atribuir(Cargos);
        Atribuir(Departamentos);
        Atribuir(Advertencias);
        Atribuir(Feriados);
        Atribuir(Ferias);
        Atribuir(Pontos);
        Atribuir(ManutencoesPonto);
        Atribuir(Holerites);
        Atribuir(BancoHoras);
        Atribuir(Notificacoes);
        Atribuir(Logs);
    }
}

internal sealed class UnidadeDeTrabalhoFake : IUnidadeDeTrabalho
{
    private readonly BancoFake _banco;

    public UnidadeDeTrabalhoFake(BancoFake banco) => _banco = banco;

    public Task SalvarAsync(CancellationToken cancelamento = default)
    {
        if (_banco.ConflitoNoProximoSalvar)
        {
            _banco.ConflitoNoProximoSalvar = false;
            throw new ConflitoDePersistenciaException("chave única violada");
        }

        _banco.AtribuirIds();
        _banco.Salvamentos++;
        return Task.CompletedTask;
    }

    public Task<ITransacao> IniciarTransacaoAsync(CancellationToken cancelamento = default) =>
        Task.FromResult<ITransacao>(new TransacaoFake(_banco));

    private sealed class TransacaoFake : ITransacao
    {
        private readonly BancoFake _banco;
        private bool _confirmada;

        public TransacaoFake(BancoFake banco) => _banco = banco;

        public Task ConfirmarAsync(CancellationToken cancelamento = default)
        {
            _confirmada = true;
            _banco.TransacoesConfirmadas++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_confirmada) _banco.TransacoesDescartadas++;
            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class UsuarioRepositorioFake : IUsuarioRepositorio
{
    private readonly BancoFake _banco;

    public UsuarioRepositorioFake(BancoFake banco) => _banco = banco;

    public Task<Usuario?> ObterPorEmailAsync(string emailNormalizado, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Usuarios.FirstOrDefault(u => string.Equals(u.Email, emailNormalizado, StringComparison.OrdinalIgnoreCase)));

    public Task<Usuario?> ObterPorIdAsync(int idUsuario, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Usuarios.FirstOrDefault(u => u.Id == idUsuario));
}

internal sealed class FuncionarioRepositorioFake : IFuncionarioRepositorio
{
    private readonly BancoFake _banco;

    public FuncionarioRepositorioFake(BancoFake banco) => _banco = banco;

    public Task<Funcionario?> ObterPorIdAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Funcionarios.FirstOrDefault(f => f.Id == idFuncionario));

    public Task<FuncionarioDetalhes?> ObterDetalhesAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Detalhes.FirstOrDefault(d => d.IdFuncionario == idFuncionario));

    public Task<bool> EhGestorAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Funcionarios.Any(f => f.Ativo && f.IdGestor == idFuncionario));
}

internal sealed class PontoRepositorioFake : IPontoRepositorio
{
    private readonly BancoFake _banco;

    public PontoRepositorioFake(BancoFake banco) => _banco = banco;

    public Task<Ponto?> ObterDoDiaAsync(int idFuncionario, DateOnly data, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Pontos.FirstOrDefault(p => p.IdFuncionario == idFuncionario && p.Data == data));

    public Task<IReadOnlyList<Ponto>> ListarPeriodoAsync(int idFuncionario, DateOnly inicio, DateOnly fim, CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<Ponto>>(
            _banco.Pontos.Where(p => p.IdFuncionario == idFuncionario && p.Data >= inicio && p.Data <= fim).ToList());

    public void Adicionar(Ponto ponto) => _banco.Pontos.Add(ponto);
}

internal sealed class FeriadoRepositorioFake : IFeriadoRepositorio
{
    private readonly BancoFake _banco;

    public FeriadoRepositorioFake(BancoFake banco) => _banco = banco;

    public Task<IReadOnlyList<Feriado>> ListarPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<Feriado>>(_banco.Feriados.Where(f => f.Data >= inicio && f.Data <= fim).OrderBy(f => f.Data).ToList());

    public Task<Feriado?> ObterParaEdicaoAsync(int idFeriado, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Feriados.FirstOrDefault(f => f.Id == idFeriado));

    public Task<bool> ExisteAsync(DateOnly data, string tipo, string? cidade, string? estado, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Feriados.Any(f =>
            f.Data == data && f.Tipo == tipo && f.Estado == estado &&
            string.Equals(f.Cidade, cidade, StringComparison.OrdinalIgnoreCase)));

    public void Adicionar(Feriado feriado) => _banco.Feriados.Add(feriado);

    public void Remover(Feriado feriado) => _banco.Feriados.Remove(feriado);
}

internal sealed class FeriasRepositorioFake : IFeriasRepositorio
{
    private readonly BancoFake _banco;

    public FeriasRepositorioFake(BancoFake banco) => _banco = banco;

    public Task<IReadOnlyList<Ferias>> ListarAprovadasNoPeriodoAsync(
        int idFuncionario, DateOnly inicio, DateOnly fim, CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<Ferias>>(_banco.Ferias
            .Where(f => f.IdFuncionario == idFuncionario && f.Status == StatusSolicitacao.Aprovada
                        && f.DataInicio <= fim && f.DataFim >= inicio)
            .ToList());

    public Task<IReadOnlyList<Ferias>> ListarAtivasDoFuncionarioAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<Ferias>>(_banco.Ferias
            .Where(f => f.IdFuncionario == idFuncionario
                        && (f.Status == StatusSolicitacao.Pendente || f.Status == StatusSolicitacao.Aprovada))
            .ToList());

    public Task<Ferias?> ObterParaEdicaoAsync(int idFerias, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Ferias.FirstOrDefault(f => f.Id == idFerias));

    public Task<ResultadoPaginado<Ferias>> ListarAsync(FiltroFerias filtro, CancellationToken cancelamento = default)
    {
        var consulta = _banco.Ferias.AsEnumerable();
        if (filtro.IdFuncionario is { } id) consulta = consulta.Where(f => f.IdFuncionario == id);
        if (filtro.IdGestor is { } gestor)
            consulta = consulta.Where(f => _banco.Funcionarios.Any(x => x.Id == f.IdFuncionario && x.IdGestor == gestor));
        if (filtro.Status is { } status) consulta = consulta.Where(f => f.Status == status);

        var todas = consulta.OrderByDescending(f => f.DataSolicitacao).ThenByDescending(f => f.Id).ToList();
        return Task.FromResult(new ResultadoPaginado<Ferias>(
            todas.Skip(filtro.Paginacao.Pular).Take(filtro.Paginacao.Tamanho).ToList(), todas.Count));
    }

    public void Adicionar(Ferias ferias) => _banco.Ferias.Add(ferias);

    public void Remover(Ferias ferias) => _banco.Ferias.Remove(ferias);
}

internal sealed class BancoHorasRepositorioFake : IBancoHorasRepositorio
{
    private readonly BancoFake _banco;

    public BancoHorasRepositorioFake(BancoFake banco) => _banco = banco;

    public Task<IReadOnlyList<BancoHoras>> ListarDoDiaAsync(int idFuncionario, DateOnly data, CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<BancoHoras>>(
            _banco.BancoHoras.Where(b => b.IdFuncionario == idFuncionario && b.Data == data).ToList());

    public Task<IReadOnlyList<BancoHoras>> ListarTodosAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<BancoHoras>>(_banco.BancoHoras.Where(b => b.IdFuncionario == idFuncionario).ToList());

    public Task<IReadOnlyList<BancoHoras>> ListarPeriodoAsync(int idFuncionario, DateOnly inicio, DateOnly fim, CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<BancoHoras>>(_banco.BancoHoras
            .Where(b => b.IdFuncionario == idFuncionario && b.Data >= inicio && b.Data <= fim)
            .OrderByDescending(b => b.Data).ThenByDescending(b => b.Id).ToList());

    public void Adicionar(BancoHoras lancamento) => _banco.BancoHoras.Add(lancamento);
}

internal sealed class NotificacaoRepositorioFake : INotificacaoRepositorio
{
    private readonly BancoFake _banco;

    public NotificacaoRepositorioFake(BancoFake banco) => _banco = banco;

    public void Adicionar(Notificacao notificacao) => _banco.Notificacoes.Add(notificacao);

    public Task<ResultadoPaginado<Notificacao>> ListarAsync(
        int idFuncionario, bool somenteNaoLidas, Paginacao paginacao, CancellationToken cancelamento = default)
    {
        var consulta = _banco.Notificacoes.Where(n => n.IdFuncionario == idFuncionario && (!somenteNaoLidas || !n.Lida));
        var todas = consulta.OrderByDescending(n => n.DataEnvio).ThenByDescending(n => n.Id).ToList();
        return Task.FromResult(new ResultadoPaginado<Notificacao>(
            todas.Skip(paginacao.Pular).Take(paginacao.Tamanho).ToList(), todas.Count));
    }

    public Task<int> ContarNaoLidasAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Notificacoes.Count(n => n.IdFuncionario == idFuncionario && !n.Lida));

    public Task<Notificacao?> ObterParaEdicaoAsync(int idNotificacao, CancellationToken cancelamento = default) =>
        Task.FromResult(_banco.Notificacoes.FirstOrDefault(n => n.Id == idNotificacao));

    public Task<IReadOnlyList<Notificacao>> ListarNaoLidasParaEdicaoAsync(int idFuncionario, CancellationToken cancelamento = default) =>
        Task.FromResult<IReadOnlyList<Notificacao>>(_banco.Notificacoes.Where(n => n.IdFuncionario == idFuncionario && !n.Lida).ToList());
}

internal sealed class AuditoriaFake : IAuditoria
{
    private readonly BancoFake _banco;
    private readonly IRelogio _relogio;

    public AuditoriaFake(BancoFake banco, IRelogio relogio)
    {
        _banco = banco;
        _relogio = relogio;
    }

    public void Registrar(int idUsuario, string acao, string? tabela, int? idRegistro, string? descricao) =>
        _banco.Logs.Add(new LogAuditoria
        {
            IdUsuario = idUsuario,
            DataHora = _relogio.Agora().DateTime,
            Acao = acao,
            Tabela = tabela,
            IdRegistro = idRegistro,
            Descricao = descricao
        });
}

/// <summary>
/// Monta o cenário de um teste: relógio, banco em memória e os casos de uso prontos.
/// O relógio começa em quarta-feira, 21/10/2026, 08:00 (dia útil).
/// </summary>
internal sealed partial class Cenario
{
    public const string SenhaPadrao = "Senha@123";

    public RelogioFixo Relogio { get; }
    public BancoFake Banco { get; } = new();
    public HashFalso Hash { get; } = new();
    public ServicoTokenFalso Tokens { get; }
    public EnviadorEmailFalso Email { get; } = new();

    private readonly UnidadeDeTrabalhoFake _unidade;
    private readonly AuditoriaFake _auditoria;

    public Cenario(string agoraBrasilia = "2026-10-21T08:00:00")
    {
        Relogio = new RelogioFixo(agoraBrasilia);
        Tokens = new ServicoTokenFalso(Relogio);
        _unidade = new UnidadeDeTrabalhoFake(Banco);
        _auditoria = new AuditoriaFake(Banco, Relogio);
    }

    // ---- Dados ----

    public Funcionario NovoFuncionario(int id, string nome, int? idGestor = null, bool ativo = true, string admissao = "2024-03-11")
    {
        var funcionario = new Funcionario
        {
            Nome = nome,
            Email = $"{nome.ToLowerInvariant().Replace(' ', '.')}@hrauto.local",
            DataAdmissao = ApoioApp.Data(admissao),
            DataNascimento = new DateOnly(1995, 5, 5),
            Ativo = ativo,
            IdGestor = idGestor,
            Cargo = new Cargo { NomeCargo = "Analista" },
            Departamento = new Departamento { Nome = "Tecnologia" }
        }.DefinirId(id);
        Banco.Funcionarios.Add(funcionario);
        return funcionario;
    }

    public Usuario NovoUsuario(
        Funcionario funcionario, string email, string senha = SenhaPadrao, PerfilAcesso perfil = PerfilAcesso.Colaborador, bool ativo = true)
    {
        var usuario = new Usuario
        {
            IdFuncionario = funcionario.Id,
            Funcionario = funcionario,
            IdPerfil = (int)perfil,
            Email = email,
            Senha = Hash.Gerar(senha),
            Ativo = ativo
        }.DefinirId(500 + funcionario.Id);
        Banco.Usuarios.Add(usuario);
        return usuario;
    }

    public Solicitante Solicitante(Usuario usuario) =>
        new(usuario.Id, usuario.IdFuncionario, (PerfilAcesso)usuario.IdPerfil);

    // ---- Casos de uso ----

    private UsuarioRepositorioFake Usuarios => new(Banco);
    private FuncionarioRepositorioFake Funcionarios => new(Banco);
    private PontoRepositorioFake Pontos => new(Banco);

    public AutenticarUsuario Autenticar() => new(Usuarios, Hash, Tokens, _auditoria, _unidade, Relogio, new UsuarioGestaoRepositorioFake(Banco));

    public AlterarSenha AlterarSenha() => new(Usuarios, Hash, _auditoria, _unidade);

    public RecuperarSenha RecuperarSenha() =>
        new(Usuarios, Hash, Tokens, Email, _auditoria, _unidade, new OpcoesRecuperacaoSenha("https://hrauto.local/redefinir-senha"));

    public ConsultarMeusDados MeusDados() => new(Funcionarios);

    public FechamentoDoDia Fechamento() => new(
        Funcionarios, new FeriadoRepositorioFake(Banco), new FeriasRepositorioFake(Banco),
        new BancoHorasRepositorioFake(Banco), new NotificacaoRepositorioFake(Banco),
        JornadaPadrao.Padrao, new CalculadoraJornada(), Relogio);

    public RegistrarBatida RegistrarBatida() => new(Funcionarios, Pontos, Fechamento(), _auditoria, _unidade, Relogio);

    public ConsultarStatusPontoHoje StatusPontoHoje() => new(Pontos, Relogio);

    public SincronizarBatidas Sincronizar() => new(Funcionarios, Pontos, Fechamento(), _auditoria, _unidade, Relogio);

    public ConsultarEspelhoPonto Espelho() => new(
        Funcionarios, Pontos, new FeriadoRepositorioFake(Banco), new FeriasRepositorioFake(Banco),
        new BancoHorasRepositorioFake(Banco), JornadaPadrao.Padrao, new CalculadoraJornada(), Relogio);
}
