using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Enums;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Autenticacao;

/// <summary>Cabeçalho do dashboard (RF45, RF11): código, nome, cargo e perfil de acesso de quem está logado.</summary>
public sealed record MeusDadosDto(
    string Codigo,
    string Nome,
    string Email,
    string Cargo,
    string Departamento,
    string Perfil,
    bool EhGestor,
    DateOnly DataAdmissao);

public sealed class ConsultarMeusDados
{
    private readonly IFuncionarioRepositorio _funcionarios;

    public ConsultarMeusDados(IFuncionarioRepositorio funcionarios) => _funcionarios = funcionarios;

    public async Task<Resultado<MeusDadosDto>> ExecutarAsync(Solicitante solicitante, CancellationToken cancelamento = default)
    {
        var funcionario = await _funcionarios.ObterPorIdAsync(solicitante.IdFuncionario, cancelamento);
        if (funcionario is null || !funcionario.Ativo)
            return Resultado<MeusDadosDto>.Falha(TipoErro.NaoAutenticado, "Sessão inválida. Entre novamente.");

        var ehGestor = await _funcionarios.EhGestorAsync(funcionario.Id, cancelamento);

        return Resultado<MeusDadosDto>.Ok(new MeusDadosDto(
            CodigoFuncionario.Formatar(funcionario.Id),
            funcionario.Nome,
            funcionario.Email,
            funcionario.Cargo?.NomeCargo ?? string.Empty,
            funcionario.Departamento?.Nome ?? string.Empty,
            NomesPerfil.De(solicitante.Perfil),
            ehGestor,
            funcionario.DataAdmissao));
    }
}

/// <summary>
/// RF02: o app apaga o token do aparelho; aqui só registramos o logout na auditoria.
/// O JWT é sem estado (RNF03) e continua tecnicamente válido até expirar, por isso a validade é curta (8 h).
/// </summary>
public sealed class EncerrarSessao
{
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;

    public EncerrarSessao(IAuditoria auditoria, IUnidadeDeTrabalho unidade)
    {
        _auditoria = auditoria;
        _unidade = unidade;
    }

    public async Task<Resultado> ExecutarAsync(Solicitante solicitante, CancellationToken cancelamento = default)
    {
        _auditoria.Registrar(solicitante.IdUsuario, "Logout", "USUARIO", solicitante.IdUsuario, null);
        await _unidade.SalvarAsync(cancelamento);
        return Resultado.Ok();
    }
}
