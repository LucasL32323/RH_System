using HRAuto.Application.Abstractions;
using HRAuto.Application.Common;
using HRAuto.Domain.Comunicacao;
using HRAuto.Domain.Common;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.Holerites;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Holerites;

/// <summary>
/// RF29: o RH/Admin publica o holerite (PDF, até 5 MB, RNF17) de um funcionário para um mês/ano. O arquivo vai para a pasta
/// do servidor com nome sorteado e o banco guarda só o caminho relativo. Competência repetida dá conflito, a menos que
/// o RH peça para substituir (o arquivo antigo é mantido no disco por rastreabilidade, RNF07). Ninguém publica o próprio
/// holerite. O funcionário é avisado e a publicação é auditada. Se a gravação no banco falhar, o arquivo novo é apagado.
/// </summary>
public sealed class PublicarHolerite
{
    private readonly IFuncionarioRepositorio _funcionarios;
    private readonly IFuncionarioCadastroRepositorio _cadastro;
    private readonly IHoleriteRepositorio _holerites;
    private readonly IArmazenamentoHolerites _armazenamento;
    private readonly INotificacaoRepositorio _notificacoes;
    private readonly IAuditoria _auditoria;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IRelogio _relogio;

    public PublicarHolerite(
        IFuncionarioRepositorio funcionarios,
        IFuncionarioCadastroRepositorio cadastro,
        IHoleriteRepositorio holerites,
        IArmazenamentoHolerites armazenamento,
        INotificacaoRepositorio notificacoes,
        IAuditoria auditoria,
        IUnidadeDeTrabalho unidade,
        IRelogio relogio)
    {
        _funcionarios = funcionarios;
        _cadastro = cadastro;
        _holerites = holerites;
        _armazenamento = armazenamento;
        _notificacoes = notificacoes;
        _auditoria = auditoria;
        _unidade = unidade;
        _relogio = relogio;
    }

    public async Task<Resultado<HoleritePublicadoDto>> ExecutarAsync(
        Solicitante solicitante, PublicarHoleriteDto? dto, CancellationToken cancelamento = default)
    {
        if (!solicitante.Perfil.EhRhOuAdmin())
            return Resultado<HoleritePublicadoDto>.Falha(TipoErro.Proibido, "Apenas o RH pode publicar holerites.");

        if (dto is null || string.IsNullOrWhiteSpace(dto.Codigo))
            return Resultado<HoleritePublicadoDto>.Falha("Informe o código do funcionário (FUNC-0001).");
        if (!CodigoFuncionario.TentarConverter(dto.Codigo, out var codigo))
            return Resultado<HoleritePublicadoDto>.Falha("Código inválido. Use o formato FUNC-0001.");
        if (dto.Mes is null || dto.Ano is null)
            return Resultado<HoleritePublicadoDto>.Falha("Informe o mês e o ano do holerite.");

        if (codigo.Id == solicitante.IdFuncionario)
            return Resultado<HoleritePublicadoDto>.Falha(TipoErro.Proibido, "Você não pode publicar o seu próprio holerite.");

        var funcionario = await _funcionarios.ObterPorIdAsync(codigo.Id, cancelamento);
        if (funcionario is null)
            return Resultado<HoleritePublicadoDto>.Falha(TipoErro.NaoEncontrado, "Funcionário não encontrado.");

        var mes = dto.Mes.Value;
        var ano = dto.Ano.Value;
        var agora = _relogio.Agora();
        var hoje = _relogio.Hoje();

        try
        {
            RegrasHolerite.ValidarCompetencia(mes, ano, hoje, funcionario.DataAdmissao);
            RegrasHolerite.ValidarArquivo(dto.NomeArquivo, dto.Conteudo.Span);
        }
        catch (DominioException e)
        {
            return Resultado<HoleritePublicadoDto>.Falha(e.Message);
        }

        var existente = await _holerites.ObterDaCompetenciaParaEdicaoAsync(funcionario.Id, ano, mes, cancelamento);
        if (existente is not null && !dto.Substituir)
            return Resultado<HoleritePublicadoDto>.Falha(
                TipoErro.Conflito, $"Já existe holerite de {mes:D2}/{ano} para {funcionario.Codigo}. Peça para substituir.");

        // Arquivo primeiro: se o banco falhar, o arquivo novo é apagado e nada fica pendurado.
        var caminho = await _armazenamento.SalvarAsync(funcionario.Id, ano, mes, dto.Conteudo, cancelamento);
        Holerite holerite;
        try
        {
            await using var transacao = await _unidade.IniciarTransacaoAsync(cancelamento);

            if (existente is null)
            {
                holerite = RegrasHolerite.Criar(funcionario.Id, mes, ano, hoje, caminho);
                _holerites.Adicionar(holerite);
            }
            else
            {
                holerite = existente;
                RegrasHolerite.Substituir(holerite, hoje, caminho);
            }

            _notificacoes.Adicionar(NotificacoesJornada.HoleritePublicado(holerite, existente is not null, agora.DateTime));
            await _unidade.SalvarAsync(cancelamento); // gera o id do holerite

            _auditoria.Registrar(solicitante.IdUsuario, existente is null ? "PublicarHolerite" : "SubstituirHolerite", "HOLERITE", holerite.Id,
                $"{funcionario.Codigo}: {mes:D2}/{ano}");
            await _unidade.SalvarAsync(cancelamento);

            await transacao.ConfirmarAsync(cancelamento);
        }
        catch (ConflitoDePersistenciaException)
        {
            await TentarRemoverAsync(caminho);
            return Resultado<HoleritePublicadoDto>.Falha(
                TipoErro.Conflito, "O holerite foi alterado ao mesmo tempo por outra operação. Tente novamente.");
        }
        catch
        {
            await TentarRemoverAsync(caminho);
            throw;
        }

        var nomes = new Dictionary<int, string> { [funcionario.Id] = funcionario.Nome };
        return Resultado<HoleritePublicadoDto>.Ok(new HoleritePublicadoDto(MapeadorHolerite.Holerite(holerite, nomes), existente is not null));
    }

    // Limpeza de erro: nunca esconde a falha original.
    private async Task TentarRemoverAsync(string caminho)
    {
        try { await _armazenamento.RemoverAsync(caminho, CancellationToken.None); }
        catch { /* arquivo órfão fica na pasta; o erro original é o que importa */ }
    }
}
