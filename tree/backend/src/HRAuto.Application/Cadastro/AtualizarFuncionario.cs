using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Cadastro;
using HRAuto.Domain.Common;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Seguranca;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Cadastro;

/// <summary>
/// RF09, RF10, RF16: o RH/Admin corrige os dados de um funcionário (atualização completa).
/// Regras: o RH não altera dados de quem tem acesso RH ou Admin (nem os seus: só o Admin); o e-mail do funcionário
/// acompanha o e-mail de login; o gestor não pode formar hierarquia circular. O log guarda só os nomes dos campos.
/// </summary>
public sealed class AtualizarFuncionario
{
    private readonly IFuncionarioCadastroRepositorio _funcionarios;
    private readonly IUsuarioGestaoRepositorio _usuarios;
    private readonly ICargoRepositorio _cargos;
    private readonly IDepartamentoRepositorio _departamentos;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public AtualizarFuncionario(
        IFuncionarioCadastroRepositorio funcionarios,
        IUsuarioGestaoRepositorio usuarios,
        ICargoRepositorio cargos,
        IDepartamentoRepositorio departamentos,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio)
    {
        _funcionarios = funcionarios;
        _usuarios = usuarios;
        _cargos = cargos;
        _departamentos = departamentos;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<Resultado<FuncionarioResumoDto>> ExecutarAsync(
        Solicitante solicitante, string? codigoTexto, AtualizarFuncionarioDto? dto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<FuncionarioResumoDto>.Falha(TipoErro.Proibido, "Apenas o RH pode editar funcionários.");
        if (!CodigoFuncionario.TentarConverter(codigoTexto, out var codigo))
            return Resultado<FuncionarioResumoDto>.Falha("Código inválido. Use o formato FUNC-0001.");
        if (dto is null)
            return Resultado<FuncionarioResumoDto>.Falha("Envie os dados do funcionário.");
        if (!MapeadorCadastro.TentarLerGestor(dto.CodigoGestor, out var idGestor))
            return Resultado<FuncionarioResumoDto>.Falha("Código do gestor inválido. Use o formato FUNC-0001.");

        var funcionario = await _funcionarios.ObterParaEdicaoAsync(codigo.Id, cancelamento);
        if (funcionario is null)
            return Resultado<FuncionarioResumoDto>.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado.");

        var usuario = await _usuarios.ObterPorFuncionarioAsync(funcionario.Id, cancelamento);
        if (usuario is not null && !RegrasAcesso.PodeGerenciar(solicitante.Perfil, (PerfilAcesso)usuario.IdPerfil))
            return Resultado<FuncionarioResumoDto>.Falha(TipoErro.Proibido, "Você não pode alterar os dados de um usuário RH ou Admin.");

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
            return Resultado<FuncionarioResumoDto>.Falha(e.Message);
        }

        var cargo = await _cargos.ObterAsync(dados.IdCargo, cancelamento);
        if (cargo is null) return Resultado<FuncionarioResumoDto>.Falha("O cargo informado não existe.");
        var departamento = await _departamentos.ObterAsync(dados.IdDepartamento, cancelamento);
        if (departamento is null) return Resultado<FuncionarioResumoDto>.Falha("O departamento informado não existe.");

        Domain.Entities.Funcionario? gestor = null;
        if (dados.IdGestor is { } idDoGestor)
        {
            gestor = await _funcionarios.ObterAsync(idDoGestor, cancelamento);
            if (gestor is null) return Resultado<FuncionarioResumoDto>.Falha("O gestor informado não existe.");
            if (dados.IdGestor != funcionario.IdGestor) // só revalida quando o gestor muda
            {
                try
                {
                    var cadeia = await _funcionarios.ObterCadeiaDeGestoresAsync(idDoGestor, cancelamento);
                    RegrasFuncionario.ValidarGestor(funcionario.Id, gestor, cadeia);
                }
                catch (DominioException e)
                {
                    return Resultado<FuncionarioResumoDto>.Falha(e.Message);
                }
            }
        }

        if (await _funcionarios.CpfJaCadastradoAsync(dados.Cpf, funcionario.Id, cancelamento))
            return Resultado<FuncionarioResumoDto>.Falha(TipoErro.Conflito, "Já existe outro funcionário com este CPF.");
        if (await _funcionarios.EmailJaCadastradoAsync(dados.Email, funcionario.Id, cancelamento)
            || await _usuarios.EmailEmUsoAsync(dados.Email, usuario?.Id, cancelamento))
            return Resultado<FuncionarioResumoDto>.Falha(TipoErro.Conflito, "Já existe outro funcionário ou usuário com este e-mail.");

        var detalhes = await _funcionarios.ObterDetalhesParaEdicaoAsync(funcionario.Id, cancelamento);
        var alterados = RegrasFuncionario.CamposAlterados(funcionario, detalhes, dados);

        if (alterados.Count > 0)
        {
            RegrasFuncionario.Aplicar(funcionario, dados);
            if (detalhes is null)
                _funcionarios.AdicionarDetalhes(RegrasFuncionario.CriarDetalhes(funcionario.Id, dados));
            else
                RegrasFuncionario.AplicarDetalhes(detalhes, dados);

            if (usuario is not null) usuario.Email = dados.Email; // o login acompanha o e-mail do cadastro

            _auditoria.Registrar(
                solicitante.IdUsuario, "AtualizarFuncionario", "FUNCIONARIO", funcionario.Id,
                $"{funcionario.Codigo}: {string.Join(", ", alterados)}");
            try
            {
                await _unidade.SalvarAsync(cancelamento);
            }
            catch (ConflitoDePersistenciaException)
            {
                return Resultado<FuncionarioResumoDto>.Falha(TipoErro.Conflito, "Já existe um usuário com este e-mail.");
            }
        }

        // Nomes montados a partir do que foi carregado (não das propriedades de navegação, que o EF só corrige ao gravar).
        return Resultado<FuncionarioResumoDto>.Ok(new FuncionarioResumoDto(
            funcionario.Codigo, funcionario.Nome, funcionario.Email, cargo.NomeCargo, departamento.Nome,
            funcionario.IdGestor is { } g ? CodigoFuncionario.Formatar(g) : null, gestor?.Nome,
            funcionario.Ativo, funcionario.DataAdmissao));
    }
}
