using HRAuto.Domain.Cadastro;
using HRAuto.Domain.Entities;
using HRAuto.Domain.Enums;
using HRAuto.Domain.ValueObjects;

namespace HRAuto.Application.Cadastro;

// ---------- Entrada ----------

/// <summary>Endereço (residência) do funcionário. O CEP aceita com ou sem hífen.</summary>
public sealed record EnderecoDto(
    string? Cep, string? Logradouro, string? Numero, string? Complemento, string? Bairro, string? Cidade, string? Uf);

/// <summary>
/// Cadastro de funcionário (RF09, RF10, RF16). O gestor é informado pelo código (FUNC-0001).
/// CriarAcesso (padrão: verdadeiro) cria também o login; Perfil (padrão: Colaborador) só vale com acesso.
/// </summary>
public sealed record CadastrarFuncionarioDto(
    string? Nome, string? Cpf, string? Rg, string? Pis, string? Ctps,
    DateOnly? DataNascimento, DateOnly? DataAdmissao,
    string? Email, string? Telefone,
    int? IdCargo, int? IdDepartamento, string? CodigoGestor,
    EnderecoDto? Endereco,
    string? Perfil, bool? CriarAcesso);

/// <summary>Atualização completa (PUT): campos omitidos são apagados quando opcionais (ex.: CodigoGestor nulo remove o gestor).</summary>
public sealed record AtualizarFuncionarioDto(
    string? Nome, string? Cpf, string? Rg, string? Pis, string? Ctps,
    DateOnly? DataNascimento, DateOnly? DataAdmissao,
    string? Email, string? Telefone,
    int? IdCargo, int? IdDepartamento, string? CodigoGestor,
    EnderecoDto? Endereco);

/// <summary>Desligamento (RF12). Sem data, usa hoje.</summary>
public sealed record DesligarFuncionarioDto(DateOnly? Data);

/// <summary>Filtros da lista (RF13). A busca é pelo código; o nome não é critério de busca.</summary>
public sealed record FiltroListaFuncionariosDto(
    string? Codigo, int? IdCargo, int? IdDepartamento, bool? Ativo, int? Pagina, int? Tamanho);

public sealed record CriarAcessoDto(string? Perfil);

/// <summary>Altera perfil e/ou situação do acesso. Campo nulo = não muda.</summary>
public sealed record AlterarAcessoDto(string? Perfil, bool? Ativo);

public sealed record SalvarEstruturaDto(string? Nome, string? Descricao);

// ---------- Saída ----------

public sealed record FuncionarioResumoDto(
    string Codigo, string Nome, string Email, string Cargo, string Departamento,
    string? CodigoGestor, string? NomeGestor, bool Ativo, DateOnly DataAdmissao);

/// <summary>Documentos mostrados apenas ao próprio funcionário e ao RH/Admin (LGPD, RNF08).</summary>
public sealed record DocumentosDto(string Cpf, string Rg, string? Pis, string? Ctps);

public sealed record AcessoDto(bool Possui, string? Perfil, bool Ativo, DateTime? UltimoLogin);

public sealed record FuncionarioDetalheDto(
    string Codigo, string Nome, string Email, string? Telefone,
    DateOnly? DataNascimento, DateOnly DataAdmissao, DateOnly? DataDesligamento, bool Ativo,
    int IdCargo, string Cargo, int IdDepartamento, string Departamento,
    string? CodigoGestor, string? NomeGestor,
    DocumentosDto? Documentos, EnderecoDto? Endereco, AcessoDto? Acesso);

public sealed record FuncionarioCadastradoDto(string Codigo, string Nome, bool AcessoCriado, string? Perfil, bool ConviteEnviado);

public sealed record AcessoCriadoDto(string Codigo, string Perfil, bool ConviteEnviado);

public sealed record EstruturaDto(int Id, string Nome, string? Descricao);

// ---------- Conversões ----------

internal static class MapeadorCadastro
{
    /// <summary>Lê o código do gestor. Vazio = sem gestor (true com nulo). Falso se o formato for inválido.</summary>
    public static bool TentarLerGestor(string? codigo, out int? idGestor)
    {
        idGestor = null;
        if (string.IsNullOrWhiteSpace(codigo)) return true;
        if (!CodigoFuncionario.TentarConverter(codigo, out var c)) return false;
        idGestor = c.Id;
        return true;
    }

    public static EntradaFuncionario Entrada(
        string? nome, string? cpf, string? rg, string? pis, string? ctps,
        DateOnly? nascimento, DateOnly? admissao, string? email, string? telefone,
        int? idCargo, int? idDepartamento, int? idGestor, EnderecoDto? endereco) =>
        new(nome, cpf, rg, pis, ctps, nascimento, admissao, email, telefone, idCargo, idDepartamento, idGestor,
            endereco is null
                ? null
                : new EntradaEndereco(endereco.Cep, endereco.Logradouro, endereco.Numero, endereco.Complemento,
                    endereco.Bairro, endereco.Cidade, endereco.Uf));

    public static EnderecoDto Endereco(FuncionarioDetalhes d) =>
        new(d.Cep, d.Endereco, d.Numero, d.Complemento, d.Bairro, d.Cidade, d.Estado);

    public static FuncionarioResumoDto Resumo(Funcionario f) =>
        new(f.Codigo, f.Nome, f.Email, f.Cargo?.NomeCargo ?? string.Empty, f.Departamento?.Nome ?? string.Empty,
            f.IdGestor is { } g ? CodigoFuncionario.Formatar(g) : null, f.Gestor?.Nome, f.Ativo, f.DataAdmissao);

    public static AcessoDto Acesso(Usuario? u) =>
        u is null
            ? new AcessoDto(false, null, false, null)
            : new AcessoDto(true, NomesPerfil.De((PerfilAcesso)u.IdPerfil), u.Ativo, u.UltimoLogin);

    /// <summary>Lê o nome do perfil enviado pelo app. Vazio = Colaborador. Números e nomes desconhecidos são recusados.</summary>
    public static bool TentarLerPerfil(string? texto, out PerfilAcesso perfil)
    {
        perfil = PerfilAcesso.Colaborador;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        return PerfilAcessoExtensoes.TentarLer(texto.Trim(), out perfil);
    }
}
