using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Seguranca;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Cadastro;

/// <summary>
/// RF06, RF07: o RH/Admin cria o acesso, troca o perfil, ativa/desativa o login e reenvia o convite.
/// Regras: Admin concede e gerencia qualquer perfil; RH só gerencia Colaborador; ninguém altera o próprio acesso;
/// o último Admin ativo não pode ser desativado nem rebaixado; o login só pode ser ativo se o funcionário estiver ativo.
/// Mudar o perfil vale a partir do próximo login (o token já emitido dura até 8 h).
/// </summary>
public sealed class GerenciarAcessoUsuario
{
    private readonly IFuncionarioCadastroRepositorio _funcionarios;
    private readonly IUsuarioGestaoRepositorio _usuarios;
    private readonly IHashSenha _hash;
    private readonly EnviadorDeConvite _convite;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;

    public GerenciarAcessoUsuario(
        IFuncionarioCadastroRepositorio funcionarios,
        IUsuarioGestaoRepositorio usuarios,
        IHashSenha hash,
        EnviadorDeConvite convite,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade)
    {
        _funcionarios = funcionarios;
        _usuarios = usuarios;
        _hash = hash;
        _convite = convite;
        _auditoria = auditoria;
        _unidade = unidade;
    }

    public async Task<Resultado<AcessoDto>> ConsultarAsync(
        Solicitante solicitante, string? codigoTexto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<AcessoDto>.Falha(TipoErro.Proibido, "Apenas o RH pode consultar acessos.");
        if (!CodigoFuncionario.TentarConverter(codigoTexto, out var codigo))
            return Resultado<AcessoDto>.Falha("Código inválido. Use o formato FUNC-0001.");
        if (await _funcionarios.ObterAsync(codigo.Id, cancelamento) is null)
            return Resultado<AcessoDto>.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado.");

        return Resultado<AcessoDto>.Ok(MapeadorCadastro.Acesso(await _usuarios.ObterPorFuncionarioAsync(codigo.Id, cancelamento)));
    }

    /// <summary>Cria o login de um funcionário que ainda não tem (e-mail do cadastro) e envia o convite.</summary>
    public async Task<Resultado<AcessoCriadoDto>> CriarAsync(
        Solicitante solicitante, string? codigoTexto, CriarAcessoDto? dto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<AcessoCriadoDto>.Falha(TipoErro.Proibido, "Apenas o RH pode criar acessos.");
        if (!CodigoFuncionario.TentarConverter(codigoTexto, out var codigo))
            return Resultado<AcessoCriadoDto>.Falha("Código inválido. Use o formato FUNC-0001.");
        if (codigo.Id == solicitante.IdFuncionario)
            return Resultado<AcessoCriadoDto>.Falha(TipoErro.Proibido, "Você não pode alterar o seu próprio acesso.");
        if (!MapeadorCadastro.TentarLerPerfil(dto?.Perfil, out var perfil))
            return Resultado<AcessoCriadoDto>.Falha("Perfil inválido. Use Colaborador, RH ou Admin.");
        if (!RegrasAcesso.PodeConceder(solicitante.Perfil, perfil))
            return Resultado<AcessoCriadoDto>.Falha(TipoErro.Proibido, $"Você não pode conceder o perfil {NomesPerfil.De(perfil)}.");

        var funcionario = await _funcionarios.ObterAsync(codigo.Id, cancelamento);
        if (funcionario is null)
            return Resultado<AcessoCriadoDto>.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado.");
        if (!funcionario.Ativo)
            return Resultado<AcessoCriadoDto>.Falha(TipoErro.Conflito, "O funcionário está desligado.");
        if (await _usuarios.ObterPorFuncionarioAsync(funcionario.Id, cancelamento) is not null)
            return Resultado<AcessoCriadoDto>.Falha(TipoErro.Conflito, "Este funcionário já tem acesso.");
        if (await _usuarios.EmailEmUsoAsync(funcionario.Email, null, cancelamento))
            return Resultado<AcessoCriadoDto>.Falha(TipoErro.Conflito, "Já existe um usuário com o e-mail deste funcionário.");

        var usuario = new Usuario
        {
            IdFuncionario = funcionario.Id,
            IdPerfil = (int)perfil,
            Email = funcionario.Email,
            Senha = _hash.Gerar(GeradorSenhaTemporaria.Gerar()),
            Ativo = true
        };
        _usuarios.Adicionar(usuario);

        try
        {
            await _unidade.SalvarAsync(cancelamento); // gera o id do usuário
            _auditoria.Registrar(
                solicitante.IdUsuario, "CriarAcesso", "USUARIO", usuario.Id,
                $"{funcionario.Codigo}: acesso {NomesPerfil.De(perfil)} criado");
            await _unidade.SalvarAsync(cancelamento);
        }
        catch (ConflitoDePersistenciaException)
        {
            return Resultado<AcessoCriadoDto>.Falha(TipoErro.Conflito, "Já existe um usuário com o e-mail deste funcionário.");
        }

        var enviado = await TentarEnviarConviteAsync(usuario, funcionario.Nome, cancelamento);
        return Resultado<AcessoCriadoDto>.Ok(new AcessoCriadoDto(funcionario.Codigo, NomesPerfil.De(perfil), enviado));
    }

    /// <summary>Troca o perfil e/ou ativa e desativa o login. Campos nulos não mudam nada.</summary>
    public async Task<Resultado<AcessoDto>> AlterarAsync(
        Solicitante solicitante, string? codigoTexto, AlterarAcessoDto? dto, CancellationToken cancelamento = default)
    {
        var preparo = await PrepararAsync(solicitante, codigoTexto, cancelamento);
        if (preparo.Falha is not null) return Resultado<AcessoDto>.Falha(preparo.Tipo, preparo.Falha);
        var (funcionario, usuario) = (preparo.Funcionario!, preparo.Usuario!);

        if (dto is null || (dto.Perfil is null && dto.Ativo is null))
            return Resultado<AcessoDto>.Falha("Informe o perfil e/ou a situação do acesso.");

        var perfilAtual = (PerfilAcesso)usuario.IdPerfil;
        var novoPerfil = perfilAtual;
        if (dto.Perfil is not null)
        {
            if (!PerfilAcessoExtensoes.TentarLer(dto.Perfil.Trim(), out novoPerfil))
                return Resultado<AcessoDto>.Falha("Perfil inválido. Use Colaborador, RH ou Admin.");
            if (!RegrasAcesso.PodeConceder(solicitante.Perfil, novoPerfil))
                return Resultado<AcessoDto>.Falha(TipoErro.Proibido, $"Você não pode conceder o perfil {NomesPerfil.De(novoPerfil)}.");
        }

        var novoAtivo = dto.Ativo ?? usuario.Ativo;
        if (novoAtivo && !funcionario.Ativo)
            return Resultado<AcessoDto>.Falha(TipoErro.Conflito, "O funcionário está desligado: reative o funcionário antes do acesso.");

        // O último Admin ativo precisa continuar Admin e ativo.
        var deixaDeSerAdminAtivo = usuario.Ativo && perfilAtual == PerfilAcesso.Admin
                                   && (novoPerfil != PerfilAcesso.Admin || !novoAtivo);
        if (deixaDeSerAdminAtivo && await _usuarios.ContarAdminsAtivosAsync(cancelamento) <= 1)
            return Resultado<AcessoDto>.Falha(TipoErro.Conflito, "Este é o último administrador ativo; ele não pode ser rebaixado nem desativado.");

        var mudancas = new List<string>();
        if (novoPerfil != perfilAtual)
            mudancas.Add($"perfil {NomesPerfil.De(perfilAtual)} -> {NomesPerfil.De(novoPerfil)}");
        if (novoAtivo != usuario.Ativo)
            mudancas.Add(novoAtivo ? "acesso ativado" : "acesso desativado");

        if (mudancas.Count > 0)
        {
            usuario.IdPerfil = (int)novoPerfil;
            usuario.Ativo = novoAtivo;
            _auditoria.Registrar(
                solicitante.IdUsuario, "AlterarAcesso", "USUARIO", usuario.Id, $"{funcionario.Codigo}: {string.Join("; ", mudancas)}");
            await _unidade.SalvarAsync(cancelamento);
        }

        return Resultado<AcessoDto>.Ok(MapeadorCadastro.Acesso(usuario));
    }

    /// <summary>Reenvia o link para o funcionário definir (ou refazer) a senha. Só para contas que o solicitante pode gerenciar.</summary>
    public async Task<Resultado> ReenviarConviteAsync(
        Solicitante solicitante, string? codigoTexto, CancellationToken cancelamento = default)
    {
        var preparo = await PrepararAsync(solicitante, codigoTexto, cancelamento);
        if (preparo.Falha is not null) return Resultado.Falha(preparo.Tipo, preparo.Falha);
        var (funcionario, usuario) = (preparo.Funcionario!, preparo.Usuario!);

        if (!usuario.Ativo || !funcionario.Ativo)
            return Resultado.Falha(TipoErro.Conflito, "O acesso está desativado.");

        _auditoria.Registrar(solicitante.IdUsuario, "ReenviarConvite", "USUARIO", usuario.Id, funcionario.Codigo);
        await _unidade.SalvarAsync(cancelamento);

        return await TentarEnviarConviteAsync(usuario, funcionario.Nome, cancelamento)
            ? Resultado.Ok()
            : Resultado.Falha(TipoErro.Conflito, "Não foi possível enviar o e-mail agora. Tente novamente em instantes.");
    }

    private async Task<bool> TentarEnviarConviteAsync(Usuario usuario, string nome, CancellationToken cancelamento)
    {
        try
        {
            await _convite.EnviarAsync(usuario, nome, cancelamento);
            return true;
        }
        catch (Exception) when (!cancelamento.IsCancellationRequested)
        {
            return false; // o acesso já foi gravado; o RH pode reenviar
        }
    }

    private sealed record Preparo(Funcionario? Funcionario, Usuario? Usuario, string? Falha, TipoErro Tipo);

    private async Task<Preparo> PrepararAsync(Solicitante solicitante, string? codigoTexto, CancellationToken cancelamento)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return new(null, null, "Apenas o RH pode gerenciar acessos.", TipoErro.Proibido);
        if (!CodigoFuncionario.TentarConverter(codigoTexto, out var codigo))
            return new(null, null, "Código inválido. Use o formato FUNC-0001.", TipoErro.Validacao);
        if (codigo.Id == solicitante.IdFuncionario)
            return new(null, null, "Você não pode alterar o seu próprio acesso.", TipoErro.Proibido);

        var funcionario = await _funcionarios.ObterAsync(codigo.Id, cancelamento);
        if (funcionario is null)
            return new(null, null, "Funcionário não encontrado.", TipoErro.NaoEncontrado);

        var usuario = await _usuarios.ObterPorFuncionarioAsync(funcionario.Id, cancelamento);
        if (usuario is null)
            return new(null, null, "Este funcionário ainda não tem acesso.", TipoErro.NaoEncontrado);
        if (!RegrasAcesso.PodeGerenciar(solicitante.Perfil, (PerfilAcesso)usuario.IdPerfil))
            return new(null, null, "Você não pode gerenciar o acesso de um usuário RH ou Admin.", TipoErro.Proibido);

        return new(funcionario, usuario, null, TipoErro.Validacao);
    }
}
