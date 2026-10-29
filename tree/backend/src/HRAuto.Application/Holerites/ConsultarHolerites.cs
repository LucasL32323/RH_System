using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Holerites;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Holerites;

/// <summary>
/// RF30: lista os holerites de quem está logado. RH/Admin podem listar os de outro funcionário pelo código (RNF08);
/// um colaborador que peça os de outra pessoa recebe "acesso negado". O caminho do arquivo nunca é devolvido.
/// </summary>
public sealed class ListarHolerites
{
    private readonly IFuncionarioRepositorio _funcionarios;
    private readonly IFuncionarioCadastroRepositorio _cadastro;
    private readonly IHoleriteRepositorio _holerites;

    public ListarHolerites(
        IFuncionarioRepositorio funcionarios, IFuncionarioCadastroRepositorio cadastro, IHoleriteRepositorio holerites)
    {
        _funcionarios = funcionarios;
        _cadastro = cadastro;
        _holerites = holerites;
    }

    public async Task<Resultado<IReadOnlyList<HoleriteDto>>> ExecutarAsync(
        Solicitante solicitante, FiltroListaHoleritesDto? filtro, CancellationToken cancelamento = default)
    {
        filtro ??= new FiltroListaHoleritesDto(null, null);

        var alvo = solicitante.IdFuncionario;
        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
        {
            if (!CodigoFuncionario.TentarConverter(filtro.Codigo, out var codigo))
                return Resultado<IReadOnlyList<HoleriteDto>>.Falha("Código inválido. Use o formato FUNC-0001.");
            alvo = codigo.Id;
        }

        if (alvo != solicitante.IdFuncionario)
        {
            if (!solicitante.Perfil.EhRhOuAdmin())
                return Resultado<IReadOnlyList<HoleriteDto>>.Falha(TipoErro.Proibido, "Você só pode ver os seus próprios holerites.");
            if (await _funcionarios.ObterPorIdAsync(alvo, cancelamento) is null)
                return Resultado<IReadOnlyList<HoleriteDto>>.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado.");
        }

        if (filtro.Ano is < RegrasHolerite.AnoMinimo)
            return Resultado<IReadOnlyList<HoleriteDto>>.Falha($"O ano deve ser {RegrasHolerite.AnoMinimo} ou posterior.");

        var lista = await _holerites.ListarAsync(alvo, filtro.Ano, cancelamento);
        var nomes = await _cadastro.ObterNomesAsync(new[] { alvo }, cancelamento);

        IReadOnlyList<HoleriteDto> itens = lista.Select(h => MapeadorHolerite.Holerite(h, nomes)).ToList();
        return Resultado<IReadOnlyList<HoleriteDto>>.Ok(itens);
    }
}

/// <summary>
/// RF30: devolve o PDF de um holerite. O dono e o RH/Admin podem baixar; para qualquer outra pessoa o holerite "não existe"
/// (404, para não revelar quais ids existem). Quando o RH/Admin baixa o de outra pessoa, isso fica no log de auditoria (RNF08).
/// </summary>
public sealed class BaixarHolerite
{
    private readonly IHoleriteRepositorio _holerites;
    private readonly IArmazenamentoHolerites _armazenamento;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;

    public BaixarHolerite(
        IHoleriteRepositorio holerites, IArmazenamentoHolerites armazenamento, IAuditoria auditoria, IUnidadeDeTrabalho unidade)
    {
        _holerites = holerites;
        _armazenamento = armazenamento;
        _auditoria = auditoria;
        _unidade = unidade;
    }

    public async Task<Resultado<ArquivoHoleriteDto>> ExecutarAsync(
        Solicitante solicitante, int idHolerite, CancellationToken cancelamento = default)
    {
        var holerite = await _holerites.ObterAsync(idHolerite, cancelamento);
        var ehDono = holerite is not null && holerite.IdFuncionario == solicitante.IdFuncionario;
        if (holerite is null || (!ehDono && !solicitante.Perfil.EhRhOuAdmin()))
            return Resultado<ArquivoHoleriteDto>.Falha(TipoErro.NaoEncontrado, "Holerite não encontrado.");

        var conteudo = RegrasHolerite.CaminhoEhSeguro(holerite.Arquivo)
            ? await _armazenamento.LerAsync(holerite.Arquivo, cancelamento)
            : null;
        if (conteudo is null)
            return Resultado<ArquivoHoleriteDto>.Falha(TipoErro.NaoEncontrado, "O arquivo deste holerite não está disponível. Procure o RH.");

        if (!ehDono)
        {
            _auditoria.Registrar(solicitante.IdUsuario, "ConsultaHolerite", "HOLERITE", holerite.Id,
                $"{CodigoFuncionario.Formatar(holerite.IdFuncionario)}: {holerite.Mes:D2}/{holerite.Ano}");
            await _unidade.SalvarAsync(cancelamento);
        }

        return Resultado<ArquivoHoleriteDto>.Ok(
            new ArquivoHoleriteDto(conteudo, RegrasHolerite.NomeDoArquivoParaDownload(holerite), "application/pdf"));
    }
}
