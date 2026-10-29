using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Cadastro;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;

namespace HRAuto.Application.Cadastro;

/// <summary>
/// RF14: cargos. Lista, cria, renomeia e exclui (só se nenhum funcionário usar o cargo). Escrita: RH e Admin.
/// A lista também fica restrita a RH/Admin: só os formulários de cadastro precisam dela.
/// </summary>
public sealed class GerenciarCargos
{
    private readonly ICargoRepositorio _cargos;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;

    public GerenciarCargos(ICargoRepositorio cargos, IAuditoria auditoria, IUnidadeDeTrabalho unidade)
    {
        _cargos = cargos;
        _auditoria = auditoria;
        _unidade = unidade;
    }

    public async Task<Resultado<IReadOnlyList<EstruturaDto>>> ListarAsync(Solicitante solicitante, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<IReadOnlyList<EstruturaDto>>.Falha(TipoErro.Proibido, "Apenas o RH pode consultar os cargos.");

        var cargos = await _cargos.ListarAsync(cancelamento);
        return Resultado<IReadOnlyList<EstruturaDto>>.Ok(
            cargos.Select(c => new EstruturaDto(c.Id, c.NomeCargo, c.Descricao)).ToList());
    }

    public async Task<Resultado<EstruturaDto>> CriarAsync(
        Solicitante solicitante, SalvarEstruturaDto? dto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<EstruturaDto>.Falha(TipoErro.Proibido, "Apenas o RH pode criar cargos.");

        string nome;
        string? descricao;
        try
        {
            nome = RegrasEstrutura.NormalizarNome(dto?.Nome, "cargo");
            descricao = RegrasEstrutura.NormalizarDescricao(dto?.Descricao);
        }
        catch (DominioException e)
        {
            return Resultado<EstruturaDto>.Falha(e.Message);
        }

        if (await _cargos.NomeJaExisteAsync(nome, null, cancelamento))
            return Resultado<EstruturaDto>.Falha(TipoErro.Conflito, "Já existe um cargo com este nome.");

        var cargo = new Cargo { NomeCargo = nome, Descricao = descricao };
        _cargos.Adicionar(cargo);

        await using var transacao = await _unidade.IniciarTransacaoAsync(cancelamento);
        await _unidade.SalvarAsync(cancelamento); // gera o id
        _auditoria.Registrar(solicitante.IdUsuario, "CriarCargo", "CARGO", cargo.Id, nome);
        await _unidade.SalvarAsync(cancelamento);
        await transacao.ConfirmarAsync(cancelamento);

        return Resultado<EstruturaDto>.Ok(new EstruturaDto(cargo.Id, cargo.NomeCargo, cargo.Descricao));
    }

    public async Task<Resultado<EstruturaDto>> AtualizarAsync(
        Solicitante solicitante, int id, SalvarEstruturaDto? dto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<EstruturaDto>.Falha(TipoErro.Proibido, "Apenas o RH pode editar cargos.");

        string nome;
        string? descricao;
        try
        {
            nome = RegrasEstrutura.NormalizarNome(dto?.Nome, "cargo");
            descricao = RegrasEstrutura.NormalizarDescricao(dto?.Descricao);
        }
        catch (DominioException e)
        {
            return Resultado<EstruturaDto>.Falha(e.Message);
        }

        var cargo = await _cargos.ObterParaEdicaoAsync(id, cancelamento);
        if (cargo is null) return Resultado<EstruturaDto>.Falha(TipoErro.NaoEncontrado, "Cargo não encontrado.");
        if (await _cargos.NomeJaExisteAsync(nome, id, cancelamento))
            return Resultado<EstruturaDto>.Falha(TipoErro.Conflito, "Já existe um cargo com este nome.");

        if (cargo.NomeCargo != nome || cargo.Descricao != descricao)
        {
            cargo.NomeCargo = nome;
            cargo.Descricao = descricao;
            _auditoria.Registrar(solicitante.IdUsuario, "AtualizarCargo", "CARGO", cargo.Id, nome);
            await _unidade.SalvarAsync(cancelamento);
        }

        return Resultado<EstruturaDto>.Ok(new EstruturaDto(cargo.Id, cargo.NomeCargo, cargo.Descricao));
    }

    public async Task<Resultado> ExcluirAsync(Solicitante solicitante, int id, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado.Falha(TipoErro.Proibido, "Apenas o RH pode excluir cargos.");

        var cargo = await _cargos.ObterParaEdicaoAsync(id, cancelamento);
        if (cargo is null) return Resultado.Falha(TipoErro.NaoEncontrado, "Cargo não encontrado.");

        var emUso = await _cargos.ContarFuncionariosAsync(id, cancelamento);
        if (emUso > 0)
            return Resultado.Falha(TipoErro.Conflito, $"O cargo está em uso por {emUso} funcionário(s) e não pode ser excluído.");

        _cargos.Remover(cargo);
        _auditoria.Registrar(solicitante.IdUsuario, "ExcluirCargo", "CARGO", cargo.Id, cargo.NomeCargo);
        try
        {
            await _unidade.SalvarAsync(cancelamento);
        }
        catch (ConflitoDePersistenciaException)
        {
            return Resultado.Falha(TipoErro.Conflito, "O cargo está em uso e não pode ser excluído.");
        }

        return Resultado.Ok();
    }
}

/// <summary>RF15: departamentos. Mesmas regras dos cargos.</summary>
public sealed class GerenciarDepartamentos
{
    private readonly IDepartamentoRepositorio _departamentos;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;

    public GerenciarDepartamentos(IDepartamentoRepositorio departamentos, IAuditoria auditoria, IUnidadeDeTrabalho unidade)
    {
        _departamentos = departamentos;
        _auditoria = auditoria;
        _unidade = unidade;
    }

    public async Task<Resultado<IReadOnlyList<EstruturaDto>>> ListarAsync(Solicitante solicitante, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<IReadOnlyList<EstruturaDto>>.Falha(TipoErro.Proibido, "Apenas o RH pode consultar os departamentos.");

        var itens = await _departamentos.ListarAsync(cancelamento);
        return Resultado<IReadOnlyList<EstruturaDto>>.Ok(
            itens.Select(d => new EstruturaDto(d.Id, d.Nome, d.Descricao)).ToList());
    }

    public async Task<Resultado<EstruturaDto>> CriarAsync(
        Solicitante solicitante, SalvarEstruturaDto? dto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<EstruturaDto>.Falha(TipoErro.Proibido, "Apenas o RH pode criar departamentos.");

        string nome;
        string? descricao;
        try
        {
            nome = RegrasEstrutura.NormalizarNome(dto?.Nome, "departamento");
            descricao = RegrasEstrutura.NormalizarDescricao(dto?.Descricao);
        }
        catch (DominioException e)
        {
            return Resultado<EstruturaDto>.Falha(e.Message);
        }

        if (await _departamentos.NomeJaExisteAsync(nome, null, cancelamento))
            return Resultado<EstruturaDto>.Falha(TipoErro.Conflito, "Já existe um departamento com este nome.");

        var departamento = new Departamento { Nome = nome, Descricao = descricao };
        _departamentos.Adicionar(departamento);

        await using var transacao = await _unidade.IniciarTransacaoAsync(cancelamento);
        await _unidade.SalvarAsync(cancelamento);
        _auditoria.Registrar(solicitante.IdUsuario, "CriarDepartamento", "DEPARTAMENTO", departamento.Id, nome);
        await _unidade.SalvarAsync(cancelamento);
        await transacao.ConfirmarAsync(cancelamento);

        return Resultado<EstruturaDto>.Ok(new EstruturaDto(departamento.Id, departamento.Nome, departamento.Descricao));
    }

    public async Task<Resultado<EstruturaDto>> AtualizarAsync(
        Solicitante solicitante, int id, SalvarEstruturaDto? dto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<EstruturaDto>.Falha(TipoErro.Proibido, "Apenas o RH pode editar departamentos.");

        string nome;
        string? descricao;
        try
        {
            nome = RegrasEstrutura.NormalizarNome(dto?.Nome, "departamento");
            descricao = RegrasEstrutura.NormalizarDescricao(dto?.Descricao);
        }
        catch (DominioException e)
        {
            return Resultado<EstruturaDto>.Falha(e.Message);
        }

        var departamento = await _departamentos.ObterParaEdicaoAsync(id, cancelamento);
        if (departamento is null) return Resultado<EstruturaDto>.Falha(TipoErro.NaoEncontrado, "Departamento não encontrado.");
        if (await _departamentos.NomeJaExisteAsync(nome, id, cancelamento))
            return Resultado<EstruturaDto>.Falha(TipoErro.Conflito, "Já existe um departamento com este nome.");

        if (departamento.Nome != nome || departamento.Descricao != descricao)
        {
            departamento.Nome = nome;
            departamento.Descricao = descricao;
            _auditoria.Registrar(solicitante.IdUsuario, "AtualizarDepartamento", "DEPARTAMENTO", departamento.Id, nome);
            await _unidade.SalvarAsync(cancelamento);
        }

        return Resultado<EstruturaDto>.Ok(new EstruturaDto(departamento.Id, departamento.Nome, departamento.Descricao));
    }

    public async Task<Resultado> ExcluirAsync(Solicitante solicitante, int id, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado.Falha(TipoErro.Proibido, "Apenas o RH pode excluir departamentos.");

        var departamento = await _departamentos.ObterParaEdicaoAsync(id, cancelamento);
        if (departamento is null) return Resultado.Falha(TipoErro.NaoEncontrado, "Departamento não encontrado.");

        var emUso = await _departamentos.ContarFuncionariosAsync(id, cancelamento);
        if (emUso > 0)
            return Resultado.Falha(TipoErro.Conflito, $"O departamento está em uso por {emUso} funcionário(s) e não pode ser excluído.");

        _departamentos.Remover(departamento);
        _auditoria.Registrar(solicitante.IdUsuario, "ExcluirDepartamento", "DEPARTAMENTO", departamento.Id, departamento.Nome);
        try
        {
            await _unidade.SalvarAsync(cancelamento);
        }
        catch (ConflitoDePersistenciaException)
        {
            return Resultado.Falha(TipoErro.Conflito, "O departamento está em uso e não pode ser excluído.");
        }

        return Resultado.Ok();
    }
}
