using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Cadastro;
using HRAuto.Domain.Common;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Seguranca;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Cadastro;

/// <summary>
/// RF12: desligamento e reativação. O cadastro nunca é apagado (RNF07): o desligamento grava a data, inativa o funcionário
/// e bloqueia o acesso. Reativar o funcionário NÃO devolve o acesso: o RH reativa o login em separado (evita volta silenciosa).
/// Ninguém desliga a si mesmo; o RH não desliga RH/Admin; não se desliga quem ainda tem subordinados ativos
/// nem o último Admin ativo.
/// </summary>
public sealed class AlterarSituacaoFuncionario
{
    private readonly IFuncionarioCadastroRepositorio _funcionarios;
    private readonly IUsuarioGestaoRepositorio _usuarios;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public AlterarSituacaoFuncionario(
        IFuncionarioCadastroRepositorio funcionarios,
        IUsuarioGestaoRepositorio usuarios,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio)
    {
        _funcionarios = funcionarios;
        _usuarios = usuarios;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<Resultado> DesligarAsync(
        Solicitante solicitante, string? codigoTexto, DesligarFuncionarioDto? dto, CancellationToken cancelamento = default)
    {
        var preparo = await PrepararAsync(solicitante, codigoTexto, cancelamento);
        if (preparo.Falha is not null) return preparo.Falha;
        var (funcionario, usuario) = (preparo.Funcionario!, preparo.Usuario);

        if (funcionario.Ativo)
        {
            var subordinados = await _funcionarios.ContarSubordinadosAtivosAsync(funcionario.Id, cancelamento);
            if (subordinados > 0)
                return Resultado.Falha(TipoErro.Conflito,
                    $"{funcionario.Nome} ainda é gestor de {subordinados} funcionário(s) ativo(s). Troque o gestor deles antes de desligar.");

            if (usuario is { Ativo: true } && (PerfilAcesso)usuario.IdPerfil == PerfilAcesso.Admin
                && await _usuarios.ContarAdminsAtivosAsync(cancelamento) <= 1)
                return Resultado.Falha(TipoErro.Conflito, "Este é o último administrador ativo; ele não pode ser desligado.");
        }

        try
        {
            RegrasFuncionario.Desligar(funcionario, dto?.Data ?? _relogio.Hoje(), _relogio.Hoje());
        }
        catch (DominioException e)
        {
            return Resultado.Falha(e.Message);
        }

        if (usuario is not null) usuario.Ativo = false; // sem funcionário ativo não há acesso

        _auditoria.Registrar(
            solicitante.IdUsuario, "DesligarFuncionario", "FUNCIONARIO", funcionario.Id,
            $"{funcionario.Codigo} desligado em {funcionario.DataDesligamento:dd/MM/yyyy}");
        await _unidade.SalvarAsync(cancelamento);
        return Resultado.Ok();
    }

    public async Task<Resultado> ReativarAsync(
        Solicitante solicitante, string? codigoTexto, CancellationToken cancelamento = default)
    {
        var preparo = await PrepararAsync(solicitante, codigoTexto, cancelamento);
        if (preparo.Falha is not null) return preparo.Falha;
        var funcionario = preparo.Funcionario!;

        try
        {
            RegrasFuncionario.Reativar(funcionario);
        }
        catch (DominioException e)
        {
            return Resultado.Falha(TipoErro.Conflito, e.Message);
        }

        _auditoria.Registrar(
            solicitante.IdUsuario, "ReativarFuncionario", "FUNCIONARIO", funcionario.Id, $"{funcionario.Codigo} reativado");
        await _unidade.SalvarAsync(cancelamento);
        return Resultado.Ok();
    }

    private sealed record Preparo(Domain.Entities.Funcionario? Funcionario, Domain.Entities.Usuario? Usuario, Resultado? Falha);

    /// <summary>Verificações comuns: perfil, código, existência, "não em si mesmo" e hierarquia de perfis.</summary>
    private async Task<Preparo> PrepararAsync(Solicitante solicitante, string? codigoTexto, CancellationToken cancelamento)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return new(null, null, Resultado.Falha(TipoErro.Proibido, "Apenas o RH pode alterar a situação de funcionários."));
        if (!CodigoFuncionario.TentarConverter(codigoTexto, out var codigo))
            return new(null, null, Resultado.Falha("Código inválido. Use o formato FUNC-0001."));
        if (codigo.Id == solicitante.IdFuncionario)
            return new(null, null, Resultado.Falha(TipoErro.Proibido, "Você não pode alterar a sua própria situação."));

        var funcionario = await _funcionarios.ObterParaEdicaoAsync(codigo.Id, cancelamento);
        if (funcionario is null)
            return new(null, null, Resultado.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado."));

        var usuario = await _usuarios.ObterPorFuncionarioAsync(funcionario.Id, cancelamento);
        if (usuario is not null && !RegrasAcesso.PodeGerenciar(solicitante.Perfil, (PerfilAcesso)usuario.IdPerfil))
            return new(null, null, Resultado.Falha(TipoErro.Proibido, "Você não pode alterar um usuário RH ou Admin."));

        return new(funcionario, usuario, null);
    }
}
