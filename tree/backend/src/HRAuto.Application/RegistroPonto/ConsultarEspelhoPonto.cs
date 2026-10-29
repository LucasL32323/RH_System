using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.RegistroPonto;

/// <summary>
/// Espelho de ponto (RF21): batidas e totais por período. Quem pode ver:
/// o colaborador, o próprio; o gestor, a equipe (quem tem ele como id_gestor); RH e Admin, todos (RNF06, RNF08).
/// Dias antes da admissão e dias futuros não aparecem. Sem datas informadas, devolve do dia 1 do mês até hoje.
/// </summary>
public sealed class ConsultarEspelhoPonto
{
    public const int MaximoDiasPorConsulta = 366;

    private readonly IFuncionarioRepositorio _funcionarios;
    private readonly IPontoRepositorio _pontos;
    private readonly IFeriadoRepositorio _feriados;
    private readonly IFeriasRepositorio _ferias;
    private readonly IBancoHorasRepositorio _bancoHoras;
    private readonly JornadaPadrao _jornada;
    private readonly CalculadoraJornada _calculadora;
    private readonly IRelogio _relogio;

    public ConsultarEspelhoPonto(
        IFuncionarioRepositorio funcionarios,
        IPontoRepositorio pontos,
        IFeriadoRepositorio feriados,
        IFeriasRepositorio ferias,
        IBancoHorasRepositorio bancoHoras,
        JornadaPadrao jornada,
        CalculadoraJornada calculadora,
        IRelogio relogio)
    {
        _funcionarios = funcionarios;
        _pontos = pontos;
        _feriados = feriados;
        _ferias = ferias;
        _bancoHoras = bancoHoras;
        _jornada = jornada;
        _calculadora = calculadora;
        _relogio = relogio;
    }

    /// <param name="codigo">FUNC-0001 do funcionário consultado; vazio = o próprio solicitante.</param>
    public async Task<Resultado<EspelhoPontoDto>> ExecutarAsync(
        Solicitante solicitante, string? codigo, DateOnly? de, DateOnly? ate, CancellationToken cancelamento = default)
    {
        var hoje = _relogio.Hoje();
        var inicio = de ?? new DateOnly(hoje.Year, hoje.Month, 1);
        var fim = ate ?? hoje;

        if (fim < inicio)
            return Resultado<EspelhoPontoDto>.Falha("A data final não pode ser anterior à inicial.");
        if (fim.DayNumber - inicio.DayNumber + 1 > MaximoDiasPorConsulta)
            return Resultado<EspelhoPontoDto>.Falha($"O período pode ter no máximo {MaximoDiasPorConsulta} dias.");

        var idAlvo = solicitante.IdFuncionario;
        if (!string.IsNullOrWhiteSpace(codigo))
        {
            if (!CodigoFuncionario.TentarConverter(codigo, out var codigoLido))
                return Resultado<EspelhoPontoDto>.Falha("Código inválido. Use o formato FUNC-0001.");
            idAlvo = codigoLido.Id;
        }

        var alvo = await _funcionarios.ObterPorIdAsync(idAlvo, cancelamento);
        if (alvo is null)
            return Resultado<EspelhoPontoDto>.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado.");

        var proprio = alvo.Id == solicitante.IdFuncionario;
        var daEquipe = alvo.IdGestor == solicitante.IdFuncionario;
        if (!proprio && !daEquipe && !solicitante.Perfil.EhRhOuAdmin())
            return Resultado<EspelhoPontoDto>.Falha(TipoErro.Proibido, "Você não tem permissão para ver o ponto deste funcionário.");

        var detalhes = await _funcionarios.ObterDetalhesAsync(alvo.Id, cancelamento);
        var pontos = (await _pontos.ListarPeriodoAsync(alvo.Id, inicio, fim, cancelamento)).ToDictionary(p => p.Data);
        var feriados = await _feriados.ListarPeriodoAsync(inicio, fim, cancelamento);
        var datasDeFeriado = ResolvedorContextoDia.DatasDeFeriadoAplicaveis(feriados, detalhes?.Cidade, detalhes?.Estado);
        var ferias = await _ferias.ListarAprovadasNoPeriodoAsync(alvo.Id, inicio, fim, cancelamento);

        var resumos = new List<ResumoDia>();
        var dias = new List<DiaEspelhoDto>();

        for (var dia = inicio; dia <= fim; dia = dia.AddDays(1))
        {
            pontos.TryGetValue(dia, out var ponto);

            // Antes da admissão e dias que ainda não aconteceram não entram no espelho (a menos que haja batida gravada).
            if (ponto is null && (dia < alvo.DataAdmissao || dia > hoje)) continue;

            var contexto = ResolvedorContextoDia.Resolver(dia, _jornada, ferias, datasDeFeriado);
            var resumo = _calculadora.Calcular(ponto, dia, contexto, diaEncerrado: dia < hoje);
            resumos.Add(resumo);

            dias.Add(new DiaEspelhoDto(
                dia, resumo.Contexto, resumo.Situacao,
                ponto?.Entrada, ponto?.SaidaIntervalo, ponto?.RetornoIntervalo, ponto?.Saida,
                ResumoDiaDto.Minutos(resumo.Trabalhado), ResumoDiaDto.Minutos(resumo.HorasExtras),
                ResumoDiaDto.Minutos(resumo.HorasFaltantes), ResumoDiaDto.Minutos(resumo.Atraso)));
        }

        var total = CalculadoraJornada.Totalizar(resumos);
        var totais = new TotaisEspelhoDto(
            ResumoDiaDto.Minutos(total.Trabalhado), ResumoDiaDto.Minutos(total.HorasExtras),
            ResumoDiaDto.Minutos(total.HorasFaltantes), ResumoDiaDto.Minutos(total.Atrasos),
            total.DiasTrabalhados, total.Faltas, total.DiasInconsistentes, ResumoDiaDto.Minutos(total.Saldo));

        var saldoBanco = RegrasBancoHoras.Saldo(await _bancoHoras.ListarTodosAsync(alvo.Id, cancelamento));

        return Resultado<EspelhoPontoDto>.Ok(new EspelhoPontoDto(
            CodigoFuncionario.Formatar(alvo.Id), alvo.Nome, inicio, fim, dias, totais, saldoBanco));
    }
}
