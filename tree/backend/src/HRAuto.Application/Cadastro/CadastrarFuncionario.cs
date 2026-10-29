using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Cadastro;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Seguranca;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Cadastro;

/// <summary>
/// RF09, RF10, RF16: o RH (ou Admin) cadastra o funcionário com CPF, RG e residência, define cargo, departamento e gestor
/// e, por padrão, cria o acesso. O código FUNC-0001 nasce do id gerado pelo banco.
/// Tudo é gravado numa transação: funcionário (gera o id), detalhes, usuário e log de auditoria.
/// </summary>
public sealed class CadastrarFuncionario
{
    private readonly IFuncionarioCadastroRepositorio _funcionarios;
    private readonly IUsuarioGestaoRepositorio _usuarios;
    private readonly ICargoRepositorio _cargos;
    private readonly IDepartamentoRepositorio _departamentos;
    private readonly IHashSenha _hash;
    private readonly EnviadorDeConvite _convite;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public CadastrarFuncionario(
        IFuncionarioCadastroRepositorio funcionarios,
        IUsuarioGestaoRepositorio usuarios,
        ICargoRepositorio cargos,
        IDepartamentoRepositorio departamentos,
        IHashSenha hash,
        EnviadorDeConvite convite,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio)
    {
        _funcionarios = funcionarios;
        _usuarios = usuarios;
        _cargos = cargos;
        _departamentos = departamentos;
        _hash = hash;
        _convite = convite;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<Resultado<FuncionarioCadastradoDto>> ExecutarAsync(
        Solicitante solicitante, CadastrarFuncionarioDto? dto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<FuncionarioCadastradoDto>.Falha(TipoErro.Proibido, "Apenas o RH pode cadastrar funcionários.");
        if (dto is null)
            return Resultado<FuncionarioCadastradoDto>.Falha("Envie os dados do funcionário.");

        // Perfil e acesso
        var criarAcesso = dto.CriarAcesso ?? true;
        if (!MapeadorCadastro.TentarLerPerfil(dto.Perfil, out var perfil))
            return Resultado<FuncionarioCadastradoDto>.Falha("Perfil inválido. Use Colaborador, RH ou Admin.");
        if (criarAcesso && !RegrasAcesso.PodeConceder(solicitante.Perfil, perfil))
            return Resultado<FuncionarioCadastradoDto>.Falha(TipoErro.Proibido, $"Você não pode conceder o perfil {NomesPerfil.De(perfil)}.");

        if (!MapeadorCadastro.TentarLerGestor(dto.CodigoGestor, out var idGestor))
            return Resultado<FuncionarioCadastradoDto>.Falha("Código do gestor inválido. Use o formato FUNC-0001.");

        // Validação do Domain (CPF, datas, e-mail, endereço...)
        DadosCadastroFuncionario dados;
        try
        {
            var entrada = MapeadorCadastro.Entrada(
                dto.Nome, dto.Cpf, dto.Rg, dto.Pis, dto.Ctps, dto.DataNascimento, dto.DataAdmissao,
                dto.Email, dto.Telefone, dto.IdCargo, dto.IdDepartamento, idGestor, dto.Endereco);
            dados = RegrasFuncionario.Normalizar(entrada, _relogio.Hoje());
        }
        catch (DominioException e)
        {
            return Resultado<FuncionarioCadastradoDto>.Falha(e.Message);
        }

        // Existência dos vínculos
        var cargo = await _cargos.ObterAsync(dados.IdCargo, cancelamento);
        if (cargo is null) return Resultado<FuncionarioCadastradoDto>.Falha("O cargo informado não existe.");
        var departamento = await _departamentos.ObterAsync(dados.IdDepartamento, cancelamento);
        if (departamento is null) return Resultado<FuncionarioCadastradoDto>.Falha("O departamento informado não existe.");

        if (dados.IdGestor is { } idDoGestor)
        {
            var gestor = await _funcionarios.ObterAsync(idDoGestor, cancelamento);
            if (gestor is null) return Resultado<FuncionarioCadastradoDto>.Falha("O gestor informado não existe.");
            try
            {
                RegrasFuncionario.ValidarGestor(0, gestor, Array.Empty<int>());
            }
            catch (DominioException e)
            {
                return Resultado<FuncionarioCadastradoDto>.Falha(e.Message);
            }
        }

        // Unicidade (a checagem no banco existe só para o e-mail do usuário; o resto é regra desta camada)
        if (await _funcionarios.CpfJaCadastradoAsync(dados.Cpf, null, cancelamento))
            return Resultado<FuncionarioCadastradoDto>.Falha(TipoErro.Conflito, "Já existe um funcionário com este CPF.");
        if (await _funcionarios.EmailJaCadastradoAsync(dados.Email, null, cancelamento)
            || await _usuarios.EmailEmUsoAsync(dados.Email, null, cancelamento))
            return Resultado<FuncionarioCadastradoDto>.Falha(TipoErro.Conflito, "Já existe um funcionário ou usuário com este e-mail.");

        var funcionario = RegrasFuncionario.Criar(dados);
        _funcionarios.Adicionar(funcionario);

        Usuario? usuario = null;
        try
        {
            await using var transacao = await _unidade.IniciarTransacaoAsync(cancelamento);

            await _unidade.SalvarAsync(cancelamento); // gera o id do funcionário

            _funcionarios.AdicionarDetalhes(RegrasFuncionario.CriarDetalhes(funcionario.Id, dados));

            if (criarAcesso)
            {
                usuario = new Usuario
                {
                    IdFuncionario = funcionario.Id,
                    IdPerfil = (int)perfil,
                    Email = dados.Email,
                    Senha = _hash.Gerar(GeradorSenhaTemporaria.Gerar()), // ninguém conhece: o funcionário define a dele pelo convite
                    Ativo = true
                };
                _usuarios.Adicionar(usuario);
            }

            // Sem CPF, RG nem endereço no log: LGPD (RNF08).
            _auditoria.Registrar(
                solicitante.IdUsuario, "CadastrarFuncionario", "FUNCIONARIO", funcionario.Id,
                $"{funcionario.Codigo} cadastrado" + (criarAcesso ? $" com acesso {NomesPerfil.De(perfil)}" : " sem acesso"));
            await _unidade.SalvarAsync(cancelamento);

            await transacao.ConfirmarAsync(cancelamento);
        }
        catch (ConflitoDePersistenciaException)
        {
            return Resultado<FuncionarioCadastradoDto>.Falha(
                TipoErro.Conflito, "Já existe um usuário com este e-mail. Confira os dados e tente novamente.");
        }

        // O convite sai depois da gravação: se o e-mail falhar, o cadastro já está salvo e o RH pode reenviar.
        var conviteEnviado = false;
        if (usuario is not null)
        {
            try
            {
                await _convite.EnviarAsync(usuario, funcionario.Nome, cancelamento);
                conviteEnviado = true;
            }
            catch (Exception) when (!cancelamento.IsCancellationRequested)
            {
                conviteEnviado = false;
            }
        }

        return Resultado<FuncionarioCadastradoDto>.Ok(new FuncionarioCadastradoDto(
            funcionario.Codigo, funcionario.Nome, usuario is not null,
            usuario is null ? null : NomesPerfil.De(perfil), conviteEnviado));
    }
}
