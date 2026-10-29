using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Common;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Jornada;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.BancoDeHoras;

/// <summary>
/// RF24: saldo e extrato do banco de horas. O próprio funcionário vê o seu (sem código = o meu); o gestor vê o da equipe
/// (subordinados diretos); RH e Admin veem de todos. Sem período, mostra os últimos 90 dias; o saldo é sempre o total.
/// </summary>
public sealed class ConsultarBancoHoras
{
    public const int DiasPadrao = 90;
    public const int DiasMaximos = 366;

    private readonly IFuncionarioCadastroRepositorio _funcionarios;
    private readonly IBancoHorasRepositorio _bancoHoras;
    private readonly IRelogio _relogio;

    public ConsultarBancoHoras(IFuncionarioCadastroRepositorio funcionarios, IBancoHorasRepositorio bancoHoras, IRelogio relogio)
    {
        _funcionarios = funcionarios;
        _bancoHoras = bancoHoras;
        _relogio = relogio;
    }

    public async Task<Resultado<BancoHorasDto>> ExecutarAsync(
        Solicitante solicitante, string? codigoTexto, DateOnly? inicio, DateOnly? fim, CancellationToken cancelamento = default)
    {
        var idAlvo = solicitante.IdFuncionario;
        if (!string.IsNullOrWhiteSpace(codigoTexto))
        {
            if (!CodigoFuncionario.TentarConverter(codigoTexto, out var codigo))
                return Resultado<BancoHorasDto>.Falha("Código inválido. Use o formato FUNC-0001.");
            idAlvo = codigo.Id;
        }

        var dataFim = fim ?? _relogio.Hoje();
        var dataInicio = inicio ?? dataFim.AddDays(-(DiasPadrao - 1));
        if (dataInicio > dataFim)
            return Resultado<BancoHorasDto>.Falha("A data inicial não pode ser posterior à data final.");
        if (dataFim.DayNumber - dataInicio.DayNumber + 1 > DiasMaximos)
            return Resultado<BancoHorasDto>.Falha($"O período pode ter no máximo {DiasMaximos} dias.");

        // 403 antes de 404: quem não pode ver o funcionário também não descobre se o código existe.
        if (idAlvo != solicitante.IdFuncionario && !solicitante.Perfil.EhRhOuAdmin()
            && !await _funcionarios.EhSubordinadoDiretoAsync(solicitante.IdFuncionario, idAlvo, cancelamento))
            return Resultado<BancoHorasDto>.Falha(TipoErro.Proibido, "Você não tem acesso a este funcionário.");

        var funcionario = await _funcionarios.ObterAsync(idAlvo, cancelamento);
        if (funcionario is null)
            return Resultado<BancoHorasDto>.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado.");

        IReadOnlyList<Domain.Entities.BancoHoras> todos;
        decimal saldo;
        try
        {
            todos = await _bancoHoras.ListarTodosAsync(idAlvo, cancelamento);
            saldo = RegrasBancoHoras.Saldo(todos);
        }
        catch (DominioException)
        {
            // Lançamento com tipo desconhecido no banco: erro de dados, não do usuário.
            return Resultado<BancoHorasDto>.Falha(TipoErro.Conflito, "Há um lançamento com tipo inválido no banco de horas. Procure o administrador.");
        }

        var extrato = todos
            .Where(l => l.Data >= dataInicio && l.Data <= dataFim)
            .OrderByDescending(l => l.Data).ThenByDescending(l => l.Id)
            .Select(MapeadorBancoHoras.Lancamento)
            .ToList();

        return Resultado<BancoHorasDto>.Ok(new BancoHorasDto(
            funcionario.Codigo, funcionario.Nome, saldo, MapeadorBancoHoras.FormatarSaldo(saldo), dataInicio, dataFim, extrato));
    }
}
